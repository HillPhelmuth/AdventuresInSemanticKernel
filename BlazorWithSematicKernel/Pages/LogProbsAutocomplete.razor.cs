using ChatComponents;
using DocumentFormat.OpenXml.Office.Word;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using SkPluginLibrary.Abstractions;

namespace BlazorWithSematicKernel.Pages;

public partial class LogProbsAutocomplete
{
    [Inject] private ITokenization LogProbService { get; set; } = default!;

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

    private string _userInput = "";
    private string _suggestion = "";
    private string _remainingSuggestion = "";

    private CancellationTokenSource _debounceCts = new();

    private List<string> _suggestions = [];

    private int _selectedSuggestionIndex = 0;

    // Update autocomplete as user types
    private async void UpdateAutocomplete(ChangeEventArgs e)
    {
        var previousSuggestion = _remainingSuggestion;
        _userInput = e.Value.ToString();
        if (!string.IsNullOrEmpty(previousSuggestion) &&
            !previousSuggestion.StartsWith(_userInput[previousSuggestion.Length..], StringComparison.OrdinalIgnoreCase))
        {
            _remainingSuggestion = ""; // Clear the suggestion
        }
        // Cancel the previous debounce task if it's still running
        await _debounceCts.CancelAsync();
        _debounceCts = new CancellationTokenSource();

        try
        {
            // Wait for 1 second (1000 milliseconds) before executing the request
            await Task.Delay(1500, _debounceCts.Token);

            // Generate the autocomplete suggestion
            _suggestions = await LogProbService.GenerateAutoCompleteOptions(_userInput).Take(3).ToListAsync();
            _selectedSuggestionIndex = 0;
            _suggestion = _suggestions[_selectedSuggestionIndex];
            await InvokeAsync(StateHasChanged);
            if (!string.IsNullOrEmpty(_suggestion))
            {
                _remainingSuggestion = _suggestion; // Show the full suggestion from the service
            }
            else
            {
                _remainingSuggestion = ""; // Clear suggestion if nothing is returned
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException)
        {
            // If the task was canceled, ignore and return (user kept typing)
        }
    }
    ElementReference _textArea2;
    [Inject]
    private IJSRuntime JSRuntime { get; set; }
    private async Task HandleKeyPress(KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "Tab" or "ArrowUp" or "ArrowDown")
        {
            await JSRuntime.InvokeVoidAsync("window.event.preventDefault");
        }
        switch (args.Key)
        {
            case "Tab":
            case "Enter":
                _userInput += _remainingSuggestion;
                _remainingSuggestion = ""; // Clear the suggestion after accepting
                _suggestions.Clear(); // Clear the suggestion list
                _selectedSuggestionIndex = 0;
                StateHasChanged();
                await _textArea2.FocusAsync();
                return;
            case "Escape":
                _remainingSuggestion = ""; // Clear the suggestion
                _suggestions.Clear(); // Clear the suggestion list
                _selectedSuggestionIndex = 0;
                StateHasChanged();
                return;
            case "ArrowUp" when _selectedSuggestionIndex == 0:
                return;
            case "ArrowUp":
                _selectedSuggestionIndex--;
                _suggestion = _suggestions[_selectedSuggestionIndex];
                _remainingSuggestion = _suggestion;
                // _remainingSuggestion = _suggestion.Substring(_userInput.Length);
                break;
            case "ArrowDown" when _selectedSuggestionIndex == _suggestions.Count - 1:
                return;
            case "ArrowDown":
                _selectedSuggestionIndex++;
                _suggestion = _suggestions[_selectedSuggestionIndex];
                _remainingSuggestion = _suggestion;
                // _remainingSuggestion = _suggestion.Substring(_userInput.Length);
                break;
        }
        await InvokeAsync(StateHasChanged);
    }
}