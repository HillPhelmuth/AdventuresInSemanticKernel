namespace SkPluginComponents.Models;

public class AskUserResults
{
    public AskUserResults(bool success, AskUserParameters parameters)
    {
        IsSuccess = success;
        Parameters = parameters;
    }
    public bool IsSuccess { get; private set; }
    public AskUserParameters Parameters { get; }

    public static AskUserResults Empty(bool success)
    {
        return new AskUserResults(success, new AskUserParameters());
    }
}
