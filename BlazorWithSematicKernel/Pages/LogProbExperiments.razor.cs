using Azure.AI.OpenAI;
using Markdig;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages
{
    public partial class LogProbExperiments : ComponentBase
    {
        //[Inject]
        //private IConfiguration Configuration { get; set; } = default!;
        //private string ApiKey => Configuration["OpenAI:ApiKey"]!;
        [Inject]
        private ITokenization LogProbService { get; set; } = default!;
        private ChatChoice? _chatResponse;
        private List<ChatTokenLogProbabilityResult> _tokens = [];
        private string _output = string.Empty;
        private string _query = string.Empty;
        private bool _isBusy;
        private TokenString? _selectedTokenString;
        private bool _showRaw;
        private class LogProbInputForm
        {
            public string? SystemPrompt { get; set; } = "You are a helpful AI model";
            public string? UserInput { get; set; } = "5 random words, one word per line";
            public string? Model { get; set; } = "gpt-3.5-turbo";
            public float Tempurature { get; set; } = 1.0f;
            public float TopP { get; set; } = 1.0f;
        }
        private List<string> _models = ["gpt-3.5-turbo", "gpt-4-turbo-preview"];
        private LogProbInputForm _logProbInputForm = new();
        private async void SendQuery(LogProbInputForm logProbInputForm)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            var choice = await LogProbService.GetLogProbs(logProbInputForm.UserInput!,logProbInputForm.Tempurature,logProbInputForm.TopP, logProbInputForm.SystemPrompt, logProbInputForm.Model);
            
            _output = choice?.Message.Content?.ToString() ?? "";
            _tokens = choice?.LogProbabilityInfo.TokenLogProbabilityResults.ToList() ?? [];
            _isBusy = false;
            StateHasChanged();
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            text = text.Replace("\n", "<br/>");

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }

    }
    public static class LogProbExtensions
    {
        public static List<TokenString> AsTokenStrings(this List<ChatTokenLogProbabilityResult> logProbContentItems)
        {
            var result = new List<TokenString>();
            foreach (var logProb in logProbContentItems)
            {
                var tokenString = new TokenString(logProb.Token, logProb.NormalizedLogProb());
                if (logProb.TopLogProbabilityEntries is {Count: > 0})
                {
                    var innerResult = logProb.TopLogProbabilityEntries.Select(item => new TokenString(item.Token, item.NormalizedLogProb())).ToList();
                    tokenString.TopLogProbs = innerResult;
                }
                result.Add(tokenString);
            }            
            return result;
        }
        public static double NormalizedLogProb(this ChatTokenLogProbabilityResult logProbabilityResult)
        {
            return Math.Exp(logProbabilityResult.LogProbability);
        }
        public static double NormalizedLogProb(this ChatTokenLogProbabilityInfo logProbInfo)
        {
            return Math.Exp(logProbInfo.LogProbability);
        }
    }
}
