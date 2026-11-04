using System;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Local front end navigation. Mission state, damage and purchases remain owned by their own systems.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-180)]
    public sealed class IntercomFrontEnd : MonoBehaviour, IIntercomMenuInput
    {
        public const string BuildVersion = "V19";
        [Header("Current game")]
        public IntercomMissionDirector mission;
        public IntercomPlayerInput input;
        public IntercomWeaponDefinition weapon;
        public IntercomHealth playerHealth;
        public IntercomPlayerMotor playerMotor;
        [Header("Presentation")]
        public string gameTitle = "ЗАБОТА";
        public Texture2D heroPortrait;
        public Texture2D weaponPortrait;
        [Tooltip("Optional transparent cartoon illustration for the home job card.")]
        public Texture2D homeDoodle;
        [Tooltip("Optional transparent cartoon illustration for the rescue job ticket.")]
        public Texture2D rescueDoodle;
        [Tooltip("Optional dedicated room camera. Leave empty to use the tutorial yard as the menu backdrop.")]
        public IntercomFrontEndRoomCamera roomCamera;

        public bool HasRoomScene => roomCamera != null && roomCamera.IsShown;

        public bool IsVisible => isActiveAndEnabled && mission != null && mission.isActiveAndEnabled && mission.BlocksGameplay;
        public IntercomFrontEndSection Section => state.CurrentSection;
        public bool IsResult => mission != null && (mission.Phase == IntercomMissionPhase.Victory || mission.Phase == IntercomMissionPhase.Defeat);
        public event Action<IntercomFrontEndSection> SectionChanged;

        private readonly IntercomFrontEndState state = new IntercomFrontEndState();
        private readonly IntercomFrontEndView view = new IntercomFrontEndView();
        private IntercomPlayerInput registeredInput;
        private IntercomFrontEndRoomCamera registeredRoomCamera;
        private IntercomMissionPhase observedPhase;
        private bool observedVisible, observedPhaseReady;
        private string notice = "";
        public string Notice => notice;

        private void OnEnable() { BindInput(); SyncState(); }
        private void Update() { BindInput(); SyncState(); }
        private void OnDisable()
        {
            if (registeredInput != null) registeredInput.UnregisterFrontEnd(this);
            registeredInput = null;
            ReleaseRoomCamera();
            observedVisible = false;
        }
        private void OnDestroy() { view.Dispose(); }

        private void BindInput()
        {
            if (registeredInput == input) return;
            if (registeredInput != null) registeredInput.UnregisterFrontEnd(this);
            registeredInput = null;
            if (input != null && input.RegisterFrontEnd(this)) registeredInput = input;
        }
        private void SyncState()
        {
            if (mission == null) observedPhaseReady = false;
            else if (!observedPhaseReady || observedPhase != mission.Phase)
            {
                observedPhaseReady = true; observedPhase = mission.Phase;
                if (mission.Phase != IntercomMissionPhase.Active)
                { state.Select(IntercomFrontEndSection.Home); notice = ""; }
            }
            bool visible = IsVisible;
            if (visible != observedVisible)
            {
                observedVisible = visible;
                if (input != null) input.CancelUntilRelease();
            }
            if (!visible) ReleaseRoomCamera();
            else if (registeredRoomCamera != roomCamera || (registeredRoomCamera != null && !registeredRoomCamera.IsShown)) ShowRoom(Section);
        }

        private void ShowRoom(IntercomFrontEndSection section)
        {
            if (registeredRoomCamera != roomCamera)
            {
                ReleaseRoomCamera();
                registeredRoomCamera = roomCamera;
            }
            if (registeredRoomCamera != null) registeredRoomCamera.Show(section);
        }

        private void ReleaseRoomCamera()
        {
            if (registeredRoomCamera != null) registeredRoomCamera.Hide();
            registeredRoomCamera = null;
        }

        public bool Navigate(IntercomFrontEndSection section)
        {
            if (!IsVisible || !IntercomFrontEndState.Valid(section)) return false;
            if (IsResult) { mission.ReturnToSelection(); observedPhase=mission.Phase; observedPhaseReady=true; }
            bool changed = state.Select(section);
            notice = "";
            ShowRoom(section);
            if (changed) SectionChanged?.Invoke(section);
            return true;
        }

        public void Back()
        {
            if (!IsVisible) return;
            if (IsResult) { mission.ReturnToSelection(); observedPhase=mission.Phase; observedPhaseReady=true; }
            bool changed = state.Back(); notice = "";
            ShowRoom(Section);
            if (changed) SectionChanged?.Invoke(Section);
        }

        public bool PointerDown(float x, float y)
        {
            if (!IsVisible) return false;
            IntercomFrontEndLayout layout = CurrentLayout();
            IntercomFrontEndAction action = layout.Hit(IsResult ? IntercomFrontEndSection.Home : Section,x,y);
            switch (action)
            {
                case IntercomFrontEndAction.Home: return Navigate(IntercomFrontEndSection.Home);
                case IntercomFrontEndAction.Play: return Navigate(IntercomFrontEndSection.Play);
                case IntercomFrontEndAction.Robot: return Navigate(IntercomFrontEndSection.Robot);
                case IntercomFrontEndAction.Armory: return Navigate(IntercomFrontEndSection.Armory);
                case IntercomFrontEndAction.SelectElimination: return StartRun(IntercomMissionMode.Elimination);
                case IntercomFrontEndAction.SelectRescue: return StartRun(IntercomMissionMode.Rescue);
                case IntercomFrontEndAction.SelectFreePlay: return StartRun(IntercomMissionMode.FreePlay);
                case IntercomFrontEndAction.Primary:
                    if (IsResult)
                    {
                        bool started = mission.RetryMission();
                        notice = started ? "" : mission.ResultText; SyncState(); return true;
                    }
                    return Navigate(IntercomFrontEndSection.Play);
                case IntercomFrontEndAction.Secondary:
                    if (IsResult) { Back(); return true; }
                    return Navigate(Section == IntercomFrontEndSection.Robot ? IntercomFrontEndSection.Armory : IntercomFrontEndSection.Robot);
                default: return false;
            }
        }
        private bool StartRun(IntercomMissionMode mode)
        {
            bool started = mission.StartMission(mode);
            notice = started ? "" : mission.ResultText;
            SyncState(); return true;
        }
        public IntercomFrontEndLayout CurrentLayout()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f,0f,Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            return IntercomFrontEndLayout.Calculate(safe.x,safe.y,safe.width,safe.height);
        }
        private void OnGUI()
        {
            if (!IsVisible || Event.current == null || Event.current.type != EventType.Repaint) return;
            Color previousColor = GUI.color; Matrix4x4 previousMatrix = GUI.matrix; int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -80;
            try { view.Draw(this,CurrentLayout()); }
            finally { GUI.color = previousColor; GUI.matrix = previousMatrix; GUI.depth = previousDepth; }
        }
    }
}
