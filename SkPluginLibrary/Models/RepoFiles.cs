// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;

namespace SkPluginLibrary.Models;

public static class RepoFiles
{
    public static string AppBasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

    /// <summary>
    /// Scan the local folders from the repo, looking for "samples/skills" folder.
    /// </summary>
    /// <returns>The full path to samples/skills</returns>
    public static string SamplePluginsPath()
    {
        return PathToYamlPlugins;


    }
    public static string PathToYamlPlugins => Path.Combine(AppBasePath, "Plugins", "YamlPlugins");
    public static string PluginDirectoryPath => Path.Combine(AppBasePath, "Plugins", "SemanticPlugins");
    public static string ApiPluginDirectoryPath => Path.Combine(AppBasePath, "Plugins", "ApiPlugins");
    public static string CodeTextDirectoryPath => Path.Combine(AppBasePath, "Data", "CodeFiles");

}
