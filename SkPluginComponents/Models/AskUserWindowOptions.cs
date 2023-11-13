namespace SkPluginComponents.Models;

public class AskUserWindowOptions
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public Location Location { get; set; } = Location.Center;
    public bool ShowCloseButton { get; set; }
    public string Width { get; set; } = "auto";
    public string Height { get; set; } = "auto";
    public string Style { get; set; } = "width:auto; height: auto;";
    public string CssClasses { get; set; } = "";
    public bool CloseOnOuterClick { get; set; } = true;
}
