namespace SkPluginLibrary.Models;

public enum AIModel
{
    [Description("Select a model")]
    None,
    [ModelName("gpt-4o-mini")]
    [Description("Latest gpt-4o-mini")]
    [ModelProvidor("OpenAI")]
    Gpt4OMini,
    [ModelName("gpt-4.1-mini")]
    [Description("gpt-4.1-mini")]
    [ModelProvidor("OpenAI")]
    Gpt41Mini,
    [ModelName("gpt-4.1-nano")]
    [Description("gpt-4.1-nano")]
    [ModelProvidor("OpenAI")]
    Gpt41Nano,
    [ModelName("gpt-4.1")]
    [Description("gpt-4.1")]
    [ModelProvidor("OpenAI")]
    Gpt41,
    [ModelName("gpt-4o-2024-08-06")]
    [Description("Current gpt-4o")]
    [ModelProvidor("OpenAI")]
    Gpt4OCurrent,
    [ModelName("gpt-4o-2024-11-20")]
    [Description("Latest gpt-4o")]
    [ModelProvidor("OpenAI")]
    Gpt4OLatest,
    [ModelName("o4-mini")]
    [Description("o4-mini")]
    [ModelProvidor("OpenAI")]
    O4Mini,
    [ModelName("o3")]
    [Description("o3")]
    [ModelProvidor("OpenAI")]
    O3,
    [ModelName("gpt-3.5-turbo")]
    [Description("Latest gpt-3.5-turbo")]
    [ModelProvidor("OpenAI")]
    Gpt35Turbo,
    [ModelName("gpt-4-turbo")]
    [Description("Latest gpt-4-turbo")]
    [ModelProvidor("OpenAI")]
    Gpt4Turbo,
    [ModelName("chatgpt-4o-latest")]
    [Description("Dynamic gpt-4o")]
    [ModelProvidor("OpenAI")]
    Gpt4OChatGptLatest,//ft:gpt-4o-mini-2024-07-18:hillphelmuth:novel-gen-mini:A138kqIT
    [ModelName("o1-mini")]
    [Description("OpenAI o1-mini")]
    [ModelProvidor("OpenAI")]
    O1SeriesMini,
    // OpenRouter-hosted models (OpenAI-compatible endpoint, selected by the OpenRouter provider)
    [ModelName("anthropic/claude-sonnet-4.5")]
    [Description("OpenRouter: Claude Sonnet 4.5")]
    [ModelProvidor("OpenRouter")]
    OpenRouterClaudeSonnet,
    [ModelName("google/gemini-2.5-flash")]
    [Description("OpenRouter: Gemini 2.5 Flash")]
    [ModelProvidor("OpenRouter")]
    OpenRouterGeminiFlash,
    [ModelName("mistralai/mistral-large")]
    [Description("OpenRouter: Mistral Large")]
    [ModelProvidor("OpenRouter")]
    OpenRouterMistralLarge,
    [ModelName("meta-llama/llama-3.3-70b-instruct")]
    [Description("OpenRouter: Llama 3.3 70B")]
    [ModelProvidor("OpenRouter")]
    OpenRouterLlama,
    [ModelName("deepseek/deepseek-chat")]
    [Description("OpenRouter: DeepSeek Chat")]
    [ModelProvidor("OpenRouter")]
    OpenRouterDeepSeek
}
public class ModelNameAttribute(string model) : Attribute
{
    public string Model { get; set; } = model;
}
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ModelProvidorAttribute(string providor) : Attribute
{
    public string Providor { get; set; } = providor;
}