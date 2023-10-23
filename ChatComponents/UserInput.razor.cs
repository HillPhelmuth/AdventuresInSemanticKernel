using Microsoft.AspNetCore.Components;

namespace ChatComponents
{
    public partial class UserInput : ComponentBase
    {
        [Parameter]
        public string HelperText { get; set; } = "";
        [Parameter]
        public bool IsBusy { get; set; }

        [Parameter]
        public string ButtonLabel { get; set; } = "Submit";
        [Parameter]
        public EventCallback<string> MessageSubmit { get; set; }
        [Parameter]
        public EventCallback<string> MessageChanged { get; set; }

        private bool _isDisabled = false;

        private class RequestForm
        {
            public string? Content { get; set; }
        }

        private RequestForm _requestForm = new();

        private void SubmitRequest(RequestForm form)
        {
            MessageSubmit.InvokeAsync(form.Content);
            _requestForm = new RequestForm();
            StateHasChanged();
        }
    }
}
