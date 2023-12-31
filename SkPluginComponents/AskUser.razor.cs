using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SkPluginComponents.Models;
using System.Diagnostics;
using Markdig;

namespace SkPluginComponents
{
    public partial class AskUser
    {
        [Parameter]
        public bool IsOpen { get; set; }
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }
        [Parameter]
        public Location ModalLocation { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }
        [Inject]
        private AskUserService AskUserService { get; set; } = default!;
        private Type? ComponentType { get; set; }
        private AskUserParameters? AskUserParameters { get; set; }
        private AskUserWindowOptions AskUserOptions { get; set; } = new();

        public void Reset()
        {
            ChildContent = null;
            ComponentType = null;
        }
        protected override Task OnInitializedAsync()
        {
            Debug.WriteLine("Modal Initialized");
            AskUserService.OnOpenComponent += Open;
            AskUserService.OnOpenFragment += OpenFragment;
            AskUserService.OnModalClose += Close;
            return base.OnInitializedAsync();
        }

        private void OpenFragment(RenderFragment<AskUserService> childContent, AskUserParameters? parameters = null,
            AskUserWindowOptions? options = null)
        {
            Console.WriteLine("ModalService OnOpenFragment handled in Modal.razor");
            Reset();
            ChildContent = childContent.Invoke(AskUserService);
            AskUserParameters = parameters;
            AskUserOptions = options ?? new AskUserWindowOptions();
            IsOpen = true;
            InvokeAsync(StateHasChanged);
        }
        private void Open(Type type, AskUserParameters? parameters = null, AskUserWindowOptions? options = null)
        {
            Console.WriteLine("ModalService OnOpenComponent handled in Modal.razor");
            Reset();
            ComponentType = type;
            AskUserParameters = parameters;
            AskUserOptions = options ?? new AskUserWindowOptions();
            IsOpen = true;
            InvokeAsync(StateHasChanged);
        }

        private void Close(AskUserResults? results = null)
        {
            Console.WriteLine("ModalService OnClose handled in Modal.razor");
            IsOpen = false;
            InvokeAsync(StateHasChanged);
        }
        private void CloseSelf()
        {
            AskUserService.CloseSelf();
        }
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
        
        private void OutClick()
        {
            if (AskUserOptions.CloseOnOuterClick)
            {
                CloseSelf();
            }
        }

        private void Close()
        {
            IsOpen = false;
            IsOpenChanged.InvokeAsync(IsOpen);
        }
    }
}
