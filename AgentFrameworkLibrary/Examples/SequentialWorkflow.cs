using System;
using System.Threading.Tasks;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public static class Program
{
    public static async Task Main()
    {
        var writer = new AgentDefinition { Name = "Writer", Instructions = "Write one sentence about space." };
        var reviewer = new AgentDefinition { Name = "Reviewer", Instructions = "Improve the sentence for clarity." };
        using var a = AgentFactory.CreateClient(SampleContext.Current, writer);
        using var b = AgentFactory.CreateClient(SampleContext.Current, reviewer);
        var workflow = AgentWorkflowBuilder.BuildSequential([
            AgentFactory.Create(a, writer, []), AgentFactory.Create(b, reviewer, [])]);
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, "Begin."));
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (var evt in run.WatchStreamAsync())
            if (evt is AgentResponseUpdateEvent update) Console.Write(update.Update.Text);
    }
}
