using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>
/// A named group of related <see cref="AIFunction"/> tools. This is the Agent Framework / Microsoft.Extensions.AI
/// replacement for what used to be a Semantic Kernel plugin: Agent Framework agents simply take a flat
/// list of <see cref="AITool"/>s, so a "plugin" here is only a naming/grouping convenience for the UI.
/// </summary>
public class ToolPlugin : IEnumerable<AIFunction>
{
    public ToolPlugin(string name, IEnumerable<AIFunction> functions, string? description = null)
    {
        Name = name;
        Description = description ?? "";
        Functions = functions.ToList();
        foreach (var fn in Functions) Owners.AddOrUpdate(fn, name);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AIFunction, string> Owners = new();

    /// <summary>Name of the group a function was registered in, if any.</summary>
    public static string PluginNameOf(AIFunction function) => Owners.TryGetValue(function, out var n) ? n : "";

    public IEnumerator<AIFunction> GetEnumerator() => Functions.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Display metadata (name, description, parameters) for every function in the group.</summary>
    public List<FunctionMetadata> GetFunctionsMetadata() => Functions.Select(f => f.GetMetadata()).ToList();

    public string Name { get; }
    public string Description { get; }
    public List<AIFunction> Functions { get; }
    public int FunctionCount => Functions.Count;
    /// <summary>
    /// The functions as tools an agent can call. Names are prefixed with the group name
    /// (e.g. <c>TimePlugin_Now</c>) so tools from different groups never collide.
    /// </summary>
    public IEnumerable<AITool> Tools => Functions.Select(Prefixed);

    public AITool Prefixed(AIFunction function) => new PrefixedFunction(Name, function);

    private sealed class PrefixedFunction(string prefix, AIFunction inner) : DelegatingAIFunction(inner)
    {
        public override string Name { get; } = Sanitize($"{prefix}_{inner.Name}");
        private static string Sanitize(string name)
        {
            var clean = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
            return clean.Length > 64 ? clean[..64] : clean;
        }
    }

    public AIFunction? GetFunction(string name) =>
        Functions.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Creates a tool group from every public instance method on <paramref name="target"/> that has a
    /// <see cref="DescriptionAttribute"/> (the same convention the old [KernelFunction] methods followed).
    /// A trailing "Async" is trimmed from method names to keep tool names stable.
    /// </summary>
    public static ToolPlugin FromObject(object target, string? name = null, string? description = null)
    {
        var type = target.GetType();
        var functions = new List<AIFunction>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName || method.GetCustomAttribute<DescriptionAttribute>() is null) continue;
            // Streaming (IAsyncEnumerable) methods cannot be returned to a model as a tool result.
            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)) continue;
            var fnName = method.Name.EndsWith("Async") && method.Name.Length > 5 ? method.Name[..^5] : method.Name;
            functions.Add(AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions { Name = fnName }));
        }
        return new ToolPlugin(name ?? type.Name, functions, description);
    }

    /// <summary>Creates a tool group from explicit tools (e.g. from <c>AIFunctionFactory.Create</c>).</summary>
    public static ToolPlugin FromTools(string name, IEnumerable<AITool> tools, string? description = null) =>
        new(name, tools.OfType<AIFunction>(), description);
}


/// <summary>Display-friendly description of a tool: its group, name, description and parameters.</summary>
public record FunctionMetadata(string PluginName, string Name, string Description, List<ParameterMetadata> Parameters);

public record ParameterMetadata(string Name, string? Description, string? DefaultValue, bool IsRequired, Type? ParameterType = null);

public static class AIFunctionMetadataExtensions
{
    public static FunctionMetadata GetMetadata(this AIFunction function)
    {
        var parameters = new List<ParameterMetadata>();
        var schema = function.JsonSchema;
        if (schema.ValueKind == System.Text.Json.JsonValueKind.Object && schema.TryGetProperty("properties", out var props))
        {
            var required = schema.TryGetProperty("required", out var req) && req.ValueKind == System.Text.Json.JsonValueKind.Array
                ? req.EnumerateArray().Select(x => x.GetString()).ToHashSet() : [];
            foreach (var prop in props.EnumerateObject())
            {
                var description = prop.Value.TryGetProperty("description", out var d) ? d.GetString() : null;
                var def = prop.Value.TryGetProperty("default", out var dv) ? dv.ToString() : null;
                var type = prop.Value.TryGetProperty("type", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetString() : "string";
                var clr = type switch { "integer" => typeof(int), "number" => typeof(double), "boolean" => typeof(bool), _ => typeof(string) };
                parameters.Add(new ParameterMetadata(prop.Name, description, def, required.Contains(prop.Name), clr));
            }
        }
        return new FunctionMetadata(ToolPlugin.PluginNameOf(function), function.Name, function.Description, parameters);
    }
}
