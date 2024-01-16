using Microsoft.SemanticKernel;
using SkPluginLibrary.Services;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Models.Helpers;

namespace SkPluginLibrary.Plugins
{
    public class ReplCsharpPlugin
    {
        private readonly KernelFunction _generateCodeFunction;
        private readonly KernelFunction _generateScriptFunction;
        private readonly CompilerService _compilerService;
        private readonly ScriptService _scriptService;
        private readonly Kernel _kernel;


        public ReplCsharpPlugin(Kernel kernel, ScriptService scriptService, CompilerService compilerService)
        {
            _scriptService = scriptService;
            _compilerService = compilerService;
            _generateCodeFunction = kernel.ImportPluginFromPromptDirectoryYaml("CodingPlugin")["CodeCSharp"];
            _generateScriptFunction = kernel.ImportPluginFromPromptDirectoryYaml("CodingPlugin")["CSharpScript"];
            _kernel = kernel;

        }

        public ReplCsharpPlugin(Kernel kernel)
        {
            var codingPlugin = kernel.ImportPluginFromPromptDirectoryYaml("CodingPlugin");
            _generateCodeFunction = codingPlugin["CodeCSharp"];
            _generateScriptFunction = codingPlugin["CSharpScript"];
            _scriptService = new ScriptService();
            _compilerService = new CompilerService();
            _kernel = kernel;
        }
        [KernelFunction("ReplConsole"), Description("Describe c# code to both generate and execute")]
        [return: Description("Object contains the output, the generated code snippet, and the full updated code that includes the generated snippet")]
        public async Task<CodeOutputModel> ReplConsoleAsync(string input, [Description("Previously written or generated code")] string? existingCode)
        {
            var args = new KernelArguments
            {
                ["input"] = input,
                ["existingCode"] = existingCode
            };
            var code = await _kernel.InvokeAsync(_generateCodeFunction, args);
            Console.WriteLine($"Result Type = {code.ValueType?.Name}");
            var codeResult = code.Result().Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            var combinedCode = $"{existingCode}\n{codeResult}";
            var refs = CompileResources.PortableExecutableReferences;
            var result = await _compilerService.SubmitCode(codeResult, refs);
            return new CodeOutputModel { Output = result, Code = codeResult, ExistingCode = combinedCode };
        }
        [KernelFunction("ReplScript"), Description("Describe c# code to generate and execute as a script")]
        public async Task<CodeOutputModel> ReplScriptAsync(string input, [Description("Previously written or generated code")] string? existingCode)

        {
            var args = new KernelArguments
            {
                ["input"] = input,
                ["existingCode"] = existingCode
            };
            var code = await _kernel.InvokeAsync(_generateScriptFunction, args);
            var codeResult = code.Result().Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            var combinedCode = $"{existingCode}\n{codeResult}";
            //var existingCode = combinedCode.TrimStart('\n');
            var refs = CompileResources.PortableExecutableReferences;
            var scriptResult = await _scriptService.EvaluateAsync(codeResult);
            //context.Variables["existingCode"] = existingCode;
            return new CodeOutputModel { Output = scriptResult, Code = codeResult, ExistingCode = combinedCode };
        }

        [KernelFunction, Description("Execute the provided c# code. The code must be complete and compilable")]
        [return:Description("Console output of executed c# code")]
        public async Task<string> ExecuteCode([Description("C# code to execute")] string input)
        {
            input = input.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
            var result = await _compilerService.SubmitCode(input, CompileResources.PortableExecutableReferences);
            return result;
        }


        [KernelFunction,
         Description(
             "Seperates c# code into it's distinct syntax elements and describes each element in plain language")]
        public async Task<string> SummarizeCodeSyntaxElements([Description("The c# code to analyze")] string input)
        {
            var summaryFunc = _kernel.CreateFunctionFromPrompt("Generate a summary of the c# code snippet. Be as detailed and specific as possible.", executionSettings: new OpenAIPromptExecutionSettings { ChatSystemPrompt = "You are a c# code documentation expert", MaxTokens = 512, Temperature = 0.5 });
            var elementsCollections = CodeElementsDescriptionsModel.ExtractCodeElements(input);
            var elements = elementsCollections.GetAllSyntaxDescriptions();
            var tasks = elements.Select(GenerateSnippitDoc).ToList();
            var elementsWithDescriptions = await Task.WhenAll(tasks);
            return JsonSerializer.Serialize(elementsWithDescriptions, new JsonSerializerOptions { WriteIndented = true });

        }

        private async Task<CodeElementDescription> GenerateSnippitDoc(CodeElementDescription codeElementDescription)
        {
            var kernel = CoreKernelService.ChatCompletionKernel("gpt-3.5-turbo-1106");
            var function = kernel.CreateFunctionFromPrompt("Generate a summary of the c# code snippet. Be as detailed and specific as possible.[Snippet]\n```csharp\n{{$input}}\n````", executionSettings: new OpenAIPromptExecutionSettings { ChatSystemPrompt = "You are a c# code documentation expert", MaxTokens = 512, Temperature = 0.5 });
            var codeSnippet = codeElementDescription.Code;
            var kernelResult = await kernel.InvokeAsync(function, new KernelArguments() { { "input", codeSnippet } });
            codeElementDescription.GeneratedDescription = kernelResult.Result();
            return codeElementDescription;
        }
    }
}

