using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Plugins;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Agents.Examples;

/// <summary>
/// Admin, Coder and Executor agent definitions for the group-chat samples.
/// Each agent's tools are plain <see cref="AIFunction"/>s grouped in a <see cref="ToolPlugin"/>.
/// </summary>
public class CodeGenAndExecuteAgent
{
    private static async Task<AgentProxy> GenerateCoderAgent()
    {
        var yamlPath = RepoFiles.PathToYamlPlugins;
        var consoleCode = YamlPromptFunction.FromYaml(await File.ReadAllTextAsync(Path.Combine(yamlPath, "CodingPlugin", "CodeCSharp.yaml")), () => CoreKernelService.CreateChatClient());
        var script = YamlPromptFunction.FromYaml(await File.ReadAllTextAsync(Path.Combine(yamlPath, "CodingPlugin", "CSharpScript.yaml")), () => CoreKernelService.CreateChatClient());
        var coderTools = new ToolPlugin("Coder", [consoleCode, script], "Writes c# code that's ready for execution");
        return new AgentProxy
        {
            Name = "Coder",
            Description = "Writes c# console applications or c# scripting code",
            Instructions = "Write c# code that's ready for execution based on instructions. If the provided instructions include existing code with an error description, fix the code so that it's ready for execution.",
            Plugins = [coderTools]
        };
    }

    private static AgentProxy GenerateExecutorAgent() => new()
    {
        Name = "Code Executor",
        Description = "Executes c# code",
        Instructions = "Execute c# code as a console application or as an repl script as instructed. If execution results in an error, provide the error details",
        Plugins = [ToolPlugin.FromObject(new CodeExecutorPlugin(), "CodeExecuter")]
    };

    private static AgentProxy GenerateAdminAgent() => new()
    {
        Name = "Admin",
        Description = "Administers the group of agents",
        Instructions = AdminInstructions
    };

    public static async Task<List<AgentProxy>> AgentsAsProxies()
    {
        return [GenerateAdminAgent(), await GenerateCoderAgent(), GenerateExecutorAgent()];
    }

    private const string AdminInstructions = """
        You act as group admin that leads other agents to resolve tasks together. Here's the workflow you follow:
        -workflow-
        if all_steps_are_resolved
            terminate_chat
        else
            resolve_step
        -end-
        
        The task will be provided by the user. Break the task into steps and assign each step to the appropriate agent.
        
        Here are some examples for resolve_step:
        - The step to resolve is xxx, let's work on this step.
        """;
}
