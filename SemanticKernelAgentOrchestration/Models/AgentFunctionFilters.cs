using Microsoft.SemanticKernel;

namespace SemanticKernelAgentOrchestration.Models
{
    public class AgentFunctionFilters : IFunctionInvocationFilter
    {
        public event EventHandler<FunctionInvocationContext>? FunctionInvoking;
        public event EventHandler<FunctionInvocationContext>? FunctionInvoked;
        public void OnFunctionInvoking(FunctionInvocationContext context)
        {
            FunctionInvoking?.Invoke(this, context);
        }

        public void OnFunctionInvoked(FunctionInvocationContext context)
        {
            FunctionInvoked?.Invoke(this, context);
        }

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            OnFunctionInvoking(context);
            await next.Invoke(context);
            OnFunctionInvoked(context);
        }
    }
    public class AgentAutoInvokeFilter : IAutoFunctionInvocationFilter
    {
        public event EventHandler<AutoFunctionInvocationContext>? AutoFunctionInvoking;
        public event EventHandler<AutoFunctionInvocationContext>? AutoFunctionInvoked;
	    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
	    {
            AutoFunctionInvoking?.Invoke(this, context);
            await next.Invoke(context);
            AutoFunctionInvoked?.Invoke(this, context);
	    }
    }
    public class PromptFilterHook : IPromptRenderFilter
    {
        public event EventHandler<PromptRenderContext>? PromptRendered;
        public event EventHandler<PromptRenderContext>? PromptRendering;
        public void OnPromptRendering(PromptRenderContext context)
        {
            PromptRendering?.Invoke(this, context);
        }

        public void OnPromptRendered(PromptRenderContext context)
        {
            PromptRendered?.Invoke(this, context);
        }

        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            OnPromptRendering(context);
            await next.Invoke(context);
            OnPromptRendered(context);
        }
    }
}
