using BlazorAceEditor.Models;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components;

public partial class ReplScript
{
    private AceEditor _editor;

    private readonly AceEditorOptions _aceEditorOptions = new()
    {
        ShowLineNumbers = true,
        Mode = "csharp",
        VScrollBarAlwaysVisible = true,
        Theme = "terminal"
    };

    [Inject]
    private ICustomNativePlugins CoreKernelService { get; set; } = default!;
    [Inject] private AceEditorJsInterop AceEditorJsInterop { get; set; } = default!;
    private class CodeRequestForm
    {
        public string? Description { get; set; }
    }
    private CodeRequestForm _codeRequestForm = new();
    private string output = "";
    private bool _isBusy;

    private async void HandleInit(AceEditor editor)
    {
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(100);
        _isBusy = false;
        StateHasChanged();
    }
    private async void Submit(CodeRequestForm codeRequest)
    {
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var code = await _editor.GetValue();
        var input = codeRequest.Description;
        var result = await CoreKernelService.GenerateCompileAndExecuteReplPlugin(input, code, ReplType.ReplScript);
        output = result.Output;
        Console.WriteLine(result.Code);
        await _editor.SetValue(result.Code);
        _isBusy = false;
        StateHasChanged();
    }
}