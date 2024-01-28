using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SkPluginLibrary.Plugins
{
    public class StreamingPlugin
    {
        [KernelFunction, Description("Temp to see what can be returned from an SKFunction")]
        public async IAsyncEnumerable<string> TryStreamFunction(string input)
        {
            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(100);
                yield return $"{i} -- Yeah!";
            }
        }

        [KernelFunction, Description("Generates a streaming chat response")]
        public async IAsyncEnumerable<string> ExecuteChatStreamResponse([Description("User's latest input")] string input,
            [Description("System prompt Instructions for chat model")] string systemPrompt = "",
            [Description(HistoryDescription)] string history = "")
        {
            var chatKernel = CoreKernelService.ChatCompletionKernel();
            


            var chatService = chatKernel.GetRequiredService<IChatCompletionService>();
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
            await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chatHistory,
                               new OpenAIPromptExecutionSettings { Temperature = 1.0, TopP = 1.0 }))
            {
                yield return token.ToString();
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
}
