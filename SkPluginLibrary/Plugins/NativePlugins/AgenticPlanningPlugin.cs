using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class AgenticPlanningPlugin(ILoggerFactory loggerFactory, AgentPlanState agentPlanState)
{
    private static string PromptFromYaml(string fileName) =>
        YamlPromptFunction.FromYaml(File.ReadAllText(Path.Combine(RepoFiles.PathToYamlPlugins, "AgenticPromptsPlugin", fileName))).Template;

    [Description("Generate a detailed, step-by-step plan to complete the user provided task")]
    public async Task<string> GeneratePlan(
        [Description("Task or objective")] string task,
        [Description("Relevent context required to properly formulate a plan")] string context,
        [Description("Constraints that must be observed")] string constraints,
        [Description("An object containing the available tools/functions that can be used to complete the task and thus may be included in the plan")] AvailableTools availableTools
        )
    {
        var client = ChatClientFactory.Create(AIModel.O4Mini);
        // Structured output: the model's reply is deserialized straight into an AgentPlan.
        var plan = await PromptRunner.RunAsync<AgentPlan>(client, PromptFromYaml("CreatePlan.yaml"), new Dictionary<string, object?>
        {
            ["task"] = task,
            ["context"] = context,
            ["constraints"] = constraints,
            ["availableTools"] = availableTools.ToMarkdown()
        });
        agentPlanState.ActivePlan = plan ?? new AgentPlan();
        return JsonSerializer.Serialize(agentPlanState.ActivePlan);
    }

    [Description("Evaluate a plan step and determine whether it has been completed successfully based on the **ExpectedOutput**")]
    public async Task<string> EvaluatePlanStep([Description("The step to evaluate")] Step step, [Description("The result of the step actions")] string stepResult)
    {
        var client = ChatClientFactory.Create(AIModel.Gpt41);
        return await PromptRunner.RunAsync(client, PromptFromYaml("EvaluateStep.yaml"), new Dictionary<string, object?>
        {
            ["step"] = JsonSerializer.Serialize(step),
            ["stepResult"] = stepResult
        }, new ChatOptions { Temperature = 0.3f });
    }

    [Description("Modify an existing plan to complete the user provided task when the current plan is insufficient and/or incomplete.")]
    public async Task<string> ModifyPlan(
        [Description("Task or objective")] string task,
        [Description("Relevent context required to properly formulate a plan")] string context,
        [Description("Constraints that must be observed")] string constraints,
        [Description("The required or desired modifications")] string requestedModifications,
        [Description("An object containing the available tools/functions that can be used to complete the task and thus may be included in the plan")] AvailableTools availableTools
        )
    {
        var client = ChatClientFactory.Create(AIModel.O4Mini);
        var plan = await PromptRunner.RunAsync<AgentPlan>(client, PromptFromYaml("ModifyPlan.yaml"), new Dictionary<string, object?>
        {
            ["task"] = task,
            ["context"] = context,
            ["constraints"] = constraints,
            ["availableTools"] = availableTools.ToMarkdown(),
            ["currentPlan"] = agentPlanState.ActivePlan?.ToMarkdown() ?? "No Active Plan",
            ["requestedModifications"] = requestedModifications
        });
        agentPlanState.ActivePlan = plan ?? new AgentPlan();
        return JsonSerializer.Serialize(agentPlanState.ActivePlan);
    }

    [Description("Use the tool to think about something. It will not obtain new information or change the database, but just append the thought to the log. Use it when complex reasoning or some cache memory is needed.")]
    public void Think([Description("The thought to append to the log")] string thought)
    {
        loggerFactory.CreateLogger<AgenticPlanningPlugin>().LogInformation("Thought: {thought}", thought);
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
