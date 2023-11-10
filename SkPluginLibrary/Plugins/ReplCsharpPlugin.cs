using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Plugins.Memory;
using SkPluginLibrary.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.TemplateEngine.Basic;

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
        public async Task<CodeOutputModel> ReplConsoleAsync(string input, [SKName("existingCode"), Description("Previously written or generated code")] string? existingCode, SKContext context)
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
            return new CodeOutputModel { Output = result, Code = codeResult };
        }
        [SKFunction, SKName("ReplScript"), Description("Describe c# code to generate and execute as a script")]
        public async Task<CodeOutputModel> ReplScriptAsync(string input, [SKName("existingCode"), Description("Previously written or generated code")] string? existingCode, SKContext context)

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
            return new CodeOutputModel { Output = result, Code = codeResult };
            //return JsonSerializer.Serialize(new CodeOutputModel { Output = result, Code = codeResult });
        }

        [SKFunction, Description("Execute provided c# code. Returns the console output")]
        public async Task<string> ExecuteCode([Description("C# code to execute")]string input)
        {
            input = input.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            var result = await _compilerService.SubmitCode(input, CompileResources.PortableExecutableReferences);
            return result;
        }
        

        [SKFunction,
         Description(
             "Seperates c# code into it's distinct syntax elements and describes each element in plain language")]
        public async Task<string> SummarizeCodeSyntaxElements([Description("The c# code to analyze")] string input)
        {
            var summaryFunc = _kernel.CreateSemanticFunction("Generate a summary of the c# code snippet. Be as detailed and specific as possible.", requestSettings: new OpenAIRequestSettings {ChatSystemPrompt = "You are a c# code documentation expert", MaxTokens = 512, Temperature = 0.5});
            var elementsCollections = CodeElementsDescriptionsModel.ExtractCodeElements(input);
            var elements = elementsCollections.GetAllSyntaxDescriptions();
            var tasks = new List<Task<CodeElementDescription>>();
            foreach (var element in elements)
            {
                var task = GenerateSnippitDoc(element);
                tasks.Add(task);
            }
            var elementsWithDescriptions = await Task.WhenAll(tasks);
            return JsonSerializer.Serialize(elementsWithDescriptions, new JsonSerializerOptions {WriteIndented = true});

        }

        private async Task<CodeElementDescription> GenerateSnippitDoc(CodeElementDescription codeElementDescription)
        {
            var kernel = CoreKernelService.ChatCompletionKernel("gpt-3.5-turbo-1106");
            var function = kernel.CreateSemanticFunction("Generate a summary of the c# code snippet. Be as detailed and specific as possible.[Snippet]\n```csharp\n{{$input}}\n````", requestSettings: new OpenAIRequestSettings { ChatSystemPrompt = "You are a c# code documentation expert", MaxTokens = 512, Temperature = 0.5 });
            var codeSnippet = codeElementDescription.Code;
            var kernelResult = await kernel.RunAsync(codeElementDescription.Code, function);
            codeElementDescription.GeneratedDescription = kernelResult.Result();
            return codeElementDescription;
        }
    }
}

