using System.ComponentModel;

namespace SkPluginLibrary.Models;

public enum ExecutionType
{
    None,
    [Description("Execute a single function with relevant inputs")]
    SingleFunction,
    [Description("Execute multiple functions in chain")]
    [LongDescription("Execute multiple functions in chain. Start by selecting plugins. Then, select which specific functions from those plugin will be used in your function chain and in what order. The output of one function is the input of the next function in the chain. The output of the last function is the final result.")]
    ChainFunctions,
    [Description("Execute Action Planner with selected Plugins and Functions")]
    [LongDescription("Execute Action Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to add any additional inputs required for any function. The Action Planner will then determine which function to execute based on your input")]
    ActionPlanner,
    [Description("Execute a Sequential Planner with selected Plugins and Functions")]
    [LongDescription("Execute a Sequential Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Sequential Plan. The Sequential Planner will then determine which functions are required and in what order to satisfy your input reqeuest")]
    SequentialPlanner,
    [Description("Execute a Stepwise Planner with selected Plugins and Functions")]
    [LongDescription("Execute a Stepwise Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Stepwise Plan. The Stepwise Planner will then determine which functions are required and in what order to satisfy your input reqeuest")]
    StepwisePlanner,
    [Description("Create Chat using an Action Planner")]
    [LongDescription("Execute Action Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to add any additional inputs required for any function. The Action Planner will then determine which function to execute based on your input, the result of which will added to a system prompt for an interactive chat")]
    ActionPlannerChat,
    [Description("Create Chat using a Sequential Planner")]
    [LongDescription("Execute a Sequential Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Sequential Plan. The Sequential Planner will then determine which functions are required and in what order to satisfy your input reqeuest, the result of which will added to a system prompt for an interactive chat")]
    SequentialPlannerChat,
    [Description("Create Chat using a Stepwise Planner")]
    [LongDescription("Execute a Stepwise Planner with selected Plugins and Functions. Start by selecting plugins. Then you'll have the opportunity to either exclude any function from or require inclusion of any function in the Stepwise Plan. The Stepwise Planner will then determine which functions are required and in what order to satisfy your input reqeuest, the result of which will added to a system prompt for an interactive chat")]
    StepwisePlannerChat
}

[AttributeUsage(AttributeTargets.All)]
public class LongDescriptionAttribute : Attribute
{
    public string LongDescription { get; set; }

    public LongDescriptionAttribute(string longDescription)
    {
        LongDescription = longDescription;
    }


}