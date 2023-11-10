using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Plugins
{
    public class AskUserPlugin
    {
        private readonly IKernel _kernel;

        public AskUserPlugin(IKernel kernel)
        {
            _kernel = kernel;
            kernel.FunctionInvoking += FunctionInvokedHandler;
            kernel.FunctionInvoked += FunctionInvokedHandler;
        }

        private void FunctionInvokedHandler(object? sender, FunctionInvokingEventArgs e)
        {
            var originalOutput = e.SKContext.Result; 
        }

        private void FunctionInvokedHandler(object? sender, FunctionInvokedEventArgs args)
        {

        }
    }
}
