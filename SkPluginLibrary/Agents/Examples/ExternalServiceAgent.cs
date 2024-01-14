using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Experimental.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace SkPluginLibrary.Agents.Examples;

public class ExternalServiceAgent
{
    private const string AdvisorPromptTemplate = """
                                                 Advise clients on tax filing, taxes and financial matters.
                                                 Advise them based on their provided refund/owe status and amount.
                                                 You will need the clients userName first. Use that to get their refund status and amount.
                                                 If you aren't provided with any information you require, politely request it.
                                                 """;
    private static readonly List<IAgent> Agents = [];
    private ChatHistory _chatHistory = [];
    public async IAsyncEnumerable<string> ChatStream(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = CreateKernel();
        var plugin = await kernel.ImportPluginFromOpenApiAsync("RefundPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "ExternalServiceExamplePlugin", "openapi.json"), cancellationToken: cancellationToken);
        var settings = new OpenAIPromptExecutionSettings() { ChatSystemPrompt = AdvisorPromptTemplate, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, MaxTokens = 512 };
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory.AddSystemMessage(AdvisorPromptTemplate);
        _chatHistory.AddUserMessage(input);
        var response = "";
        await foreach (var message in chat.GetStreamingChatMessageContentsAsync(_chatHistory, settings, kernel, cancellationToken))
        {
            var content = message.Content;
            response += content;
            yield return content;
        }
        _chatHistory.AddAssistantMessage(response);
    }
    public static async Task GenerateRefundAgent()
    {
        var kernel = CreateKernel();
        var plugin = await kernel.ImportPluginFromOpenApiAsync("RefundPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "ExternalServiceExamplePlugin", "openapi.json"));
        var agent = await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName("Refund Agent")
            .WithDescription("Gets refund information for a user")
            .WithInstructions("Get refund information for a user based on their userName as requested")
            .WithPlugin(plugin).BuildAsync();
        Agents.Add(agent);

    }
    public static async Task GenerateAdvisorAgent()
    {
        var function = KernelFunctionFactory.CreateFromPrompt(AdvisorPromptTemplate, functionName: "Advise", description: "Provides tax and financial advice based on refund status and amount");
    }
    private static Kernel CreateKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));
        kernelBuilder.AddOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey);
        var kernel = kernelBuilder.Build();
        return kernel;
    }
}