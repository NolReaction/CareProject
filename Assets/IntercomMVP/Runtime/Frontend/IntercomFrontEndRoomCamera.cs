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
            if (!isActiveAndEnabled || roomCamera == null || roomCamera == gameplayCamera || !IntercomFrontEndState.Valid(section)) return;
            Transform next = section == IntercomFrontEndSection.Robot ? robotAnchor
                : section == IntercomFrontEndSection.Armory ? armoryAnchor
                : section == IntercomFrontEndSection.Play ? playAnchor : homeAnchor;
            if (next == null) next = homeAnchor;
            if (next == null) return;
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
            if (!shown || roomCamera == null || target == null) return;
            float sharpness = float.IsNaN(travelSharpness) || float.IsInfinity(travelSharpness) ? 5f : Mathf.Max(.1f,travelSharpness);
            float step = 1f-Mathf.Exp(-sharpness*Mathf.Max(0f,Time.unscaledDeltaTime));
            roomCamera.transform.SetPositionAndRotation(Vector3.Lerp(roomCamera.transform.position,target.position,step),
                Quaternion.Slerp(roomCamera.transform.rotation,target.rotation,step));
        }
        private void OnDisable() { Hide(); }
    }
}
