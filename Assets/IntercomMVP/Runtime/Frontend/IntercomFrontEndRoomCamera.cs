using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Optional dedicated 3D hub camera. It never repositions the gameplay camera or touches character animation.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomFrontEndRoomCamera : MonoBehaviour
    {
        public Camera roomCamera;
        public Camera gameplayCamera;
        public Transform homeAnchor;
        public Transform playAnchor;
        public Transform robotAnchor;
        public Transform armoryAnchor;
        [Min(0.1f)] public float travelSharpness = 5f;
        private Transform target;
        private Camera activeRoomCamera, activeGameplayCamera;
        private bool shown, gameplayWasEnabled, roomWasEnabled;
        private Vector3 savedPosition;
        private Quaternion savedRotation;

        public bool IsShown => shown;
        public void Show(IntercomFrontEndSection section)
        {
            if (shown && (roomCamera != activeRoomCamera || gameplayCamera != activeGameplayCamera)) Hide();
            if (!isActiveAndEnabled || roomCamera == null || roomCamera == gameplayCamera) { Hide(); return; }
            if (!IntercomFrontEndState.Valid(section)) return;
            Transform next = section == IntercomFrontEndSection.Robot ? robotAnchor
                : section == IntercomFrontEndSection.Armory ? armoryAnchor
                : section == IntercomFrontEndSection.Play ? playAnchor : homeAnchor;
            if (next == null) next = homeAnchor;
            if (next == null || !Finite(next.position) || !Finite(next.rotation)) { Hide(); return; }
            target = next;
            if (!shown)
            {
                activeRoomCamera = roomCamera; activeGameplayCamera = gameplayCamera;
                savedPosition = roomCamera.transform.position; savedRotation = roomCamera.transform.rotation;
                roomWasEnabled = roomCamera.enabled;
                gameplayWasEnabled = gameplayCamera != null && gameplayCamera.enabled;
                roomCamera.transform.SetPositionAndRotation(target.position,target.rotation);
                shown = true;
            }
            roomCamera.enabled = true;
            if (gameplayCamera != null) gameplayCamera.enabled = false;
        }
        public void Hide()
        {
            if (!shown) return;
            if (activeRoomCamera != null)
            { activeRoomCamera.enabled = roomWasEnabled; activeRoomCamera.transform.SetPositionAndRotation(savedPosition,savedRotation); }
            if (activeGameplayCamera != null) activeGameplayCamera.enabled = gameplayWasEnabled;
            activeRoomCamera = activeGameplayCamera = null;
            target = null; shown = false;
        }
        private void LateUpdate()
        {
            if (shown && (roomCamera != activeRoomCamera || gameplayCamera != activeGameplayCamera)) { Hide(); return; }
            if (!shown) return;
            if (roomCamera == null || target == null || !Finite(target.position) || !Finite(target.rotation)) { Hide(); return; }
            float sharpness = float.IsNaN(travelSharpness) || float.IsInfinity(travelSharpness) ? 5f : Mathf.Max(.1f,travelSharpness);
            // A long import/focus hitch must not throw the camera through the rooms in one frame.
            float delta = Time.unscaledDeltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f) delta = 0f;
            if (delta > .05f) delta = .05f;
            float step = 1f-Mathf.Exp(-sharpness*delta);
            roomCamera.transform.SetPositionAndRotation(Vector3.Lerp(roomCamera.transform.position,target.position,step),
                Quaternion.Slerp(roomCamera.transform.rotation,target.rotation,step));
        }
        private void OnDisable() { Hide(); }
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
