namespace BlazorWithSematicKernel.Model;


public record SimScore(string Prompt, double Score)
{
    public string? ComparedTo { get; set; }
}
