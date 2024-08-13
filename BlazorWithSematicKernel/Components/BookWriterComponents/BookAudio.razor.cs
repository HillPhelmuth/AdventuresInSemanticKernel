using BlazorWithSematicKernel.Services;
using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWithSematicKernel.Components.BookWriterComponents;

public partial class BookAudio : ComponentBase
{
	[Inject] private IJSRuntime JsRuntime { get; set; } = default!;
	private AudioService AudioService => new(JsRuntime);
	[Inject]
	private ICustomNativePlugins CustomNativePlugins { get; set; } = default!;
	[Parameter]
	public string TextToAudio { get; set; } = string.Empty;
	[Parameter]
	public string? Title { get; set; }
	private ElementReference _ref;

	private double _progress;
	private bool _isAudioStarted;
	private bool _isAudioPlaying;
	private bool _isStreamingComplete;
	private bool _hasStarted;
	private string _elapsed = "00:00:00";
	private string _duration = "00:00:00";
	[Parameter]
	public string? BookAudioId { get; set; }
	private string? AudioElementId => $"audioplayer-{BookAudioId}";

	protected override async Task OnParametersSetAsync()
	{
		if (!string.IsNullOrEmpty(TextToAudio) && !_isAudioStarted && !string.IsNullOrEmpty(AudioElementId))
			await StartStreaming();
		await base.OnParametersSetAsync();
	}
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender && !_isAudioStarted && !string.IsNullOrEmpty(AudioElementId))
			await StartStreaming();
		await base.OnAfterRenderAsync(firstRender);
	}
	private async Task StartStreaming()
	{
		_isAudioStarted = true;
		StateHasChanged();
		await Task.Delay(1);
		var totalBase64 = "";
		await foreach (var audioChunk in CustomNativePlugins.TextToAudioAsync(TextToAudio))
		{
			//if (!_hasStarted)
			//{
			//	try
			//	{
			//		await AudioService.Init(BookAudioId);
			//	}
			//	catch
			//	{

			//		Console.WriteLine($"Error on AppendBuffer\n\nID: {BookAudioId}\n\nChapter Name: {TextToAudio.Split("\n")[0]}");
			//		throw;
			//	}
			//	_hasStarted = true;
			//	StateHasChanged();
			//}
			Console.WriteLine($"Audio out provided, {audioChunk.GetValueOrDefault().Length} bytes");
			var chuckData = audioChunk.GetValueOrDefault().ToArray();
			var chuckDataBase64 = Convert.ToBase64String(chuckData);
			totalBase64 += chuckDataBase64;
			
			//try
			//{
			//	//await AudioService.AppendBuffer(chuckDataBase64);
			//}
			//catch (Exception ex)
			//{
			//	Console.WriteLine($"Error on AppendBuffer\n\nID: {BookAudioId}\n\nChapter Name: {TextToAudio.Split("\n")[0]}");
			//	throw;
			//}
		}
		
		string audioUrl = $"data:audio/mpeg;base64,{totalBase64}";
		await AudioService.Init(AudioElementId, audioUrl);
		await Task.Delay(1000);
		_hasStarted = true;
		//await AudioService.EndOfStream();
		_isStreamingComplete = true;
		StateHasChanged();

	}
	private async void TickTimer()
	{
		while (_isAudioStarted)
		{
			await Task.Delay(1000);
			if (!_isAudioPlaying) return;
			var progress = await AudioService.GetProgress(AudioElementId);
			_progress = progress;
			var elapse = await AudioService.GetCurrentTime(AudioElementId);
			var duration = await AudioService.GetDuration(AudioElementId);
			Console.WriteLine($"Ticked: {progress}, Elapsed: {elapse},Duration: {duration}");
			_elapsed = ConvertSecondsToTimeString(elapse);
			_duration = ConvertSecondsToTimeString(duration);
			if (duration - elapse < 1.1) return;
			StateHasChanged();
		}
	}
	private async Task TogglePlayPause(bool isPlaying)
	{
		Console.WriteLine($"Toggling Play/Pause: {isPlaying}\nID: {AudioElementId}\n\nChapter Name: {TextToAudio.Split("\n")[0]}");
		if (isPlaying) await PlayAudio();
		else await PauseAudio();
	}
	private async Task PlayAudio()
	{
		
		//_isAudioPlaying = true;
		await AudioService.Play(AudioElementId);
		TickTimer();
	}

	private async Task PauseAudio()
	{
		//_isAudioPlaying = false;
		await AudioService.Pause(AudioElementId);
	}
	private async Task ChangeProgress(double value)
	{
		_progress = value;
		StateHasChanged();
		await AudioService.ChangeProgress(value, AudioElementId);
	}
	private static string ConvertSecondsToTimeString(double seconds)
	{
		var time = TimeSpan.FromSeconds(seconds);
		return time.ToString(@"hh\:mm\:ss");
	}
}