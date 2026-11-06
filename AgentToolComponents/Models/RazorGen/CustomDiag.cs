using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace AgentToolComponents.Models.RazorGen;

public class CustomDiag
{
    [JsonConstructor]
    public CustomDiag()
    {
    }
    public CustomDiag(Diagnostic diagnostic)
    {
        var mappedLineSpan = diagnostic.Location.GetMappedLineSpan();
        var file = Path.GetFileName(mappedLineSpan.Path);
        var line = mappedLineSpan.StartLinePosition.Line;


        Code = diagnostic.Descriptor.Id;
        Severity = diagnostic.Severity;
        Description = diagnostic.GetMessage();
        File = file;
        Line = line;

    }

    public CustomDiag(RazorDiagnostic diagnostic)
    {
        Code = diagnostic.Id;
        Severity = (DiagnosticSeverity)diagnostic.Severity;
        Description = diagnostic.GetMessage();
        File = Path.GetFileName(diagnostic.Span.FilePath);
        Line = -1;
    }
    public string Code { get; set; }

    public DiagnosticSeverity Severity { get; set; }

    public string Description { get; set; }

    public int? Line { get; set; }

    public string File { get; set; }


    public override string ToString()
    {
        var line = Line >= 0 ? Line.ToString() : "Razor is hard...";
        return $"Code: {Code} Severity: {Severity} Description: {Description} Location: {File} at line {line}";
    }
}