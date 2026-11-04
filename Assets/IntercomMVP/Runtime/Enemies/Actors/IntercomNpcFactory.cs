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
            surface.impactKind=IntercomImpactKind.Robot; surface.impactVolume=.85f;
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
            npc.visual=look.transform; npc.bodyRenderer=body.GetComponent<Renderer>(); npc.telegraph=warning.transform;
            npc.variant=ordinal; npc.ownedTelegraphMesh=wedge;
            npc.emergenceRing=arrival.transform; npc.ownedEmergenceMesh=arrivalMesh;
            npc.emergenceRenderer=arrivalRenderer;
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
