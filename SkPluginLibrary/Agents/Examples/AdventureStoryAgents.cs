using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Experimental.Agents;
using SkPluginComponents;
using SkPluginComponents.Models;
using SkPluginLibrary.Agents.Models;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Agents.Examples;

public class AdventureStoryAgents(AskUserService askUserService) : IAsyncDisposable
{
    private static readonly List<IAgent> Agents = [];
    public event Action<AgentMessage>? ChatMessage;
    public event Action<ChatHistory>? ChatHistoryUpdate;
    private readonly AskUserService _askUserService = askUserService;
    private readonly AskUserPlugin _askUserPlugin = new(askUserService);
    private IAgent? _astra;
    private IAgent? _zanar;
    private IAgent? _elara;
    private ChatHistory _chatHistory = [];
    private string _currentRespondent = "Story Master";
    

    public async IAsyncEnumerable<string> ExecuteChatSequence(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _astra ??= await GenerateCommanderAstra();
        _zanar ??= await GenerateZanar();
        _elara ??= await GenerateElara();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());
        var kernel = kernelBuilder.AddOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey).Build();
        //kernel.Plugins.Add(GenerateAgentsAsPluginFunctions());
        kernel.Plugins.Add(_astra.AsPlugin());
        kernel.Plugins.Add(_zanar.AsPlugin());
        kernel.Plugins.Add(_elara.AsPlugin());
        kernel.FunctionInvoking += HandleFunctionInvoking;
        kernel.FunctionInvoked += HandleFunctionInvoked;
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = AdventureSystemPrompt, Temperature = 1.0 };
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory.AddUserMessage(input);
        var sentRespondant = false;
        var sb = new StringBuilder();
        await foreach (var streamingChatMessageContent in chat.GetStreamingChatMessageContentsAsync(_chatHistory, settings, kernel, cancellationToken))
        {
            var update = (OpenAIStreamingChatMessageContent) streamingChatMessageContent;
            var toolCall = update.ToolCallUpdate as StreamingFunctionToolCallUpdate;
            if (toolCall?.Name is not null)
            {
                _currentRespondent = toolCall.Name.Replace("_Ask","");
            }
            
            if (!sentRespondant)
            {
                sb.AppendLine($"{_currentRespondent}: ");
                yield return $"<em>{_currentRespondent}</em>:<br/> ";
                sentRespondant = true;
            }
            
            if (update.Content is not null)
            {
                sb.Append(update.Content);
                yield return update.Content;
            }
            
        }
        _chatHistory.AddAssistantMessage(sb.ToString());
        ChatHistoryUpdate?.Invoke(_chatHistory);

    }
    private void HandleFunctionInvoked(object? sender, FunctionInvokedEventArgs invokedArgs)
    {
        var function = invokedArgs.Function;
        Console.WriteLine($"\n---------Function {function.Name} Invoked-----------\nResults:\n{invokedArgs.Result}\n----------------------------");
    }
    private void HandleFunctionInvoking(object? sender, FunctionInvokingEventArgs invokingEventArgs)
    {
        var function = invokingEventArgs.Function;
        Console.WriteLine($"Function Arguments: {invokingEventArgs.Arguments.AsJson()}");
        //_currentRespondent = function.Name;
        Console.WriteLine($"Function {function.Name} Invoking");
    }
    public async Task RestartAsync()
    {
        _chatHistory.Clear();
        await RemoveAgents();
    }
    public async Task<string> ExecuteRun(string input = "Introduce the crew and tell me about our first adventure", CancellationToken cancellationToken = default)
    {
        try
        {
            var commander = await GenerateCommanderAstra();
            var zanar = await GenerateZanar();
            var elara = await GenerateElara();
            var thread = await commander.NewThreadAsync(cancellationToken);
            var askuserPlugin = KernelPluginFactory.CreateFromObject(_askUserPlugin);
            var askUser = askuserPlugin["AskUser"];
            var kernel = Kernel.CreateBuilder().Build();
            await thread.AddUserMessageAsync(input, cancellationToken);
            while (true)
            {
                
                var chatMessages = await thread.InvokeAsync(commander, cancellationToken).ToListAsync(cancellationToken);
                Console.WriteLine($"Commander Astra has {chatMessages.Count} messages");
                var message = chatMessages.First();
                ChatMessage?.Invoke(AgentMessage.FromChatMessage(message, commander.Name!));
                if (message.Content.Contains("[Engineer Zanar]"))
                {
                    chatMessages = await thread.InvokeAsync(zanar, cancellationToken).ToListAsync(cancellationToken);
                    SendChatMessages(chatMessages, zanar.Name);
                }
                else if (message.Content.Contains("[Mystic Elara]"))
                {
                    chatMessages = await thread.InvokeAsync(elara, cancellationToken).ToListAsync(cancellationToken);
                    SendChatMessages(chatMessages, elara.Name);
                }
                else if (message.Content.Contains("[New Recruit]"))
                {
                    var userResponse = await askUser.InvokeAsync(kernel, new KernelArguments { ["question"] = message.Content }, cancellationToken);
                    if (userResponse.Result().Contains("user refused to answer")) { break; }
                    var userMessage = await thread.AddUserMessageAsync(userResponse.Result(), cancellationToken);
                    var name = "You";
                    SendChatMessage(userMessage, name);
                }
                else
                {
                    var userResponse = await askUser.InvokeAsync(kernel, new KernelArguments {["question"] = message.Content }, cancellationToken);
                    if (userResponse.Result().Contains("user refused to answer")) { break; }
                    var userMessage = await thread.AddUserMessageAsync(userResponse.Result(), cancellationToken);
                    ChatMessage?.Invoke(AgentMessage.FromChatMessage(userMessage, "You"));
                }
            }
            return "Conversation complete";
        }
        finally
        {
            // ReSharper disable once MethodSupportsCancellation
            static async void DeleteAgent(IAgent agent) => await agent.DeleteAsync();

            Agents.ForEach(DeleteAgent);
            
        }
    }
    private void SendChatMessages(IEnumerable<IChatMessage> messages, string name)
    {
        foreach (var message in messages)
        {
            SendChatMessage(message, name);
        }
    }
    private void SendChatMessage(IChatMessage message, string name)
    {
        ChatMessage?.Invoke(AgentMessage.FromChatMessage(message, name));
    }
    public static async Task<List<AgentProxy>> GetAgents()
    {
        var result = new List<AgentProxy>();
        var astra = await GenerateCommanderAstra();
        var zanar = await GenerateZanar();
        var elara = await GenerateElara();
        result.Add(astra.AgentProxy());
        result.Add(zanar.AgentProxy());
        result.Add(elara.AgentProxy());
        await RemoveAgents();
        return result;
    }
    private static async Task<IAgent> GenerateCommanderAstra()
    {
        var template = FileHelper.ExtractFromAssembly<string>("CommanderAstra.yaml");
        return Track(await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey)
            //.FromTemplate(template)
            .WithInstructions(AstraInstructions)
            .WithDescription("Gruff but crafty leader of the crew.")
            .WithName("Commander Astra")
            .BuildAsync());
    }
    private static async Task<IAgent> GenerateZanar()
    {
        return Track(await new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName("Engineer Zanar")
            .WithDescription("A brilliant but quirky engineer character who specializes in technology and gadgetry. Provides information on fictional technologies, solves technical puzzles, or creates imaginative solutions to challenges faced by the team")
            .WithInstructions(ZanarInstructions)
            .BuildAsync());
    }
    private static async Task<IAgent> GenerateElara()
    {
        return Track(
            await new AgentBuilder()
                .WithOpenAIChatCompletion(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey)
                .WithName("Mystic Elara")
                .WithDescription("A character with deep knowledge of the universe's mystical and magical aspects. Offers insights into alien cultures, ancient lore, and cosmic mysteries, adding a layer of fantasy and intrigue to the narrative.")
                .WithInstructions(ElaraInstructions)
                .BuildAsync());
    }
    private KernelPlugin GenerateAgentsAsPluginFunctions()
    {
        var astraFunction = KernelFunctionFactory.CreateFromPrompt("Respond to input as Astra: {{$input}}", new OpenAIPromptExecutionSettings { Temperature = 0.9, ChatSystemPrompt = AstraInstructions}, "CommanderAstra", "Gruff but crafty leader of the crew.", loggerFactory: ConsoleLogger.LoggerFactory);
        var zanarFunction = KernelFunctionFactory.CreateFromPrompt("Respond to input as Zanar: {{$input}}", new OpenAIPromptExecutionSettings { Temperature = 0.7, ChatSystemPrompt = ZanarInstructions }, "EngineerZanar", "A brilliant but quirky engineer character who specializes in technology and gadgetry. Provides information on fictional technologies, solves technical puzzles, or creates imaginative solutions to challenges faced by the team",loggerFactory: ConsoleLogger.LoggerFactory);
        var elaraFunction = KernelFunctionFactory.CreateFromPrompt("Respond to input as Elara: {{$input}}", new OpenAIPromptExecutionSettings { Temperature = 1.1, ChatSystemPrompt = ElaraInstructions }, "MysticElara", "A character with deep knowledge of the universe's mystical and magical aspects. Offers insights into alien cultures, ancient lore, and cosmic mysteries, adding a layer of fantasy and intrigue to the narrative.", loggerFactory:ConsoleLogger.LoggerFactory);
        var kernelPlugin = KernelPluginFactory.CreateFromFunctions("Crew", new[] { astraFunction, zanarFunction, elaraFunction });
        return kernelPlugin;
    }
    private static IAgent Track(IAgent agent)
    {
        Agents.Add(agent);

        return agent;
    }
    private static async Task RemoveAgents()
    {
        foreach (var agent in Agents)
        {
            await agent.DeleteAsync();
        }
    }
    public async ValueTask DisposeAsync()
    {
        await RemoveAgents();
    }

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
public static class AgentExtensions
{
    public static AgentProxy AgentProxy(this IAgent agent)
    {
        return new AgentProxy
        {
            Description = agent.Description ?? "no description",
            Instructions = agent.Instructions,
            Name = agent.Name,
            Plugins = [.. agent.Plugins],
            IsPrimary = false
        };
    }
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
public class AgentMessage(string name, string content, string role, string id)
{
    public string Name { get; set; } = name;
    public string Content { get; set; } = content;
    public string Role { get; set; } = role;
    public string Id { get; set; } = id;
    public static AgentMessage FromChatMessage(IChatMessage message, string name)
    {
        return new AgentMessage(name, message.Content, message.Role, message.Id);
    }
}