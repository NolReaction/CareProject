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
        private static readonly Color HeartRed = new Color(.76f, .28f, .23f, 1f);
        private Texture2D plate, label, disc, ring, heart;
        private GUIStyle small, bold, number;
        private int lastAmmo = -1, lastCapacity = -1;
        private string ammoText = "", capacityText = "";
        private int lastHealth = -1, lastMaximum = -1;
        private string healthText = "";
        private bool progressReady, progressRescue, progressHasAlly;
        private int progressCurrent, progressMaximum;
        private string missionProgressText = "";
        private IntercomMissionDirector clockMission;
        private uint clockRun;
        private int lastElapsedSecond = -1;
        private string elapsedText = "00:00:00", lastHint = "";
        private float hintBegan;

        public void Draw(IntercomHudLayout layout, IntercomTwinStickState sticks,
            IntercomWeaponMagazine magazine, bool showSticks, float controlsOpacity, IntercomPlayerVitality vitality = null,
            IntercomMissionDirector mission = null, bool hasFrontEnd = false,
            bool gameplayPaused = false, bool confirmingExit = false, bool confirmingRestart = false,
            Texture2D weaponPortrait = null)
        {
            EnsureResources();
            if(mission != null && mission.BlocksGameplay) { DrawMissionMenu(layout,mission); return; }
            bool dead = vitality != null && vitality.IsDead;
            if (showSticks && !dead && !gameplayPaused)
            {
                DrawStick(layout.LeftX, layout.LeftY, layout.Radius, sticks.Left, Petrol, false, controlsOpacity);
                DrawStick(layout.RightX, layout.RightY, layout.Radius, sticks.Right, Mustard, true, controlsOpacity);
            }
            if(vitality != null && vitality.Health != null) DrawHealth(layout.Health,vitality);
            else if (layout.ShowAreaBadge) DrawArea(layout.AreaBadge);
            if(dead && mission == null) DrawDefeat(layout);
            else if (!dead && magazine != null && magazine.isActiveAndEnabled) DrawMagazine(layout.Reload, magazine, weaponPortrait);
            if(mission != null) DrawMissionObjective(layout,mission,hasFrontEnd);
            // A tiny source-build stamp makes a stale/nested installation visible in a screenshot.
            Rect safe = Screen.safeArea;
            float stampWidth = Mathf.Min(110f, safe.width*.25f), stampHeight = 14f;
            Text(new Rect(safe.x+(safe.width-stampWidth)*.5f, Screen.height-safe.y-18f, stampWidth, stampHeight),
                "ЗАБОТА · V19", small, 8f, Paper, .55f);
            if (gameplayPaused) DrawPause(layout,confirmingExit,confirmingRestart,hasFrontEnd);
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
            DrawMissionCard(layout.MissionThird,"∞","СВОБОДНЫЙ РЕЖИМ",
                "Без врагов. Движение, стрельба и предметы.",Petrol);
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

        private void DrawMissionObjective(IntercomHudLayout layout,IntercomMissionDirector mission,bool hasFrontEnd)
        {
            Rect r=GuiRect(layout.MissionObjective);
            float sx=r.width/254f, sy=r.height/61f, s=Mathf.Min(sx,sy);
            PaintedLabel(r,Paper,.94f);
            bool rescue=mission.Mode == IntercomMissionMode.Rescue;
            string title=mission.Mode == IntercomMissionMode.FreePlay ? "СВОБОДНАЯ ТРЕНИРОВКА"
                : rescue ? (mission.IsAllyRecruited ? "ДОВЕДИ ДО ВЫХОДА" : "НАЙДИ СОЮЗНИКА") : "УДАРНИКИ";
            if (r.width < 180f && mission.Mode == IntercomMissionMode.FreePlay) title="ТРЕНИРОВКА";
            else if (r.width < 180f && rescue) title=mission.IsAllyRecruited ? "К ВЫХОДУ" : "СОЮЗНИК";
            RobotStamp(new Rect(r.x+7f*sx,r.y+7f*sy,36f*sx,43f*sy),rescue ? Petrol : Mustard);
            Text(new Rect(r.x+48f*sx,r.y+6f*sy,r.width-60f*sx,18f*sy),title,bold,11f*s,Ink,.8f);
            string progress=MissionProgress(mission);
            Text(new Rect(r.x+45f*sx,r.y+24f*sy,r.width-57f*sx,28f*sy),progress,number,(rescue ? 15f : 24f)*s,Ink);
            Rect timer=GuiRect(layout.Timer);
            Texture(timer,label,Ink,.72f);
            Text(timer,TimerText(mission),number,Mathf.Min(timer.width*.16f,timer.height*.48f),Paper);
            Rect menu=GuiRect(layout.Modes);
            PaintedLabel(menu,HeartRed,.98f);
            // Two chunky enamel bars form a readable thumb target, even on a small phone.
            Fill(new Rect(menu.x+menu.width*.27f,menu.y+menu.height*.20f,menu.width*.19f,menu.height*.59f),Ink);
            Fill(new Rect(menu.x+menu.width*.54f,menu.y+menu.height*.24f,menu.width*.19f,menu.height*.56f),Ink);
            Fill(new Rect(menu.x+menu.width*.30f,menu.y+menu.height*.23f,menu.width*.12f,menu.height*.50f),Paper);
            Fill(new Rect(menu.x+menu.width*.57f,menu.y+menu.height*.27f,menu.width*.12f,menu.height*.47f),Paper);
            string toast=SessionToast(mission);
            if (!string.IsNullOrEmpty(toast))
            {
                Rect t=GuiRect(layout.Toast);
                PaintedLabel(t,Paper,.95f);
                Fill(new Rect(t.x+t.width*.025f,t.y+t.height*.20f,t.width*.008f,t.height*.60f),Mustard);
                Text(new Rect(t.x+t.width*.05f,t.y+t.height*.12f,t.width*.90f,t.height*.76f),toast,bold,
                    Mathf.Min(t.width/32f,t.height*.28f),Ink);
            }
        }

        internal string TimerText(IntercomMissionDirector mission)
        {
            ObserveSession(mission);
            float elapsed=mission.ElapsedSeconds;
            int second=float.IsNaN(elapsed) || elapsed < 0f ? 0 : (int)Mathf.Min(359999f,elapsed);
            if (lastElapsedSecond != second)
            {
                lastElapsedSecond=second;
                elapsedText=(second/3600).ToString("00")+":"+(second/60%60).ToString("00")+":"+(second%60).ToString("00");
            }
            return elapsedText;
        }

        private void ObserveSession(IntercomMissionDirector mission)
        {
            if (ReferenceEquals(clockMission,mission) && clockRun == mission.RunRevision) return;
            clockMission=mission;clockRun=mission.RunRevision;lastElapsedSecond=-1;lastHint="";hintBegan=0f;
        }

        internal string SessionToast(IntercomMissionDirector mission)
        {
            ObserveSession(mission);
            string hint=mission.HudHint ?? "";
            if (hint != lastHint) { lastHint=hint;hintBegan=mission.ElapsedSeconds; }
            if (hint.Length>0 && mission.ElapsedSeconds-hintBegan < 5f) return hint;
            if (mission.ElapsedSeconds >= 5f) return "";
            if (mission.Mode == IntercomMissionMode.FreePlay) return "ОСМОТРИ ДВОР · ДВИГАЙСЯ И СТРЕЛЯЙ";
            if (mission.Mode == IntercomMissionMode.Rescue) return "НАЙДИ СОЮЗНИКА И ДОВЕДИ ДО ВЫХОДА";
            return "ОЧИСТИ ДВОР ОТ УДАРНИКОВ";
        }

        private void DrawPause(IntercomHudLayout layout,bool confirmingExit,bool confirmingRestart,bool hasFrontEnd)
        {
            Fill(new Rect(0f,0f,Screen.width,Screen.height),Ink,.72f);
            Rect r=GuiRect(layout.PausePanel);
            Texture(r,plate,Paper);
            Text(new Rect(r.x+r.width*.07f,r.y+r.height*.06f,r.width*.86f,r.height*.14f),
                confirmingRestart ? "НАЧАТЬ ВЫЕЗД ЗАНОВО?" : confirmingExit ? (hasFrontEnd ? "ВЕРНУТЬСЯ В МАСТЕРСКУЮ?" : "ВЕРНУТЬСЯ К РЕЖИМАМ?") : "НЕБОЛЬШОЙ ПЕРЕКУР",bold,r.width*.045f,Ink);
            Text(new Rect(r.x+r.width*.07f,r.y+r.height*.21f,r.width*.86f,r.height*.13f),
                confirmingRestart || confirmingExit ? "Текущий прогресс выезда будет потерян." : "Двор подождёт. Робот тоже.",small,r.width*.031f,Ink,.78f);
            if (confirmingExit || confirmingRestart)
            {
                DrawPauseButton(layout.PauseConfirm,confirmingRestart ? "ЗАНОВО" : hasFrontEnd ? "В МАСТЕРСКУЮ" : "К РЕЖИМАМ",Warning);
                DrawPauseButton(layout.PauseCancel,"ОСТАТЬСЯ",Petrol);
            }
            else
            {
                DrawPauseButton(layout.PauseResume,"ПРОДОЛЖИТЬ",Petrol);
                DrawPauseButton(layout.PauseRetry,"НАЧАТЬ ЗАНОВО",Mustard);
                DrawPauseButton(layout.PauseExit,hasFrontEnd ? "В МАСТЕРСКУЮ" : "ВЫБРАТЬ РЕЖИМ",Paper);
            }
        }

        private void DrawPauseButton(IntercomHudLayout.Box box,string text,Color accent)
        {
            Rect r=GuiRect(box);Texture(r,label,accent);
            Text(r,text,bold,Mathf.Min(r.height*.32f,r.width*.09f),Ink);
        }

        // OnGUI is called for layout/input/repaint. Stable scores and ally HP
        // must not allocate a new string in every pass throughout a wave.
        internal string MissionProgress(IntercomMissionDirector mission)
        {
            if(mission.Mode == IntercomMissionMode.FreePlay) return "БЕЗ ВРАГОВ";
            bool rescue=mission.Mode == IntercomMissionMode.Rescue;
            IntercomHealth ally=rescue ? mission.AllyHealth : null;
            bool hasAlly=ally != null;
            int current=rescue ? (hasAlly ? Mathf.Max(0,Mathf.CeilToInt(ally.Current)) : 0) : mission.Kills;
            int maximum=rescue ? (hasAlly ? Mathf.Max(1,Mathf.CeilToInt(ally.Maximum)) : 0) : mission.Goal;
            if(!progressReady || progressRescue!=rescue || progressHasAlly!=hasAlly
                || progressCurrent!=current || progressMaximum!=maximum)
            {
                progressReady=true; progressRescue=rescue; progressHasAlly=hasAlly;
                progressCurrent=current; progressMaximum=maximum;
                missionProgressText=rescue && !hasAlly ? "СОЮЗНИК ЖДЁТ"
                    : (rescue ? "КОРПУС  " : "") + current + " / " + maximum;
            }
            return missionProgressText;
        }

        private void DrawHealth(IntercomHudLayout.Box box, IntercomPlayerVitality vitality)
        {
            Rect r=GuiRect(box);
            IntercomHealth health=vitality.Health;
            int value=Mathf.Max(0,Mathf.CeilToInt(health.Current));
            int maximum=Mathf.Max(1,Mathf.CeilToInt(health.Maximum));
            if(value!=lastHealth || maximum!=lastMaximum)
            {lastHealth=value;lastMaximum=maximum;healthText=value+" / "+maximum;}
            float progress=Mathf.Clamp01(health.Current/Mathf.Max(1f,health.Maximum));
            Color accent=vitality.ShowHitRemaining>0f ? Warning : HeartRed;
            float icon=Mathf.Min(r.height,r.width*.19f), stroke=Mathf.Max(1f,r.height*.065f);
            Rect gauge=new Rect(r.x+icon*.70f,r.y+r.height*.13f,r.width-icon*.72f,r.height*.76f);
            Texture(gauge,label,Ink,.96f);
            Rect inner=new Rect(gauge.x+stroke,gauge.y+stroke,gauge.width-stroke*2f,gauge.height-stroke*2f);
            Texture(inner,label,HeartRed,.28f);
            if(progress>0f)Texture(new Rect(inner.x,inner.y,inner.width*progress,inner.height),label,accent);
            // Five rubbed divider marks keep the continuous health bar easy to judge at a glance.
            for(int i=1;i<5;i++)
            {
                float x=inner.x+inner.width*i/5f;
                Fill(new Rect(x,inner.y+inner.height*.12f,stroke*.60f,inner.height*.20f),Paper,.33f);
            }
            Text(new Rect(gauge.x+icon*.20f,gauge.y,gauge.width-icon*.20f,gauge.height),healthText,bold,
                Mathf.Min(r.height*.40f,gauge.width*.10f),Paper);
            Rect badge=new Rect(r.x,r.y,icon,icon);
            Texture(badge,heart,Ink);
            float inset=stroke*1.2f;
            Texture(new Rect(badge.x+inset,badge.y+inset,badge.width-inset*2f,badge.height-inset*2f),heart,accent);
            // A tiny lightning seam turns a heart into the robot's repairable power core.
            Fill(new Rect(badge.x+icon*.45f,badge.y+icon*.25f,icon*.10f,icon*.23f),Paper,.86f);
            Fill(new Rect(badge.x+icon*.37f,badge.y+icon*.44f,icon*.17f,icon*.08f),Paper,.86f);
            Fill(new Rect(badge.x+icon*.40f,badge.y+icon*.49f,icon*.08f,icon*.20f),Paper,.86f);
            if(progress<=.3f)Fill(new Rect(gauge.xMax-stroke*4f,gauge.y+stroke*2f,stroke*2f,gauge.height-stroke*4f),Paper,.9f);
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

        private void DrawMagazine(IntercomHudLayout.Box box, IntercomWeaponMagazine magazine, Texture2D weaponPortrait)
        {
            Rect r=GuiRect(box);
            float sx=r.width/230f, sy=r.height/82f, s=Mathf.Min(sx,sy);
            bool loading=magazine.IsReloading;
            bool low=magazine.CurrentAmmo <= Mathf.Max(2,magazine.Capacity/4);
            Color accent=loading ? Mustard : low ? Warning : Petrol;
            PaintedLabel(r,Ink,.90f);
            Rect sticker=new Rect(r.x+6f*sx,r.y+8f*sy,67f*sx,61f*sy);
            Texture(sticker,label,Paper);
            if (weaponPortrait != null)
                Texture(new Rect(r.x+8f*sx,r.y+7f*sy,63f*sx,63f*sy),weaponPortrait,Color.white);
            else
            {
                // A readable fallback for a weapon with no authored UI portrait.
                Pistol(new Rect(r.x+12f*sx,r.y+18f*sy,42f*sx,27f*sy),Petrol);
                Pistol(new Rect(r.x+23f*sx,r.y+33f*sy,42f*sx,27f*sy),Ink);
            }
            if (lastAmmo != magazine.CurrentAmmo)
            { lastAmmo=magazine.CurrentAmmo;ammoText=lastAmmo.ToString("00"); }
            if (lastCapacity != magazine.Capacity)
            { lastCapacity=magazine.Capacity;capacityText="/ "+lastCapacity; }
            Text(new Rect(r.x+78f*sx,r.y+4f*sy,59f*sx,43f*sy),ammoText,number,37f*s,low ? Warning : Paper);
            Text(new Rect(r.x+135f*sx,r.y+15f*sy,44f*sx,27f*sy),capacityText,bold,14f*s,Paper,.82f);
            Text(new Rect(r.x+184f*sx,r.y+10f*sy,35f*sx,31f*sy),"∞",number,24f*s,Paper,.72f);
            const int slots=12;
            float progress=Mathf.Clamp01(loading ? magazine.ReloadProgress : magazine.CurrentAmmo/(float)Mathf.Max(1,magazine.Capacity));
            for (int i=0;i<slots;i++)
            {
                Rect slot=new Rect(r.x+(80f+i*11.5f)*sx,r.y+49f*sy,8.5f*sx,7f*sy);
                Fill(slot,Paper,.18f);
                float portion=Mathf.Clamp01(progress*slots-i);
                if(portion>0f) Fill(new Rect(slot.x,slot.y,slot.width*portion,slot.height),accent);
            }
            string action=loading ? "ПЕРЕЗАРЯДКА" : Application.isMobilePlatform ? "ПЕРЕЗАРЯДИТЬ" : "R · ПЕРЕЗАРЯДИТЬ";
            Text(new Rect(r.x+78f*sx,r.y+60f*sy,145f*sx,15f*sy),action,bold,9f*s,Paper,.88f);
        }

        private void PaintedLabel(Rect r,Color color,float alpha=1f)
        {
            float line=Mathf.Max(1f,Mathf.Min(r.width,r.height)*.035f);
            Texture(new Rect(r.x,r.y+line,r.width,r.height-line),label,Ink,alpha*.78f);
            Texture(new Rect(r.x+line,r.y,r.width-line*2f,r.height-line*2f),label,color,alpha);
        }

        private void RobotStamp(Rect r,Color color)
        {
            // An uneven ink-drawn tin head, rather than a generic warning glyph.
            float line=Mathf.Max(1f,r.width*.065f);
            Fill(new Rect(r.x+r.width*.45f,r.y,line,r.height*.19f),Ink);
            Texture(new Rect(r.x+r.width*.39f,r.y,r.width*.18f,r.width*.18f),disc,color);
            PaintedLabel(new Rect(r.x,r.y+r.height*.16f,r.width,r.height*.71f),color);
            Fill(new Rect(r.x+r.width*.18f,r.y+r.height*.37f,r.width*.15f,r.height*.14f),Ink);
            Fill(new Rect(r.x+r.width*.60f,r.y+r.height*.39f,r.width*.16f,r.height*.12f),Ink);
            Fill(new Rect(r.x+r.width*.26f,r.y+r.height*.65f,r.width*.40f,line),Ink);
            Fill(new Rect(r.x+r.width*.10f,r.y+r.height*.85f,r.width*.24f,line),Ink);
            Fill(new Rect(r.x+r.width*.64f,r.y+r.height*.83f,r.width*.22f,line),Ink);
        }

        private static void Pistol(Rect r,Color color)
        {
            Fill(new Rect(r.x,r.y,r.width,r.height*.31f),color);
            Fill(new Rect(r.x+r.width*.60f,r.y+r.height*.26f,r.width*.27f,r.height*.71f),color);
            Fill(new Rect(r.x+r.width*.26f,r.y+r.height*.29f,r.width*.38f,r.height*.15f),color,.8f);
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
                heart = MakeTexture("Ink heart power core", 96, 96, 4);
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
                    if(kind == 4)
                    {
                        float hx=(x+.5f-width*.5f)/(width*.40f), hy=(y+.5f-height*.47f)/(height*.40f);
                        float edge=hx*hx+hy*hy-1f;
                        alpha=edge*edge*edge-hx*hx*hy*hy*hy <= 0f ? 1f : 0f;
                    }
                    else if (kind >= 2)
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
            if (heart != null) Object.Destroy(heart);
            plate = label = disc = ring = heart = null;
        }
    }
}
