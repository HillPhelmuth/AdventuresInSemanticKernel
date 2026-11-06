using System;
using System.Linq;
using System.Threading.Tasks;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class Program
{
    public static async Task Main()
    {
        var definition = new AgentDefinition { Name = "Calculator", Instructions = "Use the multiply tool." };
        var tool = AIFunctionFactory.Create((double a, double b) => a * b, "multiply");
        using var client = AgentFactory.CreateClient(SampleContext.Current, definition);
        var agent = AgentFactory.Create(client, definition, [new ApprovalRequiredAIFunction(tool)]);
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync("Multiply 6 by 7.", session);
        foreach (var request in response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>())
        {
            Console.WriteLine("Approval requested: " + request.ToolCall);
            Console.WriteLine("Resume on this session with request.CreateResponse(true or false) after a human decision.");
        }
        Console.WriteLine(response.Text);
    }
}
