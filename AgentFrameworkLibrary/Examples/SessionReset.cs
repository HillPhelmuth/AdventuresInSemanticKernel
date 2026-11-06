using System;
using System.Threading.Tasks;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;

public static class Program
{
    public static async Task Main()
    {
        var definition = new AgentDefinition();
        using var client = AgentFactory.CreateClient(SampleContext.Current, definition);
        await using var conversation = new AgentConversation(AgentFactory.Create(client, definition, []));
        await foreach (var update in conversation.RunAsync("Remember the word lavender.")) Console.Write(update.Text);
        await foreach (var update in conversation.RunAsync("What word did I give you?")) Console.Write(update.Text);
        await conversation.ResetAsync();
        Console.WriteLine("\nA new session will be created on the next turn.");
    }
}
