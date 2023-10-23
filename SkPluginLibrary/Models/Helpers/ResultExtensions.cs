using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Orchestration;
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
        public static bool IsAsyncEnumerable(object? obj)
        {
            if (obj == null) return false;

            var type = obj.GetType();
            return type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }
    }
}
