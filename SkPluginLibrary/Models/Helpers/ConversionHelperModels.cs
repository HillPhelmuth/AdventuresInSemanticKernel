using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Models.Helpers
{
    internal class ConversionHelperModels
    {
    }
    public class SKContext
    {
        public SKContext()
        {
        }
        public IDictionary<string, object> Variables { get; set; }
        public KernelArguments Arguments { get; set; }


    }
    public static class TempExtensions
    {
        [Obsolete("Temporary method to ease conversion to rc version")]
        public static void Update(this IDictionary<string, object> dictionary, object? input = null)
        {


        }
        public static KernelArguments ToKernelArguments(this IDictionary<string, object?> dictionary)
        {
            return new KernelArguments(dictionary);
        }
        public static async Task<FunctionResult?> InvokeAsync(this Kernel kernel,CancellationToken token, params KernelFunction[] functions)
        {
            FunctionResult? currentResult = null;
            foreach (var function in functions)
            {
                var kernelArgs = new KernelArguments() { { "input", currentResult?.Result()} };
                FunctionResult? result = await function.InvokeAsync(kernel, kernelArgs, token);
                if (result is { })
                    currentResult = result;
            }
            return currentResult;
        }
    }
}
