// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SkPluginLibrary.Plugins;

public sealed class EmailPlugin
{
    [SKFunction, Description("Given an e-mail and message body, send an email")]
    public string SendEmail(
        [Description("The body of the email message to send.")] string input,
        [Description("The email address to send email to.")] string email_address) =>

        $"Sent email to: {email_address}. Body: {input}";

    [SKFunction, Description("Given a name, find email address")]
    public string GetEmailAddress(
        [Description("The name of the person whose email address needs to be found.")] string input,
        ILogger? logger = null)
    {
        // Sensitive data, logging as trace, disabled by default
        logger?.LogTrace("Returning hard coded email for {0}", input);

        return "johndoe1234@example.com";
    }
}
public sealed class MenuPlugin
{
    [SKFunction, Description("Provides a list of specials from the menu.")]
    public string GetSpecials()
    {
        return @"
Special Soup: Clam Chowder
Special Salad: Cobb Chowder
Special Drink: Chai Tea
";
    }

    [SKFunction, Description("Provides the price of the requested menu item.")]
    public string GetItemPrice(
        [Description("The name of the menu item.")]
        string menuItem)
    {
        return "$9.99";
    }
}
