namespace SkPluginLibrary.Examples;

/// <summary>Streaming an agent response token by token.</summary>
public static class Maf04_StreamingAgent
{
    public static async Task RunAsync()
    {
        AIAgent agent = new ChatClientAgent(ChatClientFactory.Create(AIModel.Gpt41Mini), new ChatClientAgentOptions
        {
            Name = "Storyteller",
            ChatOptions = new ChatOptions { Instructions = "You tell very short bedtime stories." }
        });
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Tell me a story about a curious robot."))
        {
            Console.Write(update.Text);
        }
        Console.WriteLine();
    }
}
