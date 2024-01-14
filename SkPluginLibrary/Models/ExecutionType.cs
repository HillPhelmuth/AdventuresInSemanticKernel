using System.ComponentModel;

namespace SkPluginLibrary.Models;

public enum ExecutionType
{
    None,
    [Description("Execute a single function with relevant inputs")]
    [IsActive(false)] SingleFunction,
    [Description("Execute multiple functions in chain")]
    [LongDescription("Execute multiple functions in chain. Start by selecting plugins. Then, select which specific functions from those plugin will be used in your function chain and in what order. The output of one function is the input of the next function in the chain. The output of the last function is the final result.")]
    [IsActive(false)] ChainFunctions,
    [Description("Create Chat with Plugins and Functions using OpenAI Function Calls")]
    [LongDescription("Execute OpenAI Function Calls using selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to modify any default inputs required for any function. OpenAI Function calls will then determine which function(s) to execute based on your input, the result of which will added to an interactive chat")]
    [IsActive(true)] AutoFunctionCalling,
    [Description("Execute a Sequential Planner with selected Plugins and Functions")]
    [LongDescription("Execute a Sequential Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Sequential Plan. The Sequential Planner will then determine which functions are required and in what order to satisfy your input request")]
    [IsActive(false)] SequentialPlanner,
    [Description("Execute a OpenAI Function Calling Stepwise Planner with selected Plugins and Functions")]
    [LongDescription("Execute a Stepwise Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Stepwise Plan. The Stepwise Planner will then determine which functions are required and in what order to satisfy your input request")]
    [IsActive(true)] StepwisePlanner,
    [Description("Execute a Handlebars Planner with selected Plugins and Functions")]
    [LongDescription("Execute a Handlebars Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Stepwise Plan. The Handlebars Planner will utilize the handlebars template engine (with or without loops) and determine which functions are required and in what order to satisfy your input request")]
    [IsActive(true)]
    HandlebarsPlanner,
    [Description("Create Chat with Plugins and Functions using OpenAI Function Calls")]
    [LongDescription("Execute OpenAI Function Calls using selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to modify any default inputs required for any function. OpenAI Function calls will then determine which function to execute based on your input, the result of which will added to a system prompt for an interactive chat")]
    [IsActive(true)]
    AutoFunctionCallingChat,
    [Description("Create Chat using a Sequential Planner")]
    [LongDescription("Execute a Sequential Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Sequential Plan. The Sequential Planner will then determine which functions are required and in what order to satisfy your input request, the result of which will added to a system prompt for an interactive chat")]
    [IsActive(false)] 
    SequentialPlannerChat,
    [Description("Create Chat using an OpenAI Function Calling Stepwise Planner")]
    [LongDescription("Execute a Stepwise Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Stepwise Plan. The Stepwise Planner will then determine which functions are required and in what order to satisfy your input request, the result of which will added to a system prompt for an interactive chat")]
    [IsActive(true)] StepwisePlannerChat,
    [Description("Create Chat using a Handlebars Planner")]
    [LongDescription("Execute a Handlebars Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Handlebars Plan. The Handlebars Planner will utilize the handlebars template engine (with or without loops) and determine which functions are required and in what order to satisfy your input request, the result of which will added to a system prompt for an interactive chat")]
    [IsActive(true)]
    HandlebarsPlannerChat
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