using Azure.AI.OpenAI;
using Markdig;
using Microsoft.AspNetCore.Components;
using OpenAI.Chat;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;

namespace BlazorWithSematicKernel.Pages
{
    public partial class LogProbExperiments : ComponentBase
    {
        //[Inject]
        //private IConfiguration Configuration { get; set; } = default!;
        //private string ApiKey => Configuration["OpenAI:ApiKey"]!;
        [Inject]
        private ITokenization LogProbService { get; set; } = default!;
        
        private List<ChatTokenLogProbabilityDetails> _tokens = [];
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
        private List<string> _models = [AIModel.Gpt4OMini.GetOpenAIModelName(), AIModel.Gpt4OCurrent.GetOpenAIModelName(), AIModel.Gpt35Turbo.GetOpenAIModelName(), AIModel.Gpt4OChatGptLatest.GetOpenAIModelName()];
        private LogProbInputForm _logProbInputForm = new();
        private async void SendQuery(LogProbInputForm logProbInputForm)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            ChatCompletion? choice = await LogProbService.GetLogProbs(logProbInputForm.UserInput!,logProbInputForm.Tempurature,logProbInputForm.TopP, logProbInputForm.SystemPrompt, logProbInputForm.Model);
            
            _output = choice?.ToString() ?? "";
            _tokens = choice?.ContentTokenLogProbabilities.ToList() ?? [];
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
}
