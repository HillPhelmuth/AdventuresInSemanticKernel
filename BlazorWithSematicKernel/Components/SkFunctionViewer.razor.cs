using Microsoft.AspNetCore.Components;
using ParameterView = Microsoft.SemanticKernel.KernelParameterMetadata;

namespace BlazorWithSematicKernel.Components
{
    public partial class SkFunctionViewer : ComponentBase
    {
        [Parameter]
        public Dictionary<string, KernelFunction> Functions { get; set; } = [];

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
        public EventCallback<KeyValuePair<string, KernelFunction>> FunctionSelected { get; set; }
        private bool IsHandlebars => ExecutionType is ExecutionType.HandlebarsPlanner or ExecutionType.HandlebarsPlannerChat;
        private bool IsStepwise => ExecutionType is ExecutionType.StepwisePlanner or ExecutionType.StepwisePlannerChat;
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
                    ExecutionType.SequentialPlanner or ExecutionType.SequentialPlannerChat =>
                        "Exclude or Require Functions to Execute",
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

        private async void ShowPrompt(KernelFunction function)
        {
            //if (!function.IsSemantic) return;
            var promptPath = Path.Combine(RepoFiles.PluginDirectoryPath, function.Metadata?.PluginName??"",
                function.Name, "skprompt.txt");
            if (File.Exists(promptPath))
            {
                var prompt = await File.ReadAllTextAsync(promptPath);
                DialogService.Open<ShowSkPrompt>("",
                    new Dictionary<string, object>()
                        {{"Title", $"{function.Metadata.PluginName} {function.Name}"}, {"Prompt", prompt}});
            }
        }

        private bool HasPrompt(KernelFunction function)
        {
            var promptPath = Path.Combine(RepoFiles.PluginDirectoryPath, function.Metadata?.PluginName ?? "",
                function.Name, "skprompt.txt");
            return File.Exists(promptPath);
        }
        private string _visiblePluginFunction = "";
        private void ShowParameters(IEnumerable<ParameterView> parameterView, string skillName, string functionName)
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
            public KeyValuePair<string, KernelFunction> Function { get; set; }
            public List<ParamViewField> Fields { get; set; } = [];
        }

        private FunctionForm _functionForm = new();
        private string _visibleFunctionForm = "";

        private void ShowFunctionForm(string skillName, string functionName, KernelFunction function, IEnumerable<ParameterView> parameterView)
        {
            _visibleFunctionForm = $"{skillName}-{functionName}";

            _functionForm.Function = new KeyValuePair<string, KernelFunction>(functionName, function);
            _functionForm.Fields = parameterView.Select(p => new ParamViewField(p.Name, p.Description ?? "", p.DefaultValue ?? "")).ToList();
            StateHasChanged();
        }
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

        private void SelectFunction(KeyValuePair<string, KernelFunction> sKFunction)
        {
            FunctionSelected.InvokeAsync(sKFunction);
        }
    }
}
