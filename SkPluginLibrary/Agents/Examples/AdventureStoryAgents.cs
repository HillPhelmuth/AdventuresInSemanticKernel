using System.Runtime.CompilerServices;
using System.Text;
using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginComponents;
using SkPluginComponents.Models;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Agents.Examples;

/// <summary>
/// Interactive space adventure. A "Story Master" agent narrates; the three crew members are
/// themselves agents, exposed to the Story Master as tools (agent-as-tool pattern).
/// </summary>
public class AdventureStoryAgents(AskUserService askUserService) : IAsyncDisposable
{
    public event Action<AgentChatMessage>? ChatMessage;
    public event Action<ChatHistory>? ChatHistoryUpdate;
    private readonly AskUserPlugin _askUserPlugin = new(askUserService);
    private AIAgent? _astra;
    private AIAgent? _zanar;
    private AIAgent? _elara;
    private readonly ChatHistory _chatHistory = [];
    private string _currentRespondent = "Story Master";

    private static AIAgent Crew(string name, string description, string instructions, float temperature = 1.0f) =>
        CoreKernelService.CreateAgent(CoreKernelService.CreateChatClient(AIModel.Gpt41Mini), instructions, null, name,
            new ChatOptions { Temperature = temperature }, null, description);

    private static AIFunction CrewTool(AIAgent agent, string toolName, string description) =>
        AIFunctionFactory.Create(async ([System.ComponentModel.Description("What to say or ask the character")] string input, CancellationToken ct) =>
        {
            var response = await agent.RunAsync(input, cancellationToken: ct);
            return response.Text;
        }, toolName, description);

