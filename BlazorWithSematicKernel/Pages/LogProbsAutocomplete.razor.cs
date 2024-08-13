using DocumentFormat.OpenXml.Office.Word;
using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages;

public partial class LogProbsAutocomplete
{
	[Inject]
	private ITokenization LogProbService { get; set; } = default!;

	private record AutocompleteOption(string Text);
	
	private List<AutocompleteOption> _options = [];
	private string _text = "";
	private Popup _popup;
	private RadzenTextArea _textArea;
	private bool _popupOpen;
	private async Task HandleInput()
	{
		_options.Clear();
		await foreach (var option in LogProbService.GenerateAutoCompleteOptions(_text))
		{
			_options.Add(new AutocompleteOption(option));
			if (!_popupOpen)
			{
				await _popup.ToggleAsync(_textArea.Element);
				_popupOpen = true;
			}
			await InvokeAsync(StateHasChanged);
		}
	}
	private async Task HandleSelect(AutocompleteOption option)
	{
		_text += option.Text;
		_popupOpen = false;
		await _popup.CloseAsync();
		await InvokeAsync(StateHasChanged);
	}
}