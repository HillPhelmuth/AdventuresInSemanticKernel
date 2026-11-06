using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>
/// A named group of related <see cref="AIFunction"/> tools. This is the Agent Framework / Microsoft.Extensions.AI
/// replacement for what used to be a Semantic Kernel plugin: Agent Framework agents simply take a flat
/// list of <see cref="AITool"/>s, so a "plugin" here is only a naming/grouping convenience for the UI.
/// </summary>
public class ToolPlugin
{
    public ToolPlugin(string name, IEnumerable<AIFunction> functions, string? description = null)
    {
        Name = name;
        Description = description ?? "";
        Functions = functions.ToList();
    }

    public string Name { get; }
    public string Description { get; }
    public List<AIFunction> Functions { get; }
    public int FunctionCount => Functions.Count;
    public IEnumerable<AITool> Tools => Functions;

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
            var fnName = method.Name.EndsWith("Async") && method.Name.Length > 5 ? method.Name[..^5] : method.Name;
            functions.Add(AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions { Name = fnName }));
        }
        return new ToolPlugin(name ?? type.Name, functions, description);
    }

    /// <summary>Creates a tool group from explicit tools (e.g. from <c>AIFunctionFactory.Create</c>).</summary>
    public static ToolPlugin FromTools(string name, IEnumerable<AITool> tools, string? description = null) =>
        new(name, tools.OfType<AIFunction>(), description);
}
