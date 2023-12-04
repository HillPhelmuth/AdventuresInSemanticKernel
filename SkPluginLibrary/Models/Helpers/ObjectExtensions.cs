// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Models.Helpers;
public static class ObjectExtensions
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles };

    public static string AsJson(this object obj)
    {
        return JsonSerializer.Serialize(obj, s_jsonOptions);
    }

    public static string SanitizeJson(this string json)
    {
        return Regex.Unescape(json).Replace("\"{", "{").Replace("}\"", "}");
    }
}
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true, ReferenceHandler = ReferenceHandler.IgnoreCycles };
    public static JsonSerializerOptions JsonOptionsIndented => s_jsonOptions;
}
