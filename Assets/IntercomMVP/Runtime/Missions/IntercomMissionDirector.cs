using System;
using System.Collections.Generic;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Owns one local mission, a bounded reusable enemy pool and a rescue ally. No global pause or unbounded spawning.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class IntercomMissionDirector : MonoBehaviour
    {
        public IntercomHealth playerHealth;
        public IntercomPlayerMotor playerMotor;
        public IntercomPlayerVitality playerVitality;
        public IntercomNpcNavigation navigation;
        public IntercomNpcDefinition enemyDefinition;
        public Transform[] spawnPoints;
        public Transform allySpawn;
        public Transform extractionPoint;
        public IntercomMissionDefinition eliminationDefinition;
        public IntercomMissionDefinition rescueDefinition;

        public IntercomMissionPhase Phase => state.Phase;
        public IntercomMissionMode Mode => state.Mode;
        public bool BlocksGameplay => Phase != IntercomMissionPhase.Active;
        public int Kills => state.Kills;
        public int Goal => state.Goal;
        public int KillTarget => Goal;
        public int AliveEnemies => state.Alive;
        public int SpawnedEnemies => state.Spawned;
        public int Wave => wave;
        /// <summary>Constructed reusable actors, including inactive slots. Never exceeds eight.</summary>
        public int PreparedEnemyCount
        {
            get
            {
                int count=0;
                for(int i=0;i<pool.Length;i++) if(pool[i]!=null && pool[i].actor!=null) count++;
                return count;
            }
        }
        public bool IsAllyRecruited => ally != null && ally.Recruited;
        public bool RescueRecruited => IsAllyRecruited;
        public IntercomHealth AllyHealth => ally != null ? ally.Health : null;
        public string ResultText { get; private set; } = "";
        public string HudHint
        {
            get
            {
                if(Phase != IntercomMissionPhase.Active) return "";
                if(Mode == IntercomMissionMode.Elimination && spawnBlockedTime > 5f)
                    return "Освободи точки появления";
                if(Mode == IntercomMissionMode.Rescue && ally != null && ally.WaitingForPlayer)
                    return "Союзник ждёт — вернись к нему";
                return "";
            }
        }
        public string ObjectiveText
        {
            get
            {
                if(Phase == IntercomMissionPhase.Selection) return "Выберите задание";
                if(Phase == IntercomMissionPhase.Victory) return Mode == IntercomMissionMode.Elimination ? "Двор очищен" : "Союзник спасён";
                if(Phase == IntercomMissionPhase.Defeat) return ResultText;
                if(Mode == IntercomMissionMode.FreePlay) return "Свободная тренировка — без врагов";
                if(Mode == IntercomMissionMode.Elimination)
                {
                    if(spawnBlockedTime > 5f) return "Отойдите от точек появления подкрепления";
                    // IMGUI may ask multiple times per rendered frame. Rebuild
                    // the score label only when its displayed values change.
                    if(objectiveKills != Kills || objectiveGoal != Goal)
                    {
                        objectiveKills = Kills; objectiveGoal = Goal;
                        eliminationObjective = "Уничтожьте роботов: " + Kills + " / " + Goal;
                    }
                    return eliminationObjective;
                }
                if(ally == null || !ally.Recruited) return "Найдите зелёную капсулу и подойдите к ней";
                if(ally.WaitingForPlayer) return "Вернитесь к союзнику — он ждёт вас";
                return "Доведите союзника до зелёной площадки у входа";
            }
        }

        private sealed class Slot
        {
            public IntercomNpcController actor;
            public uint life, epoch;
            public bool living;
            public float reuseAfter;
            public float targetChangedAt;
            public Action deathHandler;
        }
        private readonly Slot[] pool = new Slot[8];
        private readonly IntercomMissionState state = new IntercomMissionState();
        private readonly List<Vector3> spawnRoute = new List<Vector3>(128);
        private readonly RaycastHit[] sightHits = new RaycastHit[24];
        private IntercomMissionDefinition profile;
        private IntercomRescueAlly ally;
        private IntercomHealth subscribedHero;
        private GameObject extractionVisual;
        private uint observedRespawn;
        private int spawnCursor, wave = 1, waveLimit;
        private float spawnTimer, waveRest = -1f, targetTimer, spawnBlockedTime;
        private bool focused = true, paused, transitioning;
        private Camera displayCamera;
        private GUIStyle guidanceStyle;
        private int objectiveKills = -1, objectiveGoal = -1;
        private string eliminationObjective = "";
        private string guidanceLabel = "";
        private int guidanceMeters = -1;
        private bool guidanceWasExit;
        private IntercomPlayerInput connectedInput;
        private IntercomPlayerVitality controlOwner;
        private bool connectionWarningShown;
        private static Vector3 Flat(Vector3 value) => new Vector3(value.x,0f,value.z);
        private static float Safe(float value,float fallback,float min,float max) => IntercomNpcDefinition.Safe(value,fallback,min,max);

        private void Awake()
        {
            for(int i=0;i<pool.Length;i++) pool[i]=new Slot();
            observedRespawn=playerMotor != null ? playerMotor.RespawnVersion : 0;
        }
        private void OnEnable()
        {
            SubscribeHero(); ApplyBlock();
        }
        private void Start() { SubscribeHero(); ApplyBlock(); }

        private bool ConnectPlayerInput()
        {
            if(playerVitality==null && playerMotor!=null) playerVitality=playerMotor.GetComponent<IntercomPlayerVitality>();
            if(playerVitality==null && playerHealth!=null) playerVitality=playerHealth.GetComponent<IntercomPlayerVitality>();
            if(playerVitality==null || !playerVitality.isActiveAndEnabled) return false;
            if(!playerVitality.CanSetMissionBlocked(this)) return false;
            if(playerMotor==null) playerMotor=playerVitality.motor!=null ? playerVitality.motor : playerVitality.GetComponent<IntercomPlayerMotor>();
            if(playerHealth==null) playerHealth=playerVitality.health!=null ? playerVitality.health : playerVitality.GetComponent<IntercomHealth>();
            IntercomPlayerInput source=playerVitality.input;
            if(source==null) source=playerVitality.GetComponent<IntercomPlayerInput>();
            if(source==null || !source.isActiveAndEnabled) return false;
            if(source.mission!=null && source.mission!=this && source.mission.isActiveAndEnabled) return false;
            if(connectedInput!=null && connectedInput!=source && connectedInput.mission==this) connectedInput.mission=null;
            connectedInput=source;
            source.mission=this;
            source.vitality=playerVitality;
            playerVitality.input=source;
            if(playerVitality.motor==null) playerVitality.motor=playerMotor;
            if(playerVitality.health==null) playerVitality.health=playerHealth;
            return true;
        }

        private void ReleaseControlLock()
        {
            if(controlOwner!=null) controlOwner.SetMissionBlocked(this,false);
            controlOwner=null;
        }
        private void SubscribeHero()
        {
            if(subscribedHero==playerHealth) return;
            if(subscribedHero!=null) subscribedHero.Died-=OnHeroDied;
            subscribedHero=playerHealth;
            if(subscribedHero!=null) subscribedHero.Died+=OnHeroDied;
        }
        private void OnHeroDied()
        {
            if(transitioning || Phase!=IntercomMissionPhase.Active) return;
            state.Fail(); ResultText="Корпус героя отключён"; Finish();
        }
        private void OnAllyDied()
        {
            if(transitioning || Phase!=IntercomMissionPhase.Active || Mode!=IntercomMissionMode.Rescue) return;
            state.Fail(); ResultText="Союзник потерян"; Finish();
        }
        public bool StartMission(IntercomMissionMode mode)
        {
            if(!isActiveAndEnabled || !focused || paused || Time.timeScale<=0f || !Application.isPlaying) return false;
            return BeginRun(mode,true);
        }
        public bool RetryMission() => StartMission(Mode);
        public void ReturnToSelection()
        {
            if(!isActiveAndEnabled) return;
            transitioning=true; StopActors();
            if(ally!=null) ally.gameObject.SetActive(false);
            if(extractionVisual!=null) extractionVisual.SetActive(false);
            state.Select(); ResultText=""; transitioning=false; ApplyBlock();
        }
        private bool BeginRun(IntercomMissionMode mode,bool resetPlayer)
        {
            if(mode!=IntercomMissionMode.Elimination && mode!=IntercomMissionMode.Rescue && mode!=IntercomMissionMode.FreePlay) return false;
            var candidate=mode==IntercomMissionMode.FreePlay ? null
                : mode==IntercomMissionMode.Elimination ? eliminationDefinition : rescueDefinition;
            if(!Ready(candidate,mode,out Vector3 rescuePosition)) return false;
            transitioning=true;
            if(controlOwner!=playerVitality) ReleaseControlLock();
            if(!playerVitality.SetMissionBlocked(this,true))
            { ResultText="Управлением уже владеет другой контроллер"; transitioning=false; return false; }
            controlOwner=playerVitality;
            StopActors();
            if(ally!=null) ally.gameObject.SetActive(false);
            if(resetPlayer && !playerVitality.Retry())
            {
                state.Select(); ResultText="Не удалось вернуть героя на старт"; transitioning=false; ApplyBlock(); return false;
            }
            if(!resetPlayer) playerHealth.ResetHealth();
            observedRespawn=playerMotor.RespawnVersion;
            profile=candidate;
            state.Begin(mode,profile!=null ? Mathf.Clamp(profile.killTarget,1,500) : 0,
                profile!=null ? Mathf.Clamp(profile.rescueEnemySupply,1,500) : 0);
            wave=profile!=null ? 1 : 0; waveLimit=profile!=null ? Mathf.Min(state.Supply,Mathf.Clamp(profile.waveSize,1,50)) : 0;
            spawnCursor=0; spawnTimer=.8f; waveRest=-1f; targetTimer=0f; spawnBlockedTime=0f; ResultText="";
            if(mode==IntercomMissionMode.Rescue)
            {
                if(ally==null)
                {
                    ally=IntercomRescueAlly.Create(navigation,playerHealth,enemyDefinition,profile,rescuePosition);
                    ally.transform.SetParent(transform,true); ally.Health.Died+=OnAllyDied;
                }
                ally.ResetForMission(profile,rescuePosition); ally.gameObject.SetActive(true);
                EnsureExtractionVisual(); extractionVisual.SetActive(true);
            }
            else if(extractionVisual!=null) extractionVisual.SetActive(false);
            transitioning=false; ApplyBlock(); return true;
        }
        private bool Ready(IntercomMissionDefinition candidate,IntercomMissionMode mode,out Vector3 rescuePosition)
        {
            rescuePosition=Vector3.zero;
            if(!ConnectPlayerInput())
            { ResultText="Не подключён активный ввод героя"; return false; }
            if(mode==IntercomMissionMode.FreePlay)
            {
                if(playerHealth!=null && playerMotor!=null && playerVitality!=null) return true;
                ResultText="Не подключён герой для свободного режима"; return false;
            }
            if(candidate==null || candidate.mode!=mode || playerHealth==null || playerMotor==null || playerVitality==null
                || navigation==null || !navigation.isActiveAndEnabled || enemyDefinition==null || spawnPoints==null || spawnPoints.Length==0
                || enemyDefinition.bodyMaterial==null || enemyDefinition.detailMaterial==null || enemyDefinition.telegraphMaterial==null)
            { ResultText="Задание не настроено: проверьте ссылки Mission Director"; return false; }
            if(!navigation.IsReady) navigation.RebuildObstacleCache();
            if(!navigation.IsReady) { ResultText="Навигация двора ещё не готова"; return false; }
            bool usableSpawn=false;
            for(int i=0;i<spawnPoints.Length;i++)
                if(spawnPoints[i]!=null && navigation.TryProject(spawnPoints[i].position,out Vector3 unused)) { usableSpawn=true; break; }
            if(!usableSpawn) { ResultText="Нет доступных точек появления врагов"; return false; }
            if(mode==IntercomMissionMode.Rescue && (allySpawn==null || extractionPoint==null
                || !navigation.TryProject(allySpawn.position,out rescuePosition)
                || !navigation.TryProject(extractionPoint.position,out Vector3 extraction)))
            { ResultText="Не настроены доступные точки спасения и выхода"; return false; }
            return true;
        }
        private void Update()
        {
            SubscribeHero();
            if(playerVitality==null || !playerVitality.isActiveAndEnabled
                || connectedInput==null || !connectedInput.isActiveAndEnabled || connectedInput.mission!=this
                || connectedInput.vitality!=playerVitality || playerVitality.input!=connectedInput || controlOwner!=playerVitality)
            {
                if(!ApplyBlock())
                {
                    if(Phase==IntercomMissionPhase.Active) ReturnToSelection();
                    return;
                }
            }
            // Spread primitive construction/Awake/mesh allocation over menu
            // frames (or the initial grace period after an immediate selection).
            // Enemy arrival and death frames must only reuse prepared actors.
            if(!transitioning && focused && !paused && Time.timeScale>0f
                && (Phase==IntercomMissionPhase.Selection || (Phase==IntercomMissionPhase.Active && Mode!=IntercomMissionMode.FreePlay && state.Spawned==0)))
                PrepareOneEnemy();
            if(Phase!=IntercomMissionPhase.Active || transitioning) return;
            if(playerMotor!=null && playerMotor.RespawnVersion!=observedRespawn)
            {
                // Backspace uses the existing explicit reset epoch. Restart mission once without a second teleport.
                if(!BeginRun(Mode,false)) ReturnToSelection();
                return;
            }
            if(!focused || paused || Time.timeScale<=0f) return;
            float dt=Time.deltaTime;
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0f) return;
            dt=Mathf.Min(dt,.1f);
            if(playerHealth==null || playerHealth.IsDead) { OnHeroDied(); return; }
            if(Mode==IntercomMissionMode.FreePlay) return;
            if(Mode==IntercomMissionMode.Rescue)
            {
                if(ally==null || ally.Health==null || ally.Health.IsDead) { OnAllyDied(); return; }
                float radius=Safe(profile.extractionRadius,2f,.75f,5f);
                Vector3 exit=extractionPoint.position;
                bool allyInside=Flat(ally.transform.position-exit).sqrMagnitude<=radius*radius;
                bool heroInside=Flat(playerHealth.transform.position-exit).sqrMagnitude<=radius*radius;
                if(state.CompleteRescue(ally.Recruited,allyInside,heroInside))
                { ResultText="Союзник в безопасности"; Finish(); return; }
            }
            for(int i=0;i<pool.Length;i++)
            {
                Slot slot=pool[i];
                if(!slot.living && slot.actor!=null && slot.actor.gameObject.activeSelf && Time.time>=slot.reuseAfter)
                    slot.actor.gameObject.SetActive(false);
            }
            targetTimer-=dt;
            if(targetTimer<=0f) { targetTimer=.65f; RefreshTargets(); }
            if(state.Spawned>=waveLimit && state.Alive==0 && state.Spawned<state.Supply)
            {
                if(waveRest<0f) waveRest=Safe(profile.waveRestSeconds,2.5f,0f,15f);
                waveRest-=dt;
                if(waveRest<=0f)
                {
                    wave++; waveLimit=Mathf.Min(state.Supply,waveLimit+Mathf.Clamp(profile.waveSize,1,50));
                    waveRest=-1f; spawnTimer=.1f;
                }
            }
            spawnTimer-=dt;
            if(state.Spawned<waveLimit && state.Alive<Mathf.Clamp(profile.maximumAlive,1,8) && spawnTimer<=0f)
            {
                bool spawned=TrySpawnOne();
                spawnTimer=spawned ? Safe(profile.spawnInterval,1.2f,.25f,5f) : .2f;
                spawnBlockedTime=spawned ? 0f : spawnBlockedTime+.2f;
            }
        }
        private bool TrySpawnOne()
        {
            // A quick click can beat menu prewarming. Hold only the spawn
            // deadline until all eight slots are ready; hero controls stay live.
            if(state.Spawned==0 && PreparedEnemyCount<pool.Length) return false;
            int free=-1;
            for(int i=0;i<pool.Length;i++) if(!pool[i].living && pool[i].actor!=null && Time.time>=pool[i].reuseAfter) { free=i; break; }
            if(free<0) return false;
            float minimum=Safe(profile.minimumSpawnDistance,5f,2.5f,12f);
            for(int checkedPoints=0;checkedPoints<spawnPoints.Length;checkedPoints++)
            {
                int marker=spawnCursor++%spawnPoints.Length;
                if(spawnPoints[marker]==null || !navigation.TryProject(spawnPoints[marker].position,out Vector3 position)) continue;
                if(Flat(position-playerHealth.transform.position).sqrMagnitude<minimum*minimum) continue;
                if(ally!=null && ally.gameObject.activeInHierarchy && Flat(position-ally.transform.position).sqrMagnitude<16f) continue;
                bool occupied=false;
                for(int i=0;i<pool.Length;i++)
                    if(pool[i].living && pool[i].actor!=null && Flat(position-pool[i].actor.transform.position).sqrMagnitude<1.44f)
                    { occupied=true; break; }
                if(occupied) continue;
                IntercomNavResult route=navigation.TryFindPath(position,playerHealth.transform.position,spawnRoute);
                if(route==IntercomNavResult.Deferred) return false;
                if(route==IntercomNavResult.Unreachable) continue;
                ActivateSlot(free,position); return true;
            }
            return false;
        }
        private void PrepareOneEnemy()
        {
            if(navigation==null || !navigation.isActiveAndEnabled || !navigation.IsReady
                || playerHealth==null || playerMotor==null || enemyDefinition==null
                || enemyDefinition.bodyMaterial==null || enemyDefinition.detailMaterial==null
                || enemyDefinition.telegraphMaterial==null) return;
            for(int i=0;i<pool.Length;i++)
            {
                Slot slot=pool[i];
                if(slot.actor!=null) continue;
                slot.actor=IntercomNpcController.Create(enemyDefinition,navigation,playerHealth,playerMotor,
                    playerHealth.transform.position,i);
                // The factory activates synchronously to initialize Awake and
                // subscriptions. No rendering/AI frame occurs before parking.
                slot.actor.gameObject.SetActive(false);
                slot.actor.transform.SetParent(transform,true);
                int captured=i; slot.deathHandler=()=>OnEnemyDied(captured);
                slot.actor.Health.Died+=slot.deathHandler;
                return; // At most one allocation-heavy actor in a frame.
            }
        }
        private void ActivateSlot(int index,Vector3 position)
        {
            Slot slot=pool[index];
            if(slot.actor==null) return;
            slot.actor.gameObject.SetActive(false);
            slot.actor.PrepareForSpawn(position); slot.actor.SetTarget(playerHealth);
            unchecked { slot.life++; if(slot.life==0) slot.life++; }
            slot.epoch=state.Epoch;
            if(!state.RecordSpawn(index,slot.life,slot.epoch)) return;
            slot.living=true; slot.reuseAfter=0f; slot.targetChangedAt=Time.time;
            slot.actor.BeginEmergence();
            slot.actor.gameObject.SetActive(true);
            // Spawn command reports a fixed known position; AI must see the hero to keep tracking it.
            slot.actor.Investigate(playerHealth.transform.position);
        }
        private void OnEnemyDied(int index)
        {
            Slot slot=pool[index];
            if(transitioning || !slot.living || slot.actor==null || slot.actor.Health==null
                || !slot.actor.Health.IsDead || !state.RecordKill(index,slot.life,slot.epoch)) return;
            slot.living=false; slot.reuseAfter=Time.time+.85f;
            if(Phase==IntercomMissionPhase.Victory) { ResultText="Все "+Goal+" роботов отключены"; Finish(); }
        }
        private void RefreshTargets()
        {
            for(int i=0;i<pool.Length;i++)
            {
                Slot slot=pool[i];
                if(!slot.living || slot.actor==null || Time.time-slot.targetChangedAt<1.5f
                    || slot.actor.Phase==IntercomNpcPhase.Windup || slot.actor.Phase==IntercomNpcPhase.Recovery) continue;
                IntercomHealth target=playerHealth;
                if(Mode==IntercomMissionMode.Rescue && ally!=null && ally.Recruited && !ally.Health.IsDead)
                {
                    Vector3 source=slot.actor.transform.position;
                    float heroDistance=Flat(playerHealth.transform.position-source).magnitude;
                    float allyDistance=Flat(ally.transform.position-source).magnitude;
                    bool retaining=slot.actor.targetHealth==ally.Health;
                    if(allyDistance<8f && allyDistance<(retaining ? heroDistance+2f : heroDistance-2f)
                        && HasSight(slot.actor,ally.Health)) target=ally.Health;
                }
                if(slot.actor.targetHealth!=target)
                {
                    slot.actor.SetTarget(target);
                    slot.targetChangedAt=Time.time;
                    if(HasSight(slot.actor,target)) slot.actor.Investigate(target.transform.position);
                }
            }
        }
        private bool HasSight(IntercomNpcController actor,IntercomHealth target)
        {
            Vector3 origin=actor.transform.position+Vector3.up*.72f;
            Vector3 delta=target.transform.position+Vector3.up*.72f-origin; float distance=delta.magnitude;
            if(distance<.001f) return true;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,sightHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count>=sightHits.Length) return false;
            for(int i=0;i<count;i++)
            {
                Collider hit=sightHits[i].collider;
                if(hit==null || hit is CharacterController || hit.isTrigger || hit.transform.IsChildOf(actor.transform)
                    || hit.transform.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }
        private void EnsureExtractionVisual()
        {
            if(extractionVisual==null)
            {
                extractionVisual=GameObject.CreatePrimitive(PrimitiveType.Cylinder); extractionVisual.name="Rescue extraction zone";
                Collider collider=extractionVisual.GetComponent<Collider>(); if(collider!=null) { collider.enabled=false; Destroy(collider); }
                extractionVisual.transform.SetParent(transform,true);
                Renderer renderer=extractionVisual.GetComponent<Renderer>(); renderer.sharedMaterial=enemyDefinition.telegraphMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
                var block=new MaterialPropertyBlock(); block.SetColor(Shader.PropertyToID("_BaseColor"),new Color(.27f,.77f,.55f,.35f)); renderer.SetPropertyBlock(block);
            }
            float radius=Safe(profile.extractionRadius,2f,.75f,5f);
            extractionVisual.transform.position=extractionPoint.position+Vector3.up*.018f;
            extractionVisual.transform.localScale=new Vector3(radius*2f,.009f,radius*2f);
        }
        private void StopActors()
        {
            for(int i=0;i<pool.Length;i++)
            {
                Slot slot=pool[i]; if(slot==null) continue;
                slot.living=false; slot.reuseAfter=0f;
                if(slot.actor!=null) slot.actor.gameObject.SetActive(false);
            }
        }
        private void Finish()
        {
            StopActors();
            if(ally!=null) ally.Suspended=true;
            ApplyBlock();
        }
        private bool ApplyBlock()
        {
            if(!ConnectPlayerInput())
            {
                ReleaseControlLock();
                if(connectedInput!=null && connectedInput.mission==this) connectedInput.mission=null;
                connectedInput=null;
                if(!connectionWarningShown)
                {
                    Debug.LogWarning("Mission UI has no active player input. Hero mission lock released; connect Mission Director to the hero input.",this);
                    connectionWarningShown=true;
                }
                return false;
            }
            if(controlOwner!=playerVitality) ReleaseControlLock();
            if(!playerVitality.SetMissionBlocked(this,BlocksGameplay)) return false;
            controlOwner=playerVitality;
            connectionWarningShown=false;
            connectedInput.CancelUntilRelease();
            return true;
        }
        private void OnApplicationFocus(bool value) { focused=value; }
        private void OnApplicationPause(bool value) { paused=value; }
        private void OnGUI()
        {
            if(Phase!=IntercomMissionPhase.Active || Mode!=IntercomMissionMode.Rescue || ally==null || extractionPoint==null
                || playerHealth==null || Event.current==null || Event.current.type!=EventType.Repaint) return;
            if(displayCamera==null) displayCamera=Camera.main;
            if(displayCamera==null) return;
            Vector3 destination=ally.Recruited ? extractionPoint.position : ally.transform.position;
            Vector3 point=displayCamera.WorldToScreenPoint(destination+Vector3.up*1.75f);
            Rect safe=Screen.safeArea;
            float top=Screen.height-safe.yMax;
            float scale=Mathf.Clamp(Screen.height/720f,.65f,1.5f), width=138f*scale, height=28f*scale;
            float x=Mathf.Clamp(point.x-width*.5f,safe.x+12f,Mathf.Max(safe.x+12f,safe.xMax-width-12f));
            float minY=top+132f*scale, maxY=Mathf.Max(minY,Screen.height-safe.y-190f*scale);
            float y=Mathf.Clamp(Screen.height-point.y,minY,maxY);
            if(guidanceStyle==null) guidanceStyle=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, fontStyle=FontStyle.Bold };
            guidanceStyle.fontSize=Mathf.RoundToInt(13f*scale);
            guidanceStyle.normal.textColor=new Color(.10f,.24f,.23f,1f);
            float distance=Flat(destination-playerHealth.transform.position).magnitude;
            int meters=Mathf.CeilToInt(distance);
            if(meters!=guidanceMeters || guidanceWasExit!=ally.Recruited)
            {
                guidanceMeters=meters; guidanceWasExit=ally.Recruited;
                guidanceLabel=(ally.Recruited ? "ВЫХОД" : "СОЮЗНИК")+" · "+meters+" м";
            }
            Color prior=GUI.color; int depth=GUI.depth; GUI.depth=-34;
            GUI.color=new Color(.10f,.21f,.21f,.9f); GUI.DrawTexture(new Rect(x-2f,y-2f,width+4f,height+4f),Texture2D.whiteTexture);
            GUI.color=new Color(.72f,.85f,.71f,.94f); GUI.DrawTexture(new Rect(x,y,width,height),Texture2D.whiteTexture);
            GUI.color=Color.white; GUI.Label(new Rect(x,y,width,height),guidanceLabel,guidanceStyle);
            GUI.color=prior; GUI.depth=depth;
        }
        private void OnDisable()
        {
            transitioning=true; StopActors();
            if(ally!=null) ally.gameObject.SetActive(false);
            if(extractionVisual!=null) extractionVisual.SetActive(false);
            state.Select(); transitioning=false;
            if(subscribedHero!=null) subscribedHero.Died-=OnHeroDied; subscribedHero=null;
            ReleaseControlLock();
            if(connectedInput!=null && connectedInput.mission==this) connectedInput.mission=null;
            connectedInput=null;
        }
        private void OnDestroy()
        {
            if(subscribedHero!=null) subscribedHero.Died-=OnHeroDied;
            for(int i=0;i<pool.Length;i++)
            {
                Slot slot=pool[i];
                if(slot==null || slot.actor==null) continue;
                slot.actor.Health.Died-=slot.deathHandler; Destroy(slot.actor.gameObject);
            }
            if(ally!=null) { ally.Health.Died-=OnAllyDied; Destroy(ally.gameObject); }
            if(extractionVisual!=null) Destroy(extractionVisual);
        }
    }
}
