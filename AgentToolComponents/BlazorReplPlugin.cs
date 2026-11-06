using System.ComponentModel;
using Microsoft.CodeAnalysis;
using AgentToolComponents.Models;
using Location = AgentToolComponents.Models.Location;

namespace AgentToolComponents;

public class BlazorReplPlugin(RazorSnippetCompiler razorSnippetCompiler, AskUserService modalService)
{
    public BlazorAppIo? LastApplication { get; private set; }

    [Description(
         "Create a blazor application from provided .razor files. The application will be shown to the user in a modal dialog.")]
    public async Task<string> CreateBlazorApp(
        [Description("generated .razor files to create blazor app")]
        RazorInput razorInput, [Description("App name")] string appName = "New Blazor App", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var compileResult = await razorSnippetCompiler.CompileComponentsAsync(
            razorInput.RazorFiles.ToDictionary(x => x.Name, x => x.Code), razorInput.TopLevelComponentName);
        var errors = compileResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        LastApplication = new BlazorAppIo
        {
            Name = appName,
            Input = razorInput,
            Output = compileResult
        };
        if (errors.Any())
            throw new InvalidOperationException(string.Join("\n", errors.Select(x => $"{x.Code}: {x.Description} ({x.File}:{x.Line})")));
        var topComponent = compileResult.TryGetComponentType(razorInput.TopLevelComponentName)
            ?? throw new InvalidOperationException($"Top-level Razor component '{razorInput.TopLevelComponentName}' was not found.");
        var windowOptions = new AskUserWindowOptions
            { Title = appName, Location = Location.Center, Style = "width:80vw;height:75vh" };
        await modalService.OpenAsync<SnippetRunner>(
            parameters: new AskUserParameters { { "ComponentType", topComponent } },
            options: windowOptions, cancellationToken: cancellationToken);
        return "Blazor App created successfully!";
    }

    
}

public class BlazorAppIo
{
    public string Name { get; set; } = "New Blazor App";
    public RazorInput? Input { get; set; }
    public CompileResult? Output { get; set; }

    public Type? TopComponent
    {
        get
        {
            if (Input is null || Output is null)
                return null;
            return Output.TryGetComponentType(Input.TopLevelComponentName);
        }
    }
}