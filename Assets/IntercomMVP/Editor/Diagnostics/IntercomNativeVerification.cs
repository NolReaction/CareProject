using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IntercomMvp.Editor
{
    /// <summary>Opt-in native Editor run. It never writes a PASS until actual Play Mode scenarios have completed.</summary>
    [InitializeOnLoad]
    public static class IntercomNativeVerification
    {
        private const string Key="CareProject.NativeVerification.";
        private static Runner runner;
        [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
        [Serializable] private sealed class SavedScenes { public SavedScene[] scenes; }
        [Serializable] private sealed class Report
        {
            public string version="V24", status="RUNNING", startedUtc, finishedUtc, unityVersion, platform, framesCsv;
            public bool nativeEditorExecuted, renderingAndDevicePerformanceVerified;
            public int eliminationVictories, retries, enemyHits, projectileRobotHits, nativeVolleys, reusedLives;
            public int groundArrivals, hatchArrivals, animatedActors, maximumPool, simultaneousDeathBatches, poseChanges;
            public bool pauseFrozen, heroDeathHandled, freePlayClean, rescueCompleted, dodgeObserved, scrapExpired;
            public double wallSeconds;
            public IntercomSoakFrameMetrics.Summary frames;
            public List<string> failures=new List<string>();
            public string scope="Real Unity Editor Play Mode: swept projectiles, Health, native CharacterController/Animator, pooled arrivals/deaths/reuse, mission results, retry, pause and rescue. Diagnostic profile is a runtime copy. Timings include Editor overhead; no mobile/GPU performance claim.";
        }
        static IntercomNativeVerification()
        {
            EditorApplication.playModeStateChanged+=OnPlayMode;
            EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            Application.logMessageReceived+=ObserveLaunchLog;
            if(SessionState.GetBool(Key+"abortAfterReload",false))
                EditorApplication.delayCall+=()=> { if(EditorApplication.isPlaying)EditorApplication.isPlaying=false; };
        }
        public static string LatestReportPath => Path.Combine(Application.persistentDataPath,"CareProject_Native_Latest.json");

        [MenuItem("Tools/CareProject/Development/Verification/Run 150s Native Check")]
        public static void RunFromMenu() => Start(false);
        /// <summary>Use Unity -batchmode -projectPath PATH -executeMethod IntercomMvp.Editor.IntercomNativeVerification.RunFromCommandLine. Do not add -quit.</summary>
        public static void RunFromCommandLine() => Start(true);
        [MenuItem("Tools/CareProject/Development/Verification/Stop Native Check")]
        private static void StopFromMenu()
        {
            if(runner!=null)runner.Complete("CANCELLED","Проверка остановлена пользователем.");
            else if(SessionState.GetBool(Key+"requested",false))EditorApplication.isPlaying=false;
        }
        [MenuItem("Tools/CareProject/Development/Verification/Open Last Native Report")]
        private static void OpenReport()
        {
            if(File.Exists(LatestReportPath))EditorUtility.RevealInFinder(LatestReportPath);
            else EditorUtility.DisplayDialog("CareProject","Нативная проверка ещё не выполнялась.","OK");
        }
        private static void Start(bool batch)
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                    throw new InvalidOperationException("Останови Play Mode и дождись импорта/компиляции.");
                var originalScenes=new List<SavedScene>();
                for(int i=0;i<SceneManager.sceneCount;i++)
                {
                    Scene scene=SceneManager.GetSceneAt(i);
                    if(batch&&SceneManager.sceneCount==1&&IntercomProjectStartup.IsPristineDefaultScene(scene))continue;
                    if(scene.isDirty||string.IsNullOrEmpty(scene.path))
                        throw new InvalidOperationException("Сначала сохрани все открытые сцены. Проверка не перезаписывает несохранённую работу.");
                    originalScenes.Add(new SavedScene { path=scene.path, loaded=scene.isLoaded, active=scene==SceneManager.GetActiveScene() });
                }
                var saved=new SavedScenes { scenes=originalScenes.ToArray() };
                string path=IntercomProjectPaths.TutorialScene.Resolve();
                if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null)throw new FileNotFoundException("Готовая учебная сцена не найдена: "+path);
                SessionState.SetString(Key+"scenes",JsonUtility.ToJson(saved));
                SessionState.SetBool(Key+"batch",batch);
                SessionState.SetBool(Key+"requested",true);
                SessionState.SetBool(Key+"abortAfterReload",false);
                SessionState.SetString(Key+"earlyErrors","");
                SessionState.SetInt(Key+"exitCode",1);
                SessionState.SetFloat(Key+"timeScale",Time.timeScale);
                WriteAttempt("RUNNING",null);
                EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                EditorApplication.isPlaying=true;
            }
            catch(Exception error)
            {
                SessionState.SetBool(Key+"requested",false);
                SessionState.SetBool(Key+"abortAfterReload",false);
                WriteAttempt("FAIL",error);
                Debug.LogError("CareProject native verification: "+error.Message);
                if(batch)EditorApplication.Exit(1);
                else EditorUtility.DisplayDialog("CareProject",error.Message,"OK");
            }
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key+"requested",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                var existingCapture=Object.FindFirstObjectByType<IntercomPerformanceCapture>();
                if(existingCapture!=null)existingCapture.SuppressNextStopExport();
                try { runner=new Runner();runner.Initialize(); }
                catch(Exception error)
                {
                    try
                    {
                        if(runner!=null)runner.Complete("FAIL",error.ToString(),false);
                        else WriteAttempt("FAIL",error);
                    }
                    finally { EditorApplication.isPlaying=false; }
                }
            }
            else if(state==PlayModeStateChange.ExitingPlayMode)
            {
                if(runner!=null)runner.Complete("CANCELLED","Play Mode остановлен до завершения проверки.",false);
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                runner=null;
                bool batch=SessionState.GetBool(Key+"batch",false);int code=SessionState.GetInt(Key+"exitCode",1);
                SessionState.SetBool(Key+"requested",false);
                SessionState.SetBool(Key+"abortAfterReload",false);
                Time.timeScale=SessionState.GetFloat(Key+"timeScale",1f);
                try
                {
                    var old=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"scenes",""));
                    if(old!=null&&old.scenes!=null&&old.scenes.Length>0)
                    {
                        var setup=new SceneSetup[old.scenes.Length];
                        for(int i=0;i<setup.Length;i++)setup[i]=new SceneSetup { path=old.scenes[i].path, isLoaded=old.scenes[i].loaded, isActive=old.scenes[i].active };
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    }
                }
                catch(Exception error) { Debug.LogWarning("CareProject: не удалось вернуть прежний набор сцен: "+error.Message); }
                if(batch)EditorApplication.Exit(code);
                else if(code==0)Debug.Log("CareProject: нативная проверка PASS. Отчёт: "+LatestReportPath);
            }
        }
        private static void Tick()
        {
            if(runner==null||!EditorApplication.isPlaying)return;
            try { runner.Tick(); }
            catch(Exception error) { runner?.Complete("FAIL",error.ToString()); }
        }
        private static void ObserveLaunchLog(string message,string stack,LogType type)
        {
            if(!SessionState.GetBool(Key+"requested",false)
                ||(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert))return;
            if(runner!=null)
            {
                runner.RecordFailure(message+"\n"+stack);
                return;
            }
            string previous=SessionState.GetString(Key+"earlyErrors","");
            if(previous.Length<24000)SessionState.SetString(Key+"earlyErrors",previous+message+"\n"+stack+"\n");
        }
        private static void BeforeReload()
        {
            if(runner!=null&&EditorApplication.isPlaying)
            {
                runner.Complete("CANCELLED","Скрипты изменились во время проверки.",false);
                SessionState.SetBool(Key+"abortAfterReload",true);
            }
        }
        private static void WriteAttempt(string status,Exception error)
        {
            var report=new Report { nativeEditorExecuted=EditorApplication.isPlaying, status=status, startedUtc=DateTime.UtcNow.ToString("O"), finishedUtc=status=="RUNNING"?"":DateTime.UtcNow.ToString("O"), unityVersion=Application.unityVersion };
            if(error!=null)report.failures.Add(error.ToString());
            SaveReport(report,null);
            SessionState.SetInt(Key+"exitCode",1);
        }
        private static bool SaveReport(Report report,string runDirectory)
        {
            try
            {
                string json=JsonUtility.ToJson(report,true);Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(LatestReportPath,json);
                if(!string.IsNullOrEmpty(runDirectory))File.WriteAllText(Path.Combine(runDirectory,"Summary.json"),json);
                return true;
            }
            catch(Exception error)
            {
                report.status="FAIL";
                Debug.LogWarning("CareProject: не удалось сохранить нативный отчёт: "+error.Message);
                return false;
            }
        }

        private sealed class Runner
        {
            private enum Stage { Warmup, GroundArrival, HatchArrival, Elimination, Defeat, RetryAfterDefeat, BurstSetup, BurstDrain, FreePlay, Rescue, Drain }
            private readonly Report report=new Report();
            private readonly IntercomSoakFrameMetrics metrics=new IntercomSoakFrameMetrics();
            private IntercomMissionDirector mission;
            private IntercomProgression progression;
            private IntercomPerformanceCapture capture;
            private IntercomSimpleShooter shooter;
            private IntercomWeaponPose pose;
            private IntercomPlayerInput input;
            private IntercomMissionDefinition originalElimination, originalRescue, elimination, rescue, arrival;
            private IntercomEnemySpawnPoint[] originalAuthoredPoints;
            private Transform[] originalPerimeterPoints;
            private readonly IntercomNpcController[] known=new IntercomNpcController[8];
            private readonly uint[] lives=new uint[8];
            private readonly float[] seenAt=new float[8];
            private readonly bool[] touched=new bool[8];
            private readonly Transform[][] poseBones=new Transform[8][];
            private readonly Vector3[] bonePositions=new Vector3[8];
            private readonly Quaternion[] boneRotations=new Quaternion[8];
            private readonly List<Vector3> rescuePath=new List<Vector3>(128);
            private readonly double started;
            private bool originalCaptureSave, originalRunInBackground;
            private bool originalHeroInvulnerability, heroCaptured, captureConfigured, profileConfigured;
            private readonly string runDirectory;
            private Stage stage;
            private double stageStarted, nextAction, pauseStarted, dodgeStarted, nextBurst;
            private float pausedElapsed;
            private int pausedSpawned, lastFrame=-1, rescueWaypoint;
            private bool complete, pauseInProgress, allowDodge=true;
            private IntercomNpcController dodgedActor;
            private bool dodgedActorHitHero;
            private uint dodgedLife, dodgedStrike;
            private float dodgeMissionElapsed;
            private bool sawUnderground;
            private uint previousRun;
            private int burstTotal;

            internal Runner()
            {
                started=stageStarted=EditorApplication.timeSinceStartup;
                runDirectory=Path.Combine(Application.persistentDataPath,"CareProject_Native_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(runDirectory);
                report.startedUtc=DateTime.UtcNow.ToString("O");report.unityVersion=Application.unityVersion;
                report.platform=Application.platform.ToString();report.nativeEditorExecuted=true;
                report.renderingAndDevicePerformanceVerified=false;
            }
            internal void Initialize()
            {
                Check(string.IsNullOrEmpty(SessionState.GetString(Key+"earlyErrors","")),
                    "До начала сценариев произошли ошибки Awake/OnEnable/импорта:\n"+SessionState.GetString(Key+"earlyErrors",""));
                mission=Object.FindFirstObjectByType<IntercomMissionDirector>();
                Check(mission!=null&&mission.playerHealth!=null&&mission.playerMotor!=null,"Не подключены миссия и герой.");
                originalHeroInvulnerability=mission.playerHealth.invulnerable;heroCaptured=true;
                progression=Object.FindFirstObjectByType<IntercomProgression>();
                Check(progression!=null&&progression.BeginDiagnosticSession(),"Не удалось включить временный профиль проверки; реальные сохранения не затронуты.");
                input=mission.playerMotor.GetComponent<IntercomPlayerInput>();
                shooter=mission.playerMotor.GetComponent<IntercomSimpleShooter>();
                pose=mission.playerMotor.GetComponent<IntercomWeaponPose>();
                Check(input!=null&&shooter!=null&&pose!=null,"Не подключены настоящий ввод, пистолеты и позы героя.");
                Check(mission.enemyDefinition!=null&&mission.enemyDefinition.visualPrefab!=null,"Проверь импорт ударника: для нативной проверки нужен его настоящий prefab, а не запасная капсула.");
                originalElimination=mission.eliminationDefinition;originalRescue=mission.rescueDefinition;
                originalAuthoredPoints=mission.authoredSpawnPoints;originalPerimeterPoints=mission.spawnPoints;
                Check(originalElimination!=null&&originalRescue!=null,"Не назначены карточки режимов.");
                elimination=Object.Instantiate(originalElimination);elimination.name="Native verification temporary Elimination";elimination.hideFlags=HideFlags.DontSave;
                elimination.killTarget=18;elimination.maximumAlive=8;elimination.waveSize=6;elimination.spawnInterval=.3f;elimination.waveRestSeconds=.8f;
                elimination.visibleSpawnEvery=1;elimination.minimumVisibleSpawnDistance=5.5f;
                arrival=Object.Instantiate(elimination);arrival.name="Native verification temporary arrival";arrival.hideFlags=HideFlags.DontSave;
                arrival.killTarget=1;arrival.maximumAlive=1;arrival.waveSize=1;arrival.useRhythmicWaves=false;
                rescue=Object.Instantiate(originalRescue);rescue.name="Native verification temporary Rescue";rescue.hideFlags=HideFlags.DontSave;
                rescue.rescueEnemySupply=6;rescue.spawnInterval=.7f;rescue.maximumAlive=3;
                mission.eliminationDefinition=elimination;mission.rescueDefinition=rescue;profileConfigured=true;
                capture=mission.playerMotor.GetComponent<IntercomPerformanceCapture>();
                if(capture==null)capture=mission.playerMotor.gameObject.AddComponent<IntercomPerformanceCapture>();
                capture.mission=mission;capture.navigation=mission.navigation;
                originalCaptureSave=capture.saveWhenStopped;capture.saveWhenStopped=false;
                capture.SuppressNextStopExport();
                originalRunInBackground=Application.runInBackground;Application.runInBackground=true;Time.timeScale=1f;captureConfigured=true;
                shooter.VolleyFired+=OnVolley;shooter.DetailedProjectileHit+=OnImpact;
                mission.playerHealth.Damaged+=OnHeroDamage;
                mission.ReturnToSelection();
            }
            private void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
            private void OnVolley(Vector3 point,int barrels) { report.nativeVolleys++; }
            private void OnImpact(IntercomProjectileImpact impact) { if(impact.TargetHit&&impact.RobotHit)report.projectileRobotHits++; }
            private void OnHeroDamage(IntercomDamage damage)
            { if(dodgedActor!=null&&damage.Instigator==dodgedActor.gameObject)dodgedActorHitHero=true; }
            private double Now => EditorApplication.timeSinceStartup;
            private void Next(Stage value) { pose.SetTriggerHeld(false);stage=value;stageStarted=Now;nextAction=0;nextBurst=Now+8; }
            internal void Tick()
            {
                if(complete)return;
                if(report.failures.Count>0) { Complete("FAIL",null);return; }
                if(Now-started>150) { Complete("FAIL","Проверка не завершилась за 150 секунд. Последний этап: "+stage);return; }
                if(capture.TryGetMostRecentSample(out var frame)&&frame.frame!=lastFrame)
                { lastFrame=frame.frame;metrics.Add(frame); }
                report.maximumPool=Math.Max(report.maximumPool,mission.PreparedEnemyCount);
                Check(mission.PreparedEnemyCount<=8&&mission.AliveEnemies<=8,"Пул врагов превысил восемь объектов.");
                if(pauseInProgress)
                {
                    Check(mission.SpawnedEnemies==pausedSpawned&&Mathf.Abs(mission.ElapsedSeconds-pausedElapsed)<.0001f,"Пауза продвинула время миссии или появление врагов.");
                    if(Now-pauseStarted>.75) { input.Resume();pauseInProgress=false;report.pauseFrozen=true; }
                    return;
                }
                if(stage==Stage.Warmup)
                {
                    if(mission.PreparedEnemyCount<8) { Check(Now-stageStarted<12,"Предварительная подготовка пула не завершилась.");return; }
                    StartArrival(IntercomEnemySpawnKind.Ground);return;
                }
                if(stage==Stage.GroundArrival||stage==Stage.HatchArrival)
                {
                    Check(Now-stageStarted<16,"Контрольное появление из земли/люка не завершилось.");
                    for(int i=0;i<IntercomNpcController.ActiveActorCount;i++)
                    {
                        var actor=IntercomNpcController.GetActiveActor(i);
                        if(actor==null||actor.Health==null||actor.Health.IsDead)continue;
                        Observe(actor);
                        Check(actor.EmergenceKind==(stage==Stage.GroundArrival?IntercomEnemySpawnKind.Ground:IntercomEnemySpawnKind.Hatch),"Контрольная точка выбрала неправильный вид появления.");
                        var controller=actor.GetComponent<CharacterController>();
                        if(actor.IsEmerging)
                        { sawUnderground=true;Check(actor.Health.invulnerable&&controller!=null&&!controller.enabled,"Подземное появление не отключило урон и столкновения."); }
                        else
                        {
                            Check(sawUnderground&&controller!=null&&controller.enabled,"Появление не восстановило столкновения после подъёма.");
                            Check(actor.Health.TakeDamage(new IntercomDamage(actor.Health.Maximum+1,actor.transform.position,Vector3.forward,mission.playerHealth.gameObject)),"Робот после подъёма не принимает урон.");
                        }
                    }
                    if(mission.Phase==IntercomMissionPhase.Victory)
                    {
                        bool ground=stage==Stage.GroundArrival;mission.ReturnToSelection();
                        if(ground)StartArrival(IntercomEnemySpawnKind.Hatch);
                        else
                        {
                            mission.eliminationDefinition=elimination;mission.authoredSpawnPoints=originalAuthoredPoints;mission.spawnPoints=originalPerimeterPoints;
                            StartElimination();
                        }
                    }
                    return;
                }
                if(stage==Stage.Elimination)
                {
                    DriveCombat();
                    if(!report.pauseFrozen&&mission.ElapsedSeconds>5&&!pauseInProgress)
                    {
                        pose.SetTriggerHeld(false);Check(input.Pause(),"Настоящий ввод не включил паузу.");
                        pauseInProgress=true;pauseStarted=Now;pausedElapsed=mission.ElapsedSeconds;pausedSpawned=mission.SpawnedEnemies;return;
                    }
                    if(mission.Phase==IntercomMissionPhase.Victory)
                    {
                        pose.SetTriggerHeld(false);Check(mission.Kills==18,"Победа не совпала с количеством уничтоженных роботов.");
                        report.eliminationVictories++;
                        if(report.eliminationVictories<2)
                        {
                            previousRun=mission.RunRevision;Check(mission.RetryMission(),"Повтор после победы не запустился.");
                            Check(mission.RunRevision!=previousRun&&mission.Kills==0,"Повтор сохранил счёт предыдущего захода.");
                            mission.playerHealth.invulnerable=true;report.retries++;stageStarted=Now;
                        }
                        else
                        {
                            elimination.killTarget=50;
                            Check(mission.RetryMission(),"Не запустился заход для проверки смерти героя.");
                            report.retries++;Next(Stage.Defeat);
                        }
                    }
                    return;
                }
                if(stage==Stage.Defeat)
                {
                    mission.playerHealth.invulnerable=false;
                    Check(mission.playerHealth.TakeDamage(new IntercomDamage(mission.playerHealth.Maximum+1,mission.playerHealth.transform.position,Vector3.forward,null)),"Смерть героя не приняла урон.");
                    Check(mission.Phase==IntercomMissionPhase.Defeat,"Летальный урон не завершил миссию поражением.");
                    report.heroDeathHandled=true;
                    previousRun=mission.RunRevision;Check(mission.RetryMission(),"Повтор после смерти героя не запустился.");
                    Check(!mission.playerHealth.IsDead&&mission.RunRevision!=previousRun,"Герой не восстановился после смерти.");
                    mission.playerHealth.invulnerable=true;report.retries++;Next(Stage.RetryAfterDefeat);return;
                }
                if(stage==Stage.RetryAfterDefeat)
                { DriveCombat();if(Now-stageStarted>6)Next(Stage.BurstSetup);return; }
                if(stage==Stage.BurstSetup)
                {
                    pose.SetTriggerHeld(false);
                    // Let every previously fired projectile expire naturally. The mission/vitality lease
                    // owns shooter.enabled, including its disabled Selection state; diagnostics never changes it.
                    float flight=shooter.Weapon!=null?shooter.Weapon.projectileLifetime:shooter.projectileLifetime;
                    if(float.IsNaN(flight)||float.IsInfinity(flight)||flight<=0)flight=1.4f;
                    Check(flight<=10f,"Для ускоренного прогона время жизни пули должно быть не больше десяти секунд.");
                    if(Now-stageStarted<Math.Max(.05f,flight)+.3)return;
                    int ready=0;
                    for(int i=0;i<IntercomNpcController.ActiveActorCount;i++)
                    {
                        var actor=IntercomNpcController.GetActiveActor(i);
                        if(actor!=null&&actor.Health!=null&&!actor.Health.IsDead&&!actor.IsEmerging)ready++;
                    }
                    Check(Now-stageStarted<18,"Не появились два готовых робота для массового разлёта.");
                    if(ready<2)return;
                    Check(mission.deathEffects!=null,"Не подключён настоящий сервис запчастей.");
                    int before=mission.deathEffects.TotalBursts, accepted=0;
                    for(int i=IntercomNpcController.ActiveActorCount-1;i>=0;i--)
                    {
                        var actor=IntercomNpcController.GetActiveActor(i);
                        if(actor!=null&&actor.Health!=null&&!actor.Health.IsDead&&!actor.IsEmerging
                            &&actor.Health.TakeDamage(new IntercomDamage(actor.Health.Maximum+1,actor.transform.position,Vector3.forward,mission.playerHealth.gameObject)))accepted++;
                    }
                    Check(accepted>=2&&mission.deathEffects.TotalBursts>=before+accepted&&mission.deathEffects.ActivePieces>0,
                        "Одновременные смерти не породили настоящий разлёт запчастей.");
                    report.simultaneousDeathBatches++;burstTotal=mission.deathEffects.TotalBursts;Next(Stage.BurstDrain);return;
                }
                if(stage==Stage.BurstDrain)
                {
                    Check(mission.Phase==IntercomMissionPhase.Active,"Миссия остановила эффект до проверки естественного исчезновения.");
                    Check(mission.deathEffects.TotalBursts==burstTotal,"Во время проверки исчезновения неожиданно появился новый взрыв.");
                    if(Now-stageStarted<2.2)return;
                    Check(mission.deathEffects.ActivePieces==0,"Запчасти не исчезли по собственному времени жизни.");
                    report.scrapExpired=true;
                    mission.ReturnToSelection();Check(mission.StartMission(IntercomMissionMode.FreePlay),"Свободный режим не запустился.");Next(Stage.FreePlay);return;
                }
                if(stage==Stage.FreePlay)
                {
                    Check(mission.AliveEnemies==0&&IntercomNpcController.ActiveActorCount==0,"Свободный режим оставил активных врагов.");
                    if(Now-stageStarted>3)
                    {
                        report.freePlayClean=true;mission.ReturnToSelection();Check(mission.StartMission(IntercomMissionMode.Rescue),"Режим спасения не запустился.");
                        mission.playerHealth.invulnerable=true;rescueWaypoint=0;rescuePath.Clear();Next(Stage.Rescue);
                    }
                    return;
                }
                if(stage==Stage.Rescue)
                {
                    DriveCombat();DriveRescue();
                    if(mission.Phase==IntercomMissionPhase.Victory)
                    { report.rescueCompleted=true;Next(Stage.Drain); }
                    else Check(mission.Phase==IntercomMissionPhase.Active,"Спасение неожиданно завершилось поражением.");
                    return;
                }
                if(stage==Stage.Drain&&Now-stageStarted>2.2)
                {
                    Check(report.eliminationVictories==2&&report.retries>=3,"Не выполнены повторные победы и перезапуски.");
                    Check(report.enemyHits>0&&report.projectileRobotHits>0&&report.nativeVolleys>0,"Не подтверждён настоящий выстрел и swept-попадание по роботу.");
                    Check(report.reusedLives>0&&report.groundArrivals>0&&report.hatchArrivals>0,"Не подтверждены повторное использование, выход из земли и люка.");
                    Check(report.animatedActors>0&&report.pauseFrozen&&report.dodgeObserved,"Не подтверждены Animator, пауза или уклонение от замаха.");
                    Check(report.poseChanges>0,"В настоящих кадрах не наблюдалось движения костей ударника.");
                    Check(report.heroDeathHandled&&report.freePlayClean&&report.rescueCompleted&&report.scrapExpired,"Не завершены сценарии смерти/свободного режима/спасения/уборки обломков.");
                    Check(report.simultaneousDeathBatches>0,"Не выполнена массовая смерть врагов в одном кадре.");
                    Check(metrics.Snapshot().frames>=120,"Не получена достаточная запись настоящих завершённых кадров.");
                    Complete("PASS",null);
                }
            }
            private void StartElimination()
            {
                Check(mission.StartMission(IntercomMissionMode.Elimination),"Настоящая миссия не запустилась: "+mission.ResultText);
                Check(shooter.enabled,"Миссия не включила настоящий пистолет после снятия блокировки меню.");
                mission.playerHealth.invulnerable=true;previousRun=mission.RunRevision;Next(Stage.Elimination);
            }
            private void StartArrival(IntercomEnemySpawnKind kind)
            {
                IntercomEnemySpawnPoint point=null;
                if(originalAuthoredPoints!=null)
                    for(int i=0;i<originalAuthoredPoints.Length;i++)
                        if(originalAuthoredPoints[i]!=null&&originalAuthoredPoints[i].kind==kind) { point=originalAuthoredPoints[i];break; }
                Check(point!=null,"В сцене нет контрольной точки "+kind);
                mission.eliminationDefinition=arrival;mission.authoredSpawnPoints=new[] { point };mission.spawnPoints=Array.Empty<Transform>();
                Check(mission.StartMission(IntercomMissionMode.Elimination),"Не запустился контрольный выход: "+mission.ResultText);
                Check(shooter.enabled,"Миссия не включила настоящий пистолет после снятия блокировки меню.");
                mission.playerHealth.invulnerable=true;
                float radius=Math.Max(6.5f,Math.Max(point.minimumDistance,arrival.minimumSpawnDistance)+1f);
                bool placed=false;
                for(int n=0;n<4;n++)
                {
                    Vector3 offset=n==0?Vector3.right:n==1?Vector3.left:n==2?Vector3.forward:Vector3.back;
                    if(!mission.navigation.TryProject(point.transform.position+offset*radius,out Vector3 position))continue;
                    if((position-point.transform.position).sqrMagnitude<(radius-.5f)*(radius-.5f))continue;
                    MoveHero(position);placed=true;break;
                }
                Check(placed,"Нет безопасного положения героя рядом с контрольной точкой "+kind);
                sawUnderground=false;Next(kind==IntercomEnemySpawnKind.Ground?Stage.GroundArrival:Stage.HatchArrival);
            }
            private int Observe(IntercomNpcController actor)
            {
                int index=Array.IndexOf(known,actor);
                if(index<0) { index=Array.IndexOf(known,null);Check(index>=0,"Миссия создала более восьми разных акторов.");known[index]=actor;lives[index]=uint.MaxValue; }
                if(lives[index]!=actor.LifeVersion)
                {
                    if(lives[index]!=uint.MaxValue)report.reusedLives++;
                    lives[index]=actor.LifeVersion;seenAt[index]=Time.unscaledTime;touched[index]=false;
                    if(actor.EmergenceKind==IntercomEnemySpawnKind.Ground)report.groundArrivals++;
                    if(actor.EmergenceKind==IntercomEnemySpawnKind.Hatch)report.hatchArrivals++;
                    var skin=actor.bodyRenderer as SkinnedMeshRenderer;
                    var animator=actor.GetComponentInChildren<Animator>(true);
                    if(poseBones[index]==null&&skin!=null)poseBones[index]=skin.bones;
                    Check(skin!=null&&poseBones[index]!=null&&poseBones[index].Length==14&&animator!=null&&animator.runtimeAnimatorController!=null,"Актор не получил настоящий риг/Animator ударника.");
                    Check(poseBones[index][1]!=null&&poseBones[index][2]!=null,"Риг содержит пустые ссылки на кости.");
                    bonePositions[index]=poseBones[index][1].localPosition;boneRotations[index]=poseBones[index][2].localRotation;
                    report.animatedActors++;
                }
                Vector3 currentPosition=poseBones[index][1].localPosition;Quaternion currentRotation=poseBones[index][2].localRotation;
                if((currentPosition-bonePositions[index]).sqrMagnitude>.0000000001f||Quaternion.Angle(currentRotation,boneRotations[index])>.01f)report.poseChanges++;
                bonePositions[index]=currentPosition;boneRotations[index]=currentRotation;
                return index;
            }
            private void DriveCombat()
            {
                if(mission.Phase!=IntercomMissionPhase.Active) { pose.SetTriggerHeld(false);return; }
                bool initialApproach=stage==Stage.Elimination&&report.eliminationVictories==0&&!report.dodgeObserved;
                if(initialApproach)Check(mission.ElapsedSeconds<35,"Ни один робот не завершил контрольный замах за 35 активных секунд.");
                IntercomNpcController aim=null;
                for(int i=IntercomNpcController.ActiveActorCount-1;i>=0;i--)
                {
                    var actor=IntercomNpcController.GetActiveActor(i);
                    if(actor==null||actor.Health==null||actor.Health.IsDead)continue;
                    int slot=Observe(actor);
                    if(actor.IsEmerging)
                    { Check(actor.Health.invulnerable,"Робот принимает урон под землёй.");continue; }
                    if(initialApproach&&allowDodge&&actor.Phase==IntercomNpcPhase.Windup&&TryDodge(actor))continue;
                    if(aim==null&&mission.navigation.SegmentClear(mission.playerHealth.transform.position,actor.transform.position))aim=actor;
                    if(!initialApproach&&Now>=nextAction&&Time.unscaledTime-seenAt[slot]>1.6f)
                    {
                        float amount=touched[slot]?actor.Health.Maximum+1:1f;
                        if(actor.Health.TakeDamage(new IntercomDamage(amount,actor.transform.position+Vector3.up*.65f,Vector3.forward,mission.playerHealth.gameObject)))
                        { report.enemyHits++;touched[slot]=true;seenAt[slot]=Time.unscaledTime-1.15f;nextAction=Now+.2; }
                    }
                }
                if(dodgedActor!=null)
                {
                    Check(dodgedActor.LifeVersion==dodgedLife&&dodgedActor.Health!=null&&!dodgedActor.Health.IsDead,"Контрольный атакующий сменил жизнь до проверки промаха.");
                    Check(mission.ElapsedSeconds-dodgeMissionElapsed<5,"Контрольный замах не завершился за пять активных секунд.");
                    if(dodgedActor.ResolvedStrikeVersion!=dodgedStrike)
                    {
                        Check(!dodgedActorHitHero,"Робот попал после выхода героя за радиус замаха.");
                        report.dodgeObserved=true;dodgedActor=null;mission.playerHealth.invulnerable=true;
                    }
                }
                if(!initialApproach&&aim!=null&&aim.Health!=null&&!aim.Health.IsDead)
                {
                    Vector3 direction=aim.transform.position-mission.playerHealth.transform.position;direction.y=0;
                    if(direction.sqrMagnitude>.01f)mission.playerMotor.transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,-mission.playerMotor.modelForwardYaw,0);
                    pose.SetTriggerHeld(true);
                }
                else pose.SetTriggerHeld(false);
                if(!initialApproach&&Now>=nextBurst)
                {
                    int accepted=0;
                    for(int i=IntercomNpcController.ActiveActorCount-1;i>=0;i--)
                    {
                        var actor=IntercomNpcController.GetActiveActor(i);
                        if(actor!=null&&actor.Health!=null&&!actor.Health.IsDead&&!actor.IsEmerging
                            &&actor.Health.TakeDamage(new IntercomDamage(actor.Health.Maximum+1,actor.transform.position,Vector3.forward,mission.playerHealth.gameObject)))accepted++;
                    }
                    if(accepted>1)report.simultaneousDeathBatches++;
                    nextBurst=Now+8;
                }
            }
            private bool TryDodge(IntercomNpcController actor)
            {
                Vector3 original=mission.playerHealth.transform.position;
                for(int n=0;n<4;n++)
                {
                    Vector3 offset=n==0?Vector3.right:n==1?Vector3.left:n==2?Vector3.forward:Vector3.back;
                    if(!mission.navigation.TryProject(original+offset*4,out Vector3 point)||(point-original).sqrMagnitude<9)continue;
                    dodgedActor=actor;dodgedActorHitHero=false;dodgeStarted=Now;allowDodge=false;
                    dodgedLife=actor.LifeVersion;dodgedStrike=actor.ResolvedStrikeVersion;dodgeMissionElapsed=mission.ElapsedSeconds;
                    MoveHero(point);mission.playerHealth.invulnerable=false;return true;
                }
                return false;
            }
            private void MoveHero(Vector3 position)
            {
                var controller=mission.playerMotor.GetComponent<CharacterController>();bool enabled=controller.enabled;
                controller.enabled=false;mission.playerMotor.transform.position=position;controller.enabled=enabled;
            }
            private void DriveRescue()
            {
                if(mission.Phase!=IntercomMissionPhase.Active)return;
                Check(Now-stageStarted<65,"Союзник не завершил настоящий маршрут спасения за 65 секунд.");
                var ally=Object.FindFirstObjectByType<IntercomRescueAlly>();Check(ally!=null,"Миссия не создала союзника.");
                if(!ally.Recruited)
                { MoveHero(ally.transform.position+Vector3.right*.7f);return; }
                if(rescuePath.Count==0)
                {
                    var result=mission.navigation.TryFindPath(ally.transform.position,mission.extractionPoint.position,rescuePath);
                    if(result==IntercomNavResult.Deferred)return;
                    Check(result!=IntercomNavResult.Unreachable,"Навигация не нашла маршрут к точке спасения.");
                    if(rescuePath.Count==0)rescuePath.Add(mission.extractionPoint.position);
                }
                while(rescueWaypoint<rescuePath.Count-1&&(ally.transform.position-rescuePath[rescueWaypoint]).sqrMagnitude<1.5f)rescueWaypoint++;
                Vector3 destination=rescuePath[rescueWaypoint];Vector3 delta=destination-ally.transform.position;delta.y=0;
                if(delta.sqrMagnitude>9)destination=ally.transform.position+delta.normalized*3;
                if(mission.navigation.TryProject(destination,out Vector3 projected))MoveHero(projected);
            }
            internal void Complete(string status,string failure,bool stopPlay=true)
            {
                if(complete)return;complete=true;
                if(!string.IsNullOrEmpty(failure))report.failures.Add(failure);
                report.status=status;report.finishedUtc=DateTime.UtcNow.ToString("O");report.wallSeconds=Now-started;
                try
                {
                    report.frames=metrics.Snapshot();
                    try
                    {
                        if(pose!=null)pose.SetTriggerHeld(false);
                        if(shooter!=null) { shooter.VolleyFired-=OnVolley;shooter.DetailedProjectileHit-=OnImpact; }
                        if(mission!=null&&mission.playerHealth!=null)
                        { mission.playerHealth.Damaged-=OnHeroDamage;if(heroCaptured)mission.playerHealth.invulnerable=originalHeroInvulnerability; }
                        if(input!=null&&input.GameplayPaused)input.Resume();Time.timeScale=1;
                        if(mission!=null)mission.ReturnToSelection();
                    }
                    finally
                    {
                        if(progression!=null&&progression.IsDiagnosticSession)progression.EndDiagnosticSession();
                        if(mission!=null&&profileConfigured)
                        {
                            mission.eliminationDefinition=originalElimination;mission.rescueDefinition=originalRescue;
                            mission.authoredSpawnPoints=originalAuthoredPoints;mission.spawnPoints=originalPerimeterPoints;
                        }
                        if(captureConfigured)
                        {
                            capture.saveWhenStopped=originalCaptureSave;Application.runInBackground=originalRunInBackground;
                            report.framesCsv=Path.Combine(runDirectory,"Frames.csv");
                            if(!capture.ExportRecentFramesTo(report.framesCsv))
                            {
                                report.framesCsv="";
                                if(report.status=="PASS") { report.status="FAIL";report.failures.Add("Не сохранена настоящая запись кадров."); }
                            }
                        }
                        if(elimination!=null)Object.Destroy(elimination);if(rescue!=null)Object.Destroy(rescue);if(arrival!=null)Object.Destroy(arrival);
                    }
                }
                catch(Exception error) { report.status="FAIL";report.failures.Add("Cleanup: "+error); }
                finally
                {
                    bool saved=SaveReport(report,runDirectory);
                    SessionState.SetInt(Key+"exitCode",saved&&report.status=="PASS"?0:1);
                    Debug.Log("CareProject: сценарии завершены, завершаю Play Mode. Отчёт: "+LatestReportPath);
                    if(stopPlay)EditorApplication.isPlaying=false;
                }
            }
            internal void RecordFailure(string error)
            {
                if(report.failures.Count<24)report.failures.Add(error);
                if(!complete)return;
                report.status="FAIL";SessionState.SetInt(Key+"exitCode",1);
                SaveReport(report,runDirectory);
            }
        }
    }
}
