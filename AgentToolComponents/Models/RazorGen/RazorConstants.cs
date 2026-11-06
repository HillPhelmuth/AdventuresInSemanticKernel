namespace AgentToolComponents.Models.RazorGen;

public static class RazorConstants
{
    public const string DefaultComponentName = "__RazorOutput.razor";
    public const string DefaultFileContent = """
                                             <h1>Hello World</h1>

                                             @code {

                                             }

                                             """;
    public const string MainComponentCodePrefix = "@page \"/__razorOutput\"\n";
    public const string MainComponentPagePath = "/__razorOutput";
    public const string DefaultUsings = """
                                        @using System.ComponentModel.DataAnnotations
                                        @using System.Linq
                                        @using System.Net.Http
                                        @using System.Net.Http.Json
                                        @using Microsoft.AspNetCore.Components.Forms
                                        @using Microsoft.AspNetCore.Components.Routing
                                        @using Microsoft.AspNetCore.Components.Web
                                        @using Microsoft.JSInterop
                                        """;
    public const string WorkingDirectory = "/BlazorOut/";
}