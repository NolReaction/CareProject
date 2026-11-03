using System.Collections.Generic;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Grounded melee actor. Navigation proposes a path; the CharacterController resolves actual movement.</summary>
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(IntercomHealth))]
    public sealed partial class IntercomNpcController : MonoBehaviour
    {
        public IntercomNpcDefinition definition;
        public IntercomNpcNavigation navigation;
        public IntercomHealth targetHealth;
        public IntercomPlayerMotor resetMotor;
        public Transform visual;
        public Renderer bodyRenderer;
        public Transform telegraph;
        public int variant;
        public IntercomNpcPhase Phase => brain.Phase;
        public IntercomHealth Health => health;

        private static readonly List<IntercomNpcController> active = new List<IntercomNpcController>(16);
        private readonly IntercomNpcBrain brain = new IntercomNpcBrain();
        private readonly List<Vector3> route = new List<Vector3>(128);
        private readonly RaycastHit[] sightHits = new RaycastHit[24];
        private CharacterController controller;
        private IntercomHealth health, subscribedHealth;
        private Camera displayCamera;
        private Vector3 home, velocity, lastSeen, patrolGoal, lockedHeading, lastDestination, recoveryDirection;
        private Quaternion homeRotation;
        private uint observedRespawn, pathVersion;
        private int waypoint, patrolLeg;
        private float verticalVelocity, repathRemaining, hitRemaining, strikeRemaining, deathSeconds, stuckSeconds, recoveryRemaining, investigateRemaining;
        private bool initialized, hasFocus = true, paused, suspended;
        private MaterialPropertyBlock bodyProperties;
        private Color baseColor;
        private Mesh ownedTelegraphMesh;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private float Range => Safe(definition.attackRange, 1.05f, .3f, 3f);
        private static float Safe(float v, float fallback, float min, float max) => IntercomNpcDefinition.Safe(v, fallback, min, max);
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        private bool TargetAlive => targetHealth != null && targetHealth.isActiveAndEnabled && !targetHealth.IsDead;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<IntercomHealth>();
            home = transform.position; homeRotation = transform.rotation;
            bodyProperties = new MaterialPropertyBlock();
            displayCamera = Camera.main;
            initialized = definition != null && controller != null && health != null;
            if (!initialized)
            {
                Debug.LogWarning("NPC needs Definition, CharacterController and Health.", this);
                enabled = false; return;
            }
            health.maximumHealth = Safe(definition.maximumHealth, 75f, 1f, 100000f);
            health.team = IntercomTeam.Enemy; health.postHitInvulnerability = 0f;
            baseColor = definition.bodyColor;
            if (variant % 3 == 1) baseColor = Color.Lerp(baseColor, new Color(.35f,.56f,.53f,1f), .75f);
            else if (variant % 3 == 2) baseColor = Color.Lerp(baseColor, new Color(.65f,.39f,.3f,1f), .65f);
            ResetToHome();
        }

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            Subscribe();
        }
        private void Subscribe()
        {
            if (health == null) health = GetComponent<IntercomHealth>();
            if (subscribedHealth == health) return;
            Unsubscribe(); subscribedHealth = health;
            if (subscribedHealth != null) { subscribedHealth.Damaged += OnDamaged; subscribedHealth.Died += OnDied; }
        }
        private void Unsubscribe()
        {
            if (subscribedHealth != null) { subscribedHealth.Damaged -= OnDamaged; subscribedHealth.Died -= OnDied; }
            subscribedHealth = null;
        }
        private void OnDisable()
        {
            // Health remains authoritative even when only AI is disabled.
            // A disabled brain killed by a projectile must still release its collider.
            active.Remove(this); Suspend();
        }
        private void OnDestroy()
        {
            active.Remove(this); Unsubscribe();
            if (ownedTelegraphMesh != null) Destroy(ownedTelegraphMesh);
        }

        [ContextMenu("Reset NPC To Home")]
        public void ResetToHome()
        {
            if (!initialized) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(home, homeRotation);
            controller.enabled = true;
            health.maximumHealth = Safe(definition.maximumHealth,75f,1f,100000f);
            health.ResetHealth(); brain.Configure(definition.Rules);
            velocity = Vector3.zero; verticalVelocity = -2f;
            repathRemaining = hitRemaining = strikeRemaining = deathSeconds = stuckSeconds = recoveryRemaining = investigateRemaining = 0f;
            waypoint = patrolLeg = 0; route.Clear(); pathVersion = uint.MaxValue;
            observedRespawn = resetMotor != null ? resetMotor.RespawnVersion : 0;
            lockedHeading = transform.forward; lastSeen = home;
            PickPatrolGoal(); ApplyPresentation(0f);
        }

        /// <summary>Explicit pooled life boundary. Call while inactive, then activate; no new objects are allocated.</summary>
        public void PrepareForSpawn(Vector3 position)
        {
            if(!initialized || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
            home = position;
            homeRotation = Quaternion.identity;
            // The mission pool calls this only while the application is active.
            // Inactive pooled objects may have missed the resume callbacks.
            hasFocus = Application.isFocused;
            paused = false;
            suspended = false;
            ResetToHome();
        }

        /// <summary>Changing the victim never transfers an already prepared attack to the new victim.</summary>
        public void SetTarget(IntercomHealth target)
        {
            if(targetHealth == target) return;
            targetHealth = target;
            brain.Interrupt();
            route.Clear(); waypoint=0; velocity=Vector3.zero;
            recoveryRemaining=stuckSeconds=repathRemaining=investigateRemaining=0f;
            lastSeen=transform.position;
            if(telegraph!=null) telegraph.gameObject.SetActive(false);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>A one-time alarm records a location. It never reveals later movement behind a wall.</summary>
        public void Investigate(Vector3 lastKnownPosition)
        {
            if(!initialized || health.IsDead || !Finite(lastKnownPosition.x) || !Finite(lastKnownPosition.y) || !Finite(lastKnownPosition.z)) return;
            lastSeen=lastKnownPosition;
            investigateRemaining=12f;
            brain.Alert();
            route.Clear(); waypoint=0; repathRemaining=0f;
        }

        private bool ObserveRespawn()
        {
            uint version = resetMotor != null ? resetMotor.RespawnVersion : 0;
            if (version == observedRespawn) return false;
            ResetToHome(); return true;
        }

        private void Update()
        {
            if (!initialized) return;
            Subscribe();
            if (ObserveRespawn()) return;
            if (!hasFocus || paused || Time.timeScale <= 0f)
            { Suspend(); return; }
            float dt = Time.deltaTime;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            dt = Mathf.Min(dt, .1f);
            if(suspended && investigateRemaining>0f && !health.IsDead) brain.Alert();
            suspended = false;
            hitRemaining = Mathf.Max(0f, hitRemaining - dt);
            strikeRemaining = Mathf.Max(0f, strikeRemaining - dt);
            if (health.IsDead)
            {
                brain.Kill(); controller.enabled = false;
                deathSeconds += dt; ApplyPresentation(dt); return;
            }
            if (!controller.enabled) { Suspend(); return; }

            bool alive = TargetAlive;
            Vector3 target = alive ? targetHealth.transform.position : lastSeen;
            Vector3 delta = Flat(target - transform.position);
            float distance = delta.magnitude;
            float detection = Safe(definition.detectionRadius, 10f, 1f, 30f);
            bool visible = alive && distance <= Safe(definition.leashRadius, 18f, 1f, 40f) && ClearSight(target);
            if (visible) { lastSeen = target; investigateRemaining=0f; }
            else investigateRemaining=Mathf.Max(0f,investigateRemaining-dt);
            if(Flat(transform.position-lastSeen).sqrMagnitude<.25f) investigateRemaining=0f;
            bool attackClear = visible && distance <= Range && ClearMelee(target);
            bool outside = Flat(transform.position-home).magnitude > Safe(definition.leashRadius,18f,1f,40f)
                || (alive && Flat(lastSeen-home).magnitude > Safe(definition.leashRadius,18f,1f,40f));
            IntercomNpcPhase before = brain.Phase;
            // The attacker follows the victim during preparation, then commits
            // the last brief part of the swing. The hit still requires contact.
            if(before==IntercomNpcPhase.Windup && visible && delta.sqrMagnitude>.0001f)
            {
                float commit=Mathf.Min(Safe(definition.attackCommitSeconds,.08f,.03f,.3f),
                    Safe(definition.windupSeconds,.3f,.1f,5f)*.65f);
                float trackingDt=Mathf.Min(dt,Mathf.Max(0f,brain.WindupRemaining-commit));
                if(trackingDt>0f)
                {
                    transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta,Vector3.up),
                        Safe(definition.attackTrackingTurnSpeed,900f,1f,1800f)*trackingDt);
                    lockedHeading=transform.forward;
                }
            }
            bool strike = brain.Tick(dt, new IntercomNpcFacts(alive, visible && distance <= detection,
                visible, attackClear, outside, Flat(transform.position-home).sqrMagnitude < .16f,
                Flat(transform.position-patrolGoal).sqrMagnitude < .16f,investigateRemaining>0f));
            if (before != IntercomNpcPhase.Windup && brain.Phase == IntercomNpcPhase.Windup)
            {
                lockedHeading = delta.sqrMagnitude > .0001f ? delta.normalized : transform.forward;
                transform.rotation = Quaternion.LookRotation(lockedHeading, Vector3.up);
                velocity = Vector3.zero;
            }
            if (before == IntercomNpcPhase.Patrol && brain.Phase == IntercomNpcPhase.Idle) PickPatrolGoal();
            if (strike)
            {
                // A death/reset callback can occur while events are being handled.
                // Re-evaluate the current life before touching the target.
                if (ObserveRespawn()) return;
                AttemptStrike(); strikeRemaining = .16f;
            }

            Vector3 desired = Vector3.zero;
            if (brain.Phase == IntercomNpcPhase.Chase)
                desired = Navigate(ApproachDestination(lastSeen,visible,distance), dt) * Safe(definition.moveSpeed, 3f, .1f, 8f);
            else if (brain.Phase == IntercomNpcPhase.Return)
                desired = Navigate(home, dt) * Safe(definition.moveSpeed, 3f, .1f, 8f);
            else if (brain.Phase == IntercomNpcPhase.Patrol)
                desired = Navigate(patrolGoal, dt) * Safe(definition.patrolSpeed, .9f, 0f, 4f);
            bool planted = brain.Phase == IntercomNpcPhase.Windup || brain.Phase == IntercomNpcPhase.Recovery;
            if(recoveryRemaining>0f && !planted && desired.sqrMagnitude>.001f)
            {
                recoveryRemaining=Mathf.Max(0f,recoveryRemaining-dt);
                if(navigation!=null && navigation.SegmentClear(transform.position,transform.position+recoveryDirection*.45f))
                    desired=recoveryDirection*Safe(definition.moveSpeed,3f,.1f,8f)*.65f;
                else recoveryRemaining=0f;
            }
            velocity = planted ? Vector3.zero : Vector3.MoveTowards(velocity, desired,
                Safe(definition.acceleration,10f,.1f,60f)*dt);
            if (!planted && velocity.sqrMagnitude > .005f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(velocity,Vector3.up),
                    Safe(definition.turnSpeed,420f,1f,1200f)*dt);
            // Acceleration retains momentum, so validate the final motion too,
            // not merely the route direction or separation proposal.
            if(navigation!=null && velocity.sqrMagnitude>.0001f
                && !navigation.SegmentClear(transform.position,transform.position+velocity*dt))
            { velocity=Vector3.zero; route.Clear(); waypoint=0; repathRemaining=0f; }
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(-30f,verticalVelocity-22f*dt);
            Vector3 previous = transform.position;
            CollisionFlags flags = controller.Move((velocity+Vector3.up*verticalVelocity)*dt);
            if ((flags & CollisionFlags.Above)!=0 && verticalVelocity>0f) verticalVelocity=0f;
            float travelled = Flat(transform.position-previous).magnitude;
            stuckSeconds = desired.sqrMagnitude>.1f && travelled<.005f ? stuckSeconds+dt : 0f;
            if(stuckSeconds>Safe(definition.stuckRecoverySeconds,.4f,.2f,2f))
            {
                BeginUnstuck(desired);
                route.Clear(); waypoint=0; repathRemaining=0f; stuckSeconds=0f;
            }
            ApplyPresentation(dt);
        }

        private Vector3 Navigate(Vector3 destination, float dt)
        {
            if(navigation == null || !navigation.isActiveAndEnabled) return Vector3.zero;
            repathRemaining-=dt;
            Vector3 direct=Flat(destination-transform.position);
            if(direct.sqrMagnitude<.018f) return Vector3.zero;
            // Open ground needs no grid search, and avoids chasing stale path
            // corners while the target moves across the courtyard.
            if(navigation.SegmentClear(transform.position,destination))
            {
                route.Clear(); waypoint=0;
                return SteerCrowd(direct.normalized)*Mathf.Min(1f,direct.magnitude/.55f);
            }
            bool changed = Flat(destination-lastDestination).sqrMagnitude>.64f && repathRemaining<=.1f;
            bool blocked = waypoint<route.Count && !navigation.SegmentClear(transform.position,route[waypoint]);
            if(blocked) { route.Clear(); waypoint=0; }
            if(repathRemaining<=0f || changed || pathVersion!=navigation.Version || blocked)
            {
                IntercomNavResult result=navigation.TryFindPath(transform.position,destination,route);
                if(result!=IntercomNavResult.Deferred)
                {
                    waypoint=0; lastDestination=destination; pathVersion=navigation.Version;
                    repathRemaining=Safe(definition.repathSeconds,.32f,.15f,2f)+(variant%4)*.025f;
                    if(result==IntercomNavResult.Unreachable) return Vector3.zero;
                }
                else repathRemaining=0f; // Retry next frame even if an old route was invalidated mid-cooldown.
            }
            while(waypoint<route.Count && Flat(route[waypoint]-transform.position).sqrMagnitude<.035f) waypoint++;
            if(waypoint>=route.Count || !navigation.SegmentClear(transform.position,route[waypoint])) return Vector3.zero;
            Vector3 direction=Flat(route[waypoint]-transform.position).normalized;
            return SteerCrowd(direction);
        }

        private Vector3 SteerCrowd(Vector3 direction)
        {
            Vector3 separation=Vector3.zero;
            for(int i=0;i<active.Count;i++)
            {
                IntercomNpcController other=active[i];
                if(other==null || other==this || other.health==null || other.health.IsDead) continue;
                Vector3 offset=Flat(transform.position-other.transform.position);
                float squared=offset.sqrMagnitude;
                if(squared>.0001f && squared<1f) separation += offset.normalized*(1f-Mathf.Sqrt(squared));
                else if(squared<=.0001f)
                {
                    // Exactly overlapping actors must not share a zero escape
                    // vector. Stable identity breaks the symmetry without noise.
                    float angle=variant*2.399963f;
                    separation+=new Vector3(Mathf.Sin(angle),0f,Mathf.Cos(angle));
                }
            }
            Vector3 steered=Vector3.ClampMagnitude(direction+separation*Safe(definition.separationStrength,.9f,.1f,2f),1f);
            // Local crowd avoidance may not cut across an inflated grid corner.
            if(!navigation.SegmentClear(transform.position,transform.position+steered*.3f)) return direction;
            return steered;
        }

        private Vector3 ApproachDestination(Vector3 destination,bool visible,float distance)
        {
            if(!visible || distance>3.5f || distance<=Range || navigation==null) return destination;
            bool crowded=false;
            for(int i=0;i<active.Count;i++)
            {
                var other=active[i];
                if(other!=null && other!=this && other.health!=null && !other.health.IsDead && other.targetHealth==targetHealth
                    && Flat(other.transform.position-destination).sqrMagnitude<9f)
                { crowded=true; break; }
            }
            if(!crowded) return destination;
            // Persistent ordinal gives each pursuer a different side. Only use
            // a slot on the same clear piece of floor, never behind a wall.
            float angle=variant*2.399963f;
            Vector3 slot=destination+new Vector3(Mathf.Sin(angle),0f,Mathf.Cos(angle))*Mathf.Max(.55f,Range*.8f);
            return navigation.SegmentClear(destination,slot) && navigation.SegmentClear(transform.position,slot) ? slot : destination;
        }

        private void BeginUnstuck(Vector3 desired)
        {
            if(navigation==null || desired.sqrMagnitude<.001f) return;
            Vector3 side=new Vector3(desired.z,0f,-desired.x).normalized*(variant%2==0 ? 1f : -1f);
            if(!navigation.SegmentClear(transform.position,transform.position+side*.6f)) side=-side;
            if(!navigation.SegmentClear(transform.position,transform.position+side*.6f)) return;
            recoveryDirection=side;
            recoveryRemaining=.35f+(variant%3)*.06f;
        }

        private void PickPatrolGoal()
        {
            float radius=Safe(definition.patrolRadius,1.5f,0f,4f);
            float angle=(variant*1.7f+patrolLeg++*2.4f);
            Vector3 wanted=home+new Vector3(Mathf.Sin(angle),0f,Mathf.Cos(angle))*radius;
            patrolGoal=navigation!=null && navigation.TryProject(wanted,out Vector3 projected) ? projected : home;
            route.Clear(); waypoint=0; repathRemaining=0f;
        }

        private bool ClearMelee(Vector3 destination)
        {
            // Actual collision, not inflated navigation cells, decides melee.
            // A low ray catches waist-high cover; an upper ray catches walls.
            // A player standing legally beside a wall remains hittable.
            return ClearSight(destination,.28f) && ClearSight(destination,.82f);
        }

        private bool ClearSight(Vector3 destination, float height=.72f)
        {
            Vector3 origin=transform.position+Vector3.up*height;
            Vector3 delta=destination+Vector3.up*height-origin;
            float length=delta.magnitude;
            if(length<.001f) return true;
            int count=Physics.RaycastNonAlloc(origin,delta/length,sightHits,length,~0,QueryTriggerInteraction.Ignore);
            if(count>=sightHits.Length) return false;
            for(int i=0;i<count;i++)
            {
                Collider hit=sightHits[i].collider;
                if(hit==null || hit.isTrigger || hit is CharacterController) continue;
                if(hit.transform.IsChildOf(transform) || (targetHealth!=null && hit.transform.IsChildOf(targetHealth.transform))) continue;
                return false;
            }
            return true;
        }

        private void AttemptStrike()
        {
            if(!TargetAlive || health.IsDead || !health.isActiveAndEnabled) return;
            Vector3 delta=targetHealth.transform.position-transform.position;
            bool clear=Mathf.Abs(delta.y)<.65f && ClearMelee(targetHealth.transform.position);
            if(!IntercomNpcCombatMath.InStrike(delta.x,delta.z,lockedHeading.x,lockedHeading.z,Range,
                Safe(definition.attackHalfAngle,40f,5f,85f),clear,TargetAlive)) return;
            targetHealth.TakeDamage(new IntercomDamage(Safe(definition.damage,12f,0f,10000f),
                targetHealth.transform.position+Vector3.up*.7f,lockedHeading,gameObject));
        }

        private void OnDamaged(IntercomDamage hit)
        {
            hitRemaining=.18f;
            // Damage triggers an alert using remembered information. It does
            // not reveal an unseen victim's current coordinates.
            if(TargetAlive && ClearSight(targetHealth.transform.position)) lastSeen=targetHealth.transform.position;
            if(health!=null && !health.IsDead) brain.Alert();
        }
        private void OnDied()
        {
            brain.Kill(); velocity=Vector3.zero; route.Clear();
            if(controller!=null) controller.enabled=false;
            if(telegraph!=null) telegraph.gameObject.SetActive(false);
        }
        private void Suspend()
        {
            if(!suspended) brain.Interrupt();
            suspended=true; velocity=Vector3.zero; route.Clear(); waypoint=0; repathRemaining=recoveryRemaining=0f;
            if(telegraph!=null) telegraph.gameObject.SetActive(false);
        }
        private void OnApplicationFocus(bool focus) { hasFocus=focus; if(!focus) Suspend(); }
        private void OnApplicationPause(bool pause) { paused=pause; if(pause) Suspend(); }

        private void ApplyPresentation(float dt)
        {
            if(visual!=null)
            {
                float dead=health.IsDead ? Mathf.Clamp01(deathSeconds/.45f) : 0f;
                float windup=brain.WindupProgress;
                float punch=strikeRemaining/.16f;
                float hit=Mathf.Sin(Mathf.Clamp01(hitRemaining/.18f)*Mathf.PI);
                visual.localScale=new Vector3(1f+dead*.2f+hit*.06f,1f-dead*.85f-hit*.06f,1f+dead*.2f+hit*.06f);
                visual.localRotation=Quaternion.Euler(-windup*13f+punch*21f,0f,dead*8f);
                visual.localPosition=Vector3.up*(dead*.055f);
            }
            if(bodyRenderer!=null)
            {
                Color tint=health.IsDead ? Color.Lerp(baseColor,new Color(.24f,.27f,.26f,1f),.75f) : baseColor;
                if(brain.Phase==IntercomNpcPhase.Windup) tint=Color.Lerp(tint,new Color(.95f,.75f,.32f,1f),.5f+.3f*brain.WindupProgress);
                if(hitRemaining>0f) tint=Color.Lerp(tint,new Color(1f,.92f,.7f,1f),hitRemaining/.18f);
                bodyRenderer.GetPropertyBlock(bodyProperties); bodyProperties.SetColor(BaseColorId,tint); bodyProperties.SetColor(ColorId,tint);
                bodyRenderer.SetPropertyBlock(bodyProperties);
            }
            if(telegraph!=null)
            {
                bool show=!health.IsDead && brain.Phase==IntercomNpcPhase.Windup && !suspended;
                if(telegraph.gameObject.activeSelf!=show) telegraph.gameObject.SetActive(show);
                telegraph.localScale=Vector3.one*(.4f+.6f*brain.WindupProgress);
            }
        }

        private void OnGUI()
        {
            if(!initialized || health.IsDead || Event.current==null || Event.current.type!=EventType.Repaint) return;
            if(health.Current>=health.Maximum && (brain.Phase==IntercomNpcPhase.Idle || brain.Phase==IntercomNpcPhase.Patrol)) return;
            if(displayCamera==null) displayCamera=Camera.main;
            if(displayCamera==null) return;
            Vector3 point=displayCamera.WorldToScreenPoint(transform.position+Vector3.up*1.55f);
            if(point.z<=0f || point.x<0f || point.x>Screen.width || point.y<0f || point.y>Screen.height) return;
            float width=Mathf.Clamp(Screen.height/720f*54f,38f,88f), height=Mathf.Max(4f,width*.105f);
            Rect rect=new Rect(point.x-width*.5f,Screen.height-point.y,width,height);
            Color prior=GUI.color; int depth=GUI.depth; GUI.depth=-35;
            GUI.color=new Color(.11f,.16f,.17f,.85f); GUI.DrawTexture(new Rect(rect.x-2f,rect.y-2f,width+4f,height+4f),Texture2D.whiteTexture);
            GUI.color=brain.Phase==IntercomNpcPhase.Windup ? new Color(.92f,.69f,.29f,1f) : new Color(.71f,.36f,.25f,1f);
            GUI.DrawTexture(new Rect(rect.x,rect.y,width*Mathf.Clamp01(health.Current/health.Maximum),height),Texture2D.whiteTexture);
            GUI.color=prior; GUI.depth=depth;
        }
    }
}
