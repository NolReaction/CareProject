using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public sealed class Transform : Component { public int childCount; }
    public sealed class Camera : Component { }
    public sealed class AudioListener : Component { }
    public enum LightType { Point, Directional }
    public sealed class Light : Component { public LightType type; }
    public sealed class GameObject : Object
    {
        public string name;
        public readonly Transform transform = new Transform();
        public readonly List<Component> components = new List<Component>();
        public GameObject(string value, params Component[] items) { name=value;components.Add(transform);components.AddRange(items); }
        public T GetComponent<T>() where T:Component { foreach(var item in components) if(item is T) return (T)item;return null; }
        public T[] GetComponents<T>() where T:Component { var result=new List<T>();foreach(var item in components)result.Add((T)item);return result.ToArray(); }
    }
    public static class Application { public static bool isBatchMode;public static string dataPath; }
    public static class Debug { public static readonly List<string> Warnings=new List<string>();public static void LogWarning(string text) { Warnings.Add(text); } }
}
namespace UnityEngine.Rendering.Universal
{
    public sealed class UniversalAdditionalCameraData : UnityEngine.Component { }
    public sealed class UniversalAdditionalLightData : UnityEngine.Component { }
}
namespace UnityEngine.SceneManagement
{
    public sealed class SceneData { public bool Valid=true,Loaded=true,Dirty;public string Path="";public UnityEngine.GameObject[] Roots=new UnityEngine.GameObject[0]; }
    public struct Scene
    {
        public SceneData Data;
        public bool IsValid() { return Data!=null&&Data.Valid; }
        public bool isLoaded { get { return Data!=null&&Data.Loaded; } }
        public bool isDirty { get { return Data!=null&&Data.Dirty; } }
        public string path { get { return Data!=null?Data.Path:""; } }
        public UnityEngine.GameObject[] GetRootGameObjects() { return Data.Roots; }
    }
    public static class SceneManager { public static int sceneCount=1;public static Scene Active;public static Scene GetActiveScene() { return Active; } }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class InitializeOnLoadAttribute:Attribute { }
    public sealed class SceneAsset:UnityEngine.Object { }
    public static class EditorPrefs
    {
        private static readonly Dictionary<string,bool> Values=new Dictionary<string,bool>();
        public static bool GetBool(string key,bool fallback) { bool v;return Values.TryGetValue(key,out v)?v:fallback; }
        public static void SetBool(string key,bool value) { Values[key]=value; }
    }
    public static class EditorApplication
    {
        public static bool isCompiling,isUpdating,isPlayingOrWillChangePlaymode;
        public static double timeSinceStartup;
        public static event Action update;
        public static readonly List<string> Menus=new List<string>();
        public static bool ExecuteMenuItem(string name) { Menus.Add(name);return true; }
        public static void Tick() { if(update!=null)update(); }
    }
    public static class AssetDatabase
    {
        public static bool Ready=true;
        public static T LoadAssetAtPath<T>(string path) where T:UnityEngine.Object,new() { return Ready?new T():null; }
    }
}
namespace UnityEditor.SceneManagement
{
    public enum OpenSceneMode { Single }
    public static class EditorSceneManager
    {
        public static int Opens;
        public static bool Throw;
        public static UnityEngine.SceneManagement.Scene OpenScene(string path,OpenSceneMode mode)
        {
            Opens++;if(Throw)throw new InvalidOperationException("simulated import error");
            var scene=new UnityEngine.SceneManagement.Scene{Data=new UnityEngine.SceneManagement.SceneData{Path=path}};
            UnityEngine.SceneManagement.SceneManager.Active=scene;return scene;
        }
    }
}
namespace IntercomMvp.Editor
{
    internal static class IntercomProjectPaths
    {
        internal sealed class Key { internal string Resolve() { return Path; } }
        internal static string Path="Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity";
        internal static readonly Key TutorialScene=new Key();
    }
}
