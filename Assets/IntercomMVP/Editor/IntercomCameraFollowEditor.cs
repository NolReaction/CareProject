using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IntercomMvp.Editor
{
    [CustomEditor(typeof(IntercomCameraFollow))]
    internal sealed class IntercomCameraFollowEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            IntercomCameraFollow follow = (IntercomCameraFollow)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck() && !Application.isPlaying && follow.target != null)
                Apply(follow, "Preview Intercom camera framing");

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Выстрой ракурс в Scene и нажми Capture Scene View. Либо передвинь Main Camera и нажми Capture Current Camera. Сохрани сцену Ctrl+S. Захват сохраняет наклон, масштаб, проекцию и смещение кадра. Ширина кадра зависит от формата окна Game.", MessageType.Info);
            using (new EditorGUI.DisabledScope(follow.target == null))
            {
                if (GUILayout.Button("Capture Current Camera"))
                    Capture(follow, follow.GetComponent<Camera>());
                using (new EditorGUI.DisabledScope(SceneView.lastActiveSceneView == null || SceneView.lastActiveSceneView.in2DMode))
                {
                    if (GUILayout.Button("Capture Scene View"))
                        Capture(follow, SceneView.lastActiveSceneView.camera);
                }
                if (GUILayout.Button("Apply Follow Settings")) Apply(follow, "Apply Intercom camera framing");
            }
            if (follow.target == null)
                EditorGUILayout.HelpBox("Назначь Robot в Target, чтобы захватить ракурс относительно персонажа.", MessageType.Warning);
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Обычные изменения Play Mode временные. После настройки нажми кнопку ниже; при остановке Play параметры вернутся в сцену с поддержкой Undo. Затем Ctrl+S.", MessageType.Info);
                if (GUILayout.Button("Keep Follow Settings After Play"))
                    IntercomCameraPlayFraming.Remember(follow);
            }
        }

        private static void Capture(IntercomCameraFollow follow, Camera source)
        {
            if (source == null || follow.target == null) return;
            Undo.RecordObjects(new UnityEngine.Object[] { follow, follow.transform, follow.GetComponent<Camera>() }, "Capture Intercom camera framing");
            if (!follow.CaptureFromCamera(source)) return;
            follow.MarkFramingAuthored();
            follow.enabled = true;
            follow.SnapToTarget();
            MarkChanged(follow);
        }

        internal static void Apply(IntercomCameraFollow follow, string name)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { follow, follow.transform, follow.GetComponent<Camera>() }, name);
            follow.MarkFramingAuthored();
            follow.enabled = true;
            follow.SnapToTarget();
            MarkChanged(follow);
        }

        internal static void MarkChanged(IntercomCameraFollow follow)
        {
            foreach (UnityEngine.Object item in new UnityEngine.Object[] { follow, follow.transform, follow.GetComponent<Camera>() })
            {
                EditorUtility.SetDirty(item);
                if (PrefabUtility.IsPartOfPrefabInstance(item))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            if (!Application.isPlaying && follow.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(follow.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    /// <summary>Explicit opt-in transfer of framing only. No automatic scene saving or object lookup by name.</summary>
    [InitializeOnLoad]
    internal static class IntercomCameraPlayFraming
    {
        private const string IdentityKey = "IntercomMvp.CameraFraming.EditIdentities";
        private const string PendingKey = "IntercomMvp.CameraFraming.AfterPlay";

        [Serializable] private sealed class Identity
        {
            public string hierarchyKey;
            public string globalId;
        }
        [Serializable] private sealed class IdentityList { public List<Identity> items = new List<Identity>(); }
        [Serializable] private sealed class Framing
        {
            public string globalId;
            public float pitch, yaw, roll, orthographicSize, fieldOfView, distance, groundHeight, focusHeight;
            public bool orthographic, clampToGroundBounds;
            public Vector3 framingOffset;
            public Vector2 groundMinimum, groundMaximum;
        }

        static IntercomCameraPlayFraming()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            // Domain reloads recreate this class; SessionState preserves the deliberate pending request.
            if (!EditorApplication.isPlayingOrWillChangePlaymode
                && !string.IsNullOrEmpty(SessionState.GetString(PendingKey, string.Empty)))
                EditorApplication.delayCall += ApplyPending;
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SessionState.EraseString(PendingKey);
                var identities = new IdentityList();
                foreach (IntercomCameraFollow follow in Resources.FindObjectsOfTypeAll<IntercomCameraFollow>())
                {
                    if (EditorUtility.IsPersistent(follow) || !follow.gameObject.scene.IsValid()
                        || string.IsNullOrEmpty(follow.gameObject.scene.path)) continue;
                    identities.items.Add(new Identity
                    {
                        hierarchyKey = HierarchyKey(follow),
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(follow).ToString()
                    });
                }
                SessionState.SetString(IdentityKey, JsonUtility.ToJson(identities));
            }
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ApplyPending;
        }

        internal static void Remember(IntercomCameraFollow follow)
        {
            if (!Application.isPlaying) return;
            IdentityList identities = JsonUtility.FromJson<IdentityList>(SessionState.GetString(IdentityKey, "{}"));
            string key = HierarchyKey(follow);
            Identity identity = identities?.items?.FirstOrDefault(item => item.hierarchyKey == key);
            if (identity == null)
            {
                Debug.LogWarning("Ракурс пока не сохранён: камера должна существовать в сохранённой сцене до запуска Play. Останови Play, сохрани сцену и повтори. Камеру, созданную или перенесённую в иерархии во время Play, автоматически сопоставлять нельзя.", follow);
                return;
            }
            var framing = new Framing
            {
                globalId = identity.globalId,
                pitch = follow.pitch, yaw = follow.yaw, roll = follow.roll,
                orthographic = follow.orthographic,
                orthographicSize = follow.orthographicSize, fieldOfView = follow.fieldOfView,
                distance = follow.distance, groundHeight = follow.groundHeight, focusHeight = follow.focusHeight,
                framingOffset = follow.framingOffset, clampToGroundBounds = follow.clampToGroundBounds,
                groundMinimum = follow.groundMinimum, groundMaximum = follow.groundMaximum
            };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(framing));
            Debug.Log("Ракурс запомнен. Останови Play: параметры применятся к исходной камере, после чего сохрани сцену Ctrl+S. Последующие изменения потребуют повторного нажатия Keep Follow Settings After Play.", follow);
        }

        private static void ApplyPending()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            // Consume once before resolving; a removed camera or closed scene cannot leave a stale request.
            SessionState.EraseString(PendingKey);
            SessionState.EraseString(IdentityKey);
            Framing framing = JsonUtility.FromJson<Framing>(json);
            if (framing == null || !GlobalObjectId.TryParse(framing.globalId, out GlobalObjectId id)) return;
            IntercomCameraFollow follow = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as IntercomCameraFollow;
            if (follow == null || !follow.gameObject.scene.IsValid())
            {
                Debug.LogWarning("Запомненный ракурс не применён: исходная камера удалена или её сцена закрыта. Другие камеры не изменялись.");
                return;
            }
            Undo.RecordObjects(new UnityEngine.Object[] { follow, follow.transform, follow.GetComponent<Camera>() }, "Keep Intercom camera framing after Play");
            follow.pitch = framing.pitch; follow.yaw = framing.yaw; follow.roll = framing.roll;
            follow.orthographic = framing.orthographic;
            follow.orthographicSize = framing.orthographicSize; follow.fieldOfView = framing.fieldOfView;
            follow.distance = framing.distance; follow.groundHeight = framing.groundHeight; follow.focusHeight = framing.focusHeight;
            follow.framingOffset = framing.framingOffset; follow.clampToGroundBounds = framing.clampToGroundBounds;
            follow.groundMinimum = framing.groundMinimum; follow.groundMaximum = framing.groundMaximum;
            follow.MarkFramingAuthored();
            follow.enabled = true;
            follow.SnapToTarget();
            IntercomCameraFollowEditor.MarkChanged(follow);
            Debug.Log("Ракурс перенесён из Play в сцену. Сохрани сцену Ctrl+S. Для отмены доступен Undo.", follow);
        }

        private static string HierarchyKey(IntercomCameraFollow follow)
        {
            var parts = new List<string>();
            for (Transform node = follow.transform; node != null; node = node.parent)
                parts.Add(node.GetSiblingIndex() + ":" + node.name.Length + ":" + node.name);
            parts.Reverse();
            return follow.gameObject.scene.path + "|" + string.Join("/", parts);
        }
    }
}
