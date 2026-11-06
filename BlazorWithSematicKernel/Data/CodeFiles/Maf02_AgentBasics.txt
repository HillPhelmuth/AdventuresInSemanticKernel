namespace SkPluginLibrary.Examples;

/// <summary>A ChatClientAgent with instructions, run across several turns using an AgentSession.</summary>
public static class Maf02_AgentBasics
{
    public static async Task RunAsync()
    {
        AIAgent agent = new ChatClientAgent(ChatClientFactory.Create(AIModel.Gpt41Mini), new ChatClientAgentOptions
        {
            Name = "Pirate",
            ChatOptions = new ChatOptions { Instructions = "You are a friendly pirate. Keep answers under two sentences." }
        });

        // The session carries the conversation history between runs.
        AgentSession session = await agent.CreateSessionAsync();
        Console.WriteLine((await agent.RunAsync("Hi! My name is Adam.", session)).Text);
        Console.WriteLine((await agent.RunAsync("What is my name?", session)).Text);
    }
}
