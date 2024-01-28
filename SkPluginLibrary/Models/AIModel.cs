namespace SkPluginLibrary.Models;

public enum AIModel
{
    [OpenAIModel("gpt-3.5-turbo-1106")]
    [AzureOpenAIModel("gpt-35-turbo")]
    Gpt35,
    [OpenAIModel("gpt-4-turbo-preview")]
    [AzureOpenAIModel("gpt-4")]
    Gpt4,
    Image,
    [OpenAIModel("gpt-4-turbo-preview")]
    [AzureOpenAIModel("gpt-4")]
    Planner
}
public class OpenAIModelAttribute(string model) : Attribute
{
    public string Model { get; set; } = model;
}
public class AzureOpenAIModelAttribute(string model) : Attribute
{
    public string Model { get; set; } = model;
}