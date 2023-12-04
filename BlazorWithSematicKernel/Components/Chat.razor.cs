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
            if (_chatView!.ChatState.ChatMessages.LastOrDefault(x => x.Role == Role.Assistant)!
                .IsActiveStreaming)
            {
                _chatView!.ChatState.UpdateAssistantMessage(text);
            }
            else
                _chatView!.ChatState.AddAssistantMessage(text, _chatView!.ChatState.ChatMessages.Count + 1);
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
                    case ExecutionType.ActionPlanner:
                        {
                            await ExecuteActionChatSequence(input, false);
                            break;
                        }
                    case ExecutionType.ActionPlannerChat:
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
                    case ExecutionType.ChainFunctions:
                        {
                            var funcs = ChatRequestModel.SelectedFunctions;
                            var funcFlow = string.Join("-->", funcs.Select(x => x.Name));
                            var message = $"### Function Chain:\n\n{funcFlow}\n\nStarting input: {input}";
                            _chatView!.ChatState.AddUserMessage(message, _chatView!.ChatState.ChatMessages.Count + 1);
                            if (ChatRequestModel.Variables != null)
                                ChatRequestModel.Variables["input"] = input;
                            else
                                ChatRequestModel.Variables = new Dictionary<string, string> { { "input", input } };
                            var result = await CoreKernelService.ExecuteFunctionChain(ChatRequestModel);
                            _chatView!.ChatState.AddAssistantMessage(result, _chatView!.ChatState.ChatMessages.Count + 1);
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

        private async Task ExecuteActionChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);
            var chatWithActionPlanner = CoreKernelService.ChatWithActionPlanner(input,
                ChatRequestModel, runAsChat, _askInput);
            await ExecuteChatSequence(chatWithActionPlanner);            
        }

        private async Task ExecuteSequentialChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);
            var chatWithPlanner = CoreKernelService.ChatWithSequentialPlanner(input,
                ChatRequestModel, runAsChat, _askInput);
            await ExecuteChatSequence(chatWithPlanner);            
        }

        private async Task ExecuteStepwiseChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);

            var chatWithPlanner = CoreKernelService.ChatWithStepwisePlanner(input,
                ChatRequestModel, runAsChat, _askInput);
            await ExecuteChatSequence(chatWithPlanner);
        }
        private async Task ExecuteHandlebarsChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);
            var chatWithPlanner = CoreKernelService.ChatWithHandlebarsPlanner(input,
                               ChatRequestModel, runAsChat, _askInput);
            await ExecuteChatSequence(chatWithPlanner);
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