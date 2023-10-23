using BlazorAceEditor.Models;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components;

public partial class ReplConsole : ComponentBase
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
        public string? Description { get; set; } =
            "Generate an application that creates a list of playing cards representing a 52 card deck. It should then shuffle the deck of cards and deal 5 cards to each of 4 players. Then write to console the 5 card hand of each player.";
    }
    private CodeRequestForm _codeRequestForm = new();
    private string output = "";
    private bool _isBusy;

    private async void HandleInit(AceEditor editor)
    {
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(200);
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
        var result = await CoreKernelService.GenerateCompileAndExecuteReplPlugin(input, code);
        output = result.Output;
        Console.WriteLine(result.Code);
        await _editor.SetValue(result.Code);
        _isBusy = false;
        StateHasChanged();
    }


}