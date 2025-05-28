using ChatComponents;
using DocumentFormat.OpenXml.Office.Word;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private bool _popupOpen;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("alignScroll", _overlay, _textArea);
        }
        await base.OnAfterRenderAsync(firstRender);
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
        try
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


            // Wait for 1 second (1000 milliseconds) before executing the request
            await Task.Delay(1000, _debounceCts.Token);

            // Generate the autocomplete suggestion
            //await foreach (var suggestion in LogProbService.GenerateAutoCompleteOptions(_userInput))
            //{
            //    if (_suggestions.Count >= 3) break;
            //    _suggestions.Add(suggestion);
            //    await InvokeAsync(StateHasChanged);
            //}
            _suggestions = await LogProbService.GenerateAutoCompleteOptions(_userInput, 3).Take(3).ToListAsync();
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
        catch (Exception ex)
        {
            // Handle any other exceptions that may occur
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    private ElementReference _textArea;
    private ElementReference _overlay;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;
    private bool _preventDefault;

    private async Task HandleKeyUp()
    {
        if (_preventDefault)
        {
            Console.WriteLine("Preventing default behavior");
            await _textArea.FocusAsync();
            _preventDefault = false; // Reset the flag
        }
    }
    private async Task HandleKeyPress(KeyboardEventArgs args)
    {
        // Only prevent default for handled keys using JS interop
        //await JSRuntime.InvokeVoidAsync("maybePreventDefault", args);
        switch (args.Key)
        {
            //case "Tab":
            case "ArrowRight":
                _preventDefault = true;
                _userInput += _remainingSuggestion;
                StateHasChanged();
                _remainingSuggestion = ""; // Clear the suggestion after accepting
                _suggestions.Clear(); // Clear the suggestion list
                _selectedSuggestionIndex = 0;
                await InvokeAsync(StateHasChanged);
                await Task.Delay(1); // Let the render complete
                
                //await JSRuntime.InvokeVoidAsync("eval", $@"document.getElementById(""textbox0"").focus()");
                _popupOpen = false;
                //await _popup.CloseAsync();
              
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
            //default:
            //    _userInput += args.ShiftKey ? args.Key.ToUpper() : args.Key.ToLower();
            //    StateHasChanged();
            //    await Task.Delay(1); // Let the render complete
            //    break;
        }
        await InvokeAsync(StateHasChanged);
    }
}