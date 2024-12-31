using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components.LogProbComponents
{
    public partial class LogProbTokenDisplay : ComponentBase
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
            
            if (SpecifiedTokens == null || !SpecifiedTokens.Any() || SpecifiedTokens.Select(x => x.StringValue).Contains(TokenString.StringValue))
            {
                var text = TokenString.StringValue;
                var textAsHtml = TextAsHtml(text, TokenString.NormalizedLogProbability);
                _textAsHtml = textAsHtml!;
            }
            else
            {
                _textAsHtml = TokenString.StringValue;
            }


        }

        private static string TextAsHtml(string? text, double logProb)
        {
            if (text == null) return "";
            if (text.StartsWith(' '))
                text = $"&nbsp;{text[1..]}";
            var (hexColor, textColor) = HexColor(logProb);
            var textAsHtml = $"<span style=\"background-color:{hexColor}; color:{textColor}\">{text}</span>";
            return textAsHtml;
        }
        private static string ItemHexColor(double probability)
        {
            var red = (int)(255 * probability);
            var green = (int)(255 * probability);
            var blue = (int)(255 * probability);

            var hexColor = $"#{red:X2}{green:X2}{blue:X2}";
            return hexColor;
        }
        private static (string hexColor, string textColor) HexColor(double probability)
        {
            int red;
            int green;
            int blue;
            if (probability > 0.5)
            {
                
                blue = (int)(255 * probability);
                green = 255;
                red = (int)(255 * probability); 
            }
            else
            {
                
                blue = (int)(255 * probability);
                green = (int)(510 * probability); 
                red = (int)(255 * probability);
            }
            var hexColor = $"#{red:X2}{green:X2}{blue:X2}";

            // Calculate brightness (simple version)
            var brightness = (0.299 * red + 0.587 * green + 0.114 * blue) / 255;

            // Choose text color based on brightness
            var textColor = brightness > 0.5 ? "#000000" : "#FFFFFF";
            return (hexColor, textColor);
        }

        private void ShowTooltip(ElementReference elementReference, TokenString alternateTokens)
        {
            TooltipOptions options = new() { CloseTooltipOnDocumentClick = true, Duration = 100000 };
            Console.WriteLine($"Hover event displays {TokenString.StringValue}.\nProbability:{alternateTokens.NormalizedLogProbability:P3}");
            //var sb = new StringBuilder();
            //sb.AppendLine("<ol>");
            //foreach (var token in alternateTokens)
            //{
            //    sb.AppendLine($"<li><div>{token.StringValue}</div><div>{token.NormalizedLogProbability.ToString("P2")}</div></li>");
            //}
            //sb.AppendLine("</ol>");
            TooltipService.Open(elementReference, $"Probability: {alternateTokens.NormalizedLogProbability:P3}<br/><em style=\"font-size:90%\">click for details</em>", options);
        }
    }
}
