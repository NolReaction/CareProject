using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    public sealed partial class IntercomNpcController
    {
        /// <summary>Creates one complete temporary capsule actor. Shared authored materials keep shaders in the build.</summary>
        public static IntercomNpcController Create(IntercomNpcDefinition definition, IntercomNpcNavigation navigation,
            IntercomHealth targetHealth, IntercomPlayerMotor resetMotor, Vector3 spawn, int ordinal)
        {
            if(definition==null || definition.bodyMaterial==null || definition.detailMaterial==null || definition.telegraphMaterial==null)
                throw new System.InvalidOperationException("NPC definition needs shared body, detail and telegraph materials.");
            if(navigation==null || targetHealth==null || resetMotor==null)
                throw new System.InvalidOperationException("NPC requires explicit navigation, player health and reset motor.");
            GameObject root=new GameObject("Udarnik capsule "+(ordinal+1));
            root.SetActive(false); root.transform.position=spawn;
            CharacterController capsule=root.AddComponent<CharacterController>();
            capsule.height=1.35f; capsule.radius=.32f; capsule.center=Vector3.up*.675f;
            capsule.skinWidth=.025f; capsule.stepOffset=.18f; capsule.slopeLimit=45f; capsule.minMoveDistance=0f;
            IntercomHealth hp=root.AddComponent<IntercomHealth>();
            hp.maximumHealth=Safe(definition.maximumHealth,75f,1f,100000f);
            hp.team=IntercomTeam.Enemy; hp.postHitInvulnerability=0f;
            IntercomFootstepSurface surface=root.AddComponent<IntercomFootstepSurface>();
            surface.kind=IntercomSurfaceKind.Metal; surface.impactVolume=.75f; surface.impactPitch=.92f;
            GameObject look=new GameObject("Capsule visual - replace with UD-1 model");
            look.transform.SetParent(root.transform,false);
            GameObject body=Primitive("Body",PrimitiveType.Capsule,look.transform,new Vector3(0f,.675f,0f),
                new Vector3(.64f,.675f,.64f),definition.bodyMaterial);
            Primitive("Face",PrimitiveType.Cube,look.transform,new Vector3(0f,.94f,.294f),
                new Vector3(.39f,.19f,.045f),definition.detailMaterial);
            GameObject eyes=Primitive("Eyes",PrimitiveType.Cube,look.transform,new Vector3(-.09f,.96f,.324f),
                new Vector3(.065f,.07f,.018f),definition.bodyMaterial);
            Primitive("Eye",PrimitiveType.Cube,look.transform,new Vector3(.09f,.955f,.324f),
                new Vector3(.065f,.045f,.018f),definition.bodyMaterial);
            Primitive("Belt",PrimitiveType.Cube,look.transform,new Vector3(0f,.44f,.296f),
                new Vector3(.43f,.055f,.05f),definition.detailMaterial);
            GameObject warning=new GameObject("Locked punch warning");
            warning.transform.SetParent(root.transform,false); warning.transform.localPosition=Vector3.up*.035f;
            Mesh wedge=BuildWarningMesh(Safe(definition.attackRange,1.05f,.3f,3f),Safe(definition.attackHalfAngle,40f,5f,85f));
            warning.AddComponent<MeshFilter>().sharedMesh=wedge;
            MeshRenderer warningRenderer=warning.AddComponent<MeshRenderer>();
            warningRenderer.sharedMaterial=definition.telegraphMaterial;
            warningRenderer.shadowCastingMode=ShadowCastingMode.Off; warningRenderer.receiveShadows=false;
            warning.SetActive(false);
            IntercomNpcController npc=root.AddComponent<IntercomNpcController>();
            npc.definition=definition; npc.navigation=navigation; npc.targetHealth=targetHealth; npc.resetMotor=resetMotor;
            npc.visual=look.transform; npc.bodyRenderer=body.GetComponent<Renderer>(); npc.telegraph=warning.transform;
            npc.variant=ordinal; npc.ownedTelegraphMesh=wedge;
            root.SetActive(true);
            return npc;
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
    }
}
