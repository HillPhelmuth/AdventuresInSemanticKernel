namespace SkPluginLibrary.Models;

public enum AIModel
{
    [Description("Select a model")]
    None,
    [ModelName("gpt-3.5-turbo")]
    [AzureOpenAIModel("gpt-35-turbo")]
    [Description("Latest gpt-3.5-turbo")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
	Gpt35,
    [ModelName("gpt-4-turbo")]
    [AzureOpenAIModel("gpt-4-turbo")]
    [Description("Latest gpt-4-turbo")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
	Gpt4Turbo,
    [ModelName("gpt-4o")]
    [AzureOpenAIModel("gpt-4o")]
    [Description("Latest gpt-4o")]
    [ModelProvidor("OpenAI")]
    [ModelProvidor("AzureOpenAI")]
	Gpt4O,
	[ModelName("gpt-4o-mini")]
	[AzureOpenAIModel("gpt-4o-mini")]
	[Description("Latest gpt-4o-mini")]
	[ModelProvidor("OpenAI")]
	[ModelProvidor("AzureOpenAI")]
	Gpt4OMini,
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