using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Owns one local profile, automatic terminal-run settlement and an optional pair of magazine extensions.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class IntercomProgression : MonoBehaviour
    {
        public const int MaximumMagazineLevel = IntercomProgressionState.MaximumMagazineLevel;
        public const int MagazineRoundsPerLevel = IntercomProgressionState.MagazineRoundsPerLevel;
        public const int MaximumPendingRewards = 32;
        private const string BankA = "CareProject.Progression.v1.a", BankB = "CareProject.Progression.v1.b";
        public IntercomMissionDirector mission;
        public IntercomWeaponMagazine magazine;
        public IntercomWeaponDefinition weapon;
        [Header("Optional add-on anchors: local +Z points out of the barrel")]
        public Transform visualLeftMount, visualRightMount;

        public int Wallet => Profile.Wallet;
        public int MagazineLevel => Profile.MagazineLevel;
        public bool MagazineMaxed => MagazineLevel >= MaximumMagazineLevel;
        public int NextMagazineCost => Profile.NextMagazineCost;
        public int EffectiveMagazineCapacity => magazine != null ? magazine.Capacity
            : IntercomWeaponBalance.EffectiveMagazineCapacity(Weapon != null ? Weapon.MagazineCapacity : 24, Profile.MagazineBonus,
                Weapon != null ? Weapon.ProjectilesPerVolley : 1);
        public int NextMagazineCapacity => IntercomWeaponBalance.EffectiveMagazineCapacity(
            Weapon != null ? Weapon.MagazineCapacity : 24,
            Math.Min(MaximumMagazineLevel, MagazineLevel + 1) * MagazineRoundsPerLevel,
            Weapon != null ? Weapon.ProjectilesPerVolley : 1);
        public double EffectiveIdealTtk => Weapon != null ? IntercomWeaponBalance.IdealTtkWithReload(
            Weapon.referenceTargetHealth, Weapon.Damage, Weapon.ProjectilesPerVolley, Weapon.Interval,
            EffectiveMagazineCapacity, Weapon.ReloadSeconds) : 0d;
        public double EffectiveSustainedDps => Weapon != null ? IntercomWeaponBalance.SustainedDps(
            Weapon.Damage, Weapon.ProjectilesPerVolley, Weapon.Interval, EffectiveMagazineCapacity, Weapon.ReloadSeconds) : 0d;
        public bool HasPendingRewards => pendingCount > 0 || HasUnobservedReward;
        public int PendingRewardCount => pendingCount;
        public bool CanStartAnotherRun => pendingCount + (HasUnobservedReward ? 1 : 0) < MaximumPendingRewards;
        public bool CanBuyMagazine => isActiveAndEnabled && Profile.CanBuyMagazine && mission != null
            && magazine != null && magazine.isActiveAndEnabled && magazine.shooter != null && Weapon != null
            && !HasPendingRewards && NextMagazineCapacity > EffectiveMagazineCapacity
            && mission.Phase == IntercomMissionPhase.Selection && Time.timeScale > 0f && Application.isFocused;
        public bool HasResultReward { get; private set; }
        public IntercomRunReward ResultReward { get; private set; }
        public uint ResultRunRevision { get; private set; }
        public string SaveProblem { get; private set; } = "";
        public string LoadWarning { get; private set; } = "";
        public bool IsDiagnosticSession { get; private set; }

        private IntercomProgressionState profile, realProfile;
        private readonly IntercomMissionRunSummary[] pending = new IntercomMissionRunSummary[MaximumPendingRewards];
        private int pendingHead, pendingCount;
        private string realSaveProblem;
        private uint observedResultRevision;
        private float nextSaveAttempt;
        private Mesh extensionMesh;
        private Transform leftExtension, rightExtension;
        private bool visualsPrepared;
        private int appliedBonus;
        public IntercomWeaponDefinition EquippedWeapon => magazine != null && magazine.shooter != null && magazine.shooter.Weapon != null
            ? magazine.shooter.Weapon : weapon;
        private IntercomWeaponDefinition Weapon => EquippedWeapon;
        private IntercomProgressionState Profile => profile ?? (profile = new IntercomProgressionState());
        private bool HasUnobservedReward
        {
            get
            {
                if (mission == null || mission.ResultRevision == 0 || mission.ResultRevision == observedResultRevision) return false;
                IntercomMissionRunSummary summary = mission.LastRunSummary;
                return summary.ResultRevision == mission.ResultRevision && Eligible(summary)
                    && IntercomProgressionState.ValidRunId(summary.RunId) && summary.RunId != Profile.LastRunId;
            }
        }
        private static bool Eligible(IntercomMissionRunSummary summary)
            => (summary.Outcome == IntercomMissionPhase.Victory || summary.Outcome == IntercomMissionPhase.Defeat)
                && (summary.Mode == IntercomMissionMode.Elimination || summary.Mode == IntercomMissionMode.Rescue);

        private void Awake()
        {
            ResolveReferences();
            try
            {
                profile = IntercomProgressionState.Recover(PlayerPrefs.GetString(BankA, ""), PlayerPrefs.GetString(BankB, ""), out bool recovered);
                if (recovered) LoadWarning = "Повреждённые записи прогресса пропущены. Сохранённые данные не удалялись.";
            }
            catch (Exception)
            {
                profile = new IntercomProgressionState();
                LoadWarning = "Не удалось прочитать локальный прогресс. Сохранённые записи не удалялись.";
            }
            ApplyUpgrade();
        }
        private void Start() { ResolveReferences(); PrepareVisuals(); ApplyUpgrade(); }
        private void OnEnable() { if (profile != null) ApplyUpgrade(); }
        private void OnDisable() { EndDiagnosticSession(); SetVisual(leftExtension, false); SetVisual(rightExtension, false); }
        private void OnDestroy()
        {
            EndDiagnosticSession();
            if (magazine != null && magazine.CapacityBonus == appliedBonus) magazine.SetCapacityBonus(0);
            if (leftExtension != null) Destroy(leftExtension.gameObject);
            if (rightExtension != null) Destroy(rightExtension.gameObject);
            if (extensionMesh != null) Destroy(extensionMesh);
        }
        private void ResolveReferences()
        {
            if (mission == null) mission = GetComponent<IntercomMissionDirector>();
            if (magazine == null && mission != null && mission.playerMotor != null)
                magazine = mission.playerMotor.GetComponent<IntercomWeaponMagazine>();
            if (weapon == null && magazine != null && magazine.shooter != null) weapon = magazine.shooter.Weapon;
            if (magazine != null && magazine.shooter != null)
            {
                if (visualLeftMount == null) visualLeftMount = magazine.shooter.muzzleLeft;
                if (visualRightMount == null) visualRightMount = magazine.shooter.muzzleRight;
            }
        }
        private void Update()
        {
            if (pendingCount == 0 && (mission == null || mission.ResultRevision == 0 || mission.ResultRevision == observedResultRevision)) return;
            TryCollectCompletedRun();
        }

        /// <summary>Only the director's current immutable terminal summary may be credited.</summary>
        public bool TryCollectCompletedRun()
        {
            if (!isActiveAndEnabled) return false;
            CaptureLatestResult();
            if (pendingCount == 0 || Time.unscaledTime < nextSaveAttempt) return false;
            IntercomMissionRunSummary summary = pending[pendingHead];
            bool fresh = Profile.TrySettle(summary.RunId, summary.Kills, summary.Rescued, summary.Won, true,
                out IntercomProgressionState next, out IntercomRunReward reward);
            if (fresh && !Commit(next)) { nextSaveAttempt = Time.unscaledTime + 2f; return false; }
            if (!fresh && Profile.LastRunId != summary.RunId)
            {
                SaveProblem = "Не удалось подтвердить накопленную награду. Запчасти остаются в очереди до сохранения.";
                nextSaveAttempt = Time.unscaledTime + 2f; return false;
            }
            pending[pendingHead] = default; pendingHead = (pendingHead + 1) % MaximumPendingRewards; pendingCount--;
            ShowReward(summary, reward); nextSaveAttempt = 0f;
            return fresh;
        }
        private void CaptureLatestResult()
        {
            if (mission == null || mission.ResultRevision == 0 || mission.ResultRevision == observedResultRevision) return;
            IntercomMissionRunSummary summary = mission.LastRunSummary;
            // Retry may already have cleared HasRunSummary before this component's next Update.
            // The immutable last terminal snapshot survives that transition and is still payable exactly once.
            if (summary.ResultRevision != mission.ResultRevision) return;
            bool eligible = Eligible(summary) && IntercomProgressionState.ValidRunId(summary.RunId);
            // A recreated component sees the same run ID in the saved journal and restores its display without paying again.
            if (!eligible || Profile.LastRunId == summary.RunId)
            {
                ShowReward(summary, new IntercomRunReward(summary.Kills, summary.Rescued, summary.Won, eligible, Profile.LastCredit));
                observedResultRevision = summary.ResultRevision; return;
            }
            if (pendingCount >= MaximumPendingRewards)
            {
                SaveProblem = "Нужно сохранить накопленные награды перед следующим выездом. Не закрывай игру до сохранения.";
                return;
            }
            pending[(pendingHead + pendingCount) % MaximumPendingRewards] = summary; pendingCount++;
            observedResultRevision = summary.ResultRevision;
            HasResultReward = false; ResultReward = default; ResultRunRevision = summary.RunRevision;
        }
        private void ShowReward(IntercomMissionRunSummary summary, IntercomRunReward reward)
        {
            // Paying an older queued run must not replace the latest result screen's breakdown.
            if (mission != null && summary.ResultRevision != mission.ResultRevision) return;
            HasResultReward = reward.Eligible; ResultReward = reward; ResultRunRevision = summary.RunRevision;
        }
        public bool TryBuyMagazineUpgrade()
        {
            if (!CanBuyMagazine || !Profile.TryBuyMagazine(out IntercomProgressionState next)) return false;
            if (!Commit(next)) return false;
            ApplyUpgrade();
            return true;
        }
        private bool Commit(IntercomProgressionState next)
        {
            if (IsDiagnosticSession) { profile = next; SaveProblem = ""; return true; }
            string key = (next.Generation & 1) == 0 ? BankA : BankB;
            string previous = "";
            bool hadKey = false, readPrevious = false;
            try
            {
                hadKey = PlayerPrefs.HasKey(key);
                previous = PlayerPrefs.GetString(key, "");
                readPrevious = true;
                PlayerPrefs.SetString(key, next.Encode());
                PlayerPrefs.Save();
                profile = next; SaveProblem = "";
                return true;
            }
            catch (Exception)
            {
                // Keep both the in-memory wallet and the prior preference bytes when a local save fails.
                if (readPrevious)
                    try { if (hadKey) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key); } catch (Exception) { }
                SaveProblem = "Не удалось сохранить прогресс. Награда или покупка не подтверждена; попробуй ещё раз.";
                return false;
            }
        }

        /// <summary>Native soak tests use a fresh temporary profile and never write real preferences.</summary>
        public bool BeginDiagnosticSession()
        {
            if (IsDiagnosticSession || HasPendingRewards) return false;
            realSaveProblem = SaveProblem;
            realProfile = Profile; profile = new IntercomProgressionState(); IsDiagnosticSession = true;
            HasResultReward = false; ResultReward = default; ResultRunRevision = observedResultRevision = 0;
            nextSaveAttempt = 0f; ApplyUpgrade();
            return true;
        }
        public void EndDiagnosticSession()
        {
            if (!IsDiagnosticSession) return;
            profile = realProfile ?? new IntercomProgressionState(); realProfile = null; IsDiagnosticSession = false;
            SaveProblem = realSaveProblem ?? ""; realSaveProblem = null;
            Array.Clear(pending, 0, pending.Length); pendingHead = pendingCount = 0;
            HasResultReward = false; ResultReward = default; ResultRunRevision = 0;
            observedResultRevision = mission != null ? mission.ResultRevision : 0;
            nextSaveAttempt = 0f; ApplyUpgrade();
        }
        private void ApplyUpgrade()
        {
            appliedBonus = Profile.MagazineBonus;
            if (magazine != null) magazine.SetCapacityBonus(appliedBonus);
            ApplyVisual(leftExtension); ApplyVisual(rightExtension);
        }
        private static void SetVisual(Transform item, bool active) { if (item != null) item.gameObject.SetActive(active); }
        private void ApplyVisual(Transform item)
        {
            if (item == null) return;
            SetVisual(item, MagazineLevel > 0 && isActiveAndEnabled);
            Vector3 scale = item.parent.lossyScale;
            item.localPosition = Divide(new Vector3(0f, -.065f, -.12f), scale);
            item.localScale = Divide(new Vector3(.055f, .045f + MagazineLevel * .012f, .09f), scale);
        }
        private static Vector3 Divide(Vector3 value, Vector3 scale) => new Vector3(
            value.x / Magnitude(scale.x), value.y / Magnitude(scale.y), value.z / Magnitude(scale.z));
        private static float Magnitude(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(.0001f, Mathf.Abs(value));
        private void PrepareVisuals()
        {
            if (visualsPrepared) return;
            visualsPrepared = true;
            Material shared = Weapon != null ? Weapon.casingMaterial : null;
            if (shared == null || (visualLeftMount == null && visualRightMount == null)) return;
            extensionMesh = BuildExtensionMesh();
            leftExtension = CreateExtension(visualLeftMount, "Magazine extension L", shared);
            rightExtension = CreateExtension(visualRightMount, "Magazine extension R", shared);
        }
        private Transform CreateExtension(Transform mount, string label, Material shared)
        {
            if (mount == null) return null;
            GameObject item = new GameObject(label); item.transform.SetParent(mount, false);
            item.AddComponent<MeshFilter>().sharedMesh = extensionMesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = shared;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            item.SetActive(false); return item.transform;
        }
        private static Mesh BuildExtensionMesh()
        {
            Vector3[] vertices = new Vector3[34];
            float[] x = { .5f, .35f, -.35f, -.5f, -.5f, -.35f, .35f, .5f };
            float[] z = { .35f, .5f, .5f, .35f, -.35f, -.5f, -.5f, -.35f };
            float[] heights = { -.5f, -.35f, .35f, .5f };
            for (int r = 0; r < 4; r++) for (int i = 0; i < 8; i++)
            { float inset = r == 0 || r == 3 ? .85f : 1f; vertices[r * 8 + i] = new Vector3(x[i] * inset, heights[r], z[i] * inset); }
            vertices[32] = new Vector3(0f, -.5f, 0f); vertices[33] = new Vector3(0f, .5f, 0f);
            int[] triangles = new int[192]; int n = 0;
            for (int r = 0; r < 3; r++) for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8, a = r * 8 + i, b = r * 8 + j;
                triangles[n++] = a; triangles[n++] = a + 8; triangles[n++] = b;
                triangles[n++] = a + 8; triangles[n++] = b + 8; triangles[n++] = b;
            }
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                triangles[n++] = i; triangles[n++] = j; triangles[n++] = 32;
                triangles[n++] = 24 + i; triangles[n++] = 33; triangles[n++] = 24 + j;
            }
            Mesh mesh = new Mesh { name = "Magazine extension - shared owned bevel mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
