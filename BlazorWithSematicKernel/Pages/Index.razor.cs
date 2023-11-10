using BlazorWithSematicKernel.Components;
using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Pages
{
    public partial class Index
    {
        [Inject]
        private DialogService DialogService { get; set; } = default!;

        private string _outputText = "";
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                StateHasChanged();
                //await ChatGptPlugin.GetAndSaveAllMantifestFiles();
            }
            await base.OnAfterRenderAsync(firstRender);
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


    }
}
