using BlazorAceEditor.Models;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorWithSematicKernel.Pages
{
    public partial class KernelSamples : ComponentBase
    {
        [Inject]
        private ISemanticKernelSamples CoreKernelService { get; set; } = default!;
        [Inject]
        private CompilerService CompilerService { get; set; } = default!;
        [Inject] private AceEditorJsInterop AceEditorJsInterop { get; set; } = default!;

        private AceEditor _editor;

        private readonly AceEditorOptions _aceEditorOptions = new()
        {
            ShowLineNumbers = true,
            Mode = "csharp",
            VScrollBarAlwaysVisible = true,
            Theme = "terminal"
        };
        private static readonly string CodeFilesPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory(), "Data", "CodeFiles");
        private List<CodeFile> _exampleCodeFiles = [];

        private class CodeFile
        {
            public string? Name { get; init; }
            public string? Code { get; init; }
        }
        private string _output = string.Empty;
        private bool _isBusy;
        private bool _compileAndRun;
        private class SelectServiceForm
        {
            [Required]
            public CodeFile? SelectedCodeFile { get; set; }

        }
        private SelectServiceForm _selectServiceForm = new();

        protected override Task OnInitializedAsync()
        {
            _exampleCodeFiles = Directory.GetFiles(CodeFilesPath).Select(x => new CodeFile { Name = Path.GetFileNameWithoutExtension(x), Code = File.ReadAllText(x) }).ToList();
            CoreKernelService.StringWritten += HandleString;
            return base.OnInitializedAsync();
        }
        private async void HandleInit(AceEditor editor)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(200);
            _isBusy = false;
            StateHasChanged();
        }
        private void HandleString(object? sender, string e)
        {
            var str = e;
            var outputStr = $"\n{str}\n";

            _output += outputStr;
            InvokeAsync(StateHasChanged);
        }

        private async void Submit(SelectServiceForm form)
        {
            var code = CodeSampleTemplate.GetCodeFromTemplate(form.SelectedCodeFile.Name, form.SelectedCodeFile.Code);
            await _editor.SetValue(code);
            StateHasChanged();
        }
        private async void Execute(bool compileAndRun)
        {
            _isBusy = true;
            StateHasChanged();
            await Task.Delay(1);
            if (compileAndRun)
            {
                var code = await _editor.GetValue();
                var refs = CompileResources.PortableExecutableReferences;
                var executableCode = $"{CodeSampleTemplate.DisableWarning}\n\n{CodeSampleTemplate.UsingStatements}\n\n{code}";
                _output = await CompilerService.SubmitCode(executableCode, refs);
            }
            else
            {
                await CoreKernelService.RunExample(_selectServiceForm.SelectedCodeFile!.Name!);
            }
            _isBusy = false;
            StateHasChanged();
        }

    }
}
