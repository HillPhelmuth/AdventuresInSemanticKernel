namespace SkPluginLibrary.Examples;

/// <summary>
/// The foundation of Agent Framework: a Microsoft.Extensions.AI IChatClient.
/// ChatClientFactory returns an OpenAI (or OpenRouter) client behind that one interface.
/// </summary>
public static class Maf01_ChatClient
{
    public static async Task RunAsync()
    {
        IChatClient client = ChatClientFactory.Create(AIModel.Gpt41Mini);

        // One-shot request
        var response = await client.GetResponseAsync("In one sentence, what is an AI agent?");
        Console.WriteLine(response.Text);

        // Streaming
        Console.WriteLine("\n--- streaming ---");
        await foreach (var update in client.GetStreamingResponseAsync("Write a haiku about C#."))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
    }
}
