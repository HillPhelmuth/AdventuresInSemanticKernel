using Markdig;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentToolComponents.Components
{
    public partial class SimpleOutput : ComponentBase
    {
        [Inject] private Models.AskUserService ModalService { get; set; } = default!;
        [Parameter]
        public string OutputText { get; set; } = string.Empty;
        private string AsHtml(string? text)
        {
            if (text == null) return "";
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
            var result = Markdown.ToHtml(text, pipeline);
            return result;

        }
    }
}
