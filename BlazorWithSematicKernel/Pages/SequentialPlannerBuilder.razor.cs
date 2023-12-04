using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Helpers;

namespace BlazorWithSematicKernel.Pages
{
    public partial class SequentialPlannerBuilder : ComponentBase
    {
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;
        private int _currentStep;
        private List<PluginFunctions> _allPlugins = new();
        private async Task AllPlugins()
        {
            _allPlugins = await CoreKernelService.GetAllPlugins();
        }
        protected override async Task OnInitializedAsync()
        {
            await AllPlugins();
            await base.OnInitializedAsync();
        }

        private static Dictionary<ExecutionType, string> ExecutionTypeDescriptions => typeof(ExecutionType).GetEnumsWithDescriptions<ExecutionType>().Where(x => x.Key.IsActive() ).ToDictionary(x => x.Key, x => x.Value);
        private class ExecutionTypeForm
        {
            public ExecutionType ExecutionType { get; set; }
        }

        private void SetExecutionType(ExecutionTypeForm form)
        {
            _currentStep = 0;
            StateHasChanged();
            _requestModel.ExecutionType = form.ExecutionType;
            Console.WriteLine($"Execution type selected: {_requestModel.ExecutionType}");
            _currentStep = 1;
            StateHasChanged();
        }
        private ExecutionTypeForm _executionTypeForm = new();
        private Dictionary<string, ISKFunction> _allFunctions = new();
        private List<string> _excludedFunctions = new();
        private List<string> _requiredFunctions = new();
        private List<Function> _selectedFunctions = new();
        private Dictionary<string, string> _contextVariables = new();

        [Inject]
        private TooltipService TooltipService { get; set; } = default!;

        private void SelectPlugins()
        {
            var selectedPlugins = _allPlugins.Where(x => x.IsSelected).ToList();
            _allFunctions = selectedPlugins.SelectMany(x => x.SkFunctions).ToDictionary(s => s.Key, s => s.Value);
            _requestModel.SelectedPlugins = selectedPlugins;
            _currentStep = 2;
            StateHasChanged();
        }

        private string ChatStepName()
        {
            if (_executionTypeForm.ExecutionType.ToString().Contains("Chat"))
            {
                return "Chat with Plan";
            }
            if (_executionTypeForm.ExecutionType.ToString().Contains("Plan"))
            {
                return "Execute Plan";
            }
            return _executionTypeForm.ExecutionType == ExecutionType.ChainFunctions ? "Execute Function Chain" : "Execute";
        }
        private ChatRequestModel _requestModel = new();
        private bool _isReady;
        private void HandleFunctionComplete()
        {
            _requestModel.ExcludedFunctions = _excludedFunctions;
            _requestModel.RequredFunctions = _requiredFunctions;
            _requestModel.SelectedFunctions = _selectedFunctions;
            _requestModel.Variables = _contextVariables;
            _isReady = true;
            _currentStep = 3;
            StateHasChanged();

        }

        private void HandleExecuteSingle(ValueTuple<string, string, Dictionary<string, string>> functionValues)
        {

            _requestModel.Variables = functionValues.Item3;
            _isReady = true;
            _currentStep = 3;
            StateHasChanged();

        }
       
    }
}
