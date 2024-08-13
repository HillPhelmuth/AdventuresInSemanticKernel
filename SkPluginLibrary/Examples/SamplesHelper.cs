using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Examples;

public static class SamplesHelper
{
	public static Kernel CreateKernelWithChatCompletion()
	{
		var builder = Kernel.CreateBuilder();

		builder.AddOpenAIChatCompletion(
			TestConfiguration.OpenAI.Gpt35ModelId, TestConfiguration.OpenAI.ApiKey);
		
		return builder.Build();
	}
}