using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Components
{
    public partial class Chat : ComponentBase
    {
        private ChatView? _chatView;

        [Parameter] public ChatRequestModel ChatRequestModel { get; set; } = new();
        [Inject] private ICoreKernelExecution CoreKernelService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _chatView!.ChatState.Reset();
            }
            return base.OnAfterRenderAsync(firstRender);
        }

        private bool _isBusy;
        private async void HandleInput(string input)
        {

            Console.WriteLine($"ChatRequestModel Received:\n{ChatRequestModel}");

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
                    case ExecutionType.ActionPlannerWithChat:
                    {
                        await ExecuteActionChatSequence(input, true);
                        break;
                    }
                    case ExecutionType.SequentialPlanner:
                    {
                        await ExecuteSequentialChatSequence(input, false);
                        break;
                    }
                    case ExecutionType.SequentialPlannerWithChat:
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
                            ChatRequestModel.Variables = new Dictionary<string, string> {{"input", input}};
                        var result = await CoreKernelService.ExecuteFunctionChain(ChatRequestModel);
                        _chatView!.ChatState.AddAssistantMessage(result, _chatView!.ChatState.ChatMessages.Count + 1);
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
            StateHasChanged();
        }

        private async Task ExecuteActionChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);
            var hasStarted = false;
            await foreach (var response in CoreKernelService.ChatWithActionPlanner(input,
                               ChatRequestModel, runAsChat))
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

        private async Task ExecuteSequentialChatSequence(string input, bool runAsChat)
        {
            _chatView!.ChatState.AddUserMessage(input, _chatView!.ChatState.ChatMessages.Count + 1);
            var hasStarted= false;
            await foreach (var response in CoreKernelService.ChatWithSequentialPlanner(input,
                               ChatRequestModel, runAsChat))
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