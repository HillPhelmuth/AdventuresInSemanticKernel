using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Reflection;
using System.Web;

namespace SkPluginLibrary.Services;

public partial class CompilerService
{
    private CSharpCompilation? _runningCompilation;
    private IEnumerable<MetadataReference>? _references;
    private object[] _submissionStates = { null, null };
    private int _submissionIndex = 0;

    public string? CodeOutput { get; set; }


    public async Task<bool> SubmitSolution(string code, IEnumerable<MetadataReference> references, string testAgainst = "true")
    {
        Console.WriteLine("Compiling and running code");
        var sw = Stopwatch.StartNew();
        await RunSubmission(code, references);
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms elapsed \n output: {CodeOutput}");
        return CodeOutput == testAgainst || CodeOutput == $"\"{testAgainst}\"";
    }

    public async Task<string> SubmitCode(string code, IEnumerable<MetadataReference> references)
    {
        Console.WriteLine($"Code input: {code}");
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var members = root.Members;

        var hasGlobalStatement = members.OfType<GlobalStatementSyntax>().Any();
        var hasEntryPoint = members.OfType<ClassDeclarationSyntax>().HasEntryMethod();
        var hasNamespace = members.Any(x => x.Kind() == SyntaxKind.NamespaceDeclaration);
        if (hasNamespace || !hasGlobalStatement && hasEntryPoint)
        {
            CodeOutput = await RunConsole(code, references);
            Console.WriteLine($"Code output: {CodeOutput}");
            return CodeOutput;
        }
        await RunSubmission(code, references);
        Console.WriteLine($"Code output: {CodeOutput}");
        return CodeOutput;
    }

    private async Task RunSubmission(string code, IEnumerable<MetadataReference> references)
    {
        _references ??= references;

        var previousOut = Console.Out;
        try
        {
            var (success, script) = TryCompileScript(code, out var errorDiagnostics);
            if (success)
            {
                await WriteScript(script);
                //var output = HttpUtility.HtmlEncode(writer.ToString());
            }
            else
            {
                var errorOutput = errorDiagnostics.Aggregate("", (current, diag) => current + HttpUtility.HtmlEncode((object?)diag));

                CodeOutput = $"COMPILE ERROR: (errors: {errorDiagnostics.Count()}) {errorOutput}";

            }
        }
        catch (Exception ex)
        {
            CodeOutput = $"EXECUTION FAILED: {HttpUtility.HtmlEncode((string?)CSharpObjectFormatter.Instance.FormatException(ex))}";
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    private async Task WriteScript(Assembly script)
    {
        var writer = new StringWriter();
        Console.SetOut(writer);

        var entryPoint = _runningCompilation.GetEntryPoint(CancellationToken.None);
        var type = script.GetType(
            $"{entryPoint.ContainingNamespace.MetadataName}.{entryPoint.ContainingType.MetadataName}");
        var entryPointMethod = type.GetMethod(entryPoint.MetadataName);

        var submission = (Func<object[], Task>)entryPointMethod.CreateDelegate(typeof(Func<object[], Task>));

        if (_submissionIndex >= _submissionStates.Length)
        {
            Array.Resize(ref _submissionStates, Math.Max(_submissionIndex, _submissionStates.Length * 2));
        }

        var returnValue = await (Task<object?>)submission(_submissionStates);
        if (returnValue != null)
        {
            Console.WriteLine((string?)CSharpObjectFormatter.Instance.FormatObject(returnValue));
        }

        CodeOutput = writer.ToString();
    }

    //Tries to compile, if successful, it outputs the DLL Assembly. If unsuccessful, it will output the error message
    private (bool success, Assembly script) TryCompileScript(string source, out IEnumerable<Diagnostic> errorDiagnostics)
    {
        var scriptCompilation = CSharpCompilation.CreateScriptCompilation(
            Path.GetRandomFileName(),
            CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script).WithLanguageVersion(LanguageVersion.Preview)),
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: new[]
            {
                "System",
                "System.IO",
                "System.Collections.Generic",
                "System.Collections",
                "System.Console",
                "System.Diagnostics",
                "System.Dynamic",
                "System.Linq",
                "System.Linq.Expressions",
                "System.Net.Http",
                "System.Text",
                "System.Net",
                "System.Threading.Tasks",
                "System.Numerics",
                "Microsoft.CodeAnalysis",
                "Microsoft.CodeAnalysis.CSharp",
            }),
            previousScriptCompilation: _runningCompilation
        );

        errorDiagnostics = scriptCompilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error);
        if (errorDiagnostics.Any())
        {
            errorDiagnostics.Concat(scriptCompilation.GetDeclarationDiagnostics()).Concat(scriptCompilation.GetDiagnostics());
            errorDiagnostics = errorDiagnostics.Distinct();
            return (false, null);
        }

        using var outputAssembly = new MemoryStream();
        var emitResult = scriptCompilation.Emit(outputAssembly);

        if (!emitResult.Success)
        {
            errorDiagnostics = emitResult.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error);
            return (false, null);
        }
        _submissionIndex++;
        _runningCompilation = scriptCompilation;
        var script = Assembly.Load(outputAssembly.ToArray());
        return (true, script);

    }
}
public static class CompileExtensions
{
    public static bool HasEntryMethod(this IEnumerable<ClassDeclarationSyntax> memberClasses)
    {
        return memberClasses.Any(cls => cls.Members.OfType<MethodDeclarationSyntax>().Any(mthd => mthd.Identifier.Text == "Main"));
    }
}