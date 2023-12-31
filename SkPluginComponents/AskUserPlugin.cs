﻿using Microsoft.AspNetCore.Components;
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

public class AskUserPlugin(AskUserService modalService)
{
    [SKFunction, SKName("AskUser"), Description("Ask user for information, or request clarification from user.")]
    public async Task<string> AskUserAsync([Description("Question to ask the user")] string question, [Description("Type of answer required. Options are Text, Number, Boolean, Date, Array, or UnKnown")] string inputType = "Text", [Description("Use when asking the user to select one or more items for a specified set of options. Use a plain-text list with each option on its own line or a json array.")] string options = "")
    {
        if (inputType.Contains("UnKnown", StringComparison.OrdinalIgnoreCase))
            inputType = "Text";
        if (!string.IsNullOrEmpty(options))
        {
            if (Helpers.TryExractArrayFromJson(options, out var optionsList))
                options = string.Join("\n", optionsList);
        }
         
        var isInputTypeParsed = Enum.TryParse<SimpleInputType>(inputType, true, out var resultType);
        var windowOptions = new AskUserWindowOptions { Title = question, Location = Location.Bottom, Style = "width:max-content;min-width:50vw;height:max-content" };
        var results = await modalService.OpenAsync<SimpleInput>(parameters: new AskUserParameters { { "SimpleInputType", resultType }, { "Options", options} }, options: windowOptions);
        var value = results?.Parameters.Get<string>("Value") ?? "user refused to answer!";
        return value;
    }
    [SKFunction, SKName("TellUser"), Description("Show the user text or data. Used to provide information a user has requested, or when asked to show your work.")]
    public async Task TellUserAsync([Description("Information for user. Default to markdown format.")] string info)    
    {
        await modalService.OpenAsync<SimpleOutput>(parameters: new AskUserParameters { { "OutputText", info } }, options: new AskUserWindowOptions {  Location = Location.Bottom, Style = "width:max-content;min-width:50vw;height:max-content", ShowCloseButton = false, CloseOnOuterClick = true });
    }
}