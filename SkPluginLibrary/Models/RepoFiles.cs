// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Models;

public static class RepoFiles
{
  
    /// <summary>
    /// Scan the local folders from the repo, looking for "samples/skills" folder.
    /// </summary>
    /// <returns>The full path to samples/skills</returns>
    public static string SamplePluginsPath()
    {
        return PluginDirectoryPath;


    }
    public static string PluginDirectoryPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory(), "SemanticPlugins");
    public static string ApiPluginDirectoryPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory(), "ApiPlugins");
    
}