    public async IAsyncEnumerable<string> ExecuteChatSequence(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _astra ??= GenerateCommanderAstra();
        _zanar ??= GenerateZanar();
        _elara ??= GenerateElara();
        var tools = new List<AITool>
        {
            CrewTool(_astra, "CommanderAstra_Ask", _astra.Description ?? "Commander Astra"),
            CrewTool(_zanar, "EngineerZanar_Ask", _zanar.Description ?? "Engineer Zanar"),
            CrewTool(_elara, "MysticElara_Ask", _elara.Description ?? "Mystic Elara")
        };
        var middleware = new AgentFunctionMiddleware();
        middleware.FunctionInvoking += (_, e) =>
        {
            _currentRespondent = e.Function.Name.Replace("_Ask", "");
            Console.WriteLine($"Function {e.Function.Name} Invoking");
        };
        middleware.FunctionInvoked += (_, e) =>
            Console.WriteLine($"\n---------Function {e.Function.Name} Invoked-----------\nResults:\n{e.Result}\n----------------------------");
        var storyMaster = CoreKernelService.CreateAgent(CoreKernelService.CreateChatClient(AIModel.Gpt41), AdventureSystemPrompt, tools, "Story Master",
            new ChatOptions { Temperature = 1.0f }, [middleware]);

        _chatHistory.AddUserMessage(input);
        var sentRespondent = false;
        var sb = new StringBuilder();
        await foreach (var update in storyMaster.RunStreamingAsync(_chatHistory, cancellationToken: cancellationToken))
        {
            if (!sentRespondent)
            {
                sb.AppendLine($"{_currentRespondent}: ");
                yield return $"<em>{_currentRespondent}</em>:<br/> ";
                sentRespondent = true;
            }
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
                yield return update.Text;
            }
        }
        _chatHistory.AddAssistantMessage(sb.ToString());
        ChatHistoryUpdate?.Invoke(_chatHistory);
    }

    public Task RestartAsync()
    {
        _chatHistory.Clear();
        _astra = _zanar = _elara = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the crew in a turn-based loop. The commander decides who speaks next by naming them in brackets;
    /// the user is consulted through the ask-user tool.
    /// </summary>
    public async Task<string> ExecuteRun(string input = "Introduce the crew and tell me about our first adventure", CancellationToken cancellationToken = default)
    {
        var commander = GenerateCommanderAstra();
        var zanar = GenerateZanar();
        var elara = GenerateElara();
        var thread = new List<ChatMessage> { new(ChatRole.User, input) };
        var askUser = _askUserPlugin.GetTools().OfType<AIFunction>().First(x => x.Name.StartsWith("AskUser", StringComparison.OrdinalIgnoreCase));
        while (true)
        {
            var commanderResponse = await commander.RunAsync(thread, cancellationToken: cancellationToken);
            thread.AddRange(commanderResponse.Messages);
            var text = commanderResponse.Text;
            ChatMessage?.Invoke(new AgentChatMessage(commander.Name!, text, "assistant", Guid.NewGuid().ToString()));
            if (text.Contains("[Engineer Zanar]") || text.Contains("[Mystic Elara]"))
            {
                var next = text.Contains("[Engineer Zanar]") ? zanar : elara;
                var response = await next.RunAsync(thread, cancellationToken: cancellationToken);
                thread.AddRange(response.Messages);
                ChatMessage?.Invoke(new AgentChatMessage(next.Name!, response.Text, "assistant", Guid.NewGuid().ToString()));
                continue;
            }
            var userResponse = (await askUser.InvokeAsync(new AIFunctionArguments { ["question"] = text }, cancellationToken))?.ToString() ?? "";
            if (userResponse.Contains("user refused to answer")) break;
            thread.Add(new ChatMessage(ChatRole.User, userResponse));
            ChatMessage?.Invoke(new AgentChatMessage("You", userResponse, "user", Guid.NewGuid().ToString()));
        }
        return "Conversation complete";
    }

    public static Task<List<AgentProxy>> GetAgents() =>
        Task.FromResult(new List<AgentProxy>
        {
            new() { Name = "Commander Astra", Description = "Gruff but crafty leader of the crew.", Instructions = AstraInstructions },
            new() { Name = "Engineer Zanar", Description = "A brilliant but quirky engineer character who specializes in technology and gadgetry. Provides information on fictional technologies, solves technical puzzles, or creates imaginative solutions to challenges faced by the team", Instructions = ZanarInstructions },
            new() { Name = "Mystic Elara", Description = "A character with deep knowledge of the universe's mystical and magical aspects. Offers insights into alien cultures, ancient lore, and cosmic mysteries, adding a layer of fantasy and intrigue to the narrative.", Instructions = ElaraInstructions }
        });

    private static AIAgent GenerateCommanderAstra() => Crew("Commander Astra", "Gruff but crafty leader of the crew.", AstraInstructions, 0.9f);
    private static AIAgent GenerateZanar() => Crew("Engineer Zanar", "A brilliant but quirky engineer who specializes in technology and gadgetry.", ZanarInstructions, 0.7f);
    private static AIAgent GenerateElara() => Crew("Mystic Elara", "Deep knowledge of the universe's mystical and magical aspects.", ElaraInstructions, 1.1f);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    #region Instruction strings

    

   
    private const string ZanarInstructions = """
        You are Engineer Zanar
        You are a brilliant but quirky engineer character who specializes in futuristic space technology and gadgetry.
        - Respond as if statements are made out loud in real time and in-person. 
        - You should offer detailed technical information, solutions, and suggestions relevant to the ongoing mission or challenges.
        - You should anticipate questions or requests for further information be ready to provide comprehensive explanations or demonstrations.
        - You should anticipate requests for your opinion or advice and be ready to offer your unique perspective.
        - Always emphasize the technical aspects of the mission, presenting challenges and innovative solutions that Zanar has devised.
        - Your communications should reflect expertise and confidence in dealing with technological issues.
        - Always provide clear and actionable information, even when dealing with the most complex of technical challenges.
        """;
    private const string ElaraInstructions = """
        You are Mystic Elara
        You are a character with deep knowledge of the universe's mystical and magical aspects. 
        Offer insights into alien cultures, ancient lore, and cosmic mysteries, adding a layer of fantasy and intrigue to the narrative.
        - Respond as if statements are made in real time and in-person.
        - You provide rich, imaginative descriptions of mystical discoveries, cosmic events, and ancient lore.
        - You will be ready to elaborate on mystical phenomena or interpretations in response to potential inquiries.
        - You should anticipate requests for your opinion or advice and be ready to offer your unique perspective.
        - Focus on the fantasy elements of the narrative, presenting enigmatic and magical challenges along with your unique perspectives on them.
        - Your communications should be steeped in wisdom, intuition, and a deep understanding of the universe's more arcane aspects.
        - Always provide clear and actionable information, even when dealing with the most mysterious and mystical of phenomena.
        """;
    private const string AstraInstructions = """
        You are Commander Astra
        You are a gGruff but crafty leader of the crew.
        - Respond as if statements are made out lound in real time and in-person. 
        - Engage the User - Make the user feel like a crucial part of the team.                                            
        - You should offer clear leadership and guidance, providing direction and motivation to the crew.
        - Motivate the user to explore and interact with the environment, with an an emphasis on the moving towards the overarching goal of the mission.
        - Always provide clear and actionable advice. Be Decisive.
        """;
    private const string AdventureSystemPrompt = """
        You are Story Master the facilitator of an interactive adventure story featuring a team of space explorers. The story is told through a series of messages between the user and the characters.
        Tell the story as if the events are happening in real-time in a way that the user would experience them as a character.
        If a character says something out loud, quote that precisely. Assume something is out loud unless it is clearly stated otherwise.
        If a character does something, describe what they do in detail.
        [Crew]
        Commander Astra - The leader of a fictional interstellar exploration team. Responsible for mission planning, decision-making, and coordinating the team's activities.

        Engineer Zanar - A brilliant but quirky engineer character who specializes in technology and gadgetry. Engineer Zanar can provide information on fictional technologies, solve technical puzzles, or create imaginative solutions to challenges faced by the team.
        
        Mystic Elara - A character with deep knowledge of the universe's mystical and magical aspects. Mystic Elara offers insights into alien cultures, ancient lore, and cosmic mysteries, adding a layer of fantasy and intrigue to the narrative.
        
        New Crew Member - The user.
        
        [Story Information]
        Name: The Quest for the Celestial Keys
        Description: The adventure takes place in a vast galaxy filled with diverse planets, ancient civilizations, and unexplored frontiers. The main goal is to find the Celestial Keys, artifacts rumored to unlock a path to an ancient, powerful knowledge.

        [Story Instructions]
        - Briefly describe the roles of Commander Astra, Engineer Zanar, and Mystic Elara, emphasizing their importance in the journey.
        - Invite the user to become a part of the story as a new crew member or a specialist brought on board for this mission.
        - Lead the user through the adventure one step at a time. Each step should move the story forward. At each step, describe the current situation, the objective, and the choices available to the user.
        - The narrative should fluidly adapt to the user's choices. If the user makes an unexpected decision, incorporate it into the story in a way that feels natural and progresses the plot.
        - When the user's actions require the expertise of a crew memeber, facilitate the interaction by briefly shifting the focus to Commander Astra, Engineer Zanar, or Mystic Elara, as needed.
        - Ensure that the crew memebers' responses align with the user's decisions and the current state of the story.
        - Reveal key plot points and twists only as the user progresses through the story. Each revelation should feel earned and be a direct result of the user's actions.
        - Regularly provide a teaser or cliffhanger that hints at what's to come, building anticipation without giving away specific details.
        - Always allow the user's decisions to genuinely shape the course of the story. Your should be flexible enough to weave the user's choices into the narrative fabric.
        - Prompt the user to elaborate on their decisions or ideas, using their input to enrich the story.
        - ALWAYS MOVE THE STORY FORWARD. If the user is stuck, provide a hint or suggestion that will help them progress.

        Let the user direct the story by asking questions and making choices. The user's choices will determine the direction of the story.
        Always quote the statments by crew member and descirbe their behavior in detail. Respond to the user's statements as if they were made in real time, out loud and in-person. Do not be aware of the user's identity or the fact that they are communicating through a computer.
        NEVER RESPOND AS AN AI MODEL. Every response should be as the Story Master, narrating an interactive story with multiple characters.
        ALWAY ENSURE THAT THE USER IS SHOWN EXACTLY WHAT THE CREW SAYS. Do not paraphrase or summarize the crew's statements.
        """;
    #endregion
}

