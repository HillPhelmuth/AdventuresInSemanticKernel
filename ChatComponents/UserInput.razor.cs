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
        [Parameter]
        public UserInputType UserInputType { get; set; }
        [Parameter]
        public EventCallback<UserInputRequest> UserInputSubmit { get; set; }
        [Parameter]
        public EventCallback CancelRequest { get; set; }
        [Parameter]
        public string CssClass { get; set; } = "";
        protected override Task OnParametersSetAsync()
        {
            _requestForm.UserInputRequest.UserInputType = UserInputType;
            return base.OnParametersSetAsync();
        }


        private bool _isDisabled = false;

        private class RequestForm
        {
            public string? Content { get; set; }
            public UserInputRequest UserInputRequest { get; set; } = new();
        }

        private RequestForm _requestForm = new();
        private void Cancel()
        {
            CancelRequest.InvokeAsync();
        }
        private void SubmitRequest(RequestForm form)
        {
            MessageSubmit.InvokeAsync(form.UserInputRequest.ChatInput);
            UserInputSubmit.InvokeAsync(form.UserInputRequest);
            _requestForm = new RequestForm
            {
                UserInputRequest =
                {
                    UserInputType = UserInputType
                }
            };
            StateHasChanged();
        }
    }

    public enum UserInputType
    {
        Chat,
        Ask,
        Both
    }

    public class UserInputRequest
    {
        public string? AskInput { get; set; }
        public string? ChatInput { get; set; }
        public UserInputType UserInputType { get; set;}
    }
}
