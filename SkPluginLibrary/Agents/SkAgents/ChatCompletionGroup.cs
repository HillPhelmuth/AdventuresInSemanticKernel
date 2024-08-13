﻿using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.Graph;


namespace SkPluginLibrary.Agents.SkAgents;

public class ChatCompletionGroup
{
	public event Action<string>? MessageSent;
	private void WriteLine(string output)
	{
		MessageSent?.Invoke(output);
	}
	private const string ReviewerName = "ArtDirector";
	private const string ReviewerInstructions =
		"""
        You are an art director who has opinions about copywriting born of a love for David Ogilvy.
        The goal is to determine is the given copy is acceptable to print.
        If so, state that it is approved.
        If not, provide insight on how to refine suggested copy without example.
        """;

	private const string CopyWriterName = "Writer";
	private const string CopyWriterInstructions =
		"""
        You are a copywriter with ten years of experience and are known for brevity and a dry humor.
        You're laser focused on the goal at hand. Don't waste time with chit chat.
        The goal is to refine and decide on the single best copy as an expert in the field.
        Consider suggestions when refining an idea.
        """;

	
	public async Task RunAsync()
	{
		// Define the agents
		ChatCompletionAgent agentReviewer =
			new()
			{
				Instructions = ReviewerInstructions,
				Name = ReviewerName,
				Kernel = CreateKernelWithChatCompletion(),
			};

		ChatCompletionAgent agentWriter =
			new()
			{
				Instructions = CopyWriterInstructions,
				Name = CopyWriterName,
				Kernel = CreateKernelWithChatCompletion(),
			};

		// Create a chat for agent interaction.
		var chat =
			new AgentGroupChat(agentWriter, agentReviewer)
			{
				ExecutionSettings =
					new()
					{
						// Here a TerminationStrategy subclass is used that will terminate when
						// an assistant message contains the term "approve".
						TerminationStrategy =
							new ApprovalTerminationStrategy()
							{
								// Only the art-director may approve.
								Agents = [agentReviewer],
								// Limit total number of turns
								MaximumIterations = 10,
							}
					}
			};

		// Invoke chat and display messages.
		string input = "concept: maps made out of egg cartons.";
		chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
		WriteLine($"# {AuthorRole.User}: '{input}'");

		await foreach (var content in chat.InvokeAsync())
		{
			WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
		}

		WriteLine($"# IS COMPLETE: {chat.IsComplete}");
	}
	protected Kernel CreateKernelWithChatCompletion()
	{
		return CoreKernelService.CreateKernel();
	}
	
}
internal sealed class ApprovalTerminationStrategy : TerminationStrategy
{
	// Terminate when the final message contains the term "approve"
	protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
		=> Task.FromResult(history[^1].Content?.Contains("approve", StringComparison.OrdinalIgnoreCase) ?? false);
}
public class HubAndSpokeStrategy : SelectionStrategy
{
	private int _index = 1;
	public override Task<Agent> NextAsync(IReadOnlyList<Agent> agents, IReadOnlyList<ChatMessageContent> history,
		CancellationToken cancellationToken = default)
	{
		var admin = agents[0];
		if (history.Count == 0) return Task.FromResult(admin);
		
		var lastAgent = agents.FirstOrDefault(x => x.Name == history[^1].AuthorName);
		if (lastAgent?.Name != admin.Name) return Task.FromResult(admin);
		var agent = agents[_index];

		_index = (_index + 1) % agents.Count;

		return Task.FromResult(agent);

	}
}
public class PromptSelectionStrategy : SelectionStrategy
{
	private const string NextAgentPromptTemplate = """
	                                               You are in a role play game. Carefully read the conversation history and carry on the conversation, always starting with 'From {name}:'.
	                                               The available roles are:
	                                               - {{$speakerList}}

	                                               ### Conversation history

	                                               - {{$conversationHistory}}

	                                               Each message MUST start with 'From name:', e.g:
	                                               From admin:
	                                               //your message//.
	                                               """;
	public override async Task<Agent> NextAsync(IReadOnlyList<Agent> agents, IReadOnlyList<ChatMessageContent> history,
		CancellationToken ct = default)
	{
		var settings = new OpenAIPromptExecutionSettings
		{

			Temperature = 0.0,
			MaxTokens = 128,
			StopSequences = [":"],
		};
		var kernelArgs = UpdateKernelArguments(history, agents, settings);
		var promptFactory = new KernelPromptTemplateFactory();
		var templateConfig = new PromptTemplateConfig(NextAgentPromptTemplate);
		var adminAgent = agents[0] as ChatCompletionAgent;
		var prompt = await promptFactory.Create(templateConfig).RenderAsync(adminAgent?.Kernel!, kernelArgs, ct);
		var chat = adminAgent!.Kernel.GetRequiredService<IChatCompletionService>();
		var chatHistory = new ChatHistory(prompt);
		try
		{
			var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: ct);
			var name = nextAgentName!.ToString()[5..];
			Console.WriteLine("AutoSelectNextAgent: " + name);
			var nextAgent = agents.FirstOrDefault(interactive => interactive.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? adminAgent;
			return nextAgent;
		}
		catch (TaskCanceledException exception)
		{
			Console.WriteLine(exception.Message);
			return adminAgent;
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			throw;
		}
	}
	private static KernelArguments UpdateKernelArguments(IReadOnlyList<ChatMessageContent> history, IReadOnlyList<Agent> agents, OpenAIPromptExecutionSettings settings)
	{
		var groupConvoHistory = string.Join("\n ", history?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
		var kernelArgs = new KernelArguments(settings)
		{
			["speakerList"] = string.Join("\n ", agents.Select(a => $"### Name\n{a?.Name}\n### Description\n {a?.Description}\n")),
			["conversationHistory"] = groupConvoHistory
		};
		return kernelArgs;
	}
}