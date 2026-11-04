using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace IntercomMvp
{
    /// <summary>Front end input is routed through the same cancellation gate as the game sticks.</summary>
    public interface IIntercomMenuInput
    {
        bool IsVisible { get; }
        bool PointerDown(float x, float y);
        void Back();
    }

    /// <summary>Two sticks plus a separate reload button. Input is sampled before magazine and motor.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class IntercomPlayerInput : MonoBehaviour
    {
        [Header("References")]
        public Camera movementCamera;
        public Transform target;
        public IntercomPlayerVitality vitality;
        public IntercomMissionDirector mission;
        [Header("Two sticks")]
        [Range(0f, 0.8f)] public float movementDeadZone = 0.12f;
        [Range(0f, 0.8f)] public float aimDeadZone = 0.2f;
        [Range(48f, 110f)] public float stickRadiusAt720p = 70f;
        [Range(16f, 80f)] public float edgeMarginAt720p = 32f;
        public bool showOnDesktop = true;
        [Range(0.2f, 1f)] public float controlsOpacity = 0.85f;
        [Header("HUD")]
        public Texture2D weaponHudPortrait;

        public Vector2 Move { get; private set; }
        public Vector2 Aim { get; private set; }
        public bool AimActive { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool TouchActive { get; private set; }
        /// <summary>System cancellation epoch. Normal stick release does not invalidate a queued tap.</summary>
        public uint CancellationVersion { get; private set; }
        public bool ControlsVisible => Application.isMobilePlatform || showOnDesktop;
        public IIntercomMenuInput FrontEnd { get; private set; }
        public bool GameplayPaused { get; private set; }
        public bool ConfirmingExit { get; private set; }
        public bool ConfirmingRestart { get; private set; }
        private float savedTimeScale = 1f;
        private uint pausedRunRevision;

        /// <summary>Owns only the scale it stopped. External pause owners are never resumed here.</summary>
        public bool Pause()
        {
            if (GameplayPaused || ActiveMission == null || ActiveMission.Phase != IntercomMissionPhase.Active
                || (vitality != null && vitality.IsDead) || !focused || paused
                || Time.timeScale <= 0f || float.IsNaN(Time.timeScale) || float.IsInfinity(Time.timeScale)) return false;
            savedTimeScale = Time.timeScale;
            pausedRunRevision = ActiveMission.RunRevision;
            GameplayPaused = true; ConfirmingExit = ConfirmingRestart = false; Time.timeScale = 0f;
            CancelUntilRelease(); return true;
        }

        public void Resume()
        {
            if (!GameplayPaused) return;
            GameplayPaused = false; ConfirmingExit = ConfirmingRestart = false;
            if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
            CancelUntilRelease();
        }

        public bool RegisterFrontEnd(IIntercomMenuInput menu)
        {
            if (menu == null || (FrontEnd != null && !ReferenceEquals(FrontEnd, menu))) return false;
            FrontEnd = menu; CancelUntilRelease(); return true;
        }
        public void UnregisterFrontEnd(IIntercomMenuInput menu)
        {
            if (!ReferenceEquals(FrontEnd, menu)) return;
            FrontEnd = null; CancelUntilRelease();
        }

        private const int MousePointerId = int.MinValue;
        private readonly IntercomTwinStickState sticks = new IntercomTwinStickState();
        private readonly IntercomReloadControl reloadControl = new IntercomReloadControl();
        private readonly IntercomReloadControl retryControl = new IntercomReloadControl();
        private IntercomWeaponMagazine magazine;
        private Rect reloadButton;
        private readonly IntercomWorkshopHud hud = new IntercomWorkshopHud();
        private IntercomHudLayout hudLayout;
        private CharacterController targetCapsule;
        private Transform cachedTarget;
        private IntercomMissionDirector observedMission;
        private IntercomMissionDirector ActiveMission => mission != null && mission.isActiveAndEnabled ? mission : null;
        private Vector2 leftCenter, rightCenter;
        private float radius;
        private bool focused = true, paused, requireRelease, wasFireHeld, hasLayout, observedFrontEndVisible, mouseHudPointer;
        private ScreenOrientation layoutOrientation;
        private Rect layoutSafeArea;
        private int layoutWidth, layoutHeight;
#if ENABLE_INPUT_SYSTEM
        private bool enhancedTouchEnabled;
#endif

        private void Awake()
        {
            if (movementCamera == null) movementCamera = Camera.main;
            if (target == null) target = transform;
            magazine = GetComponent<IntercomWeaponMagazine>();
            if(vitality == null) vitality = GetComponent<IntercomPlayerVitality>();
            RefreshLayout();
        }

        private void OnEnable()
        {
            // A disabled component can miss the OS resume callback.
            focused = Application.isFocused; paused = false;
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
            enhancedTouchEnabled = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = true;
#endif
            CancelUntilRelease();
        }

        private void OnDisable()
        {
            Resume();
            CancelUntilRelease();
#if ENABLE_INPUT_SYSTEM
            if (enhancedTouchEnabled) EnhancedTouchSupport.Disable();
            enhancedTouchEnabled = false;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            // A finger/button still down after app switching must not restart a shot.
            CancelUntilRelease();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            CancelUntilRelease();
        }

        /// <summary>Can also be called by respawn or a future pause menu.</summary>
        public void ClearInput()
        {
            sticks.Reset();
            reloadControl.Reset();
            retryControl.Reset();
            Move = Aim = Vector2.zero;
            AimActive = FireHeld = FirePressed = ResetPressed = ReloadPressed = TouchActive = wasFireHeld = mouseHudPointer = false;
        }

        /// <summary>Respawn/pause gate: held firing controls must be released before shots resume.</summary>
        public void CancelUntilRelease()
        {
            ClearInput();
            unchecked { CancellationVersion++; }
            requireRelease = true;
        }

        private void Update()
        {
            RefreshLayout();
            if(observedMission != ActiveMission)
            {
                Resume();
                observedMission=ActiveMission;
                CancelUntilRelease();
            }
            if (GameplayPaused && (ActiveMission == null || ActiveMission.Phase != IntercomMissionPhase.Active
                || ActiveMission.RunRevision != pausedRunRevision || (vitality != null && vitality.IsDead))) Resume();
            ReloadPressed = false;
            if (!focused || paused) { CancelUntilRelease(); return; }
            if (Time.timeScale <= 0f && !GameplayPaused) { CancelUntilRelease(); return; }
            Vector2 keyboard = ReadKeyboard();
            ReadMouse(out Vector2 mousePosition, out bool mouseHeld, out bool mouseDown, out bool mouseUp);
            bool spaceHeld = SpaceHeld();
            int touchCount = TouchCount();
            TouchActive = touchCount > 0;
            if (GameplayPaused)
            { UpdatePauseInput(touchCount,mousePosition,mouseHeld,mouseDown,spaceHeld); return; }
            bool menuVisible = FrontEnd != null && FrontEnd.IsVisible;
            if (menuVisible != observedFrontEndVisible)
            { observedFrontEndVisible = menuVisible; CancelUntilRelease(); }
            if (menuVisible)
            { UpdateFrontEndInput(touchCount,mousePosition,mouseHeld,mouseDown,spaceHeld); return; }
            if(UpdateMissionInput(touchCount,mousePosition,mouseHeld,mouseDown,spaceHeld,keyboard)) return;
            if(ActiveMission == null && vitality != null && vitality.IsDead)
            {
                UpdateDefeatInput(touchCount, mousePosition, mouseHeld, mouseDown, spaceHeld);
                return;
            }
            if (requireRelease)
            {
                ClearInput();
                // Movement keys are independent of the firing/menu pointer.
                // Holding W through focus or a mode change must not keep the
                // whole character locked until a neutral keyboard frame.
                Move = vitality != null && vitality.IsDead ? Vector2.zero : keyboard;
                if (touchCount == 0 && !mouseHeld && !spaceHeld) requireRelease = false;
                return;
            }

            sticks.BeginFrame();
            if (!PollTouches()) return;
            // Touch can generate emulated mouse input in Legacy. Never treat that as a shot.
            bool allowMouse = !Application.isMobilePlatform && touchCount == 0;
            if (allowMouse)
            {
                if (mouseDown)
                {
                    mouseHudPointer=PassiveHudContains(mousePosition.x,mousePosition.y);
                    if(mouseHudPointer) sticks.PointerUp(MousePointerId);
                    else if (magazine != null && reloadControl.PointerDown(MousePointerId, mousePosition.x, mousePosition.y)) ReloadPressed = true;
                    else if (ControlsVisible) sticks.PointerDown(MousePointerId, mousePosition.x, mousePosition.y);
                }
                if (mouseHeld && !mouseHudPointer && !reloadControl.Owns(MousePointerId)) sticks.PointerMove(MousePointerId, mousePosition.x, mousePosition.y);
                if (mouseUp || !mouseHeld) { sticks.PointerUp(MousePointerId); reloadControl.PointerUp(MousePointerId); mouseHudPointer=false; }
            }
            else sticks.PointerUp(MousePointerId);
            if (sticks.EndFrame()) { CancelUntilRelease(); return; }

            Move = sticks.Left.Captured ? new Vector2(sticks.Left.X, sticks.Left.Y) : keyboard;
            Aim = new Vector2(sticks.Right.X, sticks.Right.Y);
            AimActive = Aim.sqrMagnitude > 0.000001f;
            bool mouseOwnsStick = sticks.Owns(MousePointerId);
            bool desktopFire = allowMouse && !mouseHudPointer && !mouseOwnsStick && !reloadControl.Owns(MousePointerId) && (mouseHeld || spaceHeld);
            if (!AimActive && desktopFire)
            {
                Aim = MouseAim(mousePosition);
                AimActive = Aim.sqrMagnitude > 0.000001f;
            }
            if (AimActive) Aim.Normalize();
            FireHeld = AimActive && (sticks.Right.Active || desktopFire);
            FirePressed = FireHeld && !wasFireHeld;
            wasFireHeld = FireHeld;
            ResetPressed = ReadResetPressed();
            ReloadPressed |= ReadReloadPressed();
        }

        private bool PassiveHudContains(float x,float y)
        {
            // A press that starts on an instrument belongs to the UI for its
            // whole lifetime, even when dragged over the playable world.
            if(vitality!=null && vitality.Health!=null)
            { if(hudLayout.Health.Contains(x,y)) return true; }
            else if(hudLayout.ShowAreaBadge && hudLayout.AreaBadge.Contains(x,y)) return true;
            IntercomMissionDirector active=ActiveMission;
            return active!=null && !active.BlocksGameplay
                && (hudLayout.MissionObjective.Contains(x,y) || hudLayout.Timer.Contains(x,y)
                    || (hudLayout.Toast.Contains(x,y) && !string.IsNullOrEmpty(hud.SessionToast(active))));
        }

        private void UpdatePauseInput(int touchCount, Vector2 mousePosition, bool mouseHeld,
            bool mouseDown, bool spaceHeld)
        {
            ClearInput();
            if (requireRelease)
            {
                if (touchCount == 0 && !mouseHeld && !spaceHeld) requireRelease = false;
                return;
            }
            if (ReadModesPressed())
            {
                if (ConfirmingExit || ConfirmingRestart) { ConfirmingExit = ConfirmingRestart = false; CancelUntilRelease(); }
                else Resume();
                return;
            }
            if (!Application.isMobilePlatform && touchCount == 0 && mouseDown)
            { ApplyPausePress(mousePosition.x,mousePosition.y); return; }
#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            for (int i=0;i<touches.Count;i++)
            {
                var touch=touches[i];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) { CancelUntilRelease(); return; }
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                { ApplyPausePress(touch.screenPosition.x,touch.screenPosition.y); return; }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i=0;i<Input.touchCount;i++)
            {
                Touch touch=Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return; }
                if (touch.phase == TouchPhase.Began)
                { ApplyPausePress(touch.position.x,touch.position.y); return; }
            }
#endif
        }

        private void ApplyPausePress(float x,float y)
        {
            if (ConfirmingExit || ConfirmingRestart)
            {
                if (hudLayout.PauseConfirm.Contains(x,y))
                {
                    bool restart=ConfirmingRestart;Resume();
                    if (ActiveMission != null) { if (restart) mission.RetryMission(); else mission.ReturnToSelection(); }
                }
                else if (hudLayout.PauseCancel.Contains(x,y)) ConfirmingExit = ConfirmingRestart = false;
            }
            else if (hudLayout.PauseResume.Contains(x,y)) Resume();
            else if (hudLayout.PauseRetry.Contains(x,y))
                ConfirmingRestart = true;
            else if (hudLayout.PauseExit.Contains(x,y)) ConfirmingExit = true;
            CancelUntilRelease();
        }

        private void UpdateFrontEndInput(int touchCount, Vector2 mousePosition, bool mouseHeld,
            bool mouseDown, bool spaceHeld)
        {
            ClearInput();
            if (requireRelease)
            {
                if (touchCount == 0 && !mouseHeld && !spaceHeld) requireRelease = false;
                return;
            }
            if (ReadModesPressed() || ReadResetPressed())
            { FrontEnd.Back(); CancelUntilRelease(); return; }
            if (!Application.isMobilePlatform && touchCount == 0 && mouseDown)
            { FrontEnd.PointerDown(mousePosition.x,mousePosition.y); CancelUntilRelease(); return; }
#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            for (int i=0; i<touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                { CancelUntilRelease(); return; }
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                { FrontEnd.PointerDown(touch.screenPosition.x,touch.screenPosition.y); CancelUntilRelease(); return; }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i=0; i<Input.touchCount; i++)
            {
                Touch touch=Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return; }
                if (touch.phase == TouchPhase.Began)
                { FrontEnd.PointerDown(touch.position.x,touch.position.y); CancelUntilRelease(); return; }
            }
