using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;

namespace BlazorWithSematicKernel.Pages
{
    public partial class TokenTinkerPage : ComponentBase
    {
        private string? _input;

        [Inject]
        private ITokenization CoreKernelService { get; set; } = default!;

        private bool _isBusy;
        private List<TokenString> _manualTokens = new();
        private List<TokenString> _specifiedTokens = new();
        private List<TokenString> _outputTokens = new();

        private static List<TokenString> AsTokens(string input)
        {
            return StringHelpers.EncodeDecodeWithSpaces(input);
        }
        private class LogitBiasForm
        {
            public string? TestInput { get; set; } = "";
            public List<LogitBiasItem> LogitBiasItems { get; set; } = new();
        }
        private LogitBiasForm _logitBiasForm = new();
        private class LogitBiasItem
        {
            public LogitBiasItem(TokenString tokenString)
            {
                TokenString = tokenString;
            }
            public TokenString TokenString { get; set; }
            public int LogitBias { get; set; }
        }

        private void HandleTokenStringSelected(TokenString tokenString)
        {
            if (tokenString.Token == -1) return;
            if (_logitBiasForm.LogitBiasItems.Any(x => x.TokenString == tokenString)) return;
            _logitBiasForm.LogitBiasItems.Add(new LogitBiasItem(tokenString));
            StateHasChanged();
        }

        private async void Submit(LogitBiasForm form)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var logitBiasDictionary = form.LogitBiasItems.ToDictionary(x => x.TokenString.Token, x => x.LogitBias);
            var response = await CoreKernelService.GenerateResponseWithLogitBias(logitBiasDictionary, form.TestInput);
            Console.WriteLine("Reponse received");
            _outputTokens = StringHelpers.EncodeDecodeWithSpaces(response);
            Console.WriteLine($"Output token count = {_outputTokens.Count}");
            //_specifiedTokens = _outputTokens.Where(x => form.LogitBiasItems.Select(lbItem => lbItem.TokenString.Token).Contains(x.Token))
            //    .ToList();
            _specifiedTokens = form.LogitBiasItems.Select(lbItem => lbItem.TokenString).ToList();
            Console.WriteLine($"Specified token count: {_specifiedTokens.Count}");
            Console.WriteLine($"Specific count: {_specifiedTokens.Count}");
            _isBusy = false;
            StateHasChanged();
        }
    }

}

