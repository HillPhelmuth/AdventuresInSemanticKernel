namespace SkPluginLibrary.Examples;

/// <summary>The same agent code running against OpenRouter (an OpenAI-compatible gateway) instead of OpenAI directly.</summary>
public static class Maf13_OpenRouterModels
{
    public static async Task RunAsync()
    {
        // Requires an OpenRouter section in configuration (ApiKey, ModelId, Endpoint).
        IChatClient client = ChatClientFactory.Create(AIModel.OpenRouterClaudeSonnet);
        AIAgent agent = new ChatClientAgent(client, instructions: "You answer in one short sentence.", name: "RouterAgent");
        Console.WriteLine((await agent.RunAsync("Which model are you?")).Text);
    }
}