#endif
        }

        private bool UpdateMissionInput(int touchCount, Vector2 mousePosition, bool mouseHeld,
            bool mouseDown, bool spaceHeld, Vector2 keyboard)
        {
            if(ActiveMission == null) return false;
            bool modal = mission.BlocksGameplay;
            if(modal) ClearInput();
            // The entire press that opened/closed a menu is consumed. A finger
            // carried from the result screen cannot become a stick or a shot.
            if(requireRelease)
            {
                if(modal && touchCount == 0 && !mouseHeld && !spaceHeld)
                    requireRelease=false;
                return modal;
            }
            if(ReadModesPressed() && mission.Phase != IntercomMissionPhase.Selection)
            {
                if (mission.Phase == IntercomMissionPhase.Active) Pause();
                else mission.ReturnToSelection();
                CancelUntilRelease(); return true;
            }
            if(ReadResetPressed() && mission.Phase != IntercomMissionPhase.Selection)
            {
                if (mission.Phase == IntercomMissionPhase.Active)
                { if (Pause()) ConfirmingRestart = true; }
                else mission.RetryMission();
                CancelUntilRelease(); return true;
            }
            if(!Application.isMobilePlatform && touchCount == 0 && mouseDown
                && ApplyMissionPress(mousePosition.x,mousePosition.y)) return true;
#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            for(int i=0;i<touches.Count;i++)
            {
                var touch=touches[i];
                if(touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                { CancelUntilRelease(); return true; }
                if(touch.phase == UnityEngine.InputSystem.TouchPhase.Began
                    && ApplyMissionPress(touch.screenPosition.x,touch.screenPosition.y)) return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for(int i=0;i<Input.touchCount;i++)
            {
                Touch touch=Input.GetTouch(i);
                if(touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return true; }
                if(touch.phase == TouchPhase.Began && ApplyMissionPress(touch.position.x,touch.position.y)) return true;
            }
#endif
            // Death events can be observed by Input one frame before Director.Update.
            if(!modal && vitality != null && vitality.IsDead) { ClearInput(); return true; }
            return modal;
        }

        private bool ApplyMissionPress(float x,float y)
        {
            bool first=hudLayout.MissionFirst.Contains(x,y), second=hudLayout.MissionSecond.Contains(x,y);
            bool third=hudLayout.MissionThird.Contains(x,y);
            if(mission.Phase == IntercomMissionPhase.Active)
            {
                if(!hudLayout.Modes.Contains(x,y)) return false;
                Pause();
            }
            else if(mission.Phase == IntercomMissionPhase.Selection)
            {
                if(!first && !second && !third) return false;
                mission.StartMission(third ? IntercomMissionMode.FreePlay
                    : first ? IntercomMissionMode.Elimination : IntercomMissionMode.Rescue);
            }
            else
            {
                if(first) mission.RetryMission();
                else if(second) mission.ReturnToSelection();
                else if(third) mission.StartMission(IntercomMissionMode.FreePlay);
                else return false;
            }
            CancelUntilRelease(); return true;
        }

        private void UpdateDefeatInput(int touchCount, Vector2 mousePosition, bool mouseHeld, bool mouseDown, bool spaceHeld)
        {
            // All gameplay pointers are inert while dead. Retry uses only a
            // fresh press inside the same rectangle drawn by the HUD.
            ClearInput();
            if(ReadResetPressed()) { vitality.Retry(); CancelUntilRelease(); return; }
            if(requireRelease)
            {
                if(touchCount == 0 && !mouseHeld && !spaceHeld) requireRelease = false;
                return;
            }
            bool retry = !Application.isMobilePlatform && touchCount == 0 && mouseDown
                && retryControl.PointerDown(MousePointerId, mousePosition.x, mousePosition.y);
#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            for(int i=0;i<touches.Count;i++)
            {
                var touch = touches[i];
                if(touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) { CancelUntilRelease(); return; }
                if(touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    retry |= retryControl.PointerDown(touch.touchId,touch.screenPosition.x,touch.screenPosition.y);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for(int i=0;i<Input.touchCount;i++)
            {
                Touch touch = Input.GetTouch(i);
                if(touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return; }
                if(touch.phase == TouchPhase.Began)
                    retry |= retryControl.PointerDown(touch.fingerId,touch.position.x,touch.position.y);
            }
#endif
            if(retry) { vitality.Retry(); CancelUntilRelease(); }
        }

        private void RefreshLayout()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f, 0f, Screen.width, Screen.height);
            bool changedScreen = hasLayout && (layoutWidth != Screen.width || layoutHeight != Screen.height
                || layoutOrientation != Screen.orientation || layoutSafeArea != safe);
            layoutWidth = Screen.width; layoutHeight = Screen.height;
            layoutOrientation = Screen.orientation; layoutSafeArea = safe; hasLayout = true;
            if (changedScreen) CancelUntilRelease();
            hudLayout = IntercomHudLayout.Calculate(safe.x, safe.y, safe.width, safe.height,
                stickRadiusAt720p, edgeMarginAt720p);
            radius = hudLayout.Radius;
            leftCenter = new Vector2(hudLayout.LeftX, hudLayout.LeftY);
            rightCenter = new Vector2(hudLayout.RightX, hudLayout.RightY);
            sticks.Configure(leftCenter.x, leftCenter.y, rightCenter.x, rightCenter.y, radius, movementDeadZone, aimDeadZone);
            reloadButton = new Rect(hudLayout.Reload.X, hudLayout.Reload.Y, hudLayout.Reload.Width, hudLayout.Reload.Height);
            reloadControl.Configure(reloadButton.x, reloadButton.y, reloadButton.width, reloadButton.height);
            retryControl.Configure(hudLayout.Retry.X,hudLayout.Retry.Y,hudLayout.Retry.Width,hudLayout.Retry.Height);
        }

        private Vector2 MouseAim(Vector2 screenPosition)
        {
            if (movementCamera == null) movementCamera = Camera.main;
            if (movementCamera == null) return Vector2.zero;
            if (target == null) target = transform;
            if (cachedTarget != target)
            {
                cachedTarget = target;
                targetCapsule = target.GetComponent<CharacterController>();
            }
            Vector3 origin = targetCapsule != null ? target.TransformPoint(targetCapsule.center) : target.position;
            Ray ray = movementCamera.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.up, origin).Raycast(ray, out float distance)) return Vector2.zero;
            Vector3 direction = Vector3.ProjectOnPlane(ray.GetPoint(distance) - origin, Vector3.up);
            if (direction.sqrMagnitude < 0.01f) return Vector2.zero;
            direction.Normalize();
            Vector3 forward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
        }

        private bool PollTouches()
        {
#if ENABLE_INPUT_SYSTEM
            var touches = EnhancedTouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                Vector2 point = touch.screenPosition;
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) { CancelUntilRelease(); return false; }
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began && magazine != null && reloadControl.PointerDown(touch.touchId, point.x, point.y))
                { ReloadPressed = true; continue; }
                if (reloadControl.Owns(touch.touchId))
                { if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended) reloadControl.PointerUp(touch.touchId); continue; }
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began) sticks.PointerDown(touch.touchId, point.x, point.y);
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended) sticks.PointerUp(touch.touchId);
                else sticks.PointerMove(touch.touchId, point.x, point.y);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return false; }
                if (touch.phase == TouchPhase.Began && magazine != null && reloadControl.PointerDown(touch.fingerId, touch.position.x, touch.position.y))
                { ReloadPressed = true; continue; }
                if (reloadControl.Owns(touch.fingerId))
                { if (touch.phase == TouchPhase.Ended) reloadControl.PointerUp(touch.fingerId); continue; }
                if (touch.phase == TouchPhase.Began) sticks.PointerDown(touch.fingerId, touch.position.x, touch.position.y);
                else if (touch.phase == TouchPhase.Ended) sticks.PointerUp(touch.fingerId);
                else sticks.PointerMove(touch.fingerId, touch.position.x, touch.position.y);
            }
#endif
            return true;
        }

        private static int TouchCount()
        {
#if ENABLE_INPUT_SYSTEM
            return EnhancedTouch.activeTouches.Count;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.touchCount;
#else
            return 0;
#endif
        }

        private static Vector2 ReadKeyboard()
        {
            Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return input;
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            input.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            input.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
#endif
            return Vector2.ClampMagnitude(input, 1f);
        }

        private static void ReadMouse(out Vector2 position, out bool held, out bool down, out bool up)
        {
            position = Vector2.zero; held = down = up = false;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            position = mouse.position.ReadValue(); held = mouse.leftButton.isPressed;
            down = mouse.leftButton.wasPressedThisFrame; up = mouse.leftButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition; held = Input.GetMouseButton(0);
            down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0);
#endif
        }

        private static bool SpaceHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Space);
#else
            return false;
#endif
        }

        private static bool ReadResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Backspace);
#else
            return false;
#endif
        }

        private static bool ReadReloadPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private static bool ReadModesPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || !hasLayout
                || (FrontEnd != null && FrontEnd.IsVisible)) return;
            Color previousColor = GUI.color; Matrix4x4 previousMatrix = GUI.matrix; int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -50;
            hud.Draw(hudLayout, sticks, magazine, ControlsVisible, controlsOpacity, vitality, ActiveMission, FrontEnd != null,
                GameplayPaused, ConfirmingExit, ConfirmingRestart, weaponHudPortrait);
            GUI.color = previousColor; GUI.matrix = previousMatrix; GUI.depth = previousDepth;
        }

        private void OnDestroy() { Resume(); hud.Dispose(); }
    }
}
