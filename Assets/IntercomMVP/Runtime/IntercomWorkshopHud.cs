using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Painted workshop instruments. Presentation only; never captures input.</summary>
    internal sealed class IntercomWorkshopHud
    {
        private static readonly Color Ink = new Color(.13f, .19f, .20f, 1f);
        private static readonly Color Paper = new Color(.88f, .84f, .70f, 1f);
        private static readonly Color Petrol = new Color(.38f, .57f, .55f, 1f);
        private static readonly Color Mustard = new Color(.78f, .62f, .32f, 1f);
        private static readonly Color Warning = new Color(.77f, .42f, .28f, 1f);
        private Texture2D plate, label, disc, ring;
        private GUIStyle small, bold, number;
        private int lastAmmo = -1, lastCapacity = -1;
        private string ammoText = "", capacityText = "";
        private int lastHealth = -1, lastMaximum = -1;
        private string healthText = "";

        public void Draw(IntercomHudLayout layout, IntercomTwinStickState sticks,
            IntercomWeaponMagazine magazine, bool showSticks, float controlsOpacity, IntercomPlayerVitality vitality = null,
            IntercomMissionDirector mission = null)
        {
            EnsureResources();
            if(mission != null && mission.BlocksGameplay) { DrawMissionMenu(layout,mission); return; }
            bool dead = vitality != null && vitality.IsDead;
            if (showSticks && !dead)
            {
                DrawStick(layout.LeftX, layout.LeftY, layout.Radius, sticks.Left, Petrol, false, controlsOpacity);
                DrawStick(layout.RightX, layout.RightY, layout.Radius, sticks.Right, Mustard, true, controlsOpacity);
            }
            if(vitality != null && vitality.Health != null) DrawHealth(layout.Health,vitality);
            else if (layout.ShowAreaBadge) DrawArea(layout.AreaBadge);
            if(dead && mission == null) DrawDefeat(layout);
            else if (!dead && magazine != null && magazine.isActiveAndEnabled) DrawMagazine(layout.Reload, magazine);
            if(mission != null) DrawMissionObjective(layout,mission);
        }

        private void DrawMissionMenu(IntercomHudLayout layout, IntercomMissionDirector mission)
        {
            Fill(new Rect(0f,0f,Screen.width,Screen.height),Ink,.68f);
            Rect r=GuiRect(layout.MissionPanel);
            float sx=r.width/600f, sy=r.height/330f, s=Mathf.Min(sx,sy);
            bool selection=mission.Phase == IntercomMissionPhase.Selection;
            bool victory=mission.Phase == IntercomMissionPhase.Victory;
            Color accent=selection ? Petrol : victory ? Mustard : Warning;
            Texture(r,plate,Paper);
            Texture(new Rect(r.x+19f*sx,r.y+16f*sy,42f*sx,42f*sy),label,accent);
            Text(new Rect(r.x+19f*sx,r.y+16f*sy,42f*sx,42f*sy),selection ? "!" : victory ? "+" : "×",number,29f*s,Ink);
            string title=selection ? "СЕРВИСНЫЙ ВЫЕЗД" : victory ? "ЗАДАНИЕ ВЫПОЛНЕНО" : "ВЫЕЗД НЕ УДАЛСЯ";
            Text(new Rect(r.x+66f*sx,r.y+19f*sy,r.width-94f*sx,26f*sy),title,bold,22f*s,Ink);
            string subtitle=selection && string.IsNullOrEmpty(mission.ResultText)
                ? "Выбери работу. Двор сам себя не починит." : mission.ResultText;
            Text(new Rect(r.x+22f*sx,r.y+58f*sy,r.width-44f*sx,28f*sy),subtitle,small,13f*s,Ink,.8f);
            DrawMissionCard(layout.MissionFirst,selection ? "01" : "↻",
                selection ? "50 РОБОТОВ" : victory ? "ПОВТОРИТЬ ВЫЕЗД" : "ПОПРОБОВАТЬ СНОВА",
                selection ? "Очисти двор от ударников." : "Новый заход с полным здоровьем.",Petrol);
            DrawMissionCard(layout.MissionSecond,selection ? "02" : "…",
                selection ? "СПАСЕНИЕ" : "ВЫБРАТЬ РЕЖИМ",
                selection ? "Найди союзника и доведи до выхода." : "Вернуться к списку заданий.",Mustard);
            Bolt(r.x+12f*sx,r.y+12f*sy,3f*s);
            Bolt(r.xMax-13f*sx,r.yMax-13f*sy,3f*s);
        }

        private void DrawMissionCard(IntercomHudLayout.Box box,string badge,string title,string detail,Color accent)
        {
            Rect r=GuiRect(box);
            float sx=r.width/534f, sy=r.height/95.7f, s=Mathf.Min(sx,sy);
            Texture(r,label,accent);
            Texture(new Rect(r.x+12f*sx,r.y+15f*sy,54f*sx,58f*sy),plate,Paper);
            Text(new Rect(r.x+12f*sx,r.y+15f*sy,54f*sx,58f*sy),badge,number,24f*s,Ink);
            Text(new Rect(r.x+76f*sx,r.y+16f*sy,r.width-94f*sx,31f*sy),title,bold,20f*s,Ink);
            Text(new Rect(r.x+76f*sx,r.y+50f*sy,r.width-94f*sx,25f*sy),detail,small,13f*s,Ink,.82f);
        }

        private void DrawMissionObjective(IntercomHudLayout layout,IntercomMissionDirector mission)
        {
            Rect r=GuiRect(layout.MissionObjective);
            float sx=r.width/370f, sy=r.height/66f, s=Mathf.Min(sx,sy);
            Texture(r,label,Paper,.95f);
            bool rescue=mission.Mode == IntercomMissionMode.Rescue;
            string title=rescue ? (mission.IsAllyRecruited ? "ДОВЕДИ ДО ВЫХОДА" : "НАЙДИ СОЮЗНИКА") : "УДАРНИКИ";
            if(!string.IsNullOrEmpty(mission.HudHint)) title=mission.HudHint;
            Text(new Rect(r.x+10f*sx,r.y+6f*sy,r.width-20f*sx,19f*sy),title,bold,Mathf.Max(10f,12f*s),Ink,.8f);
            string progress;
            if(rescue)
            {
                IntercomHealth ally=mission.AllyHealth;
                progress=ally == null ? "СОЮЗНИК ЖДЁТ" : "КОРПУС  " + Mathf.Max(0,Mathf.CeilToInt(ally.Current)) + " / " + Mathf.Max(1,Mathf.CeilToInt(ally.Maximum));
            }
            else progress=mission.Kills+" / "+mission.Goal;
            Text(new Rect(r.x+10f*sx,r.y+25f*sy,r.width-20f*sx,30f*sy),progress,number,Mathf.Max(rescue ? 13f : 18f,(rescue ? 18f : 25f)*s),Ink);
            Rect menu=GuiRect(layout.Modes);
            Texture(menu,plate,Paper,.96f);
            Text(menu,"РЕЖИМЫ",bold,Mathf.Min(menu.width/8f,menu.height*.3f),Ink);
        }

        private void DrawHealth(IntercomHudLayout.Box box, IntercomPlayerVitality vitality)
        {
            Rect r = GuiRect(box);
            float sx=r.width/260f, sy=r.height/76f, s=Mathf.Min(sx,sy);
            IntercomHealth health=vitality.Health;
            int value=Mathf.Max(0,Mathf.CeilToInt(health.Current));
            int maximum=Mathf.Max(1,Mathf.CeilToInt(health.Maximum));
            if(value!=lastHealth || maximum!=lastMaximum)
            {lastHealth=value;lastMaximum=maximum;healthText=value+" / "+maximum;}
            Color accent=health.Current/health.Maximum <= .3f || vitality.ShowHitRemaining > 0f ? Warning : Petrol;
            Texture(r,plate,Paper);
            Texture(new Rect(r.x+10f*sx,r.y+9f*sy,48f*sx,52f*sy),label,accent);
            // Two mismatched eyes echo the robot's small service display.
            Fill(new Rect(r.x+20f*sx,r.y+27f*sy,9f*sx,12f*sy),Ink);
            Fill(new Rect(r.x+37f*sx,r.y+30f*sy,10f*sx,8f*sy),Ink);
            Fill(new Rect(r.x+22f*sx,r.y+48f*sy,23f*sx,3f*sy),Ink,.65f);
            Text(new Rect(r.x+66f*sx,r.y+6f*sy,174f*sx,18f*sy),"КОРПУС",bold,11f*s,Ink,.75f);
            Text(new Rect(r.x+65f*sx,r.y+22f*sy,176f*sx,27f*sy),healthText,number,23f*s,Ink);
            float progress=Mathf.Clamp01(health.Current/health.Maximum);
            for(int i=0;i<10;i++)
            {
                Rect cell=new Rect(r.x+(68f+i*17f)*sx,r.y+56f*sy,13f*sx,8f*sy);
                Fill(cell,Ink,.18f);
                float portion=Mathf.Clamp01(progress*10f-i);
                if(portion>0f)Fill(new Rect(cell.x,cell.y,cell.width*portion,cell.height),accent);
            }
            Bolt(r.x+248f*sx,r.y+12f*sy,2.5f*s);
            Bolt(r.x+12f*sx,r.y+67f*sy,2.5f*s);
        }

        private void DrawDefeat(IntercomHudLayout layout)
        {
            Rect r=GuiRect(layout.Defeat), button=GuiRect(layout.Retry);
            float sx=r.width/380f, sy=r.height/178f, s=Mathf.Min(sx,sy);
            Texture(r,plate,Paper);
            Text(new Rect(r.x+16f*sx,r.y+18f*sy,r.width-32f*sx,29f*sy),"СИСТЕМА ОТКЛЮЧЕНА",bold,19f*s,Ink);
            Text(new Rect(r.x+16f*sx,r.y+54f*sy,r.width-32f*sx,23f*sy),"Корпусу нужен ещё один шанс.",small,13f*s,Ink,.75f);
            Texture(button,label,Petrol);
            Text(button,"ПОПРОБОВАТЬ СНОВА",bold,15f*s,Ink);
            Bolt(r.x+11f*sx,r.y+13f*sy,3f*s);
            Bolt(r.xMax-12f*sx,r.y+11f*sy,3f*s);
        }

        private void DrawArea(IntercomHudLayout.Box box)
        {
            Rect r = GuiRect(box);
            float s = r.width / 260f;
            Texture(r, plate, Petrol, .93f);
            Texture(new Rect(r.x + 38f*s, r.y + 8f*s, r.width - 49f*s, r.height - 15f*s), label, Paper);
            Bolt(r.x + 13f*s, r.y + 14f*s, 3f*s);
            Bolt(r.xMax - 10f*s, r.yMax - 10f*s, 2.5f*s);
            // Three little vents make the badge look like an imperfect service tag.
            for (int i = 0; i < 3; i++) Fill(new Rect(r.x + 12f*s, r.y + (28f + i*5f)*s, 16f*s, 2f*s), Ink, .75f);
            Text(new Rect(r.x + 46f*s, r.y + 10f*s, r.width - 59f*s, 24f*s), "СЕРВИСНЫЙ ДВОР", bold, 15f*s, Ink);
            Text(new Rect(r.x + 46f*s, r.y + 32f*s, r.width - 59f*s, 17f*s), "УЧЕБНЫЙ УЧАСТОК · 01", small, 10f*s, Ink, .77f);
        }

        private void DrawMagazine(IntercomHudLayout.Box box, IntercomWeaponMagazine magazine)
        {
            Rect r = GuiRect(box);
            float sx = r.width / 188f, sy = r.height / 84f, s = Mathf.Min(sx, sy);
            bool loading = magazine.IsReloading;
            bool low = magazine.CurrentAmmo <= Mathf.Max(2, magazine.Capacity / 4);
            Color accent = loading ? Mustard : low ? Warning : Petrol;
            Texture(r, plate, Paper);
            // Dark inset is deliberately off-centre; the actual button stays rectangular.
            Texture(new Rect(r.x + 9f*sx, r.y + 8f*sy, 116f*sx, 45f*sy), label, Ink);
            if (lastAmmo != magazine.CurrentAmmo)
            { lastAmmo = magazine.CurrentAmmo; ammoText = lastAmmo.ToString("00"); }
            if (lastCapacity != magazine.Capacity)
            { lastCapacity = magazine.Capacity; capacityText = "/ " + lastCapacity; }
            Text(new Rect(r.x + 17f*sx, r.y + 4f*sy, 65f*sx, 48f*sy), ammoText, number, 36f*s, Paper);
            Text(new Rect(r.x + 77f*sx, r.y + 13f*sy, 43f*sx, 28f*sy), capacityText, bold, 14f*s, Paper, .7f);
            Text(new Rect(r.x + 127f*sx, r.y + 8f*sy, 48f*sx, 16f*sy), "ЗАПАС", small, 9f*s, Ink, .72f);
            Text(new Rect(r.x + 127f*sx, r.y + 20f*sy, 48f*sx, 28f*sy), "∞", number, 27f*s, Ink);
            Bolt(r.x + 176f*sx, r.y + 10f*sy, 2.3f*s);
            Bolt(r.x + 11f*sx, r.y + 74f*sy, 2.3f*s);

            const int slots = 12;
            float progress = loading ? magazine.ReloadProgress : magazine.CurrentAmmo / (float)Mathf.Max(1, magazine.Capacity);
            progress = Mathf.Clamp01(progress);
            for (int i = 0; i < slots; i++)
            {
                Rect slot = new Rect(r.x + (12f + i*13.7f)*sx, r.y + 55f*sy, 10.4f*sx, 5f*sy);
                Fill(slot, Ink, .18f);
                float portion = Mathf.Clamp01(progress * slots - i);
                if (portion > 0f) Fill(new Rect(slot.x, slot.y, slot.width*portion, slot.height), accent);
            }
            string action = loading ? "ПЕРЕЗАРЯДКА" : Application.isMobilePlatform ? "ПЕРЕЗАРЯДИТЬ" : "R · ПЕРЕЗАРЯДИТЬ";
            Text(new Rect(r.x + 20f*sx, r.y + 62f*sy, r.width - 40f*sx, 17f*sy), action, bold, 10.5f*s, Ink);
            Fill(new Rect(r.x + 158f*sx, r.y + 74f*sy, 16f*sx, 1.2f*sy), Ink, .24f);
        }

        private void DrawStick(float x, float y, float radius, IntercomTwinStickState.Stick stick,
            Color accent, bool aim, float opacity)
        {
            float alpha = Mathf.Clamp01(opacity) * (stick.Captured ? 1f : .74f);
            float gy = Screen.height - y;
            Rect circle = new Rect(x-radius, gy-radius, radius*2f, radius*2f);
            Texture(circle, disc, Ink, alpha*.52f);
            Texture(circle, ring, Paper, alpha*.49f);
            Texture(new Rect(x-radius*.82f, gy-radius*.82f, radius*1.64f, radius*1.64f), ring, accent, alpha*.7f);
            // Short stencilled detents, not a glowing sci-fi ring.
            for (int i = -1; i <= 1; i += 2)
            {
                Fill(new Rect(x+i*radius*.92f-radius*.035f, gy-radius*.09f, radius*.07f, radius*.18f), Paper, alpha*.65f);
                Fill(new Rect(x-radius*.09f, gy+i*radius*.92f-radius*.035f, radius*.18f, radius*.07f), Paper, alpha*.65f);
            }
            float knobX = x + stick.RawX * radius * .62f;
            float knobY = gy - stick.RawY * radius * .62f;
            float knob = radius*.36f;
            Texture(new Rect(knobX-knob, knobY-knob+radius*.045f, knob*2f, knob*2f), disc, Ink, alpha*.8f);
            Texture(new Rect(knobX-knob, knobY-knob, knob*2f, knob*2f), disc, accent, alpha);
            Texture(new Rect(knobX-knob, knobY-knob, knob*2f, knob*2f), ring, Paper, alpha*.75f);
            if (aim)
            {
                Texture(new Rect(knobX-knob*.37f, knobY-knob*.37f, knob*.74f, knob*.74f), ring, Ink, alpha*.8f);
                Fill(new Rect(knobX-knob*.6f, knobY-knob*.04f, knob*1.2f, knob*.08f), Ink, alpha*.75f);
                Fill(new Rect(knobX-knob*.04f, knobY-knob*.6f, knob*.08f, knob*1.2f), Ink, alpha*.75f);
            }
            else
            {
                for (int i = -1; i <= 1; i++)
                    Fill(new Rect(knobX-knob*.42f, knobY+i*knob*.26f-knob*.04f, knob*.84f, knob*.08f), Ink, alpha*.58f);
            }
        }

        private void Bolt(float x, float y, float size)
        {
            Texture(new Rect(x-size, y-size, size*2f, size*2f), disc, Ink, .72f);
            Fill(new Rect(x-size*.6f, y-size*.13f, size*1.2f, size*.26f), Paper, .75f);
        }
        private static Rect GuiRect(IntercomHudLayout.Box box) => new Rect(box.X, Screen.height-box.Y-box.Height, box.Width, box.Height);
        private static void Fill(Rect r, Color color, float alpha = 1f) => Texture(r, Texture2D.whiteTexture, color, alpha);
        private static void Texture(Rect r, Texture texture, Color color, float alpha = 1f)
        { GUI.color = new Color(color.r, color.g, color.b, color.a*alpha); GUI.DrawTexture(r, texture); }
        private static void Text(Rect r, string value, GUIStyle style, float size, Color color, float alpha = 1f)
        {
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            style.normal.textColor = new Color(color.r, color.g, color.b, color.a*alpha);
            GUI.color = Color.white;
            GUI.Label(r, value, style);
        }

        private void EnsureResources()
        {
            if (plate == null)
            {
                plate = MakeTexture("Workshop plate", 384, 160, 0);
                label = MakeTexture("Uneven painted label", 384, 160, 1);
                disc = MakeTexture("Workshop stick disc", 128, 128, 2);
                ring = MakeTexture("Workshop stick ring", 128, 128, 3);
            }
            if (small != null) return;
            small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            bold = new GUIStyle(small) { fontStyle = FontStyle.Bold };
            number = new GUIStyle(bold);
        }

        private static Texture2D MakeTexture(string name, int width, int height, int kind)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] pixels = new Color32[width*height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float alpha, tone = 1f;
                    if (kind >= 2)
                    {
                        float dx = (x+.5f-width*.5f)/(width*.5f), dy = (y+.5f-height*.5f)/(height*.5f);
                        float distance = Mathf.Sqrt(dx*dx+dy*dy);
                        alpha = Mathf.Clamp01((1f-distance)*width*.5f);
                        if (kind == 3) alpha *= Mathf.Clamp01((distance-.94f)*width*.5f);
                    }
                    else
                    {
                        // Unequal corner cuts and a gently sloped paper edge are
                        // authored details, not random noise regenerated each frame.
                        float left = kind == 0 ? 5f : 2f;
                        float right = width - (kind == 0 ? 8f : 3f);
                        float lower = kind == 0 ? 7f : 6f + x*.012f;
                        float upper = height - (kind == 0 ? 5f : 6f - x*.007f);
                        float edge = Mathf.Min(Mathf.Min(x-left, right-x), Mathf.Min(y-lower, upper-y));
                        edge = Mathf.Min(edge, x+y-(kind == 0 ? 27f : 14f));
                        edge = Mathf.Min(edge, (width-x)+(height-y)-(kind == 0 ? 21f : 12f));
                        alpha = Mathf.Clamp01(edge);
                        if (kind == 0)
                        {
                            if (edge < 5f) tone = .23f;
                            else if (y < 13f) tone = .67f;
                            else if (y > height-12f) tone = 1.06f;
                            // Two small paint chips on the rim.
                            if ((x > 54 && x < 76 && y < 17) || (x > width-69 && x < width-43 && y > height-16)) tone *= .78f;
                        }
                    }
                    byte shade = (byte)(Mathf.Clamp01(tone)*255f);
                    pixels[y*width+x] = new Color32(shade, shade, shade, (byte)(alpha*255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            if (plate != null) Object.Destroy(plate);
            if (label != null) Object.Destroy(label);
            if (disc != null) Object.Destroy(disc);
            if (ring != null) Object.Destroy(ring);
            plate = label = disc = ring = null;
        }
    }
}
