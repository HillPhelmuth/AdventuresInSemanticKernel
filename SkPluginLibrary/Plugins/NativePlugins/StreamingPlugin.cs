using System.Text.Json;
using ChatComponents;
using Microsoft.Extensions.AI;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class StreamingPlugin(ITokenization tokenization)
{

    [Description("Temp to see what can be returned from an SKFunction")]
    public async IAsyncEnumerable<string> TryStreamFunction(string input)
    {
        for (var i = 0; i < 100; i++)
        {
            await Task.Delay(100);
            yield return $"{i} -- Yeah!";
        }
    }

    [Description("Streaming logprobs in a human readable format")]
    public IAsyncEnumerable<TokenString> StreamingLogProbs([Description("Prompt used to generate log probs")] string prompt, [Description("Maximum token count for which to get the log prob values")]int maxTokens = 3)
    {
        var history = new ChatHistory("You are a helpful AI assistant");
        history.AddUserMessage(prompt);
        return tokenization.GetStreamingLogProbs(history,maxTokens:maxTokens);
    }

    [Description("Generates a streaming chat response")]
    public async IAsyncEnumerable<string> ExecuteChatStreamResponse([Description("User's latest input")] string input,
        [Description("System prompt Instructions for chat model")] string systemPrompt = "",
        [Description(HistoryDescription)] string history = "")
    {
        var chatClient = ChatClientFactory.Create(AIModel.Gpt41Mini);
        var chatHistory = new ChatHistory(systemPrompt);
        try
        {
            var template = new[]
            {
                    new
                    {
                        user = "",
                        assistant = 0
                    }
                };
            dynamic historyList = JsonSerializer.Deserialize(history, template.GetType());
            foreach (var item in historyList)
            {
                chatHistory.AddUserMessage(item.user);
                chatHistory.AddAssistantMessage(item.assistant);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unable to add chat history to conversation.\nError:\n\n {e}");
        }

        chatHistory.AddUserMessage(input);
        await foreach (var update in chatClient.GetStreamingResponseAsync(chatHistory, new ChatOptions { Temperature = 1.0f, TopP = 1.0f }))
        {
            yield return update.Text;
        }
    }

    private const string HistoryDescription = """
                                              Chat history of current chat. In json format:

                                              [
                                                {
                                                  user: "user input",
                                                  assistant: "assistant repsonse"
                                                }
                                              ]
                                              """;
}