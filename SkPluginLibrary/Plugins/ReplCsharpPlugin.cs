using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Plugins.Memory;
using SkPluginLibrary.Services;
using System.ComponentModel;
using System.Text.Json;

namespace SkPluginLibrary.Plugins
{
    public class ReplCsharpPlugin
    {
        private readonly ISKFunction _generateCodeFunction;
        private readonly ISKFunction _generateScriptFunction;
        private readonly ISKFunction _generateKernelCodeFunction;
        private readonly CompilerService _compilerService;
        private readonly ScriptService _scriptService;
        private readonly IKernel _kernel;


        public ReplCsharpPlugin(IKernel kernel, ScriptService scriptService, CompilerService compilerService)
        {
            _scriptService = scriptService;
            _compilerService = compilerService;
            _generateCodeFunction = kernel.ImportSemanticFunctionsFromDirectory(
                RepoFiles.PluginDirectoryPath,
                "CodingPlugin")["CodeCSharp"];
            _generateScriptFunction = kernel.ImportSemanticFunctionsFromDirectory(
                RepoFiles.PluginDirectoryPath,
                "CodingPlugin")["CSharpScript"];
            _generateKernelCodeFunction =
                kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "CodingPlugin")[
                    "CSharpSemanticKernel"];
            _kernel = kernel;

        }

        public ReplCsharpPlugin(IKernel kernel)
        {
            _generateCodeFunction = kernel.ImportSemanticFunctionsFromDirectory(
                               RepoFiles.PluginDirectoryPath,
                                              "CodingPlugin")["CodeCSharp"];
            _generateScriptFunction = kernel.ImportSemanticFunctionsFromDirectory(
                               RepoFiles.PluginDirectoryPath,
                                              "CodingPlugin")["CSharpScript"];
            _scriptService = new ScriptService();
            _compilerService = new CompilerService();
            _generateKernelCodeFunction =
                kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "CodingPlugin")[
                    "CSharpSemanticKernel"];
            _kernel = kernel;
        }
        [SKFunction, SKName("ReplConsole"), Description("Describe c# code to generate and execute")]
        public async Task<string> ReplConsoleAsync(string input, [SKName("existingCode"), Description("Previously written or generated code")] string? existingCode, SKContext context)
        {

            context.Variables.Update(input);
            var code = await _kernel.RunAsync(context.Variables, _generateCodeFunction);
            var currentCode = existingCode ?? context.Variables["existingCode"];
            var codeResult = code.Result().Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            var existingCodeAfter = $"{currentCode}\n{codeResult}";
            context.Variables.Set("existingCode", existingCodeAfter.TrimStart('\n'));
            var refs = CompileResources.PortableExecutableReferences;
            var result = await _compilerService.SubmitCode(codeResult, refs);
            //context.Variables["existingCode"] = existingCode;
            return JsonSerializer.Serialize(new CodeOutputModel { Output = result, Code = codeResult });
        }
        [SKFunction, SKName("ReplScript"), Description("Describe c# code to generate and execute as a script")]
        public async Task<string> ReplScriptAsync(string input, [SKName("existingCode"), Description("Previously written or generated code")] string? existingCode, SKContext context)

        {
            context.Variables.Update(input);
            var code = await _kernel.RunAsync(context.Variables, _generateScriptFunction);
            Console.WriteLine($"Generated Code:{code.Result()}");
            var currentCode = existingCode ?? context.Variables["existingCode"];
            var codeResult = code.Result().Replace("```csharp", "").Replace("```", "").TrimStart('\n');

            context.Variables.Set("existingCode", codeResult);
            var sessionId = context.Variables["sessionId"];
            var result = await _scriptService.EvaluateAsync(codeResult);
            //context.Variables["existingCode"] = existingCode;
            return JsonSerializer.Serialize(new CodeOutputModel { Output = result, Code = codeResult });
        }
        [SKFunction, SKName("SemanticKernelCode"), Description("Describe c# code related to Semantic Kernel to generate and execute")]
        public async Task<string> SemanticKernelCodeAsync(string input, [SKName("existingCode"), Description("Previously written or generated code")] string? existingCode, SKContext context)
        {
            Console.WriteLine("Executing SemanticKernelCode function");

            var mem = new TextMemoryPlugin(_kernel.Memory);
            _kernel.ImportFunctions(mem, "textmemory");
            var templateEng = _kernel.PromptTemplateEngine;
            context.Variables["skcollection"] = "skDocsCollection";
            var template = "{{textmemory.recall input='What is Semantic Kernel' collection=$skcollection}}";
            var content = await templateEng.RenderAsync(template, context);
            Console.WriteLine($"Proof of text memory skill fuctioning:\n\n{content}");
            var codeResult = "";
            try
            {
                var code = await _kernel.RunAsync(context.Variables, _generateKernelCodeFunction);
                codeResult = code.Result().Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            }
            catch (Exception ex)
            {
                codeResult = $"//Error generating code: {ex.Message}";
            }
            context.Variables.Set("existingCode", codeResult);
            var currentCode = "";

            var newExistingCode = $"{currentCode}\n{codeResult}";
            var refs = CompileResources.PortableExecutableReferences;
            try
            {
                var result = await _compilerService.SubmitCode(codeResult, refs);
                return JsonSerializer.Serialize(new CodeOutputModel { Output = result, Code = codeResult });
            }
            catch (Exception ex)
            {
                var exStr = ex.ToString();
                return JsonSerializer.Serialize(new CodeOutputModel { Output = exStr, Code = codeResult });
            }
        }

    }
}

