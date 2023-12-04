using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using SkPluginComponents.Models;
using System.Net.Http.Headers;

namespace SkPluginComponents.Components
{
    public partial class SimpleInput : ComponentBase
    {
        [Inject]
        private AskUserService ModalService { get; set; } = default!;
        [Parameter]
        public SimpleInputType SimpleInputType { get; set; } = SimpleInputType.Text;
        [Parameter]
        public string Options { get; set; } = string.Empty;
        private string? _value;
        private List<string> _options = new();
        private struct FormFieldValue
        {
            public string? TextInput { get; set; }
            public bool? BooleanInput { get; set; }
            public DateOnly? DateInput { get; set; }
            public int? NumberInput { get; set; }
            public string? OverrideInstructions { get; set; }
        }
        private FormFieldValue _formFieldValue;
        protected override Task OnParametersSetAsync()
        {
            if (!string.IsNullOrEmpty(Options))
            {
                _options = Options.Split("\n").ToList();
                SimpleInputType = SimpleInputType.Select;
                StateHasChanged();
            }
            if (SimpleInputType == SimpleInputType.Number)
            {
                _formFieldValue.NumberInput = 0;
                StateHasChanged();
            }

            return base.OnParametersSetAsync();
        }
        private void Confirm(bool confirmed)
        {
            _formFieldValue.BooleanInput = confirmed;
            Submit();
        }
        private void Submit()
        {
            if (_formFieldValue.TextInput != null)
                _value = _formFieldValue.TextInput;
            else if (_formFieldValue.BooleanInput != null)
                _value = _formFieldValue.BooleanInput.ToString();
            else if (_formFieldValue.DateInput != null)
                _value = _formFieldValue.DateInput.ToString();
            else if (_formFieldValue.NumberInput != null)
                _value = _formFieldValue.NumberInput.ToString();
            else
                _value = string.Empty;
            if (!string.IsNullOrEmpty(_formFieldValue.OverrideInstructions))
                _value = _formFieldValue.OverrideInstructions;
            var results = new AskUserResults(true, new AskUserParameters { { "Value", _value } });
            ModalService.Close(results);
        }
    }
}
