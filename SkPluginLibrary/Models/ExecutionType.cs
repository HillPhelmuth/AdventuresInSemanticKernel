namespace SkPluginLibrary.Models;

public enum ExecutionType
{
    None,
    [Description("Execute a single function with relevant inputs")]
    [IsActive(false)] SingleFunction,
    [Description("Create Chat with Plugins and Functions using OpenAI Function Calls")]
    [LongDescription("Execute OpenAI Function Calls using selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to modify any default inputs required for any function. OpenAI Function calls will then determine which function(s) to execute based on your input, the result of which will added to an interactive chat")]
    [IsActive(true)] AutoFunctionCalling,    
    [Description("Execute a step-by-step Agent with selected Plugins and Functions")]
    [LongDescription("Run an Agent Framework agent that works step by step, calling the selected tools as often as it needs (up to 15 iterations) until it can give a final answer. Start by selecting plugins. Then you'll have the opportunity to exclude any function from the agent's tool list. Each tool call and result is shown as the agent works.")]
    [IsActive(true)] StepwiseAgent,
    [Description("Execute a Plan-and-Execute Workflow with selected Plugins and Functions")]
    [LongDescription("Run a two-stage Agent Framework workflow. A planner agent first writes a step-by-step plan from the available tools, then an executor agent carries the plan out with those tools. Start by selecting plugins. Then you'll have the opportunity to exclude any function from the tool list. The plan and the execution are streamed as the workflow runs.")]
    [IsActive(true)]
    PlanAndExecuteWorkflow,
    [Description("Create Chat with Plugins and Functions using OpenAI Function Calls")]
    [LongDescription("Execute OpenAI Function Calls using selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to modify any default inputs required for any function. OpenAI Function calls will then determine which function to execute based on your input, the result of which will added to a system prompt for an interactive chat")]
    [IsActive(false)]
    AutoFunctionCallingChat,
    [Description("Create Chat using a step-by-step Agent")]
    [LongDescription("Run the step-by-step agent with the selected Plugins and Functions, then use its result as context for an interactive chat.")]
    [IsActive(true)] StepwiseAgentChat,
    [Description("Create Chat using a Plan-and-Execute Workflow")]
    [LongDescription("Run the plan-and-execute workflow with the selected Plugins and Functions, then use its result as context for an interactive chat.")]
    [IsActive(true)]
    PlanAndExecuteWorkflowChat
}

[AttributeUsage(AttributeTargets.All)]
public class LongDescriptionAttribute(string longDescription) : Attribute
{
    public string LongDescription { get; set; } = longDescription;
}
[AttributeUsage(AttributeTargets.Field)]
public class IsActiveAttribute(bool isActive) : Attribute
{
    public bool IsActive { get; set; } = isActive;
}