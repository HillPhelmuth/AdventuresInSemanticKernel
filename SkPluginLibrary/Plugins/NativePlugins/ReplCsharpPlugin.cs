using System.Text.Json;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class ReplCsharpPlugin
{
    private readonly AIFunction _generateCodeFunction;
    private readonly AIFunction _generateScriptFunction;
    private readonly CompilerService _compilerService;
    private readonly ScriptService _scriptService;
    private readonly IChatClient _chatClient;

    public ReplCsharpPlugin(IChatClient chatClient, ScriptService scriptService, CompilerService compilerService)
    {
        _scriptService = scriptService;
        _compilerService = compilerService;
        _chatClient = chatClient;
        var codingPlugin = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("CodingPlugin", () => chatClient);
        _generateCodeFunction = codingPlugin.GetFunction("CodeCSharp") ?? throw new Exception("Could not find CodeCSharp function in CodingPlugin");
        _generateScriptFunction = codingPlugin.GetFunction("CSharpScript") ?? throw new Exception("Could not find CSharpScript function in CodingPlugin");
    }

    public ReplCsharpPlugin(IChatClient chatClient) : this(chatClient, new ScriptService(), new CompilerService())
    {
    }
    [Description("Describe c# code to both generate and execute")]
    [return: Description("Object contains the output, the generated code snippet, and the full updated code that includes the generated snippet")]
    public async Task<CodeOutputModel> ReplConsoleAsync(string input, [Description("Previously written or generated code")] string? existingCode)
    {
        var code = await _generateCodeFunction.InvokeToStringAsync(new() { ["input"] = input, ["existingCode"] = existingCode });
        var codeResult = code.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
        var combinedCode = $"{existingCode}\n{codeResult}";
        var refs = CompileResources.PortableExecutableReferences;
        var result = await _compilerService.SubmitCode(codeResult, refs);
        return new CodeOutputModel { Output = result, Code = codeResult, ExistingCode = combinedCode };
    }
    [Description("Describe c# code to generate and execute as a script")]
    public async Task<CodeOutputModel> ReplScriptAsync(string input, [Description("Previously written or generated code")] string? existingCode)

    {
        var code = await _generateScriptFunction.InvokeToStringAsync(new() { ["input"] = input, ["existingCode"] = existingCode });
        var codeResult = code.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
        var combinedCode = $"{existingCode}\n{codeResult}";
        //var existingCode = combinedCode.TrimStart('\n');
        var refs = CompileResources.PortableExecutableReferences;
        var scriptResult = await _scriptService.EvaluateAsync(codeResult);
        //context.Variables["existingCode"] = existingCode;
        return new CodeOutputModel { Output = scriptResult, Code = codeResult, ExistingCode = combinedCode };
    }

    [Description("Execute the provided c# code. The code must be complete and compilable")]
    [return: Description("Console output of executed c# code")]
    public async Task<string> ExecuteCode([Description("C# code to execute")] string input)
    {
        input = input.Replace("```csharp", "").Replace("```", "").TrimStart('\n');
        var result = await _compilerService.SubmitCode(input, CompileResources.PortableExecutableReferences);
        return result;
    }


    [Description(
         "Seperates c# code into it's distinct syntax elements and describes each element in plain language")]
    public async Task<string> SummarizeCodeSyntaxElements([Description("The c# code to analyze")] string input)
    {
        var elementsCollections = CodeElementsDescriptionsModel.ExtractCodeElements(input);
        var elements = elementsCollections.GetAllSyntaxDescriptions();
        var tasks = elements.Select(GenerateSnippitDoc).ToList();
        var elementsWithDescriptions = await Task.WhenAll(tasks);
        return JsonSerializer.Serialize(elementsWithDescriptions, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<CodeElementDescription> GenerateSnippitDoc(CodeElementDescription codeElementDescription)
    {
        codeElementDescription.GeneratedDescription = await PromptRunner.RunAsync(_chatClient,
            "Generate a summary of the c# code snippet. Be as detailed and specific as possible.[Snippet]\n```csharp\n{{$input}}\n````",
            new Dictionary<string, object?> { ["input"] = codeElementDescription.Code },
            new ChatOptions { MaxOutputTokens = 512, Temperature = 0.5f }, "You are a c# code documentation expert");
        return codeElementDescription;
    }
}
