using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planners;
using System.Text.Json;

namespace SkPluginLibrary.Models.Helpers
{
    public static class ResultExtensions
    {
        public static string Result(this KernelResult result)
        {
            var value = result.GetValue<object>();
            if (IsAsyncEnumerable(value)) return $"This is an IAsyncEnumerable Collection: {value?.GetType().FullName}";
            return value switch
            {
                RestApiOperationResponse apiOperationResponse => apiOperationResponse.Content.ToString() ?? "",
                string stringResult => stringResult,
                _ => JsonSerializer.Serialize(value)
            };
        }
        public static string Result(this FunctionResult result)
        {
            var value = result.GetValue<object>();
            if (IsAsyncEnumerable(value)) return $"This is an IAsyncEnumerable Collection: {value?.GetType().FullName}";
            return value switch
            {
                RestApiOperationResponse apiOperationResponse => apiOperationResponse.Content.ToString() ?? "",
                string stringResult => stringResult,
                _ => JsonSerializer.Serialize(value)
            };
        }

        public static IAsyncEnumerable<TResult> ResultStream<TResult>(this KernelResult result)
        {
            var value = result.GetValue<object>();
            if (value is IAsyncEnumerable<object> asyncEnumerable)
            {
                return (IAsyncEnumerable<TResult>)asyncEnumerable;
            }

            throw new Exception("This is not a streaming object");
        }

        public static bool IsStreamingResult(this KernelResult result)
        {
            var value = result.GetValue<object>();
            return IsAsyncEnumerable(value);
        }
        public static bool IsAsyncEnumerable(object? obj)
        {
            if (obj == null) return false;

            var type = obj.GetType();
            return type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }

        public static StepwiseExecutionResult AsStepwiseExecutionResult(this KernelResult kernelResult, StepwisePlannerConfig plannerConfig)
        {
            var planResult = kernelResult.FunctionResults.First();
            var result = kernelResult.GetValue<string>()!;

            return StepwiseExecutionResult(plannerConfig, result, planResult);
        }

        public static StepwiseExecutionResult AsStepwiseExecutionResult(this FunctionResult functionResult,
            StepwisePlannerConfig plannerConfig)
        {
            var result = functionResult.GetValue<string>()!;

            return StepwiseExecutionResult(plannerConfig, result, functionResult);
        }
        private static StepwiseExecutionResult StepwiseExecutionResult(StepwisePlannerConfig plannerConfig, string result,
            FunctionResult planResult)
        {
            StepwiseExecutionResult currentExecutionResult = new();
            if (result.Contains("Result not found, review _stepsTaken to see what", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Could not answer question in " + plannerConfig.MaxIterations + " iterations");
                currentExecutionResult.Answer = "Could not answer question in " + plannerConfig.MaxIterations + " iterations";
            }
            else
            {
                currentExecutionResult.Answer = result;
            }

            if (planResult.TryGetMetadataValue("stepCount", out string stepCount))
            {
                Console.WriteLine("Steps Taken: " + stepCount);
                currentExecutionResult.StepsTaken = stepCount;
            }

            if (planResult.TryGetMetadataValue("functionCount", out string functionCount))
            {
                Console.WriteLine("Functions Used: " + functionCount);
                currentExecutionResult.Functions = functionCount;
            }

            if (planResult.TryGetMetadataValue("iterations", out string iterations))
            {
                Console.WriteLine("Iterations: " + iterations);
                currentExecutionResult.Iterations = iterations;
            }

            return currentExecutionResult;
        }
    }
}
