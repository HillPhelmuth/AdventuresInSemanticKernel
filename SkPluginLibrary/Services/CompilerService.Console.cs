using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Reflection;

namespace SkPluginLibrary.Services
{
    public partial class CompilerService
    {
        private string _output = "";


        public async Task<string> RunConsole(string source, IEnumerable<MetadataReference> references)
        {
            _references = references;
            await ExecuteConsoleApp(source);
            return _output;
        }

        public (bool success, Assembly asm) TryCompileConsole(string source)
        {
            var compilation = CSharpCompilation.Create("DynamicCode")
                .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
                .AddReferences(_references!)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp11)));

            var diagnostics = compilation.GetDiagnostics();

            var error = false;
            foreach (var diag in diagnostics)
            {
                switch (diag.Severity)
                {
                    case DiagnosticSeverity.Info:
                    case DiagnosticSeverity.Warning:
                        //Console.WriteLine(diag.ToString());
                        break;
                    case DiagnosticSeverity.Error:
                        error = true;
                        Console.WriteLine(diag.ToString());
                        break;
                }
            }

            if (error)
            {
                return (false, null)!;
            }

            using var outputAssembly = new MemoryStream();
            compilation.Emit(outputAssembly);
            return (true, Assembly.Load(outputAssembly.ToArray()));
        }
        private async Task ExecuteConsoleApp(string code)
        {
            _output = "";

            Console.WriteLine("Compiling and Running code");
            var sw = Stopwatch.StartNew();

            var currentOut = Console.Out;
            var writer = new StringWriter();
            Console.SetOut(writer);
           
            Exception? exception = null;
            try
            {
                var (success, assembly) = TryCompileConsole(code);
                if (success)
                {
                    var entry = assembly.EntryPoint;
                    if (entry?.Name == "<Main>") // sync wrapper over async Task Main
                    {
                        entry = entry.DeclaringType?.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); // reflect for the async Task Main
                    }
                    var hasArgs = entry?.GetParameters().Length > 0;
                    var result = entry?.Invoke(null, hasArgs ? new object[] { Array.Empty<string>() } : null);
                    if (result is Task t)
                    {
                        await t;
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            _output = writer.ToString();
            if (exception != null)
            {
                _output += "\r\n" + exception;
            }
            Console.SetOut(currentOut);
            
            sw.Stop();
            Console.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms");

        }

    }
}
