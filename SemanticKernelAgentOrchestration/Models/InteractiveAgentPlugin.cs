using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel;

namespace SemanticKernelAgentOrchestration.Models;

public class InteractiveAgentPlugin : KernelPlugin
{
    private const string AgentFunctionName = "RunAgent";
    public InteractiveAgentPlugin(string name, ChatAgent agent, string? description = null) : base(name, description)
    {
        var runAgentFunction = KernelFunctionFactory.CreateFromMethod(new Func<string, CancellationToken, Task<ChatMessageContent>>(agent.RunAgent!), AgentFunctionName, agent.Description);
        Functions[AgentFunctionName] = runAgentFunction;        
    }
    public Dictionary<string, KernelFunction> Functions { get; } = []; 

    public override bool TryGetFunction(string name, [UnscopedRef] out KernelFunction function)
    {
        return Functions.TryGetValue(name, out function!);
    }

    public override IEnumerator<KernelFunction> GetEnumerator()
    {
        return Functions.Values.GetEnumerator();
    }

    public override int FunctionCount => Functions.Count;
}