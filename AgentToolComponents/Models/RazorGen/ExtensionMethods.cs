using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using ProjectFile = AgentToolComponents.Models.RazorFile;

namespace AgentToolComponents.Models.RazorGen;

internal static class ExtensionMethods
{
    public static RazorProjectItem CreateRazorProjectItem(this ProjectFile projectFile)
    {
        var fileName = projectFile.Name;
        var fileContent = projectFile.Code;
        Console.WriteLine($"CreateRazorProjectItem for {fileName}");
        var fullPath = RazorConstants.WorkingDirectory + fileName;

        // File paths in Razor are always of the form '/a/b/c.razor'
        var filePath = fileName;
        if (!filePath.StartsWith('/'))
        {
            filePath = '/' + filePath;
        }

        fileContent = fileContent.Replace("\r", string.Empty);

        return new WebRazorProjectItem(
            RazorConstants.WorkingDirectory,
            filePath,
            fullPath,
            fileName,
            FileKinds.Component,
            Encoding.UTF8.GetBytes(fileContent.TrimStart()));
    }
}