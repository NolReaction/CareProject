// Cohesive deterministic feature models; no scene components or Unity dependencies.
using System;

namespace IntercomMvp
{
    #region IntercomFrontEndState
    public enum IntercomFrontEndSection { Home, Play, Robot, Armory }

    /// <summary>Navigation state only. Mission, inventory and economy keep their own state owners.</summary>
    public sealed class IntercomFrontEndState
    {
        public IntercomFrontEndSection CurrentSection { get; private set; } = IntercomFrontEndSection.Home;
        public uint Revision { get; private set; }

        public bool Select(IntercomFrontEndSection section)
        {
            if (!Valid(section) || CurrentSection == section) return false;
            CurrentSection = section;
            unchecked { Revision++; }
            return true;
        }

        public bool Back() => Select(IntercomFrontEndSection.Home);
        public static bool Valid(IntercomFrontEndSection section) => (uint)section <= (uint)IntercomFrontEndSection.Armory;
    }
    #endregion

    #region IntercomFrontEndLayout
    public enum IntercomFrontEndAction
    {
        None, Home, Play, Robot, Armory,
        SelectElimination, SelectRescue, SelectFreePlay, Primary, Secondary
    }

    /// <summary>Drawing and pointer routing share these safe-area bounds, in bottom-left screen pixels.</summary>
    public readonly struct IntercomFrontEndLayout
    {
        public readonly float Scale;
        public readonly bool IsPortrait;
        public readonly IntercomHudLayout.Box Panel, Header, Footer, Tabs;
        public readonly IntercomHudLayout.Box TabHome, TabPlay, TabRobot, TabArmory;
        public readonly IntercomHudLayout.Box Body, HeroPreview, Content;
        public readonly IntercomHudLayout.Box ActionPrimary, ActionSecondary;
        public readonly IntercomHudLayout.Box ModeFirst, ModeSecond, ModeThird;

        private IntercomFrontEndLayout(float scale, bool portrait,
            IntercomHudLayout.Box panel, IntercomHudLayout.Box header, IntercomHudLayout.Box footer,
            IntercomHudLayout.Box tabs, IntercomHudLayout.Box home, IntercomHudLayout.Box play,
            IntercomHudLayout.Box robot, IntercomHudLayout.Box armory,
            IntercomHudLayout.Box body, IntercomHudLayout.Box preview, IntercomHudLayout.Box content,
            IntercomHudLayout.Box primary, IntercomHudLayout.Box secondary,
            IntercomHudLayout.Box first, IntercomHudLayout.Box second, IntercomHudLayout.Box third)
        {
            Scale = scale; IsPortrait = portrait; Panel = panel; Header = header; Footer = footer;
            Tabs = tabs; TabHome = home; TabPlay = play; TabRobot = robot; TabArmory = armory;
            Body = body; HeroPreview = preview; Content = content;
            ActionPrimary = primary; ActionSecondary = secondary;
            ModeFirst = first; ModeSecond = second; ModeThird = third;
        }

        public static IntercomFrontEndLayout Calculate(float x, float y, float width, float height)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f
                || !Finite(x + width) || !Finite(y + height) || x + width <= x || y + height <= y)
                throw new ArgumentOutOfRangeException(nameof(width), "Front end requires a finite positive safe area.");
            bool portrait = width < height * 1.15f;
            float scale = Math.Max(.1f, portrait ? Math.Min(width / 640f, height / 960f)
                : Math.Min(width / 1280f, height / 720f));
            float margin = Math.Min(24f * scale, Math.Min(width, height) * .045f);
            var panel = Box(x + margin, y + margin, width - 2f * margin, height - 2f * margin);
            float gap = Math.Min(12f * scale, Math.Min(panel.Width, panel.Height) * .025f);
            float headerHeight = Math.Min(90f * scale, panel.Height * .14f);
            float footerHeight = Math.Min(36f * scale, panel.Height * .07f);
            float tabsHeight = Math.Min(48f * scale, panel.Height * .11f);
            var header = Box(panel.X, panel.Y + panel.Height - headerHeight, panel.Width, headerHeight);
            var footer = Box(panel.X, panel.Y, panel.Width, footerHeight);
            var tabs = Box(panel.X, footer.Y + footer.Height + gap, panel.Width, tabsHeight);
            float tabWidth = (tabs.Width - 3f * gap) * .25f;
            var home = Box(tabs.X, tabs.Y, tabWidth, tabs.Height);
            var play = Box(home.X + tabWidth + gap, tabs.Y, tabWidth, tabs.Height);
            var robot = Box(play.X + tabWidth + gap, tabs.Y, tabWidth, tabs.Height);
            var armory = Box(robot.X + tabWidth + gap, tabs.Y, tabWidth, tabs.Height);
            var body = Box(panel.X, tabs.Y + tabs.Height + gap, panel.Width,
                header.Y - gap - (tabs.Y + tabs.Height + gap));
            IntercomHudLayout.Box preview, content;
            if (portrait)
            {
                float previewHeight = (body.Height - gap) * .32f;
                preview = Box(body.X, body.Y + body.Height - previewHeight, body.Width, previewHeight);
                content = Box(body.X, body.Y, body.Width, body.Height - previewHeight - gap);
            }
            else
            {
                float previewWidth = (body.Width - gap) * .57f;
                preview = Box(body.X, body.Y, previewWidth, body.Height);
                content = Box(body.X + previewWidth + gap, body.Y, body.Width - previewWidth - gap, body.Height);
            }
            float buttonHeight = Math.Min(62f * scale, content.Height * .17f);
            float buttonWidth = (content.Width - gap) * .5f;
            var primary = Box(content.X, content.Y, buttonWidth, buttonHeight);
            var secondary = Box(content.X + buttonWidth + gap, content.Y, buttonWidth, buttonHeight);
            float cardHeight = (content.Height - 2f * gap) / 3f;
            var third = Box(content.X, content.Y, content.Width, cardHeight);
            var second = Box(content.X, third.Y + cardHeight + gap, content.Width, cardHeight);
            var first = Box(content.X, second.Y + cardHeight + gap, content.Width, cardHeight);
            return new IntercomFrontEndLayout(scale, portrait, panel, header, footer, tabs, home, play, robot, armory,
                body, preview, content, primary, secondary, first, second, third);
        }

        public IntercomFrontEndAction Hit(IntercomFrontEndSection section, float x, float y)
        {
            if (!IntercomFrontEndState.Valid(section) || !Finite(x) || !Finite(y)) return IntercomFrontEndAction.None;
            if (TabHome.Contains(x, y)) return IntercomFrontEndAction.Home;
            if (TabPlay.Contains(x, y)) return IntercomFrontEndAction.Play;
            if (TabRobot.Contains(x, y)) return IntercomFrontEndAction.Robot;
            if (TabArmory.Contains(x, y)) return IntercomFrontEndAction.Armory;
            if (section == IntercomFrontEndSection.Play)
            {
                if (ModeFirst.Contains(x, y)) return IntercomFrontEndAction.SelectElimination;
                if (ModeSecond.Contains(x, y)) return IntercomFrontEndAction.SelectRescue;
                if (ModeThird.Contains(x, y)) return IntercomFrontEndAction.SelectFreePlay;
            }
            else
            {
                if (ActionPrimary.Contains(x, y)) return IntercomFrontEndAction.Primary;
                if (ActionSecondary.Contains(x, y)) return IntercomFrontEndAction.Secondary;
            }
            return IntercomFrontEndAction.None;
        }

        private static IntercomHudLayout.Box Box(float x, float y, float width, float height)
            => new IntercomHudLayout.Box(x, y, width, height);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion
}
