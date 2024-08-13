using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BlazorWithSematicKernel.Pages;

public partial class AgentGroupChatPage : ComponentBase
{
	[Inject]
	private ICustomNativePlugins CoreKernelService { get; set; } = default!;
	
	private bool _isBusy;
	private string _output = "";
	//private void Submit(FileUploadData fileUploadData)
	//{
	//	var inputs = JsonSerializer.Deserialize<QnAInput>()
	//}
	private async Task GenerateWebContext()
	{
		_isBusy = true;
		StateHasChanged();
		await Task.Delay(1);
		var inputs = JsonSerializer.Deserialize<List<QnAInput>>(await File.ReadAllTextAsync("qnaInputs.json"));
		var evalInputs = CoreKernelService.GenerateEvalInputsFromWeb(inputs);
		await foreach (var eval in evalInputs)
		{
			_output += $"{JsonSerializer.Serialize(eval)}\n";
			StateHasChanged();
			await Task.Delay(1);
		}
		_isBusy = false;
		StateHasChanged();
		await File.WriteAllTextAsync("evalInputs.json", _output);
	}
}