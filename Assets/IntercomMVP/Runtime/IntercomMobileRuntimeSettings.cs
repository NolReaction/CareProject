using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Landscape and frame pacing for the mobile prototype scene only.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomMobileRuntimeSettings : MonoBehaviour
    {
        [Range(30, 120)] public int targetFrameRate = 60;

        private void OnEnable() { Apply(); }
        private void OnApplicationPause(bool paused) { if (!paused) Apply(); }
        private void OnApplicationFocus(bool focused) { if (focused) Apply(); }

        private void Apply()
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 120);
        }
    }
}
