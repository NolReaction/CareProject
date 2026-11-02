using System;
using System.Collections;
using System.Reflection;
using IntercomMvp;
using UnityEngine;

internal static class Program
{
    private static int assertions;
    private static void Check(bool p, string reason) { assertions++; if (!p) throw new Exception(reason); }
    private static object Call(object instance, string method) => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    private static int Count(object instance, string field) => ((ICollection)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance)).Count;
    private static void Tick(IntercomCameraOcclusion camera, int frames = 1)
    { for (int i = 0; i < frames; i++) { Time.frameCount++; Time.unscaledTime += .02f; Time.unscaledDeltaTime = .02f; Call(camera, "UpdateVisibility"); } }
    private static Mesh Wall() => new Mesh { sourceVertices = new[] { new Vector3(-2, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 3, 0), new Vector3(-2, 3, 0) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
    private static MeshRenderer Part(string name, Transform parent, Mesh mesh, Vector3 position)
    { var go = new GameObject(name); go.transform.parent = parent; go.transform.position = position; go.AddComponent<MeshFilter>().sharedMesh = mesh; return go.AddComponent<MeshRenderer>(); }
    public static void Main()
    {
        var hero = new GameObject("Robot"); hero.transform.position = new Vector3(0, 0, 10); hero.AddComponent<CharacterController>();
        var rig = new GameObject("Camera"); rig.transform.position = new Vector3(0, 1, -10);
        var camera = rig.AddComponent<Camera>(); var occlusion = rig.AddComponent<IntercomCameraOcclusion>(); occlusion.target = hero.transform; occlusion.fadeVariantTemplate = new Material();
        var group = new GameObject("Warehouse + independent loading props"); var marker = group.AddComponent<IntercomOccluder>();
        var shared = Wall(); var warehouse = Part("Warehouse", group.transform, shared, new Vector3(0, 0, 5));
        var sibling = Part("Side crate", group.transform, shared, new Vector3(6, 0, 5));
        var behind = Part("Behind player", group.transform, shared, new Vector3(0, 0, 15));
        Call(occlusion, "OnEnable"); Tick(occlusion, 30);
        Check(IntercomOcclusionFade.Active.ContainsKey(warehouse), "actual foreground mesh acquires fade");
        Check(!IntercomOcclusionFade.Active.ContainsKey(sibling), "district sibling stays visible even while warehouse fades");
        Check(!IntercomOcclusionFade.Active.ContainsKey(behind), "depth endpoint excludes renderer behind target");
        Check(shared.vertexReads == 1, "three mesh instances share one cached BVH");
        Check(Count(occlusion, "entries") == 3, "default marker creates independent renderer states");
        float opacity = IntercomOcclusionFade.Active[warehouse]; Call(occlusion, "UpdateVisibility");
        Check(IntercomOcclusionFade.Active[warehouse] == opacity, "camera callback cannot advance fade twice in one frame");

        warehouse.transform.position = new Vector3(0, 0, 16); Tick(occlusion, 35);
        Check(IntercomOcclusionFade.Active.Count == 0, "moving obstruction behind hero restores every material lease");
        warehouse.transform.position = new Vector3(0, 0, 5); Tick(occlusion, 30);
        hero.activeInHierarchy = false; Tick(occlusion);
        Check(IntercomOcclusionFade.Active.Count == 0, "inactive target restores immediately");
        hero.activeInHierarchy = true; Tick(occlusion, 30);
        warehouse.enabled = false; Tick(occlusion);
        Check(!IntercomOcclusionFade.Active.ContainsKey(warehouse), "disabled renderer releases immediately");
        warehouse.enabled = true; Tick(occlusion, 30);
        warehouse.forceRenderingOff = true; Tick(occlusion);
        Check(!IntercomOcclusionFade.Active.ContainsKey(warehouse), "external visibility owner remains respected");
        warehouse.forceRenderingOff = false;

        marker.fadeWholeHierarchy = true; occlusion.RefreshScenery(); Tick(occlusion, 30);
        Check(Count(occlusion, "entries") == 1, "explicit single-building grouping can be enabled");
        Check(IntercomOcclusionFade.Active.ContainsKey(sibling), "grouping requires explicit opt-in");
        sibling.gameObject.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
        occlusion.RefreshScenery(); Tick(occlusion, 30);
        Check(!IntercomOcclusionFade.Active.ContainsKey(sibling), "nested do-not-fade marker overrides grouping");
        marker.fadeWholeHierarchy = false;

        var unreadableMesh = Wall(); unreadableMesh.isReadable = false;
        var unreadable = Part("Unimported readable data", group.transform, unreadableMesh, new Vector3(0, 0, 4));
        occlusion.RefreshScenery(); Tick(occlusion, 30);
        Check(!IntercomOcclusionFade.Active.ContainsKey(unreadable) && unreadableMesh.vertexReads == 0, "unreadable geometry stays solid without reading CPU mesh data");
        camera.enabled = false; Tick(occlusion);
        Check(IntercomOcclusionFade.Active.Count == 0, "disabled camera restores all leases");
        camera.enabled = true; camera.orthographic = false; Tick(occlusion, 30);
        Check(IntercomOcclusionFade.Active.ContainsKey(warehouse), "perspective rays also cover real foreground");
        Check(!IntercomOcclusionFade.Active.ContainsKey(behind), "perspective rays remain finite");
        Call(occlusion, "OnDisable");
        Check(IntercomOcclusionFade.Active.Count == 0, "camera disable releases active fades");
        Check(Count(occlusion, "entries") == 0 && Count(occlusion, "geometryCache") == 0, "camera disable frees entries and cached geometry");
        Console.WriteLine($"PASS {assertions} camera integration assertions against Unity fakes. Complete production component compiled; not a Unity renderer or Play Mode test.");
    }
}
