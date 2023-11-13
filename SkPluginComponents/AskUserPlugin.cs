using Microsoft.AspNetCore.Components;
using Microsoft.SemanticKernel;
using SkPluginComponents.Components;
using SkPluginComponents.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginComponents;

public class AskUserPlugin
{
    private readonly AskUserService _askUserService;

    public AskUserPlugin(AskUserService modalService)
    {
        _askUserService = modalService;
    }


    [SKFunction, SKName("AskUser"), Description("Ask user for information, or request clarification from user.")]
    public async Task<string> AskUserAsync([Description("Question to ask the user")] string question, [Description("Type of answer required. Options are Text, Number, Boolean, Date, Array, or UnKnown")] string inputType = "Text", [Description("Use when asking the user to select one or more items for a specified set of options. Use a plain-text list with each option on its own line.")] string options = "")
    {
        if (inputType.Contains("UnKnown", StringComparison.OrdinalIgnoreCase))
            inputType = "Text";
        var isInputTypeParsed = Enum.TryParse<SimpleInputType>(inputType, true, out var resultType);
        var windowOptions = new AskUserWindowOptions { Title = question, Location = Location.Bottom, Style = "width:max-content;min-width:50vw;height:max-content" };
        var results = await _askUserService.OpenAsync<SimpleInput>(parameters: new AskUserParameters { { "SimpleInputType", resultType }, { "Options", options} }, options: windowOptions);
        var value = results?.Parameters.Get<string>("Value") ?? "user refused to answer!";
        return value;
    }
}