using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using SemanticKernelAgentOrchestration.Models;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents
{
    public partial class AgentDisplay : ComponentBase
    {
        [Parameter]
        public AgentProxy? Agent { get; set; }
    }
}