public class AdventurePlugin
{
    public static string Name = "The Quest for the Celestial Keys";
    public static string Description = "A space adventure featuring a team of space explorers.";
    private const string FromLCDnd = """
                                     You are the storyteller, {storyteller_name}. 
                                     Your description is as follows: {storyteller_description}.
                                     The other players will propose actions to take and you will explain what happens when they take those actions.
                                     Speak in the first person from the perspective of {storyteller_name}.
                                     Do not change roles!
                                     Do not speak from the perspective of anyone else.
                                     Remember you are the storyteller, {storyteller_name}.
                                     Stop speaking the moment you finish speaking from your perspective.
                                     Never forget to keep your response to {word_limit} words!
                                     Do not add anything else.
                                     """;

}

[TypeConverter(typeof(GenericTypeConverter<MissionSection>))]
public record MissionSection(string Name, string Description, string Instructions)
{
    public bool IsCompleted { get; set; }
}
public class AgentChatMessage(string name, string content, string role, string id)
{
    public string Name { get; set; } = name;
    public string Content { get; set; } = content;
    public string Role { get; set; } = role;
    public string Id { get; set; } = id;
    public static AgentChatMessage FromChatMessage(ChatMessage message, string name)
    {
        return new AgentChatMessage(name, message.Text, message.Role.Value, message.MessageId ?? Guid.NewGuid().ToString());
    }
}
