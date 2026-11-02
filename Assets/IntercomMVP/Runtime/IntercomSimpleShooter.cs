using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Swept twin-stick bullets with bounded visuals, per-projectile damage snapshots and generic damage receivers.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class IntercomSimpleShooter : MonoBehaviour
    {
        [Header("Weapon definition")]
        [Tooltip("Central balance, pose, sound and effects. Empty keeps legacy prototype settings.")]
        public IntercomWeaponDefinition weapon;
        public IntercomWeaponDefinition Weapon => weapon;
        private bool DualVolley => weapon != null ? weapon.ProjectilesPerVolley == 2 : fireBothPistols;
        private float ShotDamage => weapon != null ? weapon.Damage : IntercomWeaponBalance.Positive(damage,1f);
        private float ShotWidth => weapon != null ? weapon.tracerWidth : tracerWidth;
        private float ShotLength => weapon != null ? weapon.tracerLength : tracerLength;
        [Header("References")]
        public IntercomWeaponPose weaponPose;
        [Tooltip("Barrel tip; local +Z must point out of the right gun.")]
        public Transform muzzleRight;
        [Tooltip("Barrel tip; local +Z must point out of the left gun.")]
        public Transform muzzleLeft;
        [Tooltip("Robot root. Its colliders and all child colliders are ignored.")]
        public Transform ownerRoot;
        [Tooltip("Optional origin for the wall check from the body to the muzzle. Defaults to the CharacterController centre.")]
        public Transform obstructionOrigin;
        [Tooltip("Assign an included URP Unlit material so its shader survives a mobile build.")]
        public Material projectileMaterial;

        [Header("Prototype projectiles")]
        [Min(0.1f)] public float projectileSpeed = 36f;
        [Min(0.05f)] public float projectileLifetime = 1.4f;
        [Min(0.1f)] public float projectileRange = 20f;
        [Min(0.005f)] public float projectileRadius = 0.035f;
        [Tooltip("Visual width only. Collision remains controlled by Projectile Radius.")]
        [Range(0.03f, 0.4f)] public float tracerWidth = 0.15f;
        [Min(0.04f)] public float tracerLength = 0.68f;
        [Range(4, 128)] public int poolSize = 32;
        public LayerMask collisionMask = ~0;
        public bool fireBothPistols = true;
        [Min(0.1f)] public float damage = 1f;
        public Material projectileCoreMaterial;

        /// <summary>One event per volley that actually created bullets: sound position and successful barrel count.</summary>
        public event System.Action<Vector3, int> VolleyFired;
        public event System.Action<Transform, bool> BarrelFired;
        /// <summary>Point, outward surface normal, and whether a damage receiver accepted damage.</summary>
        public event System.Action<Vector3, Vector3, bool> ProjectileHit;
        /// <summary>Resolved material contact for sound and particles; legacy ProjectileHit remains compatible.</summary>
        public event System.Action<IntercomProjectileImpact> DetailedProjectileHit;

        private struct Projectile
        {
            public Transform visual;
            public Vector3 position;
            public Vector3 direction;
            public float speed;
            public float radius;
            public float life;
            public float range;
            public float travelled;
            public float damage, visualWidth, visualLength;
            public bool active;
        }

        private readonly RaycastHit[] sweepHits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[64];
        private Projectile[] pool;
        private GameObject poolRoot;
        private Mesh projectileMesh;
        private Material fallbackMaterial;
        private CharacterController ownerCapsule;
        private IntercomPlayerMotor motor;
        private IntercomWeaponMagazine magazine;
        private IntercomWeaponPose subscribedPose;
        private int pendingShots;
        private int nextSlot;
        private bool nextRight = true;
        private bool warnedMissingMuzzles;
        private bool hasFocus = true;
        private bool appPaused;

        /// <summary>Called by setup or a future equipment handler. +Z of each marker is the barrel direction.</summary>
        public void Initialize(IntercomWeaponPose pose, Transform right, Transform left, Transform owner)
        {
            CancelPendingShots();
            weaponPose = pose;
            muzzleRight = right;
            muzzleLeft = left;
            ownerRoot = owner != null ? owner : transform;
            ownerCapsule = ownerRoot.GetComponent<CharacterController>();
            if (Application.isPlaying && isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (ownerRoot == null) ownerRoot = transform;
            if (weaponPose == null) weaponPose = GetComponent<IntercomWeaponPose>();
            ownerCapsule = ownerRoot.GetComponent<CharacterController>();
            motor = GetComponent<IntercomPlayerMotor>();
            magazine = GetComponent<IntercomWeaponMagazine>();
            CreatePool();
        }

        private void OnEnable() { Subscribe(); }

        private void OnDisable()
        {
            Unsubscribe();
            pendingShots = 0;
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++) Release(i);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (poolRoot != null) Destroy(poolRoot);
            if (projectileMesh != null) Destroy(projectileMesh);
            if (fallbackMaterial != null) Destroy(fallbackMaterial);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) pendingShots = 0;
        }

        private void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused) pendingShots = 0;
        }

        private void Subscribe()
        {
            if (subscribedPose == weaponPose) return;
            Unsubscribe();
            if (weaponPose == null) return;
            subscribedPose = weaponPose;
            subscribedPose.ShotReady += QueueShot;
            subscribedPose.FireCancelled += CancelPendingShots;
        }

        private void Unsubscribe()
        {
            if (subscribedPose != null)
            {
                subscribedPose.ShotReady -= QueueShot;
                subscribedPose.FireCancelled -= CancelPendingShots;
            }
            subscribedPose = null;
            CancelPendingShots();
        }

        private void CancelPendingShots() { pendingShots = 0; }

        private void QueueShot()
        {
            if (hasFocus && !appPaused && (magazine == null || magazine.CanFire) && subscribedPose != null && subscribedPose.IsReadyToFire)
                pendingShots = Mathf.Min(pendingShots + 1, 4);
        }

        private void LateUpdate()
        {
            if (!hasFocus || appPaused || pool == null || Time.deltaTime <= 0f) return;
            AdvanceProjectiles(Time.deltaTime);
            // The pose driver raises first and queues here during Update.
            // Sample the animated markers after Animator evaluation and turning.
            // Parallel pistols share the current animated weapon heading, not two inward yaw angles.
            int shots = subscribedPose != null && subscribedPose.isActiveAndEnabled && subscribedPose.IsReadyToFire ? pendingShots : 0;
            pendingShots = 0;
            for (int i = 0; i < shots; i++)
            {
                if (magazine != null && !magazine.CanFire) break;
                int fired = 0;
                Vector3 soundPosition = Vector3.zero;
                Vector3 fallback = motor != null ? motor.AimWorldDirection : ownerRoot != null ? ownerRoot.forward : transform.forward;
                bool planar = weapon != null && weapon.planarProjectileDirection;
                Vector3 parallel = IntercomProjectileDirection.Parallel(muzzleRight != null ? muzzleRight.forward : Vector3.zero,
                    muzzleLeft != null ? muzzleLeft.forward : Vector3.zero,fallback,planar);
                bool shareHeading = DualVolley && (weapon == null || weapon.parallelProjectiles);
                Transform firedRight = null, firedLeft = null;
                if (DualVolley)
                {
                    if (Spawn(muzzleRight, shareHeading, parallel)) { fired++; soundPosition += muzzleRight.position; firedRight=muzzleRight; }
                    if (Spawn(muzzleLeft, shareHeading, parallel)) { fired++; soundPosition += muzzleLeft.position; firedLeft=muzzleLeft; }
                }
                else
                {
                    Transform muzzle = nextRight ? muzzleRight : muzzleLeft;
                    if (muzzle == null) muzzle = muzzleRight != null ? muzzleRight : muzzleLeft;
                    if (Spawn(muzzle, false, parallel))
                    {
                        fired = 1; soundPosition = muzzle.position;
                        if(muzzle==muzzleRight) firedRight=muzzle; else firedLeft=muzzle;
                        nextRight = !nextRight;
                    }
                }
                // Commit only successful barrels, before external callbacks can request a reload.
                // Wall checks / an exhausted pool consume no round and emit no report.
                if (fired > 0)
                {
                    if(weaponPose != null) weaponPose.ConfirmShotVisual();
                    if(magazine != null) magazine.CommitVolley(fired);
                    if(firedRight != null) BarrelFired?.Invoke(firedRight,true);
                    if(firedLeft != null) BarrelFired?.Invoke(firedLeft,false);
                    VolleyFired?.Invoke(soundPosition / fired, fired);
                }
            }
        }

        private bool Spawn(Transform muzzle, bool shareHeading, Vector3 parallel)
        {
            if (muzzle == null)
            {
                if (!warnedMissingMuzzles)
                {
                    warnedMissingMuzzles = true;
                    Debug.LogWarning("Intercom MVP: подключи Muzzle Right и Muzzle Left через Setup Robot. Маркер +Z должен смотреть из ствола.", this);
                }
                return false;
            }
            Vector3 position = muzzle.position;
            float radius = IntercomWeaponBalance.Positive(weapon != null ? weapon.projectileRadius : projectileRadius,0.035f,0.005f);
            Vector3 checkOrigin = BodyCheckOrigin(position);
            // A gun can visually poke through a thin wall. Check the whole
            // body-to-barrel path as well as the spawn point, so it cannot fire
            // from the far side of that wall. Owner colliders are filtered out.
            if (OverlapsObstacle(checkOrigin, radius) || OverlapsObstacle(position, radius)
                || SegmentBlocked(checkOrigin, position, radius)) return false;

            int slot = FindAvailableSlot();
            if (slot < 0) return false; // Bounded pool: do not allocate or steal a live bullet.
            Vector3 direction = shareHeading ? parallel : IntercomProjectileDirection.Resolve(muzzle.forward, motor != null ? motor.AimWorldDirection : ownerRoot != null ? ownerRoot.forward : transform.forward, weapon != null && weapon.planarProjectileDirection);
            pool[slot].position = position;
            pool[slot].direction = direction;
            pool[slot].speed = IntercomWeaponBalance.Positive(weapon != null ? weapon.projectileSpeed : projectileSpeed,36f,0.1f);
            pool[slot].radius = radius;
            pool[slot].life = IntercomWeaponBalance.Positive(weapon != null ? weapon.projectileLifetime : projectileLifetime,1.4f,0.05f);
            pool[slot].range = IntercomWeaponBalance.Positive(weapon != null ? weapon.projectileRange : projectileRange,20f,0.1f);
            pool[slot].damage = ShotDamage;
            pool[slot].visualWidth = ShotWidth;
            pool[slot].visualLength = ShotLength;
            pool[slot].travelled = 0f;
            pool[slot].active = true;
            Transform visual = pool[slot].visual;
            visual.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            float width = IntercomTracerStyle.Width(ShotWidth);
            visual.localScale = new Vector3(width, width, IntercomTracerStyle.Length(ShotLength, 0f));
            visual.gameObject.SetActive(true);
            return true;
        }

        private void AdvanceProjectiles(float dt)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].active) continue;
                float travel = Mathf.Min(pool[i].speed * Mathf.Min(dt, pool[i].life), pool[i].range);
                Vector3 start = pool[i].position;
                Vector3 end = start + pool[i].direction * travel;
                // Overlap handles a door/target that moved into a bullet since its last step.
                Collider overlap = FirstObstacle(start, pool[i].radius, out bool overlapSaturated);
                if (overlap != null || overlapSaturated)
                {
                    Hit(overlap, start, -pool[i].direction, pool[i].direction, pool[i].damage);
                    Release(i);
                    continue;
                }
                if (FirstHit(start, end, pool[i].radius, out RaycastHit hit, out bool sweepSaturated))
                {
                    // NonAlloc hit order is undefined: damage only the nearest blocking surface.
                    Hit(hit.collider, hit.point, hit.normal, pool[i].direction, pool[i].damage);
                    Release(i);
                    continue;
                }
                if (sweepSaturated) { Release(i); continue; } // Ambiguous query: never shoot through scenery.
                pool[i].position = end;
                pool[i].life -= dt;
                pool[i].range -= travel;
                pool[i].travelled += travel;
                pool[i].visual.position = end;
                float width = IntercomTracerStyle.Width(pool[i].visualWidth);
                pool[i].visual.localScale = new Vector3(width, width,
                    IntercomTracerStyle.Length(pool[i].visualLength, pool[i].travelled));
                if (pool[i].life <= 0f || pool[i].range <= 0.0001f) Release(i);
            }
        }

        private Vector3 BodyCheckOrigin(Vector3 muzzlePosition)
        {
            if (obstructionOrigin != null) return obstructionOrigin.position;
            if (ownerCapsule != null) return ownerCapsule.transform.TransformPoint(ownerCapsule.center);
            if (ownerRoot == null) return transform.position;
            // Sensible fallback for a different actor without a capsule.
            return new Vector3(ownerRoot.position.x, muzzlePosition.y, ownerRoot.position.z);
        }

        private void Hit(Collider collider, Vector3 point, Vector3 normal, Vector3 direction, float shotDamage)
        {
            bool targetHit = false;
            if (collider != null)
            {
                IIntercomDamageable target = collider.GetComponentInParent<IIntercomDamageable>();
                if (target != null) targetHit = target.TakeDamage(new IntercomDamage(shotDamage, point, direction,
                    ownerRoot != null ? ownerRoot.gameObject : gameObject));
            }
            if (normal.sqrMagnitude < 0.001f) normal = -direction;
            normal = normal.normalized;
            DetailedProjectileHit?.Invoke(IntercomProjectileImpact.Resolve(collider, point, normal, targetHit));
            ProjectileHit?.Invoke(point, normal, targetHit);
        }

        private bool OverlapsObstacle(Vector3 position, float radius)
        {
            return FirstObstacle(position, radius, out bool saturated) != null || saturated;
        }

        private Collider FirstObstacle(Vector3 position, float radius, out bool saturated)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            saturated = !IntercomProjectileHitChoice.IsComplete(count, overlaps.Length);
            // A saturated result cannot prove which obstacle is closest. Do not invent target damage.
            if (saturated) return null;
            Collider result = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider item = overlaps[i];
                if (IsOwner(item)) continue;
                float distance = (item.ClosestPoint(position) - position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; result = item; }
            }
            return result;
        }

        private bool SegmentBlocked(Vector3 start, Vector3 end, float radius)
        {
            return FirstHit(start, end, radius, out _, out bool saturated) || saturated;
        }

        private bool FirstHit(Vector3 start, Vector3 end, float radius, out RaycastHit nearest, out bool saturated)
        {
            nearest = default;
            saturated = false;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.000001f) return false;
            int count = Physics.SphereCastNonAlloc(start, radius, delta / distance, sweepHits,
                distance, collisionMask, QueryTriggerInteraction.Ignore);
            saturated = !IntercomProjectileHitChoice.IsComplete(count, sweepHits.Length);
            if (saturated) return false;
            IntercomProjectileHitChoice choice = new IntercomProjectileHitChoice(distance);
            for (int i = 0; i < count; i++)
                choice.Consider(i, sweepHits[i].distance, IsOwner(sweepHits[i].collider));
            if (choice.Index < 0) return false;
            nearest = sweepHits[choice.Index];
            return true;
        }

        private bool IsOwner(Collider collider)
        {
            if (collider == null) return true;
            Transform item = collider.transform;
            return ownerRoot != null && (item == ownerRoot || item.IsChildOf(ownerRoot));
        }

        private int FindAvailableSlot()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                int slot = (nextSlot + i) % pool.Length;
                if (pool[slot].active) continue;
                nextSlot = (slot + 1) % pool.Length;
                return slot;
            }
            return -1;
        }

        private void Release(int index)
        {
            if (!pool[index].active) return;
            pool[index].active = false;
            if (pool[index].visual != null) pool[index].visual.gameObject.SetActive(false);
        }

        private void CreatePool()
        {
            Material material = weapon != null && weapon.projectileMaterial != null ? weapon.projectileMaterial : projectileMaterial;
            Material coreMaterial = weapon != null && weapon.projectileCoreMaterial != null ? weapon.projectileCoreMaterial : projectileCoreMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.LogError("Intercom MVP: назначь Projectile Material с доступным Unlit shader.", this);
                    enabled = false;
                    return;
                }
                fallbackMaterial = new Material(shader) { name = "Intercom Projectile (Runtime)" };
                Color color = new Color(1f, 0.82f, 0.18f, 1f);
                if (fallbackMaterial.HasProperty("_BaseColor")) fallbackMaterial.SetColor("_BaseColor", color);
                if (fallbackMaterial.HasProperty("_Color")) fallbackMaterial.SetColor("_Color", color);
                material = fallbackMaterial;
            }
            projectileMesh = IntercomTracerGeometry.Create();
            poolRoot = new GameObject("Intercom Projectile Pool");
            pool = new Projectile[Mathf.Clamp(poolSize, 4, 128)];
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject bullet = new GameObject("Projectile " + i);
                bullet.transform.SetParent(poolRoot.transform, false);
                bullet.AddComponent<MeshFilter>().sharedMesh = projectileMesh;
                MeshRenderer renderer = bullet.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material, coreMaterial != null ? coreMaterial : material };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                bullet.SetActive(false);
                pool[i].visual = bullet.transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawMuzzleGizmo(muzzleRight, new Color(1f, 0.8f, 0.15f));
            DrawMuzzleGizmo(muzzleLeft, new Color(0.15f, 0.9f, 1f));
        }

        private void DrawMuzzleGizmo(Transform muzzle, Color color)
        {
            if (muzzle == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(muzzle.position, Mathf.Max(0.005f, projectileRadius));
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 0.6f);
        }
    }
}
