using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components.TokenizationComponents
{
    public partial class TextAsTokens : ComponentBase
    {
        [Parameter]
        public List<TokenString> TokenStrings { get; set; } = new();
        [Parameter]
        public string FontSize { get; set; } = "1.1rem";
        [Parameter]
        public TokenString SelectedTokenString { get; set; } = default!;
        [Parameter]
        public EventCallback<TokenString> SelectedTokenStringChanged { get; set; }
        [Parameter]
        public List<TokenString>? SpecifiedTokens { get; set; }

        public void HandleSelectedTokenString(TokenString token)
        {
            SelectedTokenString = token;
            SelectedTokenStringChanged.InvokeAsync(token);
        }
    }
}
