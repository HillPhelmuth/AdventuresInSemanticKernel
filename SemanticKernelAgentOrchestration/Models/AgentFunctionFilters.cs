using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models
{
    /// <summary>Describes one tool call made by an agent.</summary>
    public class AgentFunctionCall(AIFunction function, AIFunctionArguments arguments, object? result = null) : EventArgs
    {
        public AIFunction Function { get; } = function;
        public AIFunctionArguments Arguments { get; } = arguments;
        public object? Result { get; set; } = result;
    }

    /// <summary>
    /// Agent Framework function-calling middleware that raises events before and after each tool call.
    /// Replaces the Semantic Kernel IFunctionInvocationFilter. Attach with
    /// <c>new AIAgentBuilder(agent).Use(middleware.InvokeAsync).Build()</c>.
    /// </summary>
    public class AgentFunctionMiddleware
    {
        public event EventHandler<AgentFunctionCall>? FunctionInvoking;
        public event EventHandler<AgentFunctionCall>? FunctionInvoked;

        public async ValueTask<object?> InvokeAsync(Microsoft.Agents.AI.AIAgent agent, FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
        {
            FunctionInvoking?.Invoke(this, new AgentFunctionCall(context.Function, context.Arguments));
            var result = await next(context, cancellationToken);
            FunctionInvoked?.Invoke(this, new AgentFunctionCall(context.Function, context.Arguments, result));
            return result;
        }
    }
}
