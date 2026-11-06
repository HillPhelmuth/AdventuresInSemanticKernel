using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models
{
    /// <summary>
    /// Agent Framework function-calling middleware that raises events before and after each tool call.
    /// Replaces the Semantic Kernel IFunctionInvocationFilter. Attach with
    /// <c>new AIAgentBuilder(agent).Use(middleware.InvokeAsync).Build()</c>.
    /// </summary>
    public class AgentFunctionMiddleware
    {
        public event EventHandler<FunctionInvocationContext>? FunctionInvoking;
        public event EventHandler<FunctionInvocationContext>? FunctionInvoked;

        public async ValueTask<object?> InvokeAsync(Microsoft.Agents.AI.AIAgent agent, FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
        {
            FunctionInvoking?.Invoke(this, context);
            var result = await next(context, cancellationToken);
            FunctionInvoked?.Invoke(this, context);
            return result;
        }
    }
}
