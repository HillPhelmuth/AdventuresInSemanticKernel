namespace SkPluginLibrary.Examples;

/// <summary>
/// Tools replace Semantic Kernel plugins. Any C# method becomes an AIFunction with AIFunctionFactory,
/// and an agent just takes a list of them.
/// </summary>
public static class Maf03_AgentWithTools
{
    [Description("Gets the current weather for a city")]
    private static string GetWeather([Description("The city name")] string city) => $"It is 21 degrees and sunny in {city}.";

    [Description("Gets the current UTC time")]
    private static string GetTime() => DateTime.UtcNow.ToString("t");

    public static async Task RunAsync()
    {
        AIAgent agent = new ChatClientAgent(ChatClientFactory.Create(AIModel.Gpt41Mini), new ChatClientAgentOptions
        {
            Name = "Assistant",
            ChatOptions = new ChatOptions
            {
                Instructions = "Use the tools to answer questions about weather and time.",
                Tools = [AIFunctionFactory.Create(GetWeather), AIFunctionFactory.Create(GetTime)]
            }
        });
        Console.WriteLine((await agent.RunAsync("What's the weather in Lisbon, and what time is it?")).Text);
    }
}
