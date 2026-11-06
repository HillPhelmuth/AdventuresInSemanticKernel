using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.JSInterop;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime;
using System.Text;

namespace AgentToolComponents.Models.RazorGen;

public class RazorCompile
{
    public RazorCompile()
    {
    }
    private static CSharpCompilation _baseCompilation;
    private IEnumerable<MetadataReference>? _references;
    public bool IsSuccess { get; set; }
    public const string DefaultRootNamespace = "AppBuilder.Output";

    private const string WorkingDirectory = "/BlazorOut/";
    private readonly RazorProjectFileSystem _fileSystem = new WebRazorProjectFileSystem();

    private readonly RazorConfiguration _config = RazorConfiguration.Create(
        RazorLanguageVersion.Latest,
        "Blazor",
        extensions: []);

    public event Action<string>? OnCompileUpdate;
    public async Task<CodeAssemblyModel> CompileToAssemblyAsync(ICollection<RazorFile> codeFiles)
    {
        Console.WriteLine($"CompileToAssemblyAsync: {string.Join("\r\n", codeFiles.Select(x => x.Name))}");
        OnCompileUpdate?.Invoke("Starting project compile and execute...");
        if (codeFiles == null)
        {
            throw new ArgumentNullException(nameof(codeFiles));
        }

        var cSharpResults = await ParseRazorFiles(codeFiles);
        var result = await GetCodeAssembly(new List<RazorToCSharpModel>(cSharpResults));
        return result;
    }

    private async Task<IEnumerable<RazorToCSharpModel>> ParseRazorFiles(ICollection<RazorFile> codeFiles)
    {
        // The first phase won't include any metadata references for component discovery. This mirrors what the build does.
        Console.WriteLine($"ConvertRazorToCSharp: {string.Join("\r\n", codeFiles.Select(x => x.Name))}");
        OnCompileUpdate?.Invoke("Parsing .razor files to C#...");
        var projectEngine = CreateRazorProjectEngine([]);
        var declarations = new List<RazorToCSharpModel>();
        foreach (var codeFile in codeFiles.Where(x => x.Name.EndsWith(".razor")))
        {
            RazorProjectItem projectItem = CreateRazorProjectItem(codeFile.Name, codeFile.Code);

            RazorCodeDocument codeDocument = projectEngine.ProcessDeclarationOnly(projectItem);
            RazorCSharpDocument cSharpDocument = codeDocument.GetCSharpDocument();

            declarations.Add(new RazorToCSharpModel
            {
                ProjectItem = projectItem,
                Code = cSharpDocument.GeneratedCode,
                Diagnostics = cSharpDocument.Diagnostics.Select(diagnostic => new CustomDiag(diagnostic)),
            });
        }
        //ToDo Likely Solution
        declarations.AddRange(codeFiles.Where(x => x.Name.EndsWith(".cs"))
            .Select(codeFile => new RazorToCSharpModel {Code = codeFile.Code, Diagnostics = new List<CustomDiag>()}));
        // Get initial core assembly
        var tempAssembly = await GetCodeAssembly(declarations);
        if (tempAssembly.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            Console.WriteLine($"Error Parsing Razor Files:\r\n{string.Join("\r\n\t", tempAssembly.Diagnostics.Select(x => $"{x.Description} - {x.Line}"))}");
            OnCompileUpdate?.Invoke("Ooops! Error parsing Razor Files!");
            return [new RazorToCSharpModel { Diagnostics = tempAssembly.Diagnostics }];
        }
        // add new assemblies to project refs
        var references = new List<MetadataReference>(_baseCompilation.References) { tempAssembly.Compilation.ToMetadataReference() };
        projectEngine = this.CreateRazorProjectEngine(references);
        var results = new List<RazorToCSharpModel>();
        // Iterates through c# code docs and converts to members of generated Razor project
        foreach (var declaration in declarations.Where(x => x.ProjectItem != null))
        {
            var codeDocument = projectEngine.Process(declaration.ProjectItem);
            var cSharpDocument = codeDocument.GetCSharpDocument();

            results.Add(new RazorToCSharpModel
            {
                ProjectItem = declaration.ProjectItem,
                Code = cSharpDocument.GeneratedCode,
                Diagnostics = cSharpDocument.Diagnostics.Select(x => new CustomDiag(x))
            });
        }

        results.AddRange(declarations.Where(x => x.ProjectItem == null));
        return results.AsEnumerable();
    }
    //ToDo Add Event action to send parsed razor file to components.
    private Task<CodeAssemblyModel> GetCodeAssembly(ICollection<RazorToCSharpModel> cSharpResults, bool firstPass = false)
    {
        Console.WriteLine($"GetCodeAssembly: {string.Join("\r\n", cSharpResults.Select(x => x.Code))}");
        var cSharpParseOptions = new CSharpParseOptions(LanguageVersion.Preview);

        if (cSharpResults.Any(r => r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)))
        {
            Console.WriteLine($"Razor compile error in GetCodeAssebly: {string.Join("\r\n", cSharpResults.SelectMany(r => r.Diagnostics).ToList())}");
            IsSuccess = false;
            return Task.FromResult(new CodeAssemblyModel { Diagnostics = cSharpResults.SelectMany(r => r.Diagnostics).ToList() });
        }

