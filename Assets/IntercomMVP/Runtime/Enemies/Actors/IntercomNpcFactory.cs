using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    public sealed partial class IntercomNpcController
    {
        /// <summary>Creates one pooled actor. The optional authored robot replaces only the visual, never collision or AI.</summary>
        public static IntercomNpcController Create(IntercomNpcDefinition definition, IntercomNpcNavigation navigation,
            IntercomHealth targetHealth, IntercomPlayerMotor resetMotor, Vector3 spawn, int ordinal)
        {
            if(definition==null || definition.bodyMaterial==null || definition.detailMaterial==null || definition.telegraphMaterial==null)
                throw new System.InvalidOperationException("NPC definition needs shared body, detail and telegraph materials.");
            if(navigation==null || targetHealth==null || resetMotor==null)
                throw new System.InvalidOperationException("NPC requires explicit navigation, player health and reset motor.");
            GameObject root=new GameObject("Udarnik "+(ordinal+1));
            root.SetActive(false); root.transform.position=spawn;
            CharacterController capsule=root.AddComponent<CharacterController>();
            capsule.height=1.35f; capsule.radius=.32f; capsule.center=Vector3.up*.675f;
            capsule.skinWidth=.025f; capsule.stepOffset=.18f; capsule.slopeLimit=45f; capsule.minMoveDistance=0f;
            IntercomHealth hp=root.AddComponent<IntercomHealth>();
            hp.maximumHealth=Safe(definition.maximumHealth,75f,1f,100000f);
            hp.team=IntercomTeam.Enemy; hp.postHitInvulnerability=0f;
            IntercomFootstepSurface surface=root.AddComponent<IntercomFootstepSurface>();
            surface.kind=IntercomSurfaceKind.Metal; surface.impactVolume=.75f; surface.impactPitch=.92f;
            surface.impactKind=IntercomImpactKind.Robot; surface.impactVolume=.85f;
            GameObject look=new GameObject("Udarnik visual - collision stays on parent");
            look.transform.SetParent(root.transform,false);
            GameObject model = null;
            Animator animator = null;
            Renderer modelRenderer = null;
            if(definition.visualPrefab!=null)
            {
                // One allocation per pool slot, while the mission prewarms. Reuse never instantiates the prefab again.
                model=Instantiate(definition.visualPrefab,look.transform,false);
                model.name="Udarnik authored model";
                animator=model.GetComponentInChildren<Animator>(true);
                modelRenderer=model.GetComponentInChildren<Renderer>(true);
                if(animator==null || animator.runtimeAnimatorController==null || modelRenderer==null
                    || model.GetComponentInChildren<Collider>(true)!=null)
                {
                    model.SetActive(false); Destroy(model); model=null; animator=null; modelRenderer=null;
                    Debug.LogWarning("Udarnik visual prefab needs an Animator/controller and renderer, with no colliders. Using capsule.", definition);
                }
                else
                {
                    model.transform.localPosition=Vector3.zero;
                    model.transform.localRotation=Quaternion.identity;
                    model.transform.localScale=Vector3.one;
                    model.SetActive(true);
                    animator.applyRootMotion=false;
                    animator.speed=0f;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                }
            }
            GameObject body=null;
            if(model==null)
            {
            body=Primitive("Body",PrimitiveType.Capsule,look.transform,new Vector3(0f,.675f,0f),
                new Vector3(.64f,.675f,.64f),definition.bodyMaterial);
            Primitive("Face",PrimitiveType.Cube,look.transform,new Vector3(0f,.94f,.294f),
                new Vector3(.39f,.19f,.045f),definition.detailMaterial);
            GameObject eyes=Primitive("Eyes",PrimitiveType.Cube,look.transform,new Vector3(-.09f,.96f,.324f),
                new Vector3(.065f,.07f,.018f),definition.bodyMaterial);
            Primitive("Eye",PrimitiveType.Cube,look.transform,new Vector3(.09f,.955f,.324f),
                new Vector3(.065f,.045f,.018f),definition.bodyMaterial);
            Primitive("Belt",PrimitiveType.Cube,look.transform,new Vector3(0f,.44f,.296f),
                new Vector3(.43f,.055f,.05f),definition.detailMaterial);
            }
            GameObject warning=new GameObject("Locked punch warning");
            warning.transform.SetParent(root.transform,false); warning.transform.localPosition=Vector3.up*.035f;
            Mesh wedge=BuildWarningMesh(Safe(definition.attackRange,1.05f,.3f,3f),Safe(definition.attackHalfAngle,40f,5f,85f));
            warning.AddComponent<MeshFilter>().sharedMesh=wedge;
            MeshRenderer warningRenderer=warning.AddComponent<MeshRenderer>();
            warningRenderer.sharedMaterial=definition.telegraphMaterial;
            warningRenderer.shadowCastingMode=ShadowCastingMode.Off; warningRenderer.receiveShadows=false;
            warning.SetActive(false);
            GameObject arrival=new GameObject("Ground arrival warning - reused");
            // The authored yard includes thin concrete slabs up to .08 m.
            // Keep the warning above those floor surfaces as well as asphalt.
            arrival.transform.SetParent(root.transform,false); arrival.transform.localPosition=Vector3.up*.09f;
            Mesh arrivalMesh=BuildEmergenceMesh();
            arrival.AddComponent<MeshFilter>().sharedMesh=arrivalMesh;
            MeshRenderer arrivalRenderer=arrival.AddComponent<MeshRenderer>();
            arrivalRenderer.sharedMaterial=definition.telegraphMaterial;
            arrivalRenderer.shadowCastingMode=ShadowCastingMode.Off; arrivalRenderer.receiveShadows=false;
            arrival.SetActive(false);
            IntercomNpcController npc=root.AddComponent<IntercomNpcController>();
            npc.definition=definition; npc.navigation=navigation; npc.targetHealth=targetHealth; npc.resetMotor=resetMotor;
            npc.visual=look.transform; npc.bodyRenderer=modelRenderer!=null?modelRenderer:body.GetComponent<Renderer>(); npc.telegraph=warning.transform;
            npc.modelAnimator=animator;
            npc.variant=ordinal; npc.ownedTelegraphMesh=wedge;
            npc.emergenceRing=arrival.transform; npc.ownedEmergenceMesh=arrivalMesh;
            npc.emergenceRenderer=arrivalRenderer;
            root.SetActive(true);
            return npc;
        }

        // The adapter lives with the existing pooled factory, rather than another component/script on every robot.
        // Frozen Animator sampling keeps presentation on the exact AI phase clock. Clips contain no damage events
        // and animate only bones below the visual child, so they cannot move the CharacterController.
        private Animator modelAnimator;
        private MaterialPropertyBlock modelOriginalProperties;
        private bool modelTintApplied;
        private int sampledModelState=int.MinValue;
        private float sampledModelTime=-1f, modelLoopSeconds, presentedMoveSpeed;
        private Transform[] modelPoseBones;
        private Vector3[] modelBlendPositions;
        private Quaternion[] modelBlendRotations;
        private Vector3[] modelPublishedPositions, modelPublishedScales;
        private Quaternion[] modelPublishedRotations;
        private bool modelPoseReady;
        private float modelBlendRemaining;
        private bool modelHasGroundEmerge, modelHasHatchEmerge;
        private static readonly int IdleState=Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkState=Animator.StringToHash("Base Layer.Walk");
        private static readonly int WindupState=Animator.StringToHash("Base Layer.Windup");
        private static readonly int StrikeState=Animator.StringToHash("Base Layer.Strike");
        private static readonly int RecoverState=Animator.StringToHash("Base Layer.Recover");
        private static readonly int HurtState=Animator.StringToHash("Base Layer.Hurt");
        private static readonly int DeathState=Animator.StringToHash("Base Layer.Death");
        private static readonly int EmergeState=Animator.StringToHash("Base Layer.Emerge");
        private static readonly int GroundEmergeState=Animator.StringToHash("Base Layer.EmergeGround");
        private static readonly int HatchEmergeState=Animator.StringToHash("Base Layer.EmergeHatch");

        private void ResetModelClock()
        {
            RestoreModelTint();
            sampledModelState=int.MinValue; sampledModelTime=-1f;
            modelLoopSeconds=presentedMoveSpeed=modelBlendRemaining=0f;
            modelPoseReady=false;
            if(modelAnimator!=null && modelPoseBones==null && bodyRenderer is SkinnedMeshRenderer skinned)
            {
                modelPoseBones=skinned.bones ?? System.Array.Empty<Transform>();
                modelBlendPositions=new Vector3[modelPoseBones.Length];
                modelBlendRotations=new Quaternion[modelPoseBones.Length];
                modelPublishedPositions=new Vector3[modelPoseBones.Length];
                modelPublishedRotations=new Quaternion[modelPoseBones.Length];
                modelPublishedScales=new Vector3[modelPoseBones.Length];
            }
        }
        private Color CaptureModelTint()
        {
            modelOriginalProperties=new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(modelOriginalProperties);
            Material material=bodyRenderer.sharedMaterial;
            return modelOriginalProperties.HasColor(BaseColorId)?modelOriginalProperties.GetColor(BaseColorId)
                :modelOriginalProperties.HasColor(ColorId)?modelOriginalProperties.GetColor(ColorId)
                :material!=null&&material.HasProperty(BaseColorId)?material.GetColor(BaseColorId)
                :material!=null&&material.HasProperty(ColorId)?material.GetColor(ColorId):new Color(1f,1f,1f,1f);
        }
        private void RestoreModelTint()
        {
            if(!modelTintApplied || bodyRenderer==null || modelOriginalProperties==null)return;
            bodyRenderer.SetPropertyBlock(modelOriginalProperties.isEmpty?null:modelOriginalProperties);
            modelTintApplied=false;colorApplied=false;
        }
        private void ApplyModelPresentation(float dt)
        {
            if(modelAnimator==null || deathBurstHidden) return;
            // PrepareForSpawn also runs on inactive pool slots. Do not force an uninitialized native Animator there.
            if(!modelAnimator.isActiveAndEnabled || !modelAnimator.isInitialized)
            { sampledModelState=int.MinValue; modelPoseReady=false; return; }
            if(sampledModelState==int.MinValue)
            {
                // An intentionally assigned older/custom visual can still use its legacy arrival state.
                modelHasGroundEmerge=modelAnimator.HasState(0,GroundEmergeState);
                modelHasHatchEmerge=modelAnimator.HasState(0,HatchEmergeState);
            }
            int state; float time;
            if(health.IsDead) { state=DeathState; time=Mathf.Clamp01(deathSeconds/.18f); }
            else if(emerging)
            {
                state=emergenceKind==IntercomEnemySpawnKind.Hatch && modelHasHatchEmerge ? HatchEmergeState
                    :emergenceKind==IntercomEnemySpawnKind.Ground && modelHasGroundEmerge ? GroundEmergeState:EmergeState;
                time=Mathf.Clamp01((EmergenceProgress-arrivalWarningFraction)/(1f-arrivalWarningFraction));
            }
            else if(brain.Phase==IntercomNpcPhase.Windup) { state=WindupState; time=brain.WindupProgress; }
            else if(brain.Phase==IntercomNpcPhase.Recovery)
            {
                float elapsed=brain.RecoveryProgress*Safe(definition.recoverySeconds,.48f,.1f,8f);
                if(strikeRemaining>0f) { state=StrikeState; time=1f-Mathf.Clamp01(strikeRemaining/.16f); }
                else { state=RecoverState; time=Mathf.Clamp01((elapsed-.16f)/Mathf.Max(.001f,Safe(definition.recoverySeconds,.48f,.1f,8f)-.16f)); }
            }
            else if(hitRemaining>0f) { state=HurtState; time=1f-Mathf.Clamp01(hitRemaining/HurtDuration); }
            else
            {
                bool moving=presentedMoveSpeed>.04f && !suspended;
                state=moving?WalkState:IdleState;
                if(state!=sampledModelState) modelLoopSeconds=0f;
                if(dt>0f) modelLoopSeconds+=dt*(moving?presentedMoveSpeed/Safe(definition.walkAnimationMetresPerSecond,.603f,.1f,8f):1f);
                float duration=moving?.8f:1.6f;
                time=(modelLoopSeconds%duration)/duration;
            }
            time=Mathf.Clamp01(time);
            if(state==sampledModelState && time==sampledModelTime) return;
            if(state!=sampledModelState)
            {
                modelBlendRemaining=0f;
                if(sampledModelState!=int.MinValue && (state==IdleState || state==WalkState) && modelPoseBones!=null)
                {
                    modelBlendRemaining=.08f;
                    for(int i=0;i<modelPoseBones.Length;i++)
                    {
                        Transform bone=modelPoseBones[i];if(bone==null)continue;
                        modelBlendPositions[i]=bone.localPosition;modelBlendRotations[i]=bone.localRotation;
                    }
                }
            }
            modelAnimator.applyRootMotion=false; modelAnimator.speed=0f;
            modelAnimator.Play(state,0,time); modelAnimator.Update(0f);
            if(modelBlendRemaining>0f && modelPoseBones!=null)
            {
                modelBlendRemaining=Mathf.Max(0f,modelBlendRemaining-Mathf.Max(0f,dt));
                float blend=1f-modelBlendRemaining/.08f;blend=blend*blend*(3f-2f*blend);
                for(int i=0;i<modelPoseBones.Length;i++)
                {
                    Transform bone=modelPoseBones[i];if(bone==null)continue;
                    bone.localPosition=Vector3.Lerp(modelBlendPositions[i],bone.localPosition,blend);
                    bone.localRotation=Quaternion.Slerp(modelBlendRotations[i],bone.localRotation,blend);
                }
            }
            sampledModelState=state; sampledModelTime=time;
            if(modelPoseBones!=null)
            {
                for(int i=0;i<modelPoseBones.Length;i++)
                {
                    Transform bone=modelPoseBones[i];if(bone==null)continue;
                    modelPublishedPositions[i]=bone.localPosition;modelPublishedRotations[i]=bone.localRotation;
                    modelPublishedScales[i]=bone.localScale;
                }
                modelPoseReady=true;
            }
        }

        private void LateUpdate()
        {
            if(!modelPoseReady || deathBurstHidden || modelAnimator==null || !modelAnimator.isActiveAndEnabled
                || !modelAnimator.isInitialized || modelPoseBones==null)return;
            // Mecanim can evaluate a speed-zero Animator after Update and replace manual bone blends.
            // Publish the cached final pose after that evaluation, including a frozen pose during pause.
            for(int i=0;i<modelPoseBones.Length;i++)
            {
                Transform bone=modelPoseBones[i];if(bone==null)continue;
                bone.localPosition=modelPublishedPositions[i];bone.localRotation=modelPublishedRotations[i];
                bone.localScale=modelPublishedScales[i];
            }
        }

        private static GameObject Primitive(string name, PrimitiveType shape, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject item=GameObject.CreatePrimitive(shape); item.name=name;
            item.transform.SetParent(parent,false); item.transform.localPosition=position; item.transform.localScale=scale;
            Collider collider=item.GetComponent<Collider>();
            if(collider!=null) { collider.enabled=false; Destroy(collider); }
            item.GetComponent<Renderer>().sharedMaterial=material;
            return item;
        }

        private static Mesh BuildWarningMesh(float radius, float halfAngle)
        {
            const int segments=16;
            Vector3[] vertices=new Vector3[segments+2]; int[] triangles=new int[segments*3];
            for(int i=0;i<=segments;i++)
            {
                float radians=(-halfAngle+2f*halfAngle*i/segments)*Mathf.Deg2Rad;
                vertices[i+1]=new Vector3(Mathf.Sin(radians)*radius,0f,Mathf.Cos(radians)*radius);
                if(i<segments) { triangles[i*3]=0; triangles[i*3+1]=i+1; triangles[i*3+2]=i+2; }
            }
            Mesh mesh=new Mesh { name="NPC punch sector", vertices=vertices, triangles=triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh BuildEmergenceMesh()
        {
            // Segmented annulus plus six small lifted chips, built once per pool slot.
            const int segments=24;
            Vector3[] vertices=new Vector3[segments*4+6*3];
            int[] triangles=new int[segments*6+6*3];
            for(int i=0;i<segments;i++)
            {
                float a=i*2f*Mathf.PI/segments,b=(i+.72f)*2f*Mathf.PI/segments;
                int v=i*4,t=i*6;
                vertices[v]=new Vector3(Mathf.Cos(a)*.55f,0f,Mathf.Sin(a)*.55f);
                vertices[v+1]=new Vector3(Mathf.Cos(a)*.64f,0f,Mathf.Sin(a)*.64f);
                vertices[v+2]=new Vector3(Mathf.Cos(b)*.64f,0f,Mathf.Sin(b)*.64f);
                vertices[v+3]=new Vector3(Mathf.Cos(b)*.55f,0f,Mathf.Sin(b)*.55f);
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v;triangles[t+4]=v+3;triangles[t+5]=v+2;
            }
            for(int i=0;i<6;i++)
            {
                float a=(i+.2f)*2f*Mathf.PI/6f;
                Vector3 radial=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
                Vector3 side=new Vector3(-radial.z,0f,radial.x);
                int v=segments*4+i*3,t=segments*6+i*3;
                vertices[v]=radial*.73f-side*.07f;
                vertices[v+1]=radial*.73f+side*.07f;
                vertices[v+2]=radial*.88f+Vector3.up*.2f;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
            }
            Mesh mesh=new Mesh{name="Pooled robot ground arrival",vertices=vertices,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
