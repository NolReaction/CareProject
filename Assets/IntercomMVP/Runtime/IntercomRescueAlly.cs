using System.Collections.Generic;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>A vulnerable ally: proximity recruitment, grounded following and no teleport catch-up.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class IntercomRescueAlly : MonoBehaviour
    {
        public IntercomNpcNavigation navigation;
        public IntercomHealth playerHealth;
        public IntercomMissionDefinition definition;
        public Transform visual;
        public IntercomHealth Health { get; private set; }
        public bool Recruited { get; private set; }
        public bool WaitingForPlayer { get; private set; }
        public float RecruitmentProgress => Mathf.Clamp01(recruitElapsed / Safe(definition != null ? definition.recruitSeconds : .6f,.6f,.1f,5f));
        public bool Suspended { get; set; }
        private CharacterController controller;
        private readonly List<Vector3> path = new List<Vector3>(128);
        private readonly RaycastHit[] sight = new RaycastHit[24];
        private int waypoint;
        private uint pathVersion;
        private float recruitElapsed, repath, vertical = -2f, deathElapsed;
        private Vector3 velocity, lastDestination;
        private bool focused = true, paused;
        private Camera displayCamera;
        private static Vector3 Flat(Vector3 value) => new Vector3(value.x,0f,value.z);
        private static float Safe(float value,float fallback,float min,float max) => IntercomNpcDefinition.Safe(value,fallback,min,max);

        public static IntercomRescueAlly Create(IntercomNpcNavigation navigation, IntercomHealth player,
            IntercomNpcDefinition appearance, IntercomMissionDefinition definition, Vector3 position)
        {
            if(navigation == null || player == null || appearance == null || appearance.bodyMaterial == null || appearance.detailMaterial == null)
                throw new System.InvalidOperationException("Rescue ally needs explicit player, navigation and shared materials.");
            GameObject root = new GameObject("Rescue ally capsule"); root.SetActive(false); root.transform.position=position;
            CharacterController capsule=root.AddComponent<CharacterController>();
            capsule.height=1.22f; capsule.radius=.29f; capsule.center=Vector3.up*.61f;
            capsule.skinWidth=.025f; capsule.stepOffset=.18f; capsule.minMoveDistance=0f;
            IntercomHealth hp=root.AddComponent<IntercomHealth>(); hp.team=IntercomTeam.Player;
            hp.maximumHealth=Safe(definition != null ? definition.allyHealth : 60f,60f,1f,10000f); hp.postHitInvulnerability=.4f;
            var surface=root.AddComponent<IntercomFootstepSurface>(); surface.kind=IntercomSurfaceKind.Metal; surface.impactVolume=.65f;
            GameObject look=new GameObject("Ally visual"); look.transform.SetParent(root.transform,false);
            GameObject body=Primitive("Friendly capsule",PrimitiveType.Capsule,look.transform,new Vector3(0,.61f,0),
                new Vector3(.58f,.61f,.58f),appearance.bodyMaterial);
            Tint(body.GetComponent<Renderer>(),new Color(.34f,.65f,.59f,1f));
            Primitive("Face",PrimitiveType.Cube,look.transform,new Vector3(0,.84f,.27f),new Vector3(.34f,.16f,.04f),appearance.detailMaterial);
            GameObject badge=Primitive("Rescue badge",PrimitiveType.Cube,look.transform,new Vector3(0,.49f,.28f),new Vector3(.18f,.21f,.035f),appearance.bodyMaterial);
            Tint(badge.GetComponent<Renderer>(),new Color(.94f,.88f,.65f,1f));
            var ally=root.AddComponent<IntercomRescueAlly>(); ally.navigation=navigation; ally.playerHealth=player;
            ally.definition=definition; ally.visual=look.transform;
            root.SetActive(true); return ally;
        }
        private static GameObject Primitive(string name,PrimitiveType shape,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            GameObject item=GameObject.CreatePrimitive(shape); item.name=name; item.transform.SetParent(parent,false);
            item.transform.localPosition=position; item.transform.localScale=scale;
            Collider collider=item.GetComponent<Collider>(); if(collider != null) { collider.enabled=false; Destroy(collider); }
            item.GetComponent<Renderer>().sharedMaterial=material; return item;
        }
        private static void Tint(Renderer renderer,Color color)
        {
            var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            block.SetColor(Shader.PropertyToID("_BaseColor"),color); block.SetColor(Shader.PropertyToID("_Color"),color); renderer.SetPropertyBlock(block);
        }
        private void Awake()
        {
            controller=GetComponent<CharacterController>(); Health=GetComponent<IntercomHealth>(); displayCamera=Camera.main;
            if(Health != null) Health.Died += OnDied;
        }
        private void OnDestroy() { if(Health != null) Health.Died -= OnDied; }
        private void OnDied() { if(controller != null) controller.enabled=false; velocity=Vector3.zero; path.Clear(); }
        public void ResetForMission(IntercomMissionDefinition profile,Vector3 position)
        {
            definition=profile;
            // An inactive pooled ally may not have received the app's resume
            // event. Only an explicit mission restart refreshes this state.
            focused=Application.isFocused; paused=false;
            if(controller == null) controller=GetComponent<CharacterController>();
            if(Health == null) Health=GetComponent<IntercomHealth>();
            controller.enabled=false; transform.SetPositionAndRotation(position,Quaternion.identity); controller.enabled=true;
            Health.maximumHealth=Safe(profile.allyHealth,60f,1f,10000f); Health.ResetHealth();
            Recruited=WaitingForPlayer=Suspended=false; recruitElapsed=repath=deathElapsed=0f;
            vertical=-2f; velocity=Vector3.zero; waypoint=0; path.Clear(); pathVersion=uint.MaxValue;
            if(visual != null) visual.localScale=Vector3.one;
        }
        private void Update()
        {
            if(controller == null || Health == null || definition == null || navigation == null) return;
            if(Suspended || !focused || paused || Time.timeScale<=0f) { velocity=Vector3.zero; return; }
            float dt=Time.deltaTime;
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0f) return;
            dt=Mathf.Min(dt,.1f);
            if(Health.IsDead)
            {
                controller.enabled=false; deathElapsed+=dt;
                if(visual != null) visual.localScale=new Vector3(1.15f,1f-Mathf.Clamp01(deathElapsed/.4f)*.85f,1.15f);
                return;
            }
            if(playerHealth == null || playerHealth.IsDead || !playerHealth.isActiveAndEnabled) { velocity=Vector3.zero; return; }
            Vector3 target=playerHealth.transform.position;
            float distance=Flat(target-transform.position).magnitude;
            if(!Recruited)
            {
                bool near=distance<=Safe(definition.recruitDistance,1.6f,.6f,3f) && ClearSight(target);
                recruitElapsed=near ? recruitElapsed+dt : 0f;
                if(recruitElapsed>=Safe(definition.recruitSeconds,.6f,.1f,5f)) Recruited=true;
            }
            WaitingForPlayer=Recruited && distance>Safe(definition.allyWaitDistance,8f,3f,20f);
            Vector3 desired=Vector3.zero;
            if(Recruited && !WaitingForPlayer && distance>1.05f)
                desired=Navigate(target,dt)*Safe(definition.allyMoveSpeed,3.4f,.2f,6f);
            velocity=Vector3.MoveTowards(velocity,desired,12f*dt);
            if(!navigation.SegmentClear(transform.position,transform.position+velocity*dt)) velocity=Vector3.zero;
            if(velocity.sqrMagnitude>.01f)
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(velocity,Vector3.up),540f*dt);
            if(controller.isGrounded && vertical<0f) vertical=-2f;
            vertical=Mathf.Max(-25f,vertical-22f*dt);
            controller.Move((velocity+Vector3.up*vertical)*dt);
        }
        private Vector3 Navigate(Vector3 destination,float dt)
        {
            if(navigation.SegmentClear(transform.position,destination))
            { path.Clear(); waypoint=0; return Flat(destination-transform.position).normalized; }
            repath-=dt;
            bool blocked=waypoint<path.Count && !navigation.SegmentClear(transform.position,path[waypoint]);
            if(blocked) { path.Clear(); waypoint=0; repath=0f; }
            if(repath<=0f || pathVersion!=navigation.Version || Flat(destination-lastDestination).sqrMagnitude>.64f)
            {
                IntercomNavResult result=navigation.TryFindPath(transform.position,destination,path);
                if(result!=IntercomNavResult.Deferred)
                {
                    waypoint=0; repath=.35f; pathVersion=navigation.Version; lastDestination=destination;
                    if(result==IntercomNavResult.Unreachable) return Vector3.zero;
                }
            }
            while(waypoint<path.Count && Flat(path[waypoint]-transform.position).sqrMagnitude<.04f) waypoint++;
            if(waypoint>=path.Count || !navigation.SegmentClear(transform.position,path[waypoint])) return Vector3.zero;
            return Flat(path[waypoint]-transform.position).normalized;
        }
        private bool ClearSight(Vector3 destination)
        {
            Vector3 origin=transform.position+Vector3.up*.65f;
            Vector3 delta=destination+Vector3.up*.65f-origin; float distance=delta.magnitude;
            if(distance<.001f) return true;
            int count=Physics.RaycastNonAlloc(origin,delta/distance,sight,distance,~0,QueryTriggerInteraction.Ignore);
            if(count>=sight.Length) return false;
            for(int i=0;i<count;i++)
            {
                Collider hit=sight[i].collider;
                if(hit==null || hit is CharacterController || hit.isTrigger || hit.transform.IsChildOf(transform)
                    || hit.transform.IsChildOf(playerHealth.transform)) continue;
                return false;
            }
            return true;
        }
        private void OnApplicationFocus(bool value) { focused=value; if(!value) { velocity=Vector3.zero; recruitElapsed=0f; } }
        private void OnApplicationPause(bool value) { paused=value; if(value) { velocity=Vector3.zero; recruitElapsed=0f; } }
        private void OnDisable() { velocity=Vector3.zero; recruitElapsed=0f; path.Clear(); waypoint=0; repath=0f; }
        private void OnGUI()
        {
            if(Health==null || Health.IsDead || Event.current==null || Event.current.type!=EventType.Repaint) return;
            if(displayCamera==null) displayCamera=Camera.main;
            if(displayCamera==null) return;
            Vector3 p=displayCamera.WorldToScreenPoint(transform.position+Vector3.up*1.5f);
            if(p.z<=0f || p.x<0 || p.x>Screen.width || p.y<0 || p.y>Screen.height) return;
            float w=Mathf.Clamp(Screen.height/720f*64f,46f,92f), h=7f;
            Color prior=GUI.color; int depth=GUI.depth; GUI.depth=-36;
            GUI.color=new Color(.1f,.18f,.18f,.9f); GUI.DrawTexture(new Rect(p.x-w*.5f-2f,Screen.height-p.y-2f,w+4f,h+4f),Texture2D.whiteTexture);
            GUI.color=new Color(.39f,.77f,.65f,1f); GUI.DrawTexture(new Rect(p.x-w*.5f,Screen.height-p.y,w*Mathf.Clamp01(Health.Current/Health.Maximum),h),Texture2D.whiteTexture);
            GUI.color=prior; GUI.depth=depth;
        }
    }
}
