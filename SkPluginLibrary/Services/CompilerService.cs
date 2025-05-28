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
    private object?[] _submissionStates = [null, null];
    private int _submissionIndex = 0;

    public string? CodeOutput { get; set; }

    // Raised when execution is completed
    public event EventHandler<ExecutionCompletedEventArgs>? ExecutionCompleted;


    public async Task<string> SubmitCode(string code, IEnumerable<MetadataReference> references)
    {
        _references = references;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var members = root.Members;

        // Determine if we should use console app or script compilation
        var hasGlobalStatement = members.OfType<GlobalStatementSyntax>().Any();
        var hasEntryPoint = members.OfType<ClassDeclarationSyntax>().HasEntryMethod();
        var hasNamespace = members.Any(x => x.Kind() == SyntaxKind.NamespaceDeclaration);
        
        // Choose compilation mode based on code structure
        var mode = (hasNamespace || (!hasGlobalStatement && hasEntryPoint)) 
                   ? CompilationMode.Console 
                   : CompilationMode.Script;
                   
        await CompileAndExecute(code, mode);
        return CodeOutput;
    }

    // Unified compilation and execution method
    private async Task CompileAndExecute(string code, CompilationMode mode)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"Compiling and running code as {mode}");
        
        var previousOut = Console.Out;
       
        var writer = new StringWriter();
        
      
        // Redirect both Console.Out and Console.In
        Console.SetOut(writer);
        
        
        try
        {
            var (success, assembly, errorDiagnostics) = TryCompile(code, mode);
            
            if (success && assembly != null)
            {
                // Execute in a separate thread to prevent UI freezing
                await ExecuteAssembly(assembly, mode);
            }
            else if (errorDiagnostics?.Any() == true)
            {
                var errorOutput = errorDiagnostics.Aggregate("", 
                    (current, diag) => current + HttpUtility.HtmlEncode((object?)diag));
                CodeOutput = $"COMPILE ERROR: (errors: {errorDiagnostics.Count()}) {errorOutput}";
            }
        }
        catch (Exception ex)
        {
            CodeOutput = $"EXECUTION FAILED: {HttpUtility.HtmlEncode((string?)CSharpObjectFormatter.Instance.FormatException(ex))}";
        }
        finally
        {
            // Capture output and restore console streams
            if (string.IsNullOrEmpty(CodeOutput))
            {
                CodeOutput = writer.ToString();
            }
            Console.SetOut(previousOut);
            
            // Notify that execution has completed
            OnExecutionCompleted(new ExecutionCompletedEventArgs(CodeOutput));
        }
        
        sw.Stop();
        Console.WriteLine($"{sw.ElapsedMilliseconds}ms elapsed | output: {CodeOutput}");
    }

    private async Task ExecuteAssembly(Assembly assembly, CompilationMode mode)
    {
        if (mode == CompilationMode.Script)
        {
            var entryPoint = _runningCompilation.GetEntryPoint(CancellationToken.None);
            var type = assembly.GetType(
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
        }
        else // Console mode
        {
            var entry = assembly.EntryPoint;
            if (entry?.Name == "<Main>") // sync wrapper over async Task Main
            {
                entry = entry.DeclaringType?.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
            var hasArgs = entry?.GetParameters().Length > 0;
            var result = entry?.Invoke(null, hasArgs ? [Array.Empty<string>()] : null);
            if (result is Task t)
            {
                await t;
            }
        }
    }

    // Unified compilation method that handles both script and console app compilation
    private (bool success, Assembly? assembly, IEnumerable<Diagnostic>? errorDiagnostics) TryCompile(string source, CompilationMode mode)
    {
        if (mode == CompilationMode.Script)
        {
            var scriptCompilation = CSharpCompilation.CreateScriptCompilation(
                Path.GetRandomFileName(),
                CSharpSyntaxTree.ParseText(source, 
                    CSharpParseOptions.Default.WithKind(SourceCodeKind.Script).WithLanguageVersion(LanguageVersion.Preview)),
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: GetDefaultNamespaces()),
                previousScriptCompilation: _runningCompilation
            );

            var (success, assembly, errorDiagnostics) = TryCompileAndEmit(scriptCompilation);
            if (!success || assembly == null) return (success, assembly, errorDiagnostics);
            _submissionIndex++;
            _runningCompilation = scriptCompilation;
            return (success, assembly, errorDiagnostics);

        }

       
        var consoleCompilation = CSharpCompilation.Create("DynamicCode")
            .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
            .AddReferences(_references!)
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source,
                new CSharpParseOptions(LanguageVersion.Preview)));

        return TryCompileAndEmit(consoleCompilation);
    }

    private static (bool success, Assembly? assembly, IEnumerable<Diagnostic>? errorDiagnostics) TryCompileAndEmit(
        CSharpCompilation compilation)
    {
        var errorDiagnostics = GetErrorDiagnostics(compilation).ToList();
        if (errorDiagnostics.Any())
        {
            return (false, null, errorDiagnostics);
        }

        using var outputAssembly = new MemoryStream();
        var emitResult = compilation.Emit(outputAssembly);
            
        if (!emitResult.Success)
        {
            return (false, null, emitResult.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error));
        }

        var assembly = Assembly.Load(outputAssembly.ToArray());
        return (true, assembly, null);
    }

    private static IEnumerable<Diagnostic> GetErrorDiagnostics(CSharpCompilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .ToList();
            
        if (diagnostics.Any())
        {
            // Add declaration and other diagnostics when there are errors
            diagnostics = diagnostics
                .Concat(compilation.GetDeclarationDiagnostics())
                .Concat(compilation.GetDiagnostics())
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Distinct()
                .ToList();
        }
        
        return diagnostics;
    }
    
    private static string[] GetDefaultNamespaces() =>
    [
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
        "Microsoft.CodeAnalysis.CSharp"
    ];
    
    // Notify listeners that execution has completed
    protected virtual void OnExecutionCompleted(ExecutionCompletedEventArgs e)
    {
        ExecutionCompleted?.Invoke(this, e);
    }
   
}

// Event args for execution completion
public class ExecutionCompletedEventArgs(string output) : EventArgs
{
    public string Output { get; } = output;
}

public enum CompilationMode
{
    Script,
    Console
}

public static class CompileExtensions
{
    public static bool HasEntryMethod(this IEnumerable<ClassDeclarationSyntax> memberClasses)
    {
        return memberClasses.Any(cls => cls.Members.OfType<MethodDeclarationSyntax>().Any(mthd => mthd.Identifier.Text == "Main"));
    }
}