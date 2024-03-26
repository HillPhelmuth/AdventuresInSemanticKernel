using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Models.Hooks
{
    public class FunctionFilterHook : IFunctionFilter
    {
        public event EventHandler<FunctionInvokingContext>? FunctionInvoking;
        public event EventHandler<FunctionInvokedContext>? FunctionInvoked;
        public void OnFunctionInvoking(FunctionInvokingContext context)
        {
            FunctionInvoking?.Invoke(this, context);
        }

        public void OnFunctionInvoked(FunctionInvokedContext context)
        {
            FunctionInvoked?.Invoke(this, context);
        }
    }
    public class PromptFilterHook : IPromptFilter
    {
        public event EventHandler<PromptRenderedContext>? PromptRendered;
        public event EventHandler<PromptRenderingContext>? PromptRendering;
        public void OnPromptRendering(PromptRenderingContext context)
        {
            PromptRendering?.Invoke(this, context);
        }

        public void OnPromptRendered(PromptRenderedContext context)
        {
            PromptRendered?.Invoke(this, context);
        }
    }
}
