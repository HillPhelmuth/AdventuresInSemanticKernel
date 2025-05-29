using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class AgenticPlanningPlugin(ILoggerFactory loggerFactory)
{
    [KernelFunction, Description("Generate a detailed, step-by-step plan to complete the user provided task")]
    public async Task<string> GeneratePlan([FromKernelServices] AgentPlanState agentPlanState,
        [Description("Task or objective")] string task,
        [Description("Relevent context required to properly formulate a plan")] string context,
        [Description("Constraints that must be observed")] string constraints,
        [Description("An object containing the available tools/functions that can be used to complete the task and thus may be included in the plan")] AvailableTools availableTools
        )
    {
        var kernel = CoreKernelService.CreateKernel(AIModel.O4Mini);
        var settings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "low", ResponseFormat = typeof(AgentPlan) };
        var planFunction = KernelFunctionYaml.FromPromptYaml(FileHelper.ExtractFromAssembly<string>("CreatePlan.yaml"));
        var kernelArguments = new KernelArguments(settings)
        {
            ["task"] = task,
            ["context"] = context,
            ["constraints"] = constraints,
            ["availableTools"] = availableTools.ToMarkdown()
        };
        var planResult = await kernel.InvokeAsync(planFunction, kernelArguments);
        var planJson = planResult.ToString();
        agentPlanState.ActivePlan = JsonSerializer.Deserialize<AgentPlan>(planJson);
        return planJson;
    }

    [KernelFunction, Description("Evaluate a plan step and determine whether it has been completed successfully based on the **ExpectedOutput**")]
    public async Task<string> EvaluatePlanStep([FromKernelServices] AgentPlanState agentPlanState, [Description("The step to evaluate")] Step step, [Description("The result of the step actions")] string stepResult)
    {
        var kernel = CoreKernelService.CreateKernel(AIModel.Gpt41);

        var settings = new OpenAIPromptExecutionSettings() { Temperature = 0.3 };
        var planFunction = KernelFunctionYaml.FromPromptYaml(FileHelper.ExtractFromAssembly<string>("EvaluateStep.yaml"));

        var kernelArguments = new KernelArguments(settings)
        {
            ["step"] = step,
            ["stepResult"] = stepResult
        };

        var evaluationResult = await kernel.InvokeAsync(planFunction, kernelArguments);
        return evaluationResult.ToString();
    }
    [KernelFunction, Description("Modify an existing plan to complete the user provided task when the current plan is insufficient and/or incomplete.")]
    public async Task<string> ModifyPlan([FromKernelServices] AgentPlanState agentPlanState,
        [Description("Task or objective")] string task,
        [Description("Relevent context required to properly formulate a plan")] string context,
        [Description("Constraints that must be observed")] string constraints,
        [Description("The required or desired modifications")] string requestedModifications,
        [Description("An object containing the available tools/functions that can be used to complete the task and thus may be included in the plan")] AvailableTools availableTools
        )
    {
        var kernel = CoreKernelService.CreateKernel(AIModel.O4Mini);
        var settings = new OpenAIPromptExecutionSettings() { ReasoningEffort = "low", ResponseFormat = typeof(AgentPlan) };
        var planFunction = KernelFunctionYaml.FromPromptYaml(FileHelper.ExtractFromAssembly<string>("ModifyPlan.yaml"));
        var kernelArguments = new KernelArguments(settings)
        {
            ["task"] = task,
            ["context"] = context,
            ["constraints"] = constraints,
            ["availableTools"] = availableTools.ToMarkdown(),
            ["currentPlan"] = agentPlanState.ActivePlan?.ToMarkdown() ?? "No Active Plan",
            ["requestedModifications"] = requestedModifications
        };
        var planResult = await kernel.InvokeAsync(planFunction, kernelArguments);
        var planJson = planResult.ToString();
        agentPlanState.ActivePlan = JsonSerializer.Deserialize<AgentPlan>(planJson);
        return planJson;
    }

    [KernelFunction, Description("Use the tool to think about something. It will not obtain new information or change the database, but just append the thought to the log. Use it when complex reasoning or some cache memory is needed.")]
    public void Think(Kernel kernel, [Description("The thought to append to the log")] string thought)
    {
        
        
    }
    private const string CollectionName = "thoughts";
    private async Task<ISemanticTextMemory> GetOrCreateMemoryStore(ITextEmbeddingGenerationService embeddingGenerationService, bool delete = false)
    {
        var sqliteConnect = await SqliteMemoryStore.ConnectAsync(@".\Data\PromptLab.db");
        var collections = new List<string>();
        await foreach (var item in sqliteConnect.GetCollectionsAsync())
        {
            collections.Add(item);
        }
        //var collections = await sqliteConnect.GetCollectionsAsync().ToListAsync() ?? [];
        if (!collections.Contains(CollectionName))
        {
            await sqliteConnect.CreateCollectionAsync(CollectionName);
        }
        else if (delete)
        {
            await sqliteConnect.DeleteCollectionAsync(CollectionName);
            await sqliteConnect.CreateCollectionAsync(CollectionName);
        }

        var memoryBuilder = new MemoryBuilder().WithMemoryStore(sqliteConnect).WithLoggerFactory(loggerFactory);
        return memoryBuilder.WithTextEmbeddingGeneration(embeddingGenerationService)
            .Build();
    }
}
[TypeConverter(typeof(GenericTypeConverter<AgentPlan>))]
public class AgentPlan
{
    public List<Step> Steps { get; set; } = [];

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Agent Plan");
        foreach (var step in Steps)
        {
            sb.AppendLine(step.ToMarkdown());
        }
        return sb.ToString();
    }
}
[TypeConverter(typeof(GenericTypeConverter<Step>))]
public class Step
{
    [Description("The name of the step. (Step 1, Step 2 etc.)")]
    public required string Identifier { get; set; }
    [Description("The goal of the step")]
    public required string Objective { get; set; }
    [Description("The output expected when the step is complete")]
    public string ExpectedOutput { get; set; }
    [Description("Tools/functions to call to complete the step.")]
    public string? Actions { get; set; }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {Identifier}");
        sb.AppendLine($"**Objective:** {Objective}");
        sb.AppendLine($"**Expected Output:** {ExpectedOutput}");
        if (Actions != null)
            sb.AppendLine($"**Actions:** {Actions}");
        return sb.ToString();
    }
}
[TypeConverter(typeof(GenericTypeConverter<AvailableTools>))]
public class AvailableTools
{
    [Description("Collection of tools available")]
    public List<AvailableTool> Tools { get; set; } = [];
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Available Tools");
        foreach (var tool in Tools)
        {
            sb.AppendLine(tool.ToMarkdown());
        }
        return sb.ToString();
    }
}
public class AvailableTool
{
    public AvailableTool(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public AvailableTool()
    {

    }
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {Name}");
        sb.AppendLine($"**Description:** {Description}");
        return sb.ToString();
    }

    public string? Name { get; set; }
    public string? Description { get; set; }



}
