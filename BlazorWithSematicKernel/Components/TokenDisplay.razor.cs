using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components
{
    public partial class TokenDisplay : ComponentBase
    {

        [Parameter]
        [EditorRequired]
        public TokenString TokenString { get; set; } = default!;
        [Parameter]
        public EventCallback<TokenString> OnClick { get; set; }
        [CascadingParameter(Name = "SpecifiedTokens")]
        public List<TokenString>? SpecifiedTokens { get; set; }
        private int _token;

        private string _textAsHtml = "";
        private ElementReference _ref;
        [Inject] private TooltipService TooltipService { get; set; } = default!;
        private bool _shouldRender;
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            //Console.WriteLine("Parameters for TokenDisplay.razor set");
            //var encoding = Tiktoken.Encoding.ForModel("gpt-3.5-turbo");
            if (_token == TokenString.Token) return;
            _token = TokenString.Token;
            if (SpecifiedTokens == null || !SpecifiedTokens.Any() || SpecifiedTokens.Select(x => x.StringValue).Contains(TokenString.StringValue))
            {
                var text = TokenString.StringValue;
                var textAsHtml = TokenString!.Token != -1 ? TextAsHtml(text) : text;
                _textAsHtml = textAsHtml!;
            }
            else
            {
                _textAsHtml = TokenString.StringValue;
            }


        }

        private static string TextAsHtml(string? text)
        {
            var (hexColor, textColor) = HexColor();
            var textAsHtml = $"<span style=\"background-color:{hexColor}; color:{textColor}\">{text}</span>";
            return textAsHtml;
        }

        private static (string hexColor, string textColor) HexColor()
        {
            var rand = new Random();

            var red = rand.Next(256);
            var green = rand.Next(256);
            var blue = rand.Next(256);

            var hexColor = $"#{red:X2}{green:X2}{blue:X2}";

            // Calculate brightness (simple version)
            var brightness = (0.299 * red + 0.587 * green + 0.114 * blue) / 255;

            // Choose text color based on brightness
            var textColor = brightness > 0.5 ? "#000000" : "#FFFFFF";
            return (hexColor, textColor);
        }

        private void ShowTooltip(ElementReference elementReference)
        {
            TooltipOptions options = new() { CloseTooltipOnDocumentClick = true };
            Console.WriteLine($"Hover event displays {_token}");
            TooltipService.Open(elementReference, $"{_token}", options);
        }
    }
}
