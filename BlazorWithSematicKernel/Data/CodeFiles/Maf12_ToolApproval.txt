namespace SkPluginLibrary.Examples;

/// <summary>
/// Human-in-the-loop tool approval - something Semantic Kernel never had. Wrapping a function in
/// ApprovalRequiredAIFunction makes the agent pause and ask before it runs.
/// </summary>
public static class Maf12_ToolApproval
{
    [Description("Deletes a file. Destructive.")]
    private static string DeleteFile([Description("File name")] string name) => $"(pretend) deleted {name}";

    public static async Task RunAsync()
    {
        AIAgent agent = new ChatClientAgent(ChatClientFactory.Create(AIModel.Gpt41Mini), new ChatClientAgentOptions
        {
            Name = "FileAssistant",
            ChatOptions = new ChatOptions
            {
                Instructions = "You manage files using the tools.",
                Tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeleteFile))]
            }
        });

        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync("Please delete old-report.txt", session);

        // The run stops with approval requests instead of calling the tool.
        var approvals = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
        foreach (var request in approvals)
        {
            Console.WriteLine($"Approval requested: {request.ToolCall}");
            // In a real app, ask the user here. This sample approves automatically.
            var reply = new ChatMessage(ChatRole.User, [request.CreateResponse(approved: true)]);
            response = await agent.RunAsync(reply, session);
        }
        Console.WriteLine(response.Text);
    }
}
