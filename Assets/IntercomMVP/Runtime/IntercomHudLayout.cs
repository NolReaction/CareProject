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
        public readonly Box MissionPanel, MissionFirst, MissionSecond, MissionObjective, Modes;
        public readonly bool ShowAreaBadge;

        private IntercomHudLayout(float scale, float radius, float left, float right, float bottom,
            Box reload, Box badge, bool showBadge, Box health, Box defeat, Box retry,
            Box missionPanel, Box missionFirst, Box missionSecond, Box missionObjective, Box modes)
        {
            Scale = scale; Radius = radius; LeftX = left; RightX = right; LeftY = RightY = bottom;
            Reload = reload; AreaBadge = badge; ShowAreaBadge = showBadge;
            Health = health; Defeat = defeat; Retry = retry;
            MissionPanel = missionPanel; MissionFirst = missionFirst; MissionSecond = missionSecond;
            MissionObjective = missionObjective; Modes = modes;
        }

        public static IntercomHudLayout Calculate(float x, float y, float width, float height,
            float stickRadiusAt720p, float edgeMarginAt720p)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width), "HUD requires a finite safe area.");
            float scale = Math.Max(0.25f, Math.Min(width / 1280f, height / 720f));
            float requestedRadius = Finite(stickRadiusAt720p) ? Math.Max(48f, Math.Min(110f, stickRadiusAt720p)) : 70f;
            float radius = Math.Min(requestedRadius * scale, Math.Min(width * 0.18f, height * 0.18f));
            // The stick capture area extends to 1.3 x visual radius. Keep that
            // entire area inside the safe rectangle, including small windows.
            float requestedMargin = Finite(edgeMarginAt720p) ? Math.Max(0f, edgeMarginAt720p) : 32f;
            float margin = Math.Max(requestedMargin * scale, radius * 0.3f + 2f);
            margin = Math.Min(margin, Math.Min(width * 0.1f, height * 0.1f));
            float left = x + radius + margin, right = x + width - radius - margin;
            float bottom = y + radius + margin;
            float panelWidth = Math.Min(width * 0.43f, Math.Max(136f, 188f * scale));
            float panelHeight = Math.Min(height * 0.28f, Math.Max(62f, 84f * scale));
            float gap = Math.Max(5f, 10f * scale);
            float panelX = Math.Max(x + 2f, Math.Min(right - panelWidth * 0.5f, x + width - panelWidth - 2f));
            float panelY = bottom + radius * 1.3f + gap;
            Box reload = new Box(panelX, panelY, panelWidth, panelHeight);
            float badgeWidth = Math.Min(width * 0.46f, Math.Max(202f, 260f * scale));
            float badgeHeight = Math.Max(46f, 62f * scale);
            float badgeMargin = Math.Max(8f, 18f * scale);
            Box badge = new Box(x + badgeMargin, y + height - badgeMargin - badgeHeight, badgeWidth, badgeHeight);
            bool showBadge = width >= 580f && height >= 300f && badge.Y > bottom + radius * 1.3f + 20f;
            float healthWidth = Math.Min(width*.55f, Math.Max(180f,260f*scale));
            float healthHeight = Math.Min(height*.30f, Math.Max(54f,76f*scale));
            Box health = new Box(x+badgeMargin, y+height-badgeMargin-healthHeight, healthWidth, healthHeight);
            float defeatWidth = Math.Min(width*.82f, Math.Max(260f,380f*scale));
            float defeatHeight = Math.Min(height*.62f, Math.Max(154f,178f*scale));
            Box defeat = new Box(x+(width-defeatWidth)*.5f, y+(height-defeatHeight)*.5f, defeatWidth, defeatHeight);
            float padding = Math.Min(defeatWidth*.08f, 18f*scale);
            float retryHeight = Math.Min(defeatHeight*.32f, Math.Max(44f,50f*scale));
            Box retry = new Box(defeat.X+padding, defeat.Y+padding, defeatWidth-2f*padding, retryHeight);
            float modesWidth = Math.Min(width*.25f, Math.Max(76f,102f*scale));
            float modesHeight = Math.Min(height*.2f, Math.Max(36f,43f*scale));
            Box modes = new Box(x+width-badgeMargin-modesWidth,y+height-badgeMargin-modesHeight,modesWidth,modesHeight);
            float available = modes.X-(health.X+health.Width)-gap*2f;
            float objectiveWidth = available >= 150f ? Math.Min(available,Math.Max(220f,370f*scale)) : Math.Min(width-badgeMargin*2f,Math.Max(200f,270f*scale));
            float objectiveHeight = Math.Min(height*.2f,Math.Max(54f,66f*scale));
            float objectiveX = available >= 150f ? health.X+health.Width+gap+(available-objectiveWidth)*.5f : modes.X+modes.Width-objectiveWidth;
            float objectiveY = available >= 150f ? y+height-badgeMargin-objectiveHeight : health.Y-gap-objectiveHeight;
            Box objective = new Box(objectiveX,Math.Max(y+2f,objectiveY),objectiveWidth,objectiveHeight);
            float missionWidth = Math.Min(width*.9f,Math.Max(480f,600f*scale));
            float missionHeight = Math.Min(height*.82f,Math.Max(270f,330f*scale));
            Box missionPanel = new Box(x+(width-missionWidth)*.5f,y+(height-missionHeight)*.5f,missionWidth,missionHeight);
            float inset = missionWidth*.055f;
            float cardHeight = missionHeight*.29f;
            Box missionSecond = new Box(missionPanel.X+inset,missionPanel.Y+missionHeight*.075f,missionWidth-inset*2f,cardHeight);
            Box missionFirst = new Box(missionSecond.X,missionPanel.Y+missionHeight*.415f,missionSecond.Width,cardHeight);
            return new IntercomHudLayout(scale, radius, left, right, bottom, reload, badge, showBadge, health, defeat, retry,
                missionPanel,missionFirst,missionSecond,objective,modes);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
