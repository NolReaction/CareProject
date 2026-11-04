using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Cached workshop menu art. Only repaint draws; the input owner handles all presses.</summary>
    internal sealed class IntercomFrontEndView
    {
        private static readonly Color Ink = new Color(.105f,.16f,.17f);
        private static readonly Color Paper = new Color(.88f,.85f,.74f);
        private static readonly Color LightPaper = new Color(.96f,.93f,.84f);
        private static readonly Color Petrol = new Color(.32f,.48f,.46f);
        private static readonly Color Mustard = new Color(.79f,.64f,.36f);
        private static readonly Color Rust = new Color(.67f,.34f,.25f);
        private static readonly Color Quiet = new Color(.69f,.71f,.65f);
        private GUIStyle title, heading, body, small, centered;
        private Texture2D plate, tag, disc;
        private int healthValue = -1, magazineValue = -1;
        private float speedValue = -1f, damageValue = -1f, intervalValue = -1f, reloadValue = -1f;
        private string health = "100", speed = "3,3", damage = "15", magazine = "24", interval = "0,12", reload = "1,15";

        public void Draw(IntercomFrontEnd menu,IntercomFrontEndLayout layout)
        {
            EnsureResources(); UpdateStats(menu);
            Fill(new Rect(0f,0f,Screen.width,Screen.height),Ink,.94f);
            Rect panel=R(layout.Panel);Card(panel,Paper);
            Header(menu,layout);Tabs(menu,layout);Preview(menu,layout);
            if(menu.IsResult) Result(menu,layout);
            else if(menu.Section==IntercomFrontEndSection.Play) Modes(menu,layout);
            else if(menu.Section==IntercomFrontEndSection.Robot) Robot(layout);
            else if(menu.Section==IntercomFrontEndSection.Armory) Armory(menu,layout);
            else Home(layout);
            Rect footer=R(layout.Footer);
            Text(new Rect(footer.x+14f*layout.Scale,footer.y,footer.width*.74f,footer.height),
                string.IsNullOrEmpty(menu.Notice) ? "БОЛЬШЕ ЗАБОТЫ. МЕНЬШЕ ПОЛОМОК." : menu.Notice,
                small,Mathf.Max(7f,12f*layout.Scale),Ink,.8f);
            Text(new Rect(footer.x+footer.width*.76f,footer.y,footer.width*.22f,footer.height),
                "УЧАСТОК / 01",centered,Mathf.Max(7f,11f*layout.Scale),Ink,.55f);
        }
        private void UpdateStats(IntercomFrontEnd menu)
        {
            int hp=menu.playerHealth!=null?Mathf.Max(1,Mathf.CeilToInt(menu.playerHealth.Maximum)):100;
            float sp=menu.playerMotor!=null?menu.playerMotor.moveSpeed:3.3f;
            IntercomWeaponDefinition w=menu.weapon;
            int cap=w!=null?w.MagazineCapacity:24;
            float dmg=w!=null?w.Damage:15f, rate=w!=null?w.Interval:.12f, loading=w!=null?w.ReloadSeconds:1.15f;
            if(hp!=healthValue){healthValue=hp;health=hp.ToString();}
            if(sp!=speedValue){speedValue=sp;speed=sp.ToString("0.0");}
            if(cap!=magazineValue){magazineValue=cap;magazine=cap.ToString();}
            if(dmg!=damageValue){damageValue=dmg;damage=dmg.ToString("0.#");}
            if(rate!=intervalValue){intervalValue=rate;interval=rate.ToString("0.00");}
            if(loading!=reloadValue){reloadValue=loading;reload=loading.ToString("0.00");}
        }
        private void Header(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Rect r=R(l.Header);float s=l.Scale;
            float badge=Mathf.Min(r.height*.63f,52f*s);
            Rect mark=new Rect(r.x+18f*s,r.y+(r.height-badge)*.45f,badge,badge);
            DrawTexture(mark,tag,Petrol);
            Fill(new Rect(mark.x+badge*.21f,mark.y+badge*.27f,badge*.17f,badge*.20f),Paper);
            Fill(new Rect(mark.x+badge*.62f,mark.y+badge*.30f,badge*.17f,badge*.15f),Paper);
            Fill(new Rect(mark.x+badge*.28f,mark.y+badge*.66f,badge*.45f,badge*.055f),Paper,.8f);
            float left=mark.xMax+15f*s;
            Text(new Rect(left,r.y+6f*s,r.width*.52f,r.height-12f*s),string.IsNullOrEmpty(menu.gameTitle)?"ЗАБОТА":menu.gameTitle,title,Mathf.Max(12f,43f*s),Ink);
            if(!l.IsPortrait)
            {
                Text(new Rect(r.x+r.width*.69f,r.y+19f*s,r.width*.27f,22f*s),"СЕРВИСНЫЙ ДВОР",heading,14f*s,Ink);
                Text(new Rect(r.x+r.width*.69f,r.y+41f*s,r.width*.27f,20f*s),"РАБОТА НАЙДЁТСЯ КАЖДОМУ",small,9f*s,Ink,.65f);
            }
            Bolt(r.xMax-13f*s,r.y+16f*s,3f*s);Bolt(r.x+9f*s,r.yMax-8f*s,2f*s);
        }
        private void Tabs(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Tab(l.TabHome,"ГЛАВНАЯ",menu.Section==IntercomFrontEndSection.Home,l.Scale);
            Tab(l.TabPlay,"ИГРАТЬ",menu.Section==IntercomFrontEndSection.Play,l.Scale);
            Tab(l.TabRobot,"РОБОТ",menu.Section==IntercomFrontEndSection.Robot,l.Scale);
            Tab(l.TabArmory,"ОРУЖЕЙНАЯ",menu.Section==IntercomFrontEndSection.Armory,l.Scale);
        }
        private void Tab(IntercomHudLayout.Box box,string label,bool selected,float scale)
        {
            Rect r=R(box);DrawTexture(r,tag,selected?Petrol:Quiet);
            if(selected)Fill(new Rect(r.x+r.width*.12f,r.yMax-6f*scale,r.width*.76f,3f*scale),Mustard);
            Text(new Rect(r.x+5f*scale,r.y+2f*scale,r.width-10f*scale,r.height-6f*scale),label,centered,
                Mathf.Max(7f,16f*scale),selected?LightPaper:Ink);
        }
        private void Preview(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Rect r=R(l.HeroPreview);float s=l.Scale;
            DrawTexture(r,tag,Petrol);
            float grid=Mathf.Max(15f,35f*s);
            for(float x=r.x+grid;x<r.xMax;x+=grid)Fill(new Rect(x,r.y+5f*s,1f,r.height-10f*s),Paper,.065f);
            for(float y=r.y+grid;y<r.yMax;y+=grid)Fill(new Rect(r.x+6f*s,y,r.width-12f*s,1f),Paper,.065f);
            Text(new Rect(r.x+18f*s,r.y+25f*s,r.width-36f*s,24f*s),
                menu.Section==IntercomFrontEndSection.Armory?"ПАРНЫЕ ПИСТОЛЕТЫ":"СЕРИЙНЫЙ № 01",heading,Mathf.Max(8f,15f*s),LightPaper);
            Rect art=new Rect(r.x+16f*s,r.y+34f*s,r.width-32f*s,r.height-77f*s);
            Texture2D portrait=menu.Section==IntercomFrontEndSection.Armory && menu.weaponPortrait!=null?menu.weaponPortrait:menu.heroPortrait;
            if(portrait!=null)
            {
                float ratio=(float)portrait.width/Mathf.Max(1,portrait.height);
                float width=Mathf.Min(art.width,art.height*ratio),height=width/Mathf.Max(.001f,ratio);
                DrawTexture(new Rect(art.x+(art.width-width)*.5f,art.y+(art.height-height)*.5f,width,height),portrait,Color.white);
            }
            else RobotSketch(art);
            Text(new Rect(r.x+15f*s,r.yMax-35f*s,r.width-30f*s,22f*s),
                menu.IsResult?(menu.mission.Phase==IntercomMissionPhase.Victory?"РАБОТА СДЕЛАНА":"НУЖЕН НЕБОЛЬШОЙ РЕМОНТ"):"НА СВЯЗИ. ГОТОВ К ВЫЕЗДУ.",
                centered,Mathf.Max(7f,10f*s),Paper,.85f);
            Bolt(r.x+12f*s,r.y+12f*s,2.5f*s);Bolt(r.xMax-12f*s,r.yMax-12f*s,2.5f*s);
        }
        private void Home(IntercomFrontEndLayout l)
        {
            Rect r=R(l.Content);float s=l.Scale;
            Text(new Rect(r.x+18f*s,r.y+10f*s,r.width-36f*s,r.height*.17f),"ДВОР САМ СЕБЯ\nНЕ ПОЧИНИТ.",title,Mathf.Max(10f,30f*s),Ink);
            Text(new Rect(r.x+18f*s,r.y+r.height*.22f,r.width-36f*s,r.height*.13f),
                "Ударники снова безобразничают.\nПора выйти и вернуть порядок.",body,Mathf.Max(8f,16f*s),Ink,.82f);
            float top=r.y+r.height*.40f,row=r.height*.14f;
            Feature(new Rect(r.x+18f*s,top,r.width-36f*s,row),"01","ВЫБЕРИ РАБОТУ","Очистка двора, спасение или тренировка.",Petrol,s);
            Feature(new Rect(r.x+18f*s,top+row*1.12f,r.width-36f*s,row),"02","СОБЕРИ СВОЕГО РОБОТА","Здесь будет твоя мастерская и оружейная.",Mustard,s);
            Button(l.ActionPrimary,"НА ВЫЕЗД  →",Petrol,s);Button(l.ActionSecondary,"МОЙ РОБОТ",Quiet,s);
        }
        private void Modes(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Mode(l.ModeFirst,"01","50 РОБОТОВ","ОЧИСТКА ДВОРА","Волны ударников. Отключи всех и верни тишину.",Petrol,l.Scale);
            Mode(l.ModeSecond,"02","СПАСЕНИЕ","ВЫВЕСТИ СВОЕГО","Найди союзника и доведи его до выхода.",Mustard,l.Scale);
            Mode(l.ModeThird,"∞","СВОБОДНЫЙ РЕЖИМ","БЕЗ ВРАГОВ","Проверь движение, оружие и предметы двора.",Quiet,l.Scale);
        }
        private void Robot(IntercomFrontEndLayout l)
        {
            Rect r=R(l.Content);float s=l.Scale;
            PageTitle(r,"ТВОЯ МАСТЕРСКАЯ","Начинаем с надёжного корпуса.",s);
            float top=r.y+r.height*.23f;
            StatPair(new Rect(r.x+14f*s,top,r.width-28f*s,r.height*.16f),"КОРПУС",health,"СКОРОСТЬ",speed,s);
            float row=r.height*.115f;
            UpgradeRow(new Rect(r.x+14f*s,top+r.height*.20f,r.width-28f*s,row),"01","КОРПУС","Бронепластины и прочные детали.",s);
            UpgradeRow(new Rect(r.x+14f*s,top+r.height*.20f+row*1.12f,r.width-28f*s,row),"02","ШАССИ","Ходовая часть и новые ботинки.",s);
            UpgradeRow(new Rect(r.x+14f*s,top+r.height*.20f+row*2.24f,r.width-28f*s,row),"03","ВНЕШНИЙ ВИД","Необычные детали и собственный характер.",s);
            Button(l.ActionPrimary,"ВЫБРАТЬ ВЫЕЗД",Petrol,s);Button(l.ActionSecondary,"ОРУЖЕЙНАЯ  →",Quiet,s);
        }
        private void Armory(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Rect r=R(l.Content);float s=l.Scale;
            PageTitle(r,"ОРУЖЕЙНАЯ","ПАРНЫЕ ПИСТОЛЕТЫ  /  СНАРЯЖЕНО",s);
            float top=r.y+r.height*.23f;
            StatPair(new Rect(r.x+14f*s,top,r.width-28f*s,r.height*.14f),"УРОН",damage,"МАГАЗИН",magazine,s);
            StatPair(new Rect(r.x+14f*s,top+r.height*.155f,r.width-28f*s,r.height*.14f),"ИНТЕРВАЛ, С",interval,"ПЕРЕЗАРЯДКА, С",reload,s);
            UpgradeRow(new Rect(r.x+14f*s,top+r.height*.33f,r.width-28f*s,r.height*.12f),"+","УЛУЧШЕНИЯ ОРУЖИЯ","Будущие уровни и новые модификации.",s);
            UpgradeRow(new Rect(r.x+14f*s,top+r.height*.47f,r.width-28f*s,r.height*.12f),"?","НОВОЕ ОРУЖИЕ","В оружейной появятся другие типы.",s);
            Button(l.ActionPrimary,"ВЫБРАТЬ ВЫЕЗД",Petrol,s);Button(l.ActionSecondary,"К РОБОТУ  →",Quiet,s);
        }
        private void Result(IntercomFrontEnd menu,IntercomFrontEndLayout l)
        {
            Rect r=R(l.Content);float s=l.Scale;
            bool victory=menu.mission.Phase==IntercomMissionPhase.Victory;
            PageTitle(r,victory?"ДВОР СНОВА РАБОТАЕТ":"КОЕ-ЧТО СЛОМАЛОСЬ",victory?"ЗАДАНИЕ ВЫПОЛНЕНО":"ЭТО МОЖНО ПОЧИНИТЬ",s);
            Text(new Rect(r.x+18f*s,r.y+r.height*.27f,r.width-36f*s,r.height*.16f),
                string.IsNullOrEmpty(menu.mission.ResultText)?(victory?"Хорошая работа, напарник.":"Соберём корпус и попробуем ещё раз."):menu.mission.ResultText,
                body,Mathf.Max(9f,19f*s),Ink,.9f);
            string result=menu.mission.Mode==IntercomMissionMode.Elimination?"ОТКЛЮЧЕНО  "+menu.mission.Kills+" / "+menu.mission.Goal:
                menu.mission.Mode==IntercomMissionMode.Rescue?(victory?"СОЮЗНИК ДОМА":"СОЮЗНИК ЖДЁТ ПОМОЩИ"):"ТРЕНИРОВКА ЗАВЕРШЕНА";
            Rect stamp=new Rect(r.x+18f*s,r.y+r.height*.49f,r.width-36f*s,r.height*.18f);
            DrawTexture(stamp,tag,victory?Mustard:Rust);Text(stamp,result,centered,Mathf.Max(8f,22f*s),victory?Ink:LightPaper);
            Button(l.ActionPrimary,"ПОПРОБОВАТЬ ЕЩЁ",Petrol,s);Button(l.ActionSecondary,"НА ГЛАВНУЮ",Quiet,s);
        }
        private void PageTitle(Rect r,string text,string caption,float s)
        {
            Text(new Rect(r.x+15f*s,r.y+8f*s,r.width-30f*s,r.height*.12f),text,title,Mathf.Max(10f,26f*s),Ink);
            Text(new Rect(r.x+16f*s,r.y+r.height*.13f,r.width-32f*s,r.height*.07f),caption,small,Mathf.Max(7f,12f*s),Ink,.7f);
        }
        private void Mode(IntercomHudLayout.Box box,string badge,string name,string category,string detail,Color accent,float s)
        {
            Rect r=R(box);DrawTexture(r,tag,accent);
            float stamp=Mathf.Min(r.height*.62f,r.width*.12f),left=r.x+stamp+27f*s;
            Rect mark=new Rect(r.x+13f*s,r.y+(r.height-stamp)*.5f,stamp,stamp);DrawTexture(mark,plate,Paper);
            Text(mark,badge,centered,Mathf.Max(8f,25f*s),Ink);
            Text(new Rect(left,r.y+12f*s,r.width-(left-r.x)-14f*s,r.height*.17f),category,small,Mathf.Max(7f,9f*s),Ink,.7f);
            Text(new Rect(left,r.y+r.height*.25f,r.width-(left-r.x)-14f*s,r.height*.29f),name,heading,Mathf.Max(8f,24f*s),Ink);
            Text(new Rect(left,r.y+r.height*.62f,r.width-(left-r.x)-18f*s,r.height*.24f),detail,body,Mathf.Max(7f,12f*s),Ink,.82f);
        }
        private void Feature(Rect r,string badge,string name,string detail,Color accent,float s)
        {
            float size=Mathf.Min(r.height*.85f,42f*s);Rect b=new Rect(r.x,r.y+2f*s,size,size);
            DrawTexture(b,tag,accent);Text(b,badge,centered,Mathf.Max(8f,16f*s),Ink);
            Text(new Rect(b.xMax+12f*s,r.y,r.width-size-14f*s,r.height*.45f),name,heading,Mathf.Max(8f,15f*s),Ink);
            Text(new Rect(b.xMax+12f*s,r.y+r.height*.48f,r.width-size-14f*s,r.height*.45f),detail,body,Mathf.Max(7f,11f*s),Ink,.7f);
        }
        private void StatPair(Rect r,string first,string a,string second,string b,float s)
        {
            float gap=10f*s,w=(r.width-gap)*.5f;
            Stat(new Rect(r.x,r.y,w,r.height),first,a,s);Stat(new Rect(r.x+w+gap,r.y,w,r.height),second,b,s);
        }
        private void Stat(Rect r,string label,string value,float s)
        {
            DrawTexture(r,tag,LightPaper);
            Text(new Rect(r.x+10f*s,r.y+5f*s,r.width*.65f,r.height*.28f),label,small,Mathf.Max(7f,10f*s),Ink,.65f);
            Text(new Rect(r.x+10f*s,r.y+r.height*.33f,r.width-20f*s,r.height*.56f),value,title,Mathf.Max(10f,27f*s),Ink);
        }
        private void UpgradeRow(Rect r,string badge,string name,string detail,float s)
        {
            DrawTexture(r,tag,Quiet,.5f);
            Rect b=new Rect(r.x+8f*s,r.y+5f*s,r.height-10f*s,r.height-10f*s);DrawTexture(b,plate,Paper,.7f);
            Text(b,badge,centered,Mathf.Max(7f,13f*s),Ink,.5f);
            float left=b.xMax+10f*s;
            Text(new Rect(left,r.y+4f*s,r.width-(left-r.x)-76f*s,r.height*.43f),name,heading,Mathf.Max(7f,12f*s),Ink,.65f);
            Text(new Rect(left,r.y+r.height*.48f,r.width-(left-r.x)-12f*s,r.height*.4f),detail,small,Mathf.Max(7f,10f*s),Ink,.6f);
            Text(new Rect(r.xMax-75f*s,r.y+5f*s,64f*s,r.height*.33f),"СКОРО",centered,Mathf.Max(7f,8f*s),Ink,.5f);
        }
        private void Button(IntercomHudLayout.Box box,string label,Color color,float s)
        {
            Rect r=R(box);DrawTexture(new Rect(r.x,r.y+3f*s,r.width,r.height),tag,Ink,.18f);DrawTexture(r,tag,color);
            Text(new Rect(r.x+4f*s,r.y,r.width-8f*s,r.height),label,centered,Mathf.Max(7f,15f*s),color.r<.5f?LightPaper:Ink);
        }
        private void RobotSketch(Rect r)
        {
            float k=Mathf.Min(r.width/240f,r.height/300f),x=r.x+r.width*.5f,y=r.y+r.height*.43f;
            DrawTexture(new Rect(x-57f*k,y-73f*k,114f*k,137f*k),plate,Paper);
            Fill(new Rect(x-32f*k,y-40f*k,64f*k,49f*k),Ink);
            for(int i=0;i<5;i++)Fill(new Rect(x-25f*k,y-32f*k+i*8f*k,50f*k,2f*k),Quiet,.8f);
            Fill(new Rect(x-29f*k,y+27f*k,13f*k,9f*k),Rust);Fill(new Rect(x+18f*k,y+27f*k,14f*k,9f*k),Mustard);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<5;i++)Fill(new Rect(x+side*67f*k-9f*k,y-24f*k+i*11f*k,18f*k,5f*k),Mustard,.9f);
                DrawTexture(new Rect(x+(side<0?-59f:10f)*k,y+72f*k,49f*k,26f*k),tag,Ink);
            }
        }
        private static Rect R(IntercomHudLayout.Box b)=>new Rect(b.X,Screen.height-b.Y-b.Height,b.Width,b.Height);
        private void Card(Rect r,Color color)
        {
            Fill(r,Ink,.85f);
            Fill(new Rect(r.x+2f,r.y+2f,r.width-4f,r.height-4f),color);
            Fill(new Rect(r.x+7f,r.y+3f,r.width-15f,2f),LightPaper,.65f);
        }
        private static void Fill(Rect r,Color c,float alpha=1f)=>DrawTexture(r,Texture2D.whiteTexture,c,alpha);
        private static void DrawTexture(Rect r,Texture texture,Color c,float alpha=1f)
        {if(r.width<=0f||r.height<=0f)return;GUI.color=new Color(c.r,c.g,c.b,c.a*alpha);GUI.DrawTexture(r,texture);}
        private static void Text(Rect r,string text,GUIStyle style,float size,Color color,float alpha=1f)
        {
            if(r.width<=0f||r.height<=0f)return;
            style.fontSize=Mathf.Max(6,Mathf.RoundToInt(size));style.normal.textColor=new Color(color.r,color.g,color.b,color.a*alpha);
            GUI.color=Color.white;GUI.Label(r,text,style);
        }
        private void Bolt(float x,float y,float size)
        {DrawTexture(new Rect(x-size,y-size,size*2f,size*2f),disc,Ink,.55f);Fill(new Rect(x-size*.65f,y-size*.12f,size*1.3f,size*.24f),Paper,.65f);}
        private void EnsureResources()
        {
            if(plate==null){plate=MakeTexture("Front end paper",256,128,0);tag=MakeTexture("Front end cut label",256,128,1);disc=MakeTexture("Front end screw",32,32,2);}
            if(title!=null)return;
            body=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleLeft,wordWrap=true,richText=false};
            small=new GUIStyle(body);heading=new GUIStyle(body){fontStyle=FontStyle.Bold};title=new GUIStyle(heading);
            centered=new GUIStyle(heading){alignment=TextAnchor.MiddleCenter};
        }
        private static Texture2D MakeTexture(string name,int w,int h,int kind)
        {
            Texture2D t=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            Color32[] pixels=new Color32[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float edge,shade=1f;
                if(kind==2)
                {float dx=(x+.5f-w*.5f)/(w*.5f),dy=(y+.5f-h*.5f)/(h*.5f);edge=(1f-Mathf.Sqrt(dx*dx+dy*dy))*w*.5f;}
                else
                {
                    float left=kind==0?3f:1.5f,right=w-(kind==0?4f:2f),bottom=kind==0?4f:4f+x*.008f,top=h-(kind==0?3f:4f-x*.006f);
                    edge=Mathf.Min(Mathf.Min(x-left,right-x),Mathf.Min(y-bottom,top-y));edge=Mathf.Min(edge,x+y-(kind==0?14f:8f));edge=Mathf.Min(edge,w-x+h-y-(kind==0?11f:7f));
                    if(kind==0){if(edge<2f)shade=.2f;else if(y<8f)shade=.75f;}
                }
                byte c=(byte)(Mathf.Clamp01(shade)*255f);pixels[y*w+x]=new Color32(c,c,c,(byte)(Mathf.Clamp01(edge)*255f));
            }
            t.SetPixels32(pixels);t.Apply(false,true);return t;
        }
        public void Dispose()
        {if(plate!=null)Object.Destroy(plate);if(tag!=null)Object.Destroy(tag);if(disc!=null)Object.Destroy(disc);plate=tag=disc=null;}
    }
}
