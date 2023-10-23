using BlazorAceEditor.Models;
using BlazorAceEditor.Models.Events;
using Microsoft.AspNetCore.Components;

namespace BlazorAceEditor
{
    public partial class AceEditor : IAsyncDisposable
    {
        [Parameter]
        public string Id { get; set; } = default!;
        [Parameter]
        public AceEditorOptions Options { get; set; } = default!;
        [Parameter]
        public string? Style { get; set; }
        [Parameter]
        public EventCallback<AceEditor> OnEditorInit { get; set; }
        [Parameter]
        public EventCallback<AceChangeEventArgs> OnEditorChange { get; set; }

        [Inject] private AceEditorJsInterop AceEditorInterop { get; set; } = default!;

        protected override Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(Id))
                Id = $"ace-editor-{Guid.NewGuid()}";
            if (string.IsNullOrEmpty(Style))
                Style = "height:20rem; width:100%; padding:1rem";
            return base.OnParametersSetAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var didInit = await AceEditorInterop.Init(Id, Options);
                if (didInit)
                {
                    await OnEditorInit.InvokeAsync(this);
                    AceEditorInterop.AceEditorChange += HandleEditorChange;
                }
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public async Task<string> GetValue() => await AceEditorInterop.GetValue(Id);

        public async Task SetValue(string text) => await AceEditorInterop.SetValue(Id, text);

        public async Task ChangeLanguage(string language) => await AceEditorInterop.SetLanguage(language);

        public async Task ChangeTheme(string theme) => await AceEditorInterop.SetTheme(theme);

        protected async void HandleEditorChange(object? sender, AceChangeEventArgs args)
        {
            await OnEditorChange.InvokeAsync(args);
        }

        public ValueTask DisposeAsync()
        {
            AceEditorInterop.AceEditorChange -= HandleEditorChange;
            //await AceEditorInterop.DisposeAsync();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
