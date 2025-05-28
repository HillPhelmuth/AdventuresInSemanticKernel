using System.Text.Json;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using Markdig;
using System.Text.Json.Serialization;
using System.Diagnostics;
using SkPluginLibrary.Models.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml;

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
        private record FunctionParameterField(string Name, string Description, object? DefaultValue, Type Type)
        {
            public string? Value { get; set; } = DefaultValue?.ToString();
            public int NumValue { get; set; } = int.TryParse(DefaultValue?.ToString(), out var num) ? num : 0;
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
            var funcView = Function.SkFunction.Metadata;
            var fields = funcView.Parameters.Select(x => new FunctionParameterField(x.Name, x.Description ?? "", x.DefaultValue ?? "", x.ParameterType ?? typeof(string))).ToList();
            if (!fields.Any(x => x.Name.Equals("input", StringComparison.InvariantCultureIgnoreCase)))
            {
                fields.Add(new FunctionParameterField("Input", "Basic input for the function", "", typeof(string)));
            }
            _functionInputsForm.FunctionParameterFields = fields;
            _promptText = PromptText(Function.SkFunction.PluginName, Function.SkFunction.Name);
            return base.OnParametersSetAsync();
        }
        //private static string? PromptText(KernelFunction function)
        //{
        //    var promptPath = Path.Combine(RepoFiles.PluginDirectoryPath, function.Metadata?.PluginName ?? "", function.Name, "skprompt.txt");
        //    if (!File.Exists(promptPath)) return null;
        //    var prompt = File.ReadAllText(promptPath);
        //    return prompt;
        //}
        private static string? PromptText(string? pluginName, string functionName)
        {
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(functionName)) return null;
            var promptPath = Path.Combine(RepoFiles.PathToYamlPlugins, pluginName, $"{functionName}.yaml");
            if (!File.Exists(promptPath)) return null;
            var prompt = File.ReadAllText(promptPath);
            var promptTemplateConfig = KernelFunctionYaml.ToPromptTemplateConfig(prompt);
            return promptTemplateConfig.Template;
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
                var newVariables = new Dictionary<string, object>();
                foreach (var input in functionInputsForm.FunctionParameterFields)
                {
                    if (input.Type.IsCollectionType())
                    {
                        var values = new InputArray { Items = [.. input.Value.Split(',')] };
                        var value = JsonSerializer.Serialize(values);
                        newVariables.Add(input.Name, value);
                    }
                    else if (input.Type == typeof(string))
                    {
                        newVariables.Add(input.Name, input.Value);
                    }
                    else if (input.Type==typeof(bool))
                    {
                        newVariables.Add(input.Name, input.BoolValue);
                    }
                    else if (input.Type.IsNumericType())
                    {
                        newVariables.Add(input.Name, input.NumValue);
                    }
                    else
                    {
                        newVariables.Add(input.Name, JsonSerializer.Serialize(input.Value));
                    }
                }

                foreach (var variable in variables)
                {
                    newVariables.TryAdd(variable.Key, variable.Value);
                }
                var kernelResult = await CoreKernelService.ExecuteKernelFunction(Function.SkFunction, newVariables);
                if (kernelResult.IsStreamingResult())
                {
                    await foreach (var item in kernelResult.ResultStream<string>())
                    {
                        _output += item;
                        StateHasChanged();
                    }
                }
                else
                {
                    _output = kernelResult.Result();
                    StateHasChanged();
                }
                //if (Function.Name is "TryStreamFunction" or "ExecuteChatStreamResponse" or "WikiSearchAndChat")
                //{
                //    var result = CoreKernelService.ExecuteFunctionStream(Function.SkFunction, variables);
                //    await foreach (var item in result)
                //    {
                //        _output += item;
                //        StateHasChanged();
                //    }

                //    StateHasChanged();
                //    return;
                //}

                //_output = await CoreKernelService.ExecuteFunction(Function.SkFunction, newVariables);
                //StateHasChanged();
            }
            catch (Exception ex)
            {
                sw.Stop();
                var notificationMessage = new NotificationMessage
                {
                    Style = "top:10px;width:30rem;height:10rem;overflow:auto; right:25vw",
                    Severity = NotificationSeverity.Error,
                    Duration = int.MaxValue,
                    Summary = $"Error Executing Function after {sw.ElapsedMilliseconds}ms",
                    Detail = $"{ex.Message}\n{ex.StackTrace}"
                };
                NotificationService.Notify(notificationMessage);
                //NotificationService.Notify(NotificationSeverity.Error, $"Error Executing Function after {sw.ElapsedMilliseconds}ms",
                //    $"{ex.Message}\n{ex.StackTrace}", int.MaxValue);
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
