using BlazorWithSematicKernel.Components;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Services;

namespace BlazorWithSematicKernel.Shared;

public partial class MainLayout
{
    [Inject]
    private DialogService DialogService { get; set; } = default!;
    [Inject]
    private TooltipService TooltipService { get; set; } = default!;
    [Inject]
    private GithubCodeReaderService GithubCodeReaderService { get; set; } = default!;

    [CascadingParameter(Name = "PageTitle")]
    public string PageTitle { get; set; } = default!;
    [CascadingParameter(Name = "SelectedCodeFile")]
    public string SelectedCodeFile { get; set; } = default!;
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var resource = CompileResources.PortableExecutableReferences;
        }
        return base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(SelectedCodeFile))
        {

        }
        await base.OnParametersSetAsync();
    }

    private bool _sidebar1Expanded = true;
    private void ShowChat()
    {
        var options = new DialogOptions
        {
            Height = "99vh",
            Width = "70vw",
            Draggable = true,
            ShowTitle = false,
            Resizable = true,
            ShowClose = true,
            CloseDialogOnOverlayClick = true
        };
        DialogService.Open<ChatWithSk>("Chat With Semantic Kernal Documentation", options: options);
    }
    private async Task ShowCode()
    {
        var options = new DialogOptions
        {
            Height = "99vh",
            Width = "70vw",
            Draggable = true,
            ShowTitle = false,
            Resizable = true,
            ShowClose = true,
            CloseDialogOnOverlayClick = true
        };
        var content = await GithubCodeReaderService.GetCodeForFileAsync($"SkPluginLibrary/{SelectedCodeFile}");
        Dictionary<string, object> parameters = new() { { "Content", content } };
        DialogService.Open<ShowCode>("Content", options: options, parameters:parameters);
    }
    private void ShowConfig()
    {
        var options = new DialogOptions
        {
            Height = "75vh",
            Width = "70vw",
            Draggable = true,
            ShowTitle = true,
            Resizable = true,
            ShowClose = true,
            CloseDialogOnOverlayClick = true
        };
        
        DialogService.Open<AddConfiguration>("Configuration", options: options);
    }

    private TooltipOptions _tooltipOptions = new() { Style = "background-color:lightblue;color:black;", Position = Radzen.TooltipPosition.Right, Duration = 60000, CloseTooltipOnDocumentClick = true };
    private void ShowTooltip(ElementReference elementReference, string text)
    {
        TooltipService.Open(elementReference, text, _tooltipOptions);
    }
}