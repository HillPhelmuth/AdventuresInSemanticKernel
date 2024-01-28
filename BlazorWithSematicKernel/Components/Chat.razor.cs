using Microsoft.AspNetCore.Components;
using Microsoft.Graph;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components
{
    public partial class Chat : ComponentBase
    {
        private ChatView? _chatView;

        [Parameter] public ChatRequestModel ChatRequestModel { get; set; } = new();
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;

        private string HelpText => ChatRequestModel.ExecutionType.ToString().Contains("Chat") ? "Plan Ask + Chat" :
            ChatRequestModel.ExecutionType.ToString().Contains("Plan") ? "Plan Ask" : "Initial Input";
        private bool AskPlusChatInput => ChatRequestModel.ExecutionType.ToString().Contains("Chat");
        private bool AskOnlyInput => ChatRequestModel.ExecutionType.ToString().Contains("Plan") && !AskPlusChatInput;
        private UserInputType _userInputType;
        private CancellationTokenSource _cancellationTokenSource = new();
        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _chatView!.ChatState.Reset();
            }
            return base.OnAfterRenderAsync(firstRender);
        }
        protected override void OnParametersSet()
        {
            _userInputType = AskPlusChatInput ? UserInputType.Both : AskOnlyInput ? UserInputType.Ask : UserInputType.Chat;
            base.OnParametersSet();
        }
        private bool _isBusy;
        private string? _askInput;
        private string? _chatInput;
        private void HandleYieldReturn(string text)
        {
            _chatView!.ChatState!.UpsertAssistantMessage(text);
        }
        private void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        private async void HandleChatInput(UserInputRequest requestInput)
        {
            var input = "";
            if (!string.IsNullOrEmpty(requestInput.AskInput))
            {
                input = $"Plan Ask: {requestInput.AskInput}\n\n";
            }

            if (!string.IsNullOrEmpty(requestInput.ChatInput))
            {
                input += $"Chat: {requestInput.ChatInput}\n\n";
            }

            _askInput = requestInput.AskInput;
            Console.WriteLine($"ChatRequestModel Received:\n{ChatRequestModel}");
            CoreKernelService.YieldAdditionalText += HandleYieldReturn;
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            try
            {
                switch (ChatRequestModel.ExecutionType)
                {
                    case ExecutionType.AutoFunctionCalling:
                        {
                            await ExecuteActionChatSequence(input, true);
                            break;
                        }
                    case ExecutionType.AutoFunctionCallingChat:
                        {
                            await ExecuteActionChatSequence(input, true);
                            break;
                        }
                    case ExecutionType.SequentialPlanner:
                        {
                            await ExecuteSequentialChatSequence(input, false);
                            break;
                        }
                    case ExecutionType.SequentialPlannerChat:
                        {
                            await ExecuteSequentialChatSequence(input, true);
                            break;
                        }
                    case ExecutionType.StepwisePlanner:
                        {
                            await ExecuteStepwiseChatSequence(input, false);
                            break;
                        }
                    case ExecutionType.StepwisePlannerChat:
                        {
                            await ExecuteStepwiseChatSequence(input, true);
                            break;
                        }
                    case ExecutionType.HandlebarsPlanner:
                        {
                            await ExecuteHandlebarsChatSequence(input, false);
                            break;
                        }
                    case ExecutionType.HandlebarsPlannerChat:
                        {
                            await ExecuteHandlebarsChatSequence(input, true);
                            break;
                        }
                    case ExecutionType.None:
                    case ExecutionType.SingleFunction:
                    default:
                        return;
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error Executing Plan", ex.Message, 10000);
            }
            _isBusy = false;
            CoreKernelService.YieldAdditionalText -= HandleYieldReturn;
            StateHasChanged();
        }

        private Task ExecuteActionChatSequence(string input, bool runAsChat)
        {
            var token = _cancellationTokenSource.Token;
            _chatView!.ChatState.AddUserMessage(input);
            var reset = _chatView!.ChatState.MessageCount > 1;
            var chatWithActionPlanner = CoreKernelService.ChatWithAutoFunctionCalling(input,
                ChatRequestModel, runAsChat, _askInput, token, reset);
            return ExecuteChatSequence(chatWithActionPlanner);
        }

        private Task ExecuteSequentialChatSequence(string input, bool runAsChat)
        {
            var token = _cancellationTokenSource.Token;
            _chatView!.ChatState.AddUserMessage(input);
            var chatWithPlanner = CoreKernelService.ChatWithSequentialPlanner(input,
                ChatRequestModel, runAsChat, _askInput, token);
            return ExecuteChatSequence(chatWithPlanner);
        }

        private Task ExecuteStepwiseChatSequence(string input, bool runAsChat)
        {
            var token = _cancellationTokenSource.Token;
            _chatView!.ChatState.AddUserMessage(input);
            var reset = _chatView!.ChatState.MessageCount > 1;
            var chatWithPlanner = CoreKernelService.ChatWithStepwisePlanner(input, ChatRequestModel, runAsChat, _askInput, token, reset);
            return ExecuteChatSequence(chatWithPlanner);
        }
        private Task ExecuteHandlebarsChatSequence(string input, bool runAsChat)
        {
            var token = _cancellationTokenSource.Token;
            _chatView!.ChatState.AddUserMessage(input);
            var reset = _chatView!.ChatState.MessageCount > 1;
            var chatWithPlanner = CoreKernelService.ChatWithHandlebarsPlanner(input,
                               ChatRequestModel, runAsChat, _askInput, token, reset);
            return ExecuteChatSequence(chatWithPlanner);
        }

        private async Task ExecuteChatSequence(IAsyncEnumerable<string> chatWithPlanner)
        {
            var hasStarted = false;
            await foreach (var response in chatWithPlanner)
            {
                if (!hasStarted)
                {
                    hasStarted = true;
                    _chatView!.ChatState.AddAssistantMessage(response,
                        _chatView!.ChatState.ChatMessages.Count + 1);
                    _chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!
                        .IsActiveStreaming = true;
                    continue;
                }

                _chatView!.ChatState.UpdateAssistantMessage(response);
            }

            var lastAsstMessage =
                _chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant);
            if (lastAsstMessage is not null)
                lastAsstMessage.IsActiveStreaming = false;
        }
    }

}