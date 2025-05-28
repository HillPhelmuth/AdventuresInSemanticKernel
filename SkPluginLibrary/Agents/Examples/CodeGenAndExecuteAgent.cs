using Microsoft.SemanticKernel.Experimental.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Agents.Extensions;
using SkPluginLibrary.Plugins;
using SkPluginLibrary.Plugins.NativePlugins;


namespace SkPluginLibrary.Agents.Examples;

public class CodeGenAndExecuteAgent
{
    private static readonly List<IAgent> Agents = [];
    private static async Task GenerateCoderAgent(string? agentId = null)
    {
        var kernel = CreateKernel();
        var yamlPath = RepoFiles.PathToYamlPlugins;
        var consoleCodeText = await File.ReadAllTextAsync(Path.Combine(yamlPath, "CodingPlugin", "CodeCSharp.yaml"));
        var consoleFunction = kernel.CreateFunctionFromPromptYaml(consoleCodeText);
        var scriptText = await File.ReadAllTextAsync(Path.Combine(yamlPath, "CodingPlugin", "CSharpScript.yaml"));
        var scriptFunc = kernel.CreateFunctionFromPromptYaml(scriptText);
        var coderAsPlugin = KernelPluginFactory.CreateFromFunctions("Coder", "Writes c# code that's ready for execution", [consoleFunction, scriptFunc]);
        
      
        if (!string.IsNullOrEmpty(agentId))
        {
            var agent = await new AgentBuilder().GetAsync(agentId);
            agent.Plugins.Add(coderAsPlugin);
                /* await AgentBuilder.GetAgentAsync(TestConfiguration.OpenAI.ApiKey, agentId, [coderAsPlugin]);*/
            Agents.Add(agent);
            return;
        }
        var coderAgent = await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4ModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName("Coder")
            .WithDescription("Writes c# console applications or c# scripting code")
            .WithInstructions("Write c# code that's ready for execution based on instructions. If the provided instructions include existing code with an error description, fix the code so that it's ready for execution.")
            .WithPlugin(coderAsPlugin).BuildAsync();
        Agents.Add(coderAgent);

    }
    private static async Task GenerateExecutorAgent(string? agentId = null)
    {
        var executerPlugin = KernelPluginFactory.CreateFromType<CodeExecutorPlugin>("CodeExecuter");
        if (!string.IsNullOrEmpty(agentId))
        {
            var agent = await new AgentBuilder().GetAsync(agentId);
            agent.Plugins.Add(executerPlugin);
            Agents.Add(agent);
            return;
        }
        var executeAgent = await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4ModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName("Code Executor")
            .WithDescription("Executes c# code")
            .WithInstructions("Execute c# code as a console application or as an repl script as instructed. If execution results in an error, provide the error details")
            .WithPlugin(executerPlugin).BuildAsync();
        Agents.Add(executeAgent);
    }
    private static async Task GenerateAdminAgent(string? agentId = null)
    {
        if (!string.IsNullOrEmpty(agentId))
        {
            var agent = await new AgentBuilder().GetAsync(agentId);
            Agents.Add(agent);
            return;
        }
        var adminAgent = await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4ModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName("Admin")
            .WithDescription("Administers the group of agents")
            .WithInstructions(AdminInstructions)
            .BuildAsync();
        Agents.Add(adminAgent);
    }
    public static async Task<List<AgentProxy>> AgentsAsProxies(string? adminId = null, string? coderId = null, string? executorId = null)
    {
        var result = new List<AgentProxy>();
        await GenerateAdminAgent(adminId);
        await GenerateCoderAgent(coderId);
        await GenerateExecutorAgent(executorId);
        result.AddRange(Agents.Select(x => x.AgentProxy()).ToList());
        //await DeleteAllAgents();
        return result.Distinct().ToList();
    }

    private static async Task DeleteAllAgents()
    {
        if (Agents.Count > 0)
        {
            foreach (var agent in Agents)
            {
                await agent.DeleteAsync();
            }
        }
    }

    private static Kernel CreateKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));
        kernelBuilder.AddOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4ModelId, TestConfiguration.OpenAI.ApiKey);
        var kernel = kernelBuilder.Build();
        return kernel;
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