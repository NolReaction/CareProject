using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace UnityEngine
{
    public static class Application { public static string dataPath; }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text,
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    }
    public static class Debug
    {
        public static int Warnings;
        public static void LogWarning(object message) { Warnings++; }
    }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute : Attribute { }
    public static class EditorApplication
    {
        public static Action delayCall;
        public static event Action projectChanged;
        public static void ProjectChanged() => projectChanged?.Invoke();
    }
    public static class AssetDatabase
    {
        private static string Root => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        private static string Full(string path) => Path.Combine(Root, path);
        public static string AssetPathToGUID(string path)
        {
            string meta = Full(path) + ".meta";
            return File.Exists(meta) ? Regex.Match(File.ReadAllText(meta), @"(?m)^guid: (\w+)").Groups[1].Value : "";
        }
        public static string GUIDToAssetPath(string guid) => GetAllAssetPaths().FirstOrDefault(
            path => string.Equals(AssetPathToGUID(path), guid, StringComparison.OrdinalIgnoreCase)) ?? "";
        public static string[] GetAllAssetPaths() => Directory.GetFiles(UnityEngine.Application.dataPath, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/')).ToArray();
        public static bool IsValidFolder(string path) => Directory.Exists(Full(path));
        public static string CreateFolder(string parent, string name)
        { Directory.CreateDirectory(Full(parent + "/" + name)); return ""; }
    }
}
