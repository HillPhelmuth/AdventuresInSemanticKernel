using System;
using System.Threading.Tasks;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;
using Microsoft.Agents.AI;

public static class Program
{
    public static async Task Main()
    {
        var definition = new AgentDefinition { Name = "Guide", Instructions = "Explain Agent Framework briefly." };
        using var client = AgentFactory.CreateClient(SampleContext.Current, definition);
        var agent = AgentFactory.Create(client, definition, []);
        var session = await agent.CreateSessionAsync();
        await foreach (var update in agent.RunStreamingAsync("What is an agent session?", session))
            Console.Write(update.Text);
    }
}
