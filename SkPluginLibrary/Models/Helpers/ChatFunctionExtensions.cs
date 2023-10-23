using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Microsoft.SemanticKernel.Orchestration;

namespace SkPluginLibrary.Models.Helpers
{
    public static class ChatFunctionExtensions
    {
        // ReSharper disable once InconsistentNaming
        public static IList<OpenAIFunction> ToOpenAIFunctions(this IReadOnlyFunctionCollection functionCollection)
        {
            return functionCollection.GetFunctionViews().Select(f => f.ToOpenAIFunction()).ToList();
        }
        // ReSharper disable once InconsistentNaming
        public static async Task<KernelResult?> FromOpenAIFunction(this IReadOnlyFunctionCollection functionCollection,
            OpenAIFunctionResponse? functionResponse, IKernel kernel)
        {
            if (functionResponse is null) return null;
            if (!functionCollection.TryGetFunctionAndContext(functionResponse, out var function, out var context))
                return null;
            return await kernel.RunAsync(context, function);
        }
    }
}
