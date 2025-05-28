using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components;
public partial class ShowCode
{
    private AceEditor? _aceEditor;
    [Parameter]
    public string Content { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (_aceEditor != null)
        {
            await _aceEditor.SetValue(Content);
        }
        await base.OnParametersSetAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (_aceEditor != null)
            {
                await _aceEditor.SetValue(Content)!;
                StateHasChanged();
            }
        }
        await base.OnAfterRenderAsync(firstRender);
    }
    private async void HandleInit(AceEditor editor)
    {
        await _aceEditor.SetValue(Content)!;
        StateHasChanged();
        await Task.Delay(200);
        StateHasChanged();
    }
}
