namespace SkPluginLibrary.Models.Hooks
{
    public class FunctionFilterHook : IFunctionInvocationFilter
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

    public class AutoInvokeFilter : IAutoFunctionInvocationFilter
    {
        public event EventHandler<AutoFunctionInvocationContext>? FunctionInvoking;
        public event EventHandler<AutoFunctionInvocationContext>? FunctionInvoked;
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            FunctionInvoking?.Invoke(this, context);
            await next(context);
            FunctionInvoked?.Invoke(this, context);
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
