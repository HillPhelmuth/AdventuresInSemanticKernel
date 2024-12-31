using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.MistralAI;

namespace SkPluginLibrary.Plugins;

public class NovelWriterPlugin(AIModel aIModel = AIModel.Gpt4OCurrent)
{
	private AIModel _aIModel = aIModel;

	private const string ChapterWriterPrompt =
"""
You are a creative and exciting fiction writer. You write intelligent and engrossing novels that expertly combine character development and growth, interpersonal and international intrigue, and thrilling action.
        
## Instructions
         
1. Write only 1 chapter based on the Chapter Outline.
        
2. You will be provided with the exact text of the Previous Chapter, if available. You must maintain consistency and continuity with the previous chapter content and tone.
        
3. You will be provided with a summary of the novel's events from before the previous chapter. You must maintain consistency with the novel's previous events and developments.
        
4. Use the Story Description and Characters to inform your writing.

5. Unless otherwise indicated in the Story Description, you should write at a level suitable for a general audience.

6. Unless otherwise indicated, avoid explicit language, graphic violence, and sexual content. However, if the Story Description or Outline includes such elements, you should include them in your writing. You are also enouraged to use them if the Story Description clearly indicates an adult theme.
        
7. The chapter must be at least 90 paragraphs long and no more than 100 paragraphs.

8. The chaper should start with the chapter name and number in the format: `## Chapter 1: {first chapter name}.`
        
## Story Description and Characters
        
{{ $description }}
        
## Summary of Novel so far
        
{{ $summary }} 
        
## Previous Chapter Text

{{ $previous_chapter }}

## Chapter Outline

```        
{{ $chapter_outline }}
```

## Objectives

- Write a compelling chapter for the novel based on the critera above. 
- It must be 80-100 paragraphs

Now, take a deep breath and start writing.
""";
	private const string OutlineWriterPrompt =
"""
# Objective

Write a novel outline about the theme specified in Theme or Topic. Include the characters provided in Character Details and the plot events in Plot Events.
Since novel itself will be written by a different writer, you must include all pertinent information in each chapter outline.
		
# Story Details

## Theme or Topic

{{ $theme }}

## Character Details

{{ $characterDetails }}

## Plot Events

{{ $plotEvents }}

# Instructions
		
- The outline must be {{ $chapterCount }} chapters long.
- Use markdown format for the outline.
- User header level 2 for each chapter (##)
- Use smaller headers for sub-sections.
- Provide a brief paragraph for each chapter, summarizing the main events and character developments.
- Include sub-sections for Setting, Character Development, Key Events, and Chapter Conclusion.
- Ensure each chapter's outline does not exceed 400 words.
		
# Outline of a Chapter
		
## Chapter 1: {Name of Chapter}
		
- **Setting**: {Introduce the setting where the primary events occur.}
- **Character Development**: {Summarize the character development occurring in this chapter}
- **Key Events**: {List and briefly describe the key events of the chapter}
- **Chapter Conclusion**: {Sum up the chapter's impact on the overall narrative and set up the next chapter}
""";
	[KernelFunction, Description("Write a chapter of a novel based on the provided outline, previous chapter, and summary of the full novel so far")]
	public IAsyncEnumerable<string> WriteChapterStreaming([Description("Chapter Outline")] string outline, [Description("Story description and characters")] string storyDescription, [Description("Precise text of the previous chapter")] string previousChapter, [Description("Combined summary of each chapter so far")] string summary)
	{
		var settings = GetPromptExecutionSettingsFromModel(_aIModel);
		var args = new KernelArguments(settings)
		{
			["chapter_outline"] = outline,
			["description"] = storyDescription,
			["previous_chapter"] = previousChapter,
			["summary"] = summary
		};
		var kernel = CoreKernelService.CreateKernel(_aIModel);
		return kernel.InvokePromptStreamingAsync<string>(ChapterWriterPrompt, args);
		
	}
	[KernelFunction, Description("Write a chapter of a novel based on the provided outline, previous chapter, and summary of the full novel so far")]
	public async Task<string> WriteChapter(Kernel kernelIn,[Description("Chapter Outline")] string outline, [Description("Story description and characters")] string storyDescription, [Description("Precise text of the previous chapter")] string previousChapter, [Description("Combined summary of each chapter so far")] string summary)
	{
		var settings = GetPromptExecutionSettingsFromModel(_aIModel);
		var args = new KernelArguments(settings)
		{
			["chapter_outline"] = outline,
			["description"] = storyDescription,
			["previous_chapter"] = previousChapter,
			["summary"] = summary
		};
		var kernel = kernelIn.Clone();
		//return await kernel.InvokePromptAsync<string>(ChapterWriterPrompt, args);
		var response = await kernel.InvokePromptAsync<string>(ChapterWriterPrompt, args);
		return response;
	}
	private PromptExecutionSettings GetPromptExecutionSettingsFromModel(AIModel model)
	{
		var providor = model.GetModelProvidors().FirstOrDefault();
		return providor switch
		{
			"GoogleAI" => new GeminiPromptExecutionSettings {MaxTokens = 4096},
			"MistralAI" => new MistralAIPromptExecutionSettings {MaxTokens = 4096},
			"OpenAI" or "AzureOpenAI" => new OpenAIPromptExecutionSettings {MaxTokens = 4096, ChatSystemPrompt = "You are a creative and exciting fiction writer. You write intelligent and engrossing novels that expertly combine character development and growth, interpersonal and international intrigue, and thrilling action. You write 1 chapter at a time and eac chapter is **90 paragraphs**." },
			_ => new OpenAIPromptExecutionSettings {MaxTokens = 4096}
		};
	}
	[KernelFunction, Description("Summarize all the character details and plot events in the novel chapter")]
	public async Task<string> SummarizeChapter([Description("Chapter text to summarize")] string chapterText)
	{
		var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1028 };
		var args = new KernelArguments(settings)
		{
			["novel_chapter"] = chapterText
		};
		var kernel = CoreKernelService.CreateKernel();
		var response = await kernel.InvokePromptAsync<string>("Summarize all the character details and plot events in the novel chapter. Summarize chapter by chapter:```\n {{$novel_chapter}} \n ```", args);
		return response;
	}
	[KernelFunction, Description("Create an outline for a novel")]
	public async Task<string> CreateNovelOutline([Description("The central topic or theme of the story")] string theme, [Description("A list of character details that must be included in the outline")] string characterDetails = "", [Description("Plot events that must occurr in the outline")] string plotEvents = "", [Description("The title of the novel")] string novelTitle = "", [Description("Number of chapters to include")] int chapters = 15)
	{
		
		var settings = new OpenAIPromptExecutionSettings { MaxTokens = 4096, Temperature = 0.85 };
        if (_aIModel == AIModel.O1SeriesMini)
            settings.Temperature = null;
		var args = new KernelArguments(settings)
		{
			["theme"] = theme,
			["characterDetails"] = characterDetails,
			["plotEvents"] = plotEvents,
			["novelTitle"] = novelTitle,
			["chapterCount"] = chapters
		};
		var kernel = CoreKernelService.CreateKernel(_aIModel);
		var response = await kernel.InvokePromptAsync<string>(OutlineWriterPrompt, args);
		return response;
	}
	
}
