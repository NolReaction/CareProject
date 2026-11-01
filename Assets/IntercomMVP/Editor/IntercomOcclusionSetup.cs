using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IntercomMvp.Editor
{
    internal static class IntercomOcclusionSetup
    {
        internal static void Setup(GameObject robot, Camera camera)
        {
            if (robot == null || camera == null) return;
            IntercomCameraOcclusion occlusion = camera.GetComponent<IntercomCameraOcclusion>();
            if (occlusion == null) occlusion = Undo.AddComponent<IntercomCameraOcclusion>(camera.gameObject);
            Undo.RecordObject(occlusion, "Connect player visibility");
            occlusion.target = robot.transform;
            occlusion.enabled = true;
            EditorUtility.SetDirty(occlusion);
            if (PrefabUtility.IsPartOfPrefabInstance(occlusion)) PrefabUtility.RecordPrefabInstancePropertyModifications(occlusion);

            Scene scene = robot.scene;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == robot || root == camera.gameObject) continue;
                if (root.name == IntercomPrototypeEnvironment.RootName)
                {
                    foreach (Transform group in root.transform)
                    {
                        if (group.name == "Ground surround" || group.name == "Ground markings") Mark(group.gameObject, false);
                        else if (group.name == "Arena boundary")
                            foreach (Transform wall in group) Mark(wall.gameObject, true);
                        else Mark(group.gameObject, true);
                    }
                }
                else if (root.name == "Ground - Future Map") Mark(root, false);
            }
        }

        private static void Mark(GameObject item, bool hide)
        {
            // User markers, including an intentional exclusion, are preserved.
            if (item.GetComponent<IntercomOccluder>() != null) return;
            IntercomOccluder marker = Undo.AddComponent<IntercomOccluder>(item);
            Undo.RecordObject(marker, "Group building visibility");
            marker.hideWhenBlocking = hide;
            EditorUtility.SetDirty(marker);
            if (PrefabUtility.IsPartOfPrefabInstance(marker)) PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
        }
    }
}
