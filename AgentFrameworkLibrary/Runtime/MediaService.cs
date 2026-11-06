using OpenAI.Audio;

namespace AgentFrameworkLibrary;

public sealed class MediaService(RuntimeConfiguration configuration)
{
    public async Task<byte[]> ImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        RequireOpenAI(prompt);
        var client = configuration.Current.CreateClient().GetImageClient(configuration.Get("OpenAI:ImageModelId") ?? "gpt-image-1");
        var image = await client.GenerateImageAsync(prompt, cancellationToken: cancellationToken);
        return image.Value.ImageBytes?.ToArray()
            ?? throw new InvalidOperationException("The selected image model did not return image bytes. Select a model with base64 image output.");
    }

    public async Task<byte[]> SpeechAsync(string text, CancellationToken cancellationToken = default)
    {
        RequireOpenAI(text);
        var client = configuration.Current.CreateClient().GetAudioClient(configuration.Get("OpenAI:SpeechModelId") ?? "tts-1");
        return (await client.GenerateSpeechAsync(text, GeneratedSpeechVoice.Alloy, cancellationToken: cancellationToken)).Value.ToArray();
    }

    private void RequireOpenAI(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        if (configuration.Current.Provider != "OpenAI")
            throw new NotSupportedException("Image and speech utilities require the OpenAI provider. OpenRouter media endpoints are not enabled.");
    }
}
