using System;
using AgentFrameworkLibrary;

public static class Program
{
    public static void Main()
    {
        var items = new[]
        {
            new MemoryItem("agents", "Agents use sessions.", [1f, 0f], "example"),
            new MemoryItem("tokens", "Tokens measure model input.", [0f, 1f], "example")
        };
        foreach (var match in MemoryService.Rank(items, [1f, 0f], "example", 1, 0.5))
            Console.WriteLine($"{match.Item.Text} Score: {match.Score:F2}");
    }
}
