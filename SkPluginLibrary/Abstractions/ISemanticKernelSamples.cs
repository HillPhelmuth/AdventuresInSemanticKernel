namespace SkPluginLibrary.Abstractions;

public interface ISemanticKernelSamples
{
    Task RunExample(string typename);
    public event EventHandler<string>? StringWritten;
}