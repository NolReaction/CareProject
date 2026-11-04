using System;

namespace IntercomMvp
{
    /// <summary>Shared drawing/hit-test bounds in bottom-left screen coordinates.</summary>
    public readonly struct IntercomHudLayout
    {
        public readonly struct Box
        {
            public readonly float X, Y, Width, Height;
            public Box(float x, float y, float width, float height)
            { X = x; Y = y; Width = width; Height = height; }
            public bool Contains(float x, float y) => x >= X && y >= Y && x <= X+Width && y <= Y+Height;
        }
        public readonly float Scale, Radius, LeftX, LeftY, RightX, RightY;
        public readonly Box Reload, AreaBadge, Health, Defeat, Retry;
        public readonly Box MissionPanel, MissionFirst, MissionSecond, MissionThird, MissionObjective, Modes;
        public readonly Box Timer, Toast, PausePanel, PauseResume, PauseRetry, PauseExit, PauseConfirm, PauseCancel;
        public readonly bool ShowAreaBadge;

        private IntercomHudLayout(float scale, float radius, float left, float right, float bottom,
            Box reload, Box badge, bool showBadge, Box health, Box defeat, Box retry,
            Box missionPanel, Box missionFirst, Box missionSecond, Box missionThird, Box missionObjective, Box modes,
            Box timer, Box toast, Box pausePanel, Box pauseResume, Box pauseRetry, Box pauseExit, Box pauseConfirm, Box pauseCancel)
        {
            Scale = scale; Radius = radius; LeftX = left; RightX = right; LeftY = RightY = bottom;
            Reload = reload; AreaBadge = badge; ShowAreaBadge = showBadge;
            Health = health; Defeat = defeat; Retry = retry;
            MissionPanel = missionPanel; MissionFirst = missionFirst; MissionSecond = missionSecond;
            MissionThird = missionThird;
            MissionObjective = missionObjective; Modes = modes;
            Timer = timer; Toast = toast; PausePanel = pausePanel; PauseResume = pauseResume;
            PauseRetry = pauseRetry; PauseExit = pauseExit; PauseConfirm = pauseConfirm; PauseCancel = pauseCancel;
        }

        public static IntercomHudLayout Calculate(float x, float y, float width, float height,
            float stickRadiusAt720p, float edgeMarginAt720p)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width), "HUD requires a finite safe area.");
            float scale = Math.Max(0.25f, Math.Min(width / 1280f, height / 720f));
            float requestedRadius = Finite(stickRadiusAt720p) ? Math.Max(48f, Math.Min(110f, stickRadiusAt720p)) : 70f;
            float radius = Math.Min(requestedRadius * scale, Math.Min(width * 0.14f, height * 0.14f));
            // Capture extends to 1.3 x the visible stick. Keep it entirely in
            // the safe rectangle, including compact windows and large controls.
            float requestedMargin = Finite(edgeMarginAt720p) ? Math.Max(0f, edgeMarginAt720p) : 32f;
            float margin = Math.Max(requestedMargin * scale, radius * 0.3f + 2f);
            margin = Math.Min(margin, Math.Min(width * 0.1f, height * 0.1f));
            float left = x + radius + margin, right = x + width - radius - margin;
            float bottom = y + radius + margin;
            float gap = Math.Min(Math.Max(4f, 8f * scale), Math.Min(width,height) * 0.025f);
            float inset = Math.Min(Math.Max(8f, 18f * scale), Math.Min(width, height) * 0.04f);

            // Phone-first instruments leave both thumb areas free. Pause and
            // hull sit together on the left, the equipped weapon on the right;
            // the small objective slips below the left row rather than adding
            // another full-width bar across the level.
            float top = y + height - inset;
            float modesSize = Math.Min(Math.Min(width * 0.13f, height * 0.16f), Math.Max(44f, 46f * scale));
            Box modes = new Box(x + inset, top - modesSize, modesSize, modesSize);
            float reloadWidth = Math.Min(width * 0.285f, Math.Max(140f, 230f * scale));
            float reloadHeight = Math.Min(height * 0.22f, Math.Max(48f, 82f * scale));
            Box reload = new Box(x + width - inset - reloadWidth, top - reloadHeight, reloadWidth, reloadHeight);
            float timerWidth = Math.Min(width * 0.20f, Math.Max(64f, 120f * scale));
            float timerHeight = Math.Min(height * 0.135f, Math.Max(24f, 42f * scale));
            Box timer = new Box(x + (width - timerWidth) * 0.5f, top - timerHeight, timerWidth, timerHeight);
            float healthX = modes.X + modes.Width + gap;
            float healthWidth = Math.Min(Math.Max(100f, 260f * scale), timer.X - healthX - gap);
            healthWidth = Math.Max(width * 0.01f, healthWidth);
            float healthHeight = Math.Min(modesSize * 0.74f, Math.Max(18f, 32f * scale));
            Box health = new Box(healthX, top - (modesSize + healthHeight) * 0.5f, healthWidth, healthHeight);
            float rowHeight = Math.Max(modesSize, timerHeight);
            float objectiveWidth = Math.Min(Math.Max(120f, 260f * scale), reload.X - x - inset - gap);
            float objectiveHeight = Math.Min(height * 0.13f, Math.Max(30f, 50f * scale));
            Box objective = new Box(x + inset, top - rowHeight - gap - objectiveHeight, objectiveWidth, objectiveHeight);
            Box badge = objective;
            bool showBadge = width >= 580f && height >= 300f;

            // A brief lower-center message sits in the gap between both stick
            // capture regions, leaving the level itself open after it fades.
            float corridorLeft = left + radius * 1.3f + gap;
            float corridorRight = right - radius * 1.3f - gap;
            float toastWidth = Math.Min(Math.Max(120f, 500f * scale), Math.Max(width * 0.05f, corridorRight - corridorLeft));
            float toastHeight = Math.Min(height * 0.17f, Math.Max(30f, 56f * scale));
            Box toast = new Box(x + (width - toastWidth) * 0.5f, y + inset, toastWidth, toastHeight);

            float defeatWidth = Math.Min(width * 0.82f, Math.Max(260f, 380f * scale));
            float defeatHeight = Math.Min(height * 0.62f, Math.Max(154f, 178f * scale));
            Box defeat = new Box(x + (width - defeatWidth) * 0.5f, y + (height - defeatHeight) * 0.5f, defeatWidth, defeatHeight);
            float padding = Math.Min(defeatWidth * 0.08f, 18f * scale);
            float retryHeight = Math.Min(defeatHeight * 0.32f, Math.Max(44f, 50f * scale));
            Box retry = new Box(defeat.X + padding, defeat.Y + padding, defeatWidth - 2f * padding, retryHeight);

            float pauseWidth = Math.Min(width * 0.88f, Math.Max(280f, 420f * scale));
            float pauseHeight = Math.Min(height * 0.86f, Math.Max(252f, 320f * scale));
            Box pausePanel = new Box(x + (width - pauseWidth) * 0.5f, y + (height - pauseHeight) * 0.5f, pauseWidth, pauseHeight);
            float pauseInset = pauseWidth * 0.065f, actionWidth = pauseWidth - 2f * pauseInset;
            float actionHeight = pauseHeight * 0.17f;
            Box pauseResume = new Box(pausePanel.X + pauseInset, pausePanel.Y + pauseHeight * 0.515f, actionWidth, actionHeight);
            Box pauseRetry = new Box(pauseResume.X, pausePanel.Y + pauseHeight * 0.305f, actionWidth, actionHeight);
            Box pauseExit = new Box(pauseResume.X, pausePanel.Y + pauseHeight * 0.095f, actionWidth, actionHeight);
            float confirmGap = pauseWidth * 0.035f;
            Box pauseConfirm = new Box(pauseExit.X, pauseExit.Y, (actionWidth - confirmGap) * 0.5f, actionHeight);
            Box pauseCancel = new Box(pauseConfirm.X + pauseConfirm.Width + confirmGap, pauseExit.Y, pauseConfirm.Width, actionHeight);
            float missionWidth = Math.Min(width*.9f,Math.Max(480f,600f*scale));
            float missionHeight = Math.Min(height*.86f,Math.Max(300f,390f*scale));
            Box missionPanel = new Box(x+(width-missionWidth)*.5f,y+(height-missionHeight)*.5f,missionWidth,missionHeight);
            float missionInset = missionWidth*.055f;
            float cardHeight = missionHeight*.205f;
            Box missionThird = new Box(missionPanel.X+missionInset,missionPanel.Y+missionHeight*.055f,missionWidth-missionInset*2f,cardHeight);
            Box missionSecond = new Box(missionThird.X,missionPanel.Y+missionHeight*.285f,missionThird.Width,cardHeight);
            Box missionFirst = new Box(missionThird.X,missionPanel.Y+missionHeight*.515f,missionThird.Width,cardHeight);
            return new IntercomHudLayout(scale, radius, left, right, bottom, reload, badge, showBadge, health, defeat, retry,
                missionPanel,missionFirst,missionSecond,missionThird,objective,modes,
                timer,toast,pausePanel,pauseResume,pauseRetry,pauseExit,pauseConfirm,pauseCancel);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
