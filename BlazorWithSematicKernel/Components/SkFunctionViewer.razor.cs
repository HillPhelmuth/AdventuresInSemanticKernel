using Microsoft.AspNetCore.Components;

namespace BlazorWithSematicKernel.Components
{
    public partial class SkFunctionViewer : ComponentBase
    {
        [Parameter]
        public Dictionary<string, AIFunction> Functions { get; set; } = [];

        [Parameter]
        public ExecutionType ExecutionType { get; set; }

        [Parameter] public List<string> ExcludedFunctions { get; set; } = [];
        [Parameter]
        public EventCallback<List<string>> ExcludedFunctionsChanged { get; set; }

        [Parameter] public List<string> RequiredFunctions { get; set; } = [];
        [Parameter]
        public EventCallback<List<string>> RequiredFunctionsChanged { get; set; }
        [Parameter]
        public EventCallback SelectionComplete { get; set; }
        [Parameter]
        public EventCallback<ValueTuple<string, string, Dictionary<string, string>>> FunctionInputsAdded { get; set; }

        [Parameter] public Dictionary<string, string> ContextVariables { get; set; } = [];
        [Parameter]
        public EventCallback<Dictionary<string, string>> ContextVariablesChanged { get; set; }

        [Parameter] public List<Function> PluginFunctions { get; set; } = [];
        [Parameter]
        public EventCallback<List<Function>> PluginFunctionsChanged { get; set; }
        [Parameter]
        public EventCallback<KeyValuePair<string, AIFunction>> FunctionSelected { get; set; }
        private bool IsHandlebars => ExecutionType is ExecutionType.PlanAndExecuteWorkflow or ExecutionType.PlanAndExecuteWorkflowChat;
        private bool IsStepwise => ExecutionType is ExecutionType.StepwiseAgent or ExecutionType.StepwiseAgentChat;
        private bool IsAutoFunctionCall => ExecutionType is ExecutionType.AutoFunctionCalling or ExecutionType.AutoFunctionCallingChat;
        private bool IsPlanner => IsHandlebars || IsStepwise || IsAutoFunctionCall;
        [Inject]
        private TooltipService TooltipService { get; set; } = default!;

        [Inject] private DialogService DialogService { get; set; } = default!;
        private void ShowTooltip(ElementReference elementReference, string message)
        {
            TooltipService.Open(elementReference, message);
        }

        private string Header
        {
            get
            {
                return ExecutionType switch
                {
                    ExecutionType.SingleFunction => "Select Function to Execute",
                    ExecutionType.AutoFunctionCalling or ExecutionType.AutoFunctionCallingChat => "Execute Action Plan",
                    _ => "View Functions"
                };
            }
        }

        private class ParamViewForm
        {
            public string? PluginName { get; set; }
            public string? FunctionName { get; set; }
            public List<ParamViewField> Fields { get; set; } = [];
        }
        private ParamViewForm _paramForm = new();
        private record ParamViewField(string Name, string Description, object? DefaultValue)
        {
            public string? Value { get; set; } = DefaultValue?.ToString();
        }

        private void ExcludeFunction(string functionName)
        {
            if (ExcludedFunctions.Contains(functionName)) return;
            ExcludedFunctions.Add(functionName);
            ExcludedFunctionsChanged.InvokeAsync(ExcludedFunctions);
        }

        private void RequireFunction(string functionName)
        {
            if (RequiredFunctions.Contains(functionName)) return;
            RequiredFunctions.Add(functionName);
            RequiredFunctionsChanged.InvokeAsync(RequiredFunctions);
        }

        private void ShowPrompt(AIFunction function)
        {
            var pluginName = function.GetMetadata().PluginName;
            var functionName = function.Name;
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(functionName)) return;
            var promptPath = Path.Combine(RepoFiles.PathToYamlPlugins, pluginName, $"{functionName}.yaml");
            if (!File.Exists(promptPath)) return;
            var yamlText = File.ReadAllText(promptPath);
            var promptTemplate = YamlPromptFunction.FromYaml(yamlText).Template;
            // return promptTemplate;
            //var promptPath = Path.Combine(RepoFiles.PluginDirectoryPath, function.GetMetadata().PluginName??"",
            //    function.Name, "skprompt.txt");
            if (File.Exists(promptPath))
            {
                var prompt = promptTemplate;
                DialogService.Open<ShowSkPrompt>("",
                    new Dictionary<string, object>()
                        {{"Title", $"{function.GetMetadata().PluginName} - {function.Name}"}, {"Prompt", prompt}});
            }
        }

        private bool HasPrompt(AIFunction function)
        {
            var pluginName = function.GetMetadata().PluginName;
            var functionName = function.Name;
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(functionName)) return false;
            var promptPath = Path.Combine(RepoFiles.PathToYamlPlugins, pluginName, $"{functionName}.yaml");
            if (!File.Exists(promptPath)) return false;
            var yamlText = File.ReadAllText(promptPath);
            var promptTemplate = YamlPromptFunction.FromYaml(yamlText).Template;
            return !string.IsNullOrEmpty(promptTemplate);
        }
        private string _visiblePluginFunction = "";
        private void ShowParameters(IEnumerable<ParameterMetadata> parameterView, string skillName, string functionName)
        {
            _visiblePluginFunction = $"{skillName}-{functionName}";
            _paramForm.PluginName = skillName;
            _paramForm.FunctionName = functionName;
            _paramForm.Fields = parameterView.Select(p => new ParamViewField(p.Name, p.Description ?? "", p.DefaultValue ?? "")).ToList();
            StateHasChanged();
        }


        private class FunctionForm
        {
            public int Order { get; set; } = 0;
            public KeyValuePair<string, AIFunction> Function { get; set; }
            public List<ParamViewField> Fields { get; set; } = [];
        }

        private FunctionForm _functionForm = new();
        private string _visibleFunctionForm = "";

        private void SelectFunction(FunctionForm functionForm)
        {
            PluginFunctions.Add(new Function(functionForm.Function.Key, functionForm.Function.Value)
            { Order = functionForm.Order });
            PluginFunctionsChanged.InvokeAsync(PluginFunctions);
            foreach (var parameter in functionForm.Fields)
            {
                ContextVariables.TryAdd(parameter.Name, parameter.Value);
            }
            ContextVariablesChanged.InvokeAsync(ContextVariables);
            _functionForm = new FunctionForm();
            _visibleFunctionForm = "";
        }
        private void Submit(ParamViewForm paramViewForm)
        {
            foreach (var parameter in paramViewForm.Fields)
            {
                ContextVariables.TryAdd(parameter.Name, parameter.Value);

            }
            ContextVariablesChanged.InvokeAsync(ContextVariables);
            _paramForm = new ParamViewForm();
        }

        private void SelectFunction(KeyValuePair<string, AIFunction> sKFunction)
        {
            FunctionSelected.InvokeAsync(sKFunction);
        }
    }
}
