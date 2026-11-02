using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace UnityEngine {
 public static class Application { public static string dataPath; }
 public static class JsonUtility { public static T FromJson<T>(string s) => System.Text.Json.JsonSerializer.Deserialize<T>(s,new System.Text.Json.JsonSerializerOptions {IncludeFields=true}); }
 public static class Debug { public static void Log(object s) {} public static void LogWarning(object s) {} }
}
namespace UnityEditor {
 [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem:Attribute { public MenuItem(string s) {} }
 public static class EditorApplication {public static bool isCompiling,isUpdating,isPlayingOrWillChangePlaymode;public static Action delayCall;}
 public static class SessionState {public static bool GetBool(string key,bool fallback)=>false;public static void SetBool(string key,bool val) {}}
 public static class AssetDatabase {
  static string Root=>Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,".."));
  static string Full(string p)=>Path.Combine(Root,p);
  public static string AssetPathToGUID(string p) {string m=Full(p)+".meta";return File.Exists(m)?Regex.Match(File.ReadAllText(m),@"(?m)^guid: (\w+)").Groups[1].Value:"";}
  public static string GUIDToAssetPath(string guid)=>GetAllAssetPaths().FirstOrDefault(p=>AssetPathToGUID(p)==guid)??"";
  public static string[] GetAllAssetPaths()=>Directory.GetFiles(UnityEngine.Application.dataPath,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta")).Select(p=>Path.GetRelativePath(Root,p).Replace('\\','/')).ToArray();
  public static bool IsValidFolder(string path)=>Directory.Exists(Full(path));
  public static string CreateFolder(string parent,string name) {Directory.CreateDirectory(Full(parent+"/"+name));return "";}
  public static string MoveAsset(string old,string dest) {if(File.Exists(Full(dest)))return "exists";File.Move(Full(old),Full(dest));File.Move(Full(old)+".meta",Full(dest)+".meta");return "";}
  public static bool DeleteAsset(string p){File.Delete(Full(p));File.Delete(Full(p)+".meta");return true;}
  public static string[] GetDependencies(string path,bool recursive) {string text=File.ReadAllText(Full(path));return Regex.Matches(text,@"guid: (\w+)").Select(m=>GUIDToAssetPath(m.Groups[1].Value)).Where(p=>p!="").ToArray();}
 }
}