        var syntaxTrees = new List<SyntaxTree>(cSharpResults.Count);
        syntaxTrees.AddRange(cSharpResults.Select(cs => CSharpSyntaxTree.ParseText(cs.Code, cSharpParseOptions)));
        var finalCompilation = _baseCompilation.AddSyntaxTrees(syntaxTrees);

        var compilationDiagnostics = finalCompilation.GetDiagnostics().Where(d => d.Severity > DiagnosticSeverity.Info);

        var result = new CodeAssemblyModel
        {
            Compilation = finalCompilation,
            Diagnostics = compilationDiagnostics.Select(diag => new CustomDiag(diag))
        };

        if (result.Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error))
        {
            using var peStream = new MemoryStream();
            using var pdbStream = new MemoryStream();

            var emitResult = finalCompilation.Emit(peStream, pdbStream);

            // Capture emit diagnostics (can include late binding / pe verify issues)
            if (!emitResult.Success)
            {
                var emitErrors = emitResult.Diagnostics
                    .Where(d => d.Severity >= DiagnosticSeverity.Error)
                    .Select(d => new CustomDiag(d))
                    .ToList();

                // Merge diagnostics
                var merged = result.Diagnostics.Concat(emitErrors).ToList();
                result.Diagnostics = merged;
                IsSuccess = false;
                Console.WriteLine("Emit failed:\r\n" + string.Join("\r\n", emitErrors.Select(d => d.ToString())));
                return Task.FromResult(result);
            }

            // IMPORTANT: rewind streams before loading (otherwise LoadFromStream reads 0 bytes -> BadImageFormatException)
            peStream.Position = 0;
            pdbStream.Position = 0;

            // Persist assembly bytes (optional; AFTER rewind so bytes reflect full stream)
            result.AssemblyBytes = peStream.ToArray();

            var alc = new SnippetLoadContext(isCollectible: true);

            Assembly asmLoaded;
            try
            {
                // Use the rewound streams directly
                asmLoaded = alc.LoadFromStream(peStream, pdbStream);
            }
            catch (BadImageFormatException bif)
            {
                IsSuccess = false;
                Console.WriteLine("BadImageFormatException while loading dynamic assembly: " + bif);
                // Add a synthetic diagnostic so caller can surface it
                var diag = new CustomDiag
                {
                    Code = "DYN0001",
                    Severity = DiagnosticSeverity.Error,
                    Description = "Failed to load generated assembly (BadImageFormat). The emitted PE stream was unreadable or corrupt.",
                    File = "(dynamic)",
                    Line = -1
                };
                result.Diagnostics = result.Diagnostics.Concat([diag]).ToList();
                return Task.FromResult(result);
            }

            // Get the Types from the assembly that implement IComponent
            var map = asmLoaded.GetTypes()
                .Where(t => typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(t))
                .ToDictionary(t => t.Name, t => t);

            // Get the Types from the assembly that Implement IComponent
            //var map = asmLoaded.GetTypes()
            //    .Where(t => typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(t))
            //    .ToDictionary(t => t.Name, t => t);


            result.BlazorComponentResult = new CompileResult(asmLoaded, alc, map){Diagnostics = result.Diagnostics};
            IsSuccess = true;
            return Task.FromResult(result);
        }

        IsSuccess = false;
        return Task.FromResult(result); 
    }
    private RazorProjectEngine CreateRazorProjectEngine(IReadOnlyList<MetadataReference> references)
    {
        Console.WriteLine("CreateRazorProjectEngine");
        return RazorProjectEngine.Create(_config, _fileSystem, builder =>
        {
            builder.SetRootNamespace(DefaultRootNamespace);
            builder.AddDefaultImports(RazorConstants.DefaultUsings);

            // Features that use Roslyn are mandatory for components
            CompilerFeatures.Register(builder);

            builder.Features.Add(new CompilationTagHelperFeature());
            builder.Features.Add(new DefaultMetadataReferenceFeature { References = references });
        });
    }

    private static RazorProjectItem CreateRazorProjectItem(string fileName, string fileContent)
    {
        Console.WriteLine($"CreateRazorProjectItem for {fileName}");
        var fullPath = WorkingDirectory + fileName;

        // File paths in Razor are always of the form '/a/b/c.razor'
        var filePath = fileName;
        if (!filePath.StartsWith('/'))
        {
            filePath = '/' + filePath;
        }

        fileContent = fileContent.Replace("\r", string.Empty);

        return new WebRazorProjectItem(
            WorkingDirectory,
            filePath,
            fullPath,
            fileName,
            FileKinds.Component,
            Encoding.UTF8.GetBytes(fileContent.TrimStart()));
    }
    public async Task InitAsync()
    {
        var basicReferenceAssemblyRoots = new[]
        {
            typeof(Console).Assembly, // System.Console
            typeof(Uri).Assembly, // System.Private.Uri
            typeof(AssemblyTargetedPatchBandAttribute).Assembly, // System.Private.CoreLib
            typeof(NavLink).Assembly, // Microsoft.AspNetCore.Components.Web
            typeof(IQueryable).Assembly, // System.Linq.Expressions
            typeof(HttpClientJsonExtensions).Assembly, // System.Net.Http.Json
            typeof(HttpClient).Assembly, // System.Net.Http
            typeof(IJSRuntime).Assembly, // Microsoft.JSInterop
            typeof(RequiredAttribute).Assembly, // System.ComponentModel.Annotations
            
        };

        var assemblyNames = basicReferenceAssemblyRoots
            .SelectMany(assembly => assembly.GetReferencedAssemblies().Concat([assembly.GetName()]))
            .Select(x => x.Name)
            .Distinct()
            .ToList();

        var assemblyStreams = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location)).DistinctBy(x => x.Location).Select(x => MetadataReference.CreateFromFile(x.Location));/* await GetAssemblyStreams(httpClient, assemblyNames);*/
        _references = assemblyStreams.ToList();
        //var allReferenceAssemblies = assemblyStreams.ToDictionary(a => a.Key, a => MetadataReference.CreateFromStream(a.Value));

        //_references = allReferenceAssemblies
        //    .Where(a => basicReferenceAssemblyRoots
        //        .Select(x => x.GetName().Name)
        //        .Union(basicReferenceAssemblyRoots.SelectMany(y => y.GetReferencedAssemblies().Select(z => z.Name)))
        //        .Any(n => n == a.Key))
        //    .Select(a => a.Value)
        //    .ToList();
        
        _baseCompilation = CSharpCompilation.Create(
            "Output",
            references: _references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                concurrentBuild: false, 
                specificDiagnosticOptions:
                [
                    new KeyValuePair<string, ReportDiagnostic>("CS1701", ReportDiagnostic.Suppress),
                    new KeyValuePair<string, ReportDiagnostic>("CS1702", ReportDiagnostic.Suppress),
                ]));
    }
}