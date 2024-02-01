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
}
