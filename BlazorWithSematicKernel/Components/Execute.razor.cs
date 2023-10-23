using System.Text.Json;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using Markdig;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace BlazorWithSematicKernel.Components
{
    public partial class Execute : ComponentBase
    {
        [Parameter]
        [EditorRequired]
        public required Function Function { get; set; }

        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;
        [Inject]
        private NotificationService NotificationService { get; set; } = default!;
        private record FunctionParameterField(string Name, string Description, string DefaultValue, string Type)
        {
            public string Value { get; set; } = DefaultValue;
            public int NumValue { get; set; } = int.TryParse(DefaultValue, out var num) ? num : 0;
            public bool BoolValue { get; set; }
        }
        private enum DisplayType { PlainText, Markdown, Json }
        private DisplayType _selectedDisplay = DisplayType.PlainText;
        private bool _asJson;

        private class FunctionInputsForm
        {
            public List<FunctionParameterField> FunctionParameterFields { get; set; } = new();

        }

        private async void HandleSelectChanged(DisplayType displayType)
        {
            _selectedDisplay = displayType;
            if (displayType == DisplayType.Json)
            {
                StateHasChanged();
                await Task.Delay(1);
                await ShowAsJson();
                StateHasChanged();
            }

        }
        private async Task ShowAsJson()
        {

            object? deserialize = null;
            try
            {
                deserialize = JsonSerializer.Deserialize<object>(_output);
            }
            catch (JsonException e)
            {
                Console.WriteLine(e);
            }
            await JsRuntime.InvokeVoidAsync("addJsonToViewer", deserialize);

        }
        private FunctionInputsForm _functionInputsForm = new();
        private string? _promptText;
        private string? _output;
        protected override Task OnParametersSetAsync()
        {
            var funcView = Function.SkFunction.Describe();
            var fields = funcView.Parameters.Select(x => new FunctionParameterField(x.Name, x.Description ?? "", x.DefaultValue ?? "", x.Type?.ToString() ?? "string")).ToList();
            if (!fields.Any(x => x.Name.Equals("input", StringComparison.InvariantCultureIgnoreCase)))
            {
                fields.Add(new FunctionParameterField("Input", "Basic input for the function", "", "string"));
            }
            _functionInputsForm.FunctionParameterFields = fields;
            _promptText = PromptText(Function.SkFunction);
            return base.OnParametersSetAsync();
        }
        private static string? PromptText(ISKFunction function)
        {
            if (!function.IsSemantic) return null;
            var promptPath = Path.Combine(RepoFiles.PluginDirectoryPath, function.PluginName,
                function.Name, "skprompt.txt");
            if (!File.Exists(promptPath)) return null;
            var prompt = File.ReadAllText(promptPath);
            return prompt;
        }

        private class InputArray
        {
            [JsonPropertyName("items")]
            public List<string> Items { get; set; }
        }
        private async void Submit(FunctionInputsForm functionInputsForm)
        {
            _selectedDisplay = DisplayType.PlainText;
            StateHasChanged();
            var sw = new Stopwatch();
            sw.Start();
            try
            {
                var variables = functionInputsForm.FunctionParameterFields.ToDictionary(x => x.Name, x => x.Value);
                var newVariables = new Dictionary<string, string>();
                foreach (var field in functionInputsForm.FunctionParameterFields)
                {
                    if (field.Type.Equals("array", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var values = new InputArray {Items = field.Value.Split(',').ToList()};
                        var value = JsonSerializer.Serialize(values);
                        newVariables.Add(field.Name, value);
                    }
                    else if (field.Type.Equals("string", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newVariables.Add(field.Name, field.Value);
                    }
                    else if (field.Type.Equals("boolean", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newVariables.Add(field.Name, field.BoolValue.ToString());
                    }
                    else if (field.Type.Equals("number", StringComparison.InvariantCultureIgnoreCase))
                    {
                        newVariables.Add(field.Name, field.NumValue.ToString());
                    }
                }
                if (Function.Name is "TryStreamFunction" or "ExecuteChatStreamResponse" or "WikiSearchAndChat")
                {
                    var result = CoreKernelService.ExecuteFunctionStream(Function.SkFunction, variables);
                    await foreach (var item in result)
                    {
                        _output += item;
                        StateHasChanged();
                    }

                    StateHasChanged();
                    return;
                }

                _output = await CoreKernelService.ExecuteFunction(Function.SkFunction, variables);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                sw.Stop();
                NotificationService.Notify(NotificationSeverity.Error, $"Error Executing Function after {sw.ElapsedMilliseconds}ms", ex.Message, 10000);
            }
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
    }
}
