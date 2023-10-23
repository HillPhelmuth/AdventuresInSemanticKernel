using BlazorAceEditor.Helpers;
using BlazorAceEditor.Models;
using BlazorAceEditor.Models.Events;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorAceEditor
{
    public class AceEditorJsInterop : JSModule
    {
        private DotNetObjectReference<AceEditorJsInterop> _dotNetReference;
        public event AceChangeEventHandler? AceEditorChange;
        public AceEditorJsInterop(IJSRuntime jsRuntime)
            : base(jsRuntime, "./_content/BlazorAceEditor/aceEditorInterop.js")
        {
            _dotNetReference = DotNetObjectReference.Create(this);
        }

        public async ValueTask<bool> Init(string elementId, AceEditorOptions options)
        {
            //convert to JsonElement to remove all null properties from object
            var optionsJson = JsonSerializer.Serialize(options, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            Console.WriteLine($"Options as Json:\n{optionsJson}");
            var optionsDict = JsonSerializer.Deserialize<JsonElement>(optionsJson);

            return await InvokeAsync<bool>("init", elementId, optionsDict, _dotNetReference);
        }

        public async ValueTask<string> GetValue(string elementId) => await InvokeAsync<string>("getValue", elementId);

        public async ValueTask SetValue(string elementId, string value) => await InvokeVoidAsync("setValue", elementId, value);

        public async ValueTask SetLanguage(string language)
        => await InvokeVoidAsync("setLanguage", language);
        public async ValueTask SetTheme(string theme)
        => await InvokeVoidAsync("setTheme", theme);

        public async ValueTask<List<ThemeModel>> GetThemes(bool excludeDark = false)
        {
            return await InvokeAsync<List<ThemeModel>>("availableThemes");
        }

        public async ValueTask<List<ModeModel>> GetLanguageModes()
        {
            var modes = await InvokeAsync<List<ModeModel>>("availableLanguageModes");
            return modes;
        }

        protected override ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                _dotNetReference?.Dispose();
            }
            return base.DisposeAsync(disposing);
        }
        [JSInvokable]
        public void HandleAceChange(AceChangeEventArgs args)
        {
            AceEditorChange?.Invoke(this, args);
        }
    }
}