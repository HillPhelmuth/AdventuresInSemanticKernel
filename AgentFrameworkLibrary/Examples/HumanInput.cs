using System;
using System.Threading.Tasks;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;
using Microsoft.Agents.AI;

public static class Program
{
    public static async Task Main()
    {
        using var client = new HumanParticipantClient("Reviewer", (prompt, token) =>
        {
            Console.WriteLine(prompt);
            return Task.FromResult("This sample supplies an explicit host answer. The workflow UI awaits a real user here.");
        });
        var definition = new AgentDefinition { Name = "Human reviewer", IsUserProxy = true };
        var agent = AgentFactory.Create(client, definition, []);
        Console.WriteLine((await agent.RunAsync("Review this plan.")).Text);
    }
}
