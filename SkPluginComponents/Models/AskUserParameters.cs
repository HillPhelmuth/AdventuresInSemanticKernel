namespace SkPluginComponents.Models;

public class AskUserParameters : Dictionary<string, object?>
{
    public T? Get<T>(string parameterName)
    {
        if (!ContainsKey(parameterName))
            throw new KeyNotFoundException($"{parameterName} does not exist in modal parameters");

        return (T?)this[parameterName];
    }
    public T? Get<T>(string parameterName, T defaultValue)
    {
        if (TryGetValue(parameterName, out var parameterValue))
            return (T?)parameterValue;
        parameterValue = defaultValue;
        this[parameterName] = parameterValue;
        return (T?)parameterValue;
    }
    public void Set<T>(string parameterName, T parameterValue)
    {
        this[parameterName] = parameterValue;
    }
}
