using Microsoft.SemanticKernel;
using SkPluginComponents;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkPluginComponents.Models;
using Microsoft.SemanticKernel.Events;

namespace SkPluginLibrary.Plugins
{
    public class RecursivePlugin(AskUserService askUserService)
    {
        private CancellationTokenSource _cancellationSource = new();

        [SKFunction, Description("Run a recursive Ask/Do/Show function")]
        public async Task RunRecursive()
        {
            var token = _cancellationSource.Token;
            var kernel = CoreKernelService.ChatCompletionKernel();
            kernel.FunctionInvoked += HandleFunctionResult;
            var askUserPlugin = new AskUserPlugin(askUserService);
            var ask = kernel.ImportFunctions(askUserPlugin);
            var writer = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "WriterPlugin");
            var brainstormFunction = writer["Brainstorm"];
            var recursive = new RecursivePlugin(askUserService);
            var thisFunc = kernel.ImportFunctions(recursive);
            await kernel.RunAsync(token, ask["AskUser"], brainstormFunction, ask["TellUser"], thisFunc.First().Value);
        }
        private void HandleFunctionResult(object? sender, FunctionInvokedEventArgs args)
        {
            var results = args.SKContext.Result;
            var functionName = args.FunctionView.Name;
            if (!functionName.Contains("AskUser")) return;
            if (results.Equals("stop", StringComparison.InvariantCultureIgnoreCase))
            {
                _cancellationSource.Cancel();
            }
        }
    }
}
