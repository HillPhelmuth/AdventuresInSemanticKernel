namespace SkPluginLibrary.Models;

public enum AIModel
{
    [Description("Select a model")]
    None,
    [ModelName("gpt-4o-mini")]
    [AzureOpenAIModel("gpt-35-turbo")]
    [Description("Latest gpt-4o-mini")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt4OMini,
    [ModelName("gpt-4.1-mini")]
    [AzureOpenAIModel("gpt-4.1-mini")]
    [Description("gpt-4.1-mini")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt41Mini,
    [ModelName("gpt-4.1-nano")]
    [AzureOpenAIModel("gpt-4.1-nano")]
    [Description("gpt-4.1-nano")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt41Nano,
    [ModelName("gpt-4.1")]
    [AzureOpenAIModel("gpt-4.1")]
    [Description("gpt-4.1")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt41,
    [ModelName("gpt-4o-2024-08-06")]
    [AzureOpenAIModel("gpt-4o")]
    [Description("Current gpt-4o")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt4OCurrent,
    [ModelName("gpt-4o-2024-11-20")]
    [AzureOpenAIModel("gpt-4o")]
    [Description("Latest gpt-4o")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt4OLatest,
    [ModelName("o4-mini")]
    [AzureOpenAIModel("o4-mini")]
    [Description("o4-mini")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    O4Mini,
    [ModelName("o3")]
    [AzureOpenAIModel("o3")]
    [Description("o3")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    O3,
    [ModelName("gpt-3.5-turbo")]
    [AzureOpenAIModel("gpt-35-turbo")]
    [Description("Latest gpt-3.5-turbo")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt35Turbo,
    [ModelName("gpt-4-turbo")]
    [AzureOpenAIModel("gpt-4")]
    [Description("Latest gpt-4-turbo")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
    Gpt4Turbo,
    [ModelName("chatgpt-4o-latest")]
    [AzureOpenAIModel("gpt-4o")]
    [Description("Dynamic gpt-4o")]
    [ModelProvidor("OpenAI")]
    Gpt4OChatGptLatest,//ft:gpt-4o-mini-2024-07-18:hillphelmuth:novel-gen-mini:A138kqIT
    [ModelName("o1-mini")]
    [Description("OpenAI o1-mini")]
    [ModelProvidor("OpenAI")]
    O1SeriesMini,
    [ModelName("gemini-pro")]
    [Description("Latest Gemini 1.0 Pro")]
    [ModelProvidor("GoogleAI")]
	Gemini10,
    [ModelName("gemini-1.5-pro-latest")]
    [Description("Latest Gemini 1.5 Pro")]
    [ModelProvidor("GoogleAI")]
	Gemini15,
	[ModelName("gemini-1.5-flash")]
	[Description("Gemeni Flash")]
	[ModelProvidor("GoogleAI")]
	GeminiFlash,
	[ModelName("open-mistral-7b")]
	[Description("Open Mistral 7B")]
	[ModelProvidor("MistralAI")]
	OpenMistral7B,
	[ModelName("open-mixtral-8x7b")]
	[Description("Open Mixtral 8x7B")]
	[ModelProvidor("MistralAI")]
	OpenMixtral8x7B,
	[ModelName("open-mixtral-8x22b")]
	[Description("Open Mixtral 8x22B")]
	[ModelProvidor("MistralAI")]
	OpenMixtral8x22B,
	[ModelName("mistral-small-latest")]
	[Description("Mistral Small Latest")]
	[ModelProvidor("MistralAI")]
	MistralSmallLatest,
	[ModelName("mistral-medium-latest")]
	[Description("Mistral Medium Latest")]
	[ModelProvidor("MistralAI")]
	MistralMediumLatest,
	[ModelName("mistral-large-latest")]
	[Description("Mistral Large Latest")]
	[ModelProvidor("MistralAI")]
	MistralLargeLatest
}
public class ModelNameAttribute(string model) : Attribute
{
    public string Model { get; set; } = model;
}
public class AzureOpenAIModelAttribute(string model) : Attribute
{
    public string Model { get; set; } = model;
}
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ModelProvidorAttribute(string providor) : Attribute
{
    public string Providor { get; set; } = providor;
}