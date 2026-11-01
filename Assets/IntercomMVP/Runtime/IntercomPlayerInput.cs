using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace IntercomMvp
{
    /// <summary>Exactly two controls: movement and aim/fire. Input is sampled before the player motor.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class IntercomPlayerInput : MonoBehaviour
    {
        [Header("References")]
        public Camera movementCamera;
        public Transform target;
        [Header("Two sticks")]
        [Range(0f, 0.8f)] public float movementDeadZone = 0.12f;
        [Range(0f, 0.8f)] public float aimDeadZone = 0.2f;
        [Range(48f, 110f)] public float stickRadiusAt720p = 70f;
        [Range(16f, 80f)] public float edgeMarginAt720p = 32f;
        public bool showOnDesktop = true;
        [Range(0.2f, 1f)] public float controlsOpacity = 0.85f;

        public Vector2 Move { get; private set; }
        public Vector2 Aim { get; private set; }
        public bool AimActive { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool TouchActive { get; private set; }
        /// <summary>System cancellation epoch. Normal stick release does not invalidate a queued tap.</summary>
        public uint CancellationVersion { get; private set; }
        public bool ControlsVisible => Application.isMobilePlatform || showOnDesktop;

        private const int MousePointerId = int.MinValue;
        private readonly IntercomTwinStickState sticks = new IntercomTwinStickState();
        private Texture2D discTexture, ringTexture;
        private CharacterController targetCapsule;
        private Transform cachedTarget;
        private Vector2 leftCenter, rightCenter;
        private float radius;
        private bool focused = true, paused, requireRelease, wasFireHeld, hasLayout;
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
            RefreshLayout();
        }

        private void OnEnable()
        {
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
            Move = Aim = Vector2.zero;
            AimActive = FireHeld = FirePressed = ResetPressed = TouchActive = wasFireHeld = false;
        }

        /// <summary>Respawn/pause gate: held mouse, keys and fingers must be released before control resumes.</summary>
        public void CancelUntilRelease()
        {
            ClearInput();
            unchecked { CancellationVersion++; }
            requireRelease = true;
        }

        private void Update()
        {
            RefreshLayout();
            if (!focused || paused || Time.timeScale <= 0f) { CancelUntilRelease(); return; }
            Vector2 keyboard = ReadKeyboard();
            ReadMouse(out Vector2 mousePosition, out bool mouseHeld, out bool mouseDown, out bool mouseUp);
            bool spaceHeld = SpaceHeld();
            int touchCount = TouchCount();
            TouchActive = touchCount > 0;
            if (requireRelease)
            {
                ClearInput();
                if (touchCount == 0 && !mouseHeld && !spaceHeld && keyboard.sqrMagnitude == 0f) requireRelease = false;
                return;
            }

            sticks.BeginFrame();
            if (!PollTouches()) return;
            // Touch can generate emulated mouse input in Legacy. Never treat that as a shot.
            bool allowMouse = !Application.isMobilePlatform && touchCount == 0;
            if (allowMouse)
            {
                if (mouseDown && ControlsVisible) sticks.PointerDown(MousePointerId, mousePosition.x, mousePosition.y);
                if (mouseHeld) sticks.PointerMove(MousePointerId, mousePosition.x, mousePosition.y);
                if (mouseUp || !mouseHeld) sticks.PointerUp(MousePointerId);
            }
            else sticks.PointerUp(MousePointerId);
            if (sticks.EndFrame()) { CancelUntilRelease(); return; }

            Move = sticks.Left.Captured ? new Vector2(sticks.Left.X, sticks.Left.Y) : keyboard;
            Aim = new Vector2(sticks.Right.X, sticks.Right.Y);
            AimActive = Aim.sqrMagnitude > 0.000001f;
            bool mouseOwnsStick = sticks.Owns(MousePointerId);
            bool desktopFire = allowMouse && !mouseOwnsStick && (mouseHeld || spaceHeld);
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
            float scale = Mathf.Max(0.25f, Mathf.Min(safe.width / 1280f, safe.height / 720f));
            radius = Mathf.Min(stickRadiusAt720p * scale, Mathf.Min(safe.width * 0.18f, safe.height * 0.25f));
            radius = Mathf.Max(8f, radius);
            float margin = edgeMarginAt720p * scale;
            leftCenter = new Vector2(safe.xMin + radius + margin, safe.yMin + radius + margin);
            rightCenter = new Vector2(safe.xMax - radius - margin, safe.yMin + radius + margin);
            sticks.Configure(leftCenter.x, leftCenter.y, rightCenter.x, rightCenter.y, radius, movementDeadZone, aimDeadZone);
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
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began) sticks.PointerDown(touch.touchId, point.x, point.y);
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) { CancelUntilRelease(); return false; }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended) sticks.PointerUp(touch.touchId);
                else sticks.PointerMove(touch.touchId, point.x, point.y);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began) sticks.PointerDown(touch.fingerId, touch.position.x, touch.position.y);
                else if (touch.phase == TouchPhase.Canceled) { CancelUntilRelease(); return false; }
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
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!ControlsVisible || Event.current == null || Event.current.type != EventType.Repaint) return;
            EnsureTextures();
            Color previousColor = GUI.color; Matrix4x4 previousMatrix = GUI.matrix; int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -50;
            DrawStick(leftCenter, sticks.Left, new Color(0.55f, 0.86f, 0.94f, 1f));
            DrawStick(rightCenter, sticks.Right, new Color(1f, 0.74f, 0.32f, 1f));
            GUI.color = previousColor; GUI.matrix = previousMatrix; GUI.depth = previousDepth;
        }

        private void DrawStick(Vector2 center, IntercomTwinStickState.Stick stick, Color accent)
        {
            float opacity = controlsOpacity * (stick.Captured ? 1f : 0.72f);
            DrawCircle(center, radius, discTexture, new Color(0.035f, 0.055f, 0.07f, opacity * 0.45f));
            DrawCircle(center, radius, ringTexture, new Color(accent.r, accent.g, accent.b, opacity * 0.72f));
            Vector2 knob = center + new Vector2(stick.RawX, stick.RawY) * radius * 0.68f;
            DrawCircle(knob, radius * 0.35f, discTexture, new Color(accent.r, accent.g, accent.b, opacity * 0.9f));
            DrawCircle(knob, radius * 0.35f, ringTexture, new Color(1f, 1f, 1f, opacity * 0.6f));
        }

        private static void DrawCircle(Vector2 center, float size, Texture texture, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size, Screen.height - center.y - size, size * 2f, size * 2f), texture);
        }

        private void EnsureTextures()
        {
            if (discTexture != null) return;
            discTexture = CircleTexture(false); ringTexture = CircleTexture(true);
        }

        private static Texture2D CircleTexture(bool ring)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = ring ? "Intercom Stick Ring" : "Intercom Stick Disc", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                    float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - distance) * size * 0.5f);
                    if (ring) alpha *= Mathf.Clamp01((distance - 0.94f) * size * 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            if (discTexture != null) Destroy(discTexture);
            if (ringTexture != null) Destroy(ringTexture);
        }
    }
}
