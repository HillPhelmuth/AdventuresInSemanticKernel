namespace SkPluginLibrary.Examples;

/// <summary>A concurrent workflow: several agents work on the same input in parallel and their outputs are aggregated.</summary>
public static class Maf09_ConcurrentWorkflow
{
    public static async Task RunAsync()
    {
        var client = ChatClientFactory.Create(AIModel.Gpt41Mini);
        AIAgent physicist = new ChatClientAgent(client, instructions: "You are a physicist. Answer in two sentences from a physics perspective.", name: "Physicist");
        AIAgent chemist = new ChatClientAgent(client, instructions: "You are a chemist. Answer in two sentences from a chemistry perspective.", name: "Chemist");

        Workflow workflow = AgentWorkflowBuilder.BuildConcurrent([physicist, chemist]);
        var messages = new List<ChatMessage> { new(ChatRole.User, "Why is the sky blue?") };
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent output && output.Data is List<ChatMessage> result)
            {
                foreach (var message in result.Where(m => m.Role == ChatRole.Assistant))
                    Console.WriteLine($"[{message.AuthorName}] {message.Text}\n");
            }
        }
    }
}
