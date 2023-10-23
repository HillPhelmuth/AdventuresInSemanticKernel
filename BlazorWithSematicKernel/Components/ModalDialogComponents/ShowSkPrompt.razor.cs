using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents
{
    public partial class ShowSkPrompt : ComponentBase
    {
        [Inject]
        private DialogService DialogService { get; set; } = default!;

        [Parameter] public string Prompt { get; set; } = "";

        [Parameter] public string Title { get; set; } = "";
    }
}
