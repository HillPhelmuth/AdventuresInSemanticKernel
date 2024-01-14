using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents
{
    public partial class ViewFunctions : ComponentBase
    {
        [Parameter]
        public KernelPlugin? Plugin { get; set; }
    }
}
