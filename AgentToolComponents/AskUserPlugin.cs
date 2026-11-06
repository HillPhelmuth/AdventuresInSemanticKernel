using Microsoft.AspNetCore.Components;
using AgentToolComponents.Components;
using AgentToolComponents.Models;
using AgentToolComponents.Models.RazorGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Location = AgentToolComponents.Models.Location;

namespace AgentToolComponents;

public class AskUserPlugin(AskUserService modalService)
{
    [Description("Ask user for information, or request clarification from user.")]
    [return: Description("The user's answer")]
    public async Task<string> AskUserAsync([Description("Question to ask the user")] string question, [Description("Type of answer required. Options are Text, Number, Boolean, Date, Array, or UnKnown")] string inputType = "Text", [Description("Use when asking the user to select one or more items for a specified set of options. Use a plain-text list with each option on its own line.")] string options = "", CancellationToken cancellationToken = default)
    {
        Console.WriteLine("AskUser Invoked");
        if (inputType.Contains("UnKnown", StringComparison.OrdinalIgnoreCase))
            inputType = "Text";
                 
        if (!Enum.TryParse<SimpleInputType>(inputType, true, out var resultType) || !Enum.IsDefined(resultType))
            throw new ArgumentException("Unknown user input type.", nameof(inputType));
        var windowOptions = new AskUserWindowOptions { Title = question, Location = Location.Bottom, Style = "width:max-content;min-width:50vw;height:max-content" };
        var results = await modalService.OpenAsync<SimpleInput>(parameters: new AskUserParameters { { "SimpleInputType", resultType }, { "Options", options} }, options: windowOptions, cancellationToken: cancellationToken);
        if (results is null || !results.IsSuccess) return "user refused to answer!";
        return results.Parameters.Get<string>("Value") ?? throw new InvalidOperationException("The user response did not contain a value.");
    }
    [Description("Show the user text or data. No response will be provided. Used to provide information a user has requested, or when asked to show your work when no additional feedback from the user is required.")]
    public async Task TellUserAsync([Description("Information for user. Default to markdown format.")] string info, CancellationToken cancellationToken = default)
    {
        await modalService.OpenAsync<SimpleOutput>(parameters: new AskUserParameters { { "OutputText", info } }, options: new AskUserWindowOptions {  Location = Location.Bottom, Style = "width:max-content;min-width:50vw;height:max-content", ShowCloseButton = false, CloseOnOuterClick = true }, cancellationToken: cancellationToken);
    }
}