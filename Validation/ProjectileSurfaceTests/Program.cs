using System;
using IntercomMvp;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool value,string why) { checks++; if(!value)throw new Exception(why); }
    static void Main()
    {
        var point=new Vector3(3,4,5); var normal=new Vector3(0,1,0);
        Check(IntercomProjectileImpact.Resolve(null,point,normal,false).Surface==IntercomSurfaceKind.Concrete,"untagged scenery falls back to concrete");
        Check(IntercomProjectileImpact.Resolve(null,point,normal,true).Surface==IntercomSurfaceKind.Concrete&&!IntercomProjectileImpact.Resolve(null,point,normal,true).RobotHit,"accepted damage does not infer robot identity");
        var parent=new GameObject(); var parentTag=parent.AddComponent<IntercomFootstepSurface>(); parentTag.kind=IntercomSurfaceKind.Metal;
        var child=new GameObject(); child.transform.parent=parent.transform; var collider=child.AddComponent<Collider>();
        var hit=IntercomProjectileImpact.Resolve(collider,point,normal,false);
        Check(hit.Surface==IntercomSurfaceKind.Metal,"compound collider inherits parent material");
        Check(hit.Collider==collider&&hit.Point.x==3&&hit.Point.y==4&&hit.Normal.y==1,"contact identity and location preserved");
        var childTag=child.AddComponent<IntercomFootstepSurface>(); childTag.kind=IntercomSurfaceKind.Wood;
        childTag.volume=.02f;childTag.pitch=.7f;childTag.impactVolume=.6f;childTag.impactPitch=1.1f;
        hit=IntercomProjectileImpact.Resolve(collider,point,normal,true);
        Check(hit.Surface==IntercomSurfaceKind.Wood&&hit.TargetHit,"explicit wood tag overrides target fallback");
        Check(hit.Volume==.6f&&hit.Pitch==1.1f,"impact mix independent of footstep mix");
        childTag.isActiveAndEnabled=false;
        Check(IntercomProjectileImpact.Resolve(collider,point,normal,false).Surface==IntercomSurfaceKind.Metal,"disabled child tag does not mask enabled parent");
        parentTag.isActiveAndEnabled=false;
        Check(IntercomProjectileImpact.Resolve(collider,point,normal,false).Surface==IntercomSurfaceKind.Concrete,"disabled tag chain uses fallback");
        childTag.isActiveAndEnabled=true;
        foreach(var kind in new[]{IntercomSurfaceKind.Asphalt,IntercomSurfaceKind.Concrete,IntercomSurfaceKind.Metal,IntercomSurfaceKind.Gravel,IntercomSurfaceKind.Wood,IntercomSurfaceKind.Soil})
        { childTag.kind=kind;Check(IntercomProjectileImpact.Resolve(collider,point,normal,false).Surface==kind,"all surface kinds route without names"); }
        childTag.kind=(IntercomSurfaceKind)99;childTag.impactVolume=float.NaN;childTag.impactPitch=float.PositiveInfinity;
        hit=IntercomProjectileImpact.Resolve(collider,point,normal,false);
        Check(hit.Surface==IntercomSurfaceKind.Concrete&&hit.Volume==1f&&hit.Pitch==1f,"bad inspector data sanitized");
        childTag.impactVolume=-1;childTag.impactPitch=99;hit=IntercomProjectileImpact.Resolve(collider,point,normal,false);
        Check(hit.Volume==0f&&hit.Pitch==1.4f,"per-object controls bounded");
        childTag.impactKind=IntercomImpactKind.Robot;childTag.kind=IntercomSurfaceKind.Metal;childTag.impactVolume=.85f;childTag.impactPitch=1f;
        hit=IntercomProjectileImpact.Resolve(collider,point,normal,true);
        Check(hit.RobotHit&&hit.TargetHit&&hit.Surface==IntercomSurfaceKind.Metal&&hit.Volume==.85f,"robot category independent of physical metal surface");
        Check(IntercomProjectileImpact.Resolve(collider,point,normal,false).RobotHit,"blocked damage armour contact retains explicit robot category");
        childTag.impactKind=(IntercomImpactKind)99;Check(!IntercomProjectileImpact.Resolve(collider,point,normal,true).RobotHit,"invalid impact category never becomes robot");
        childTag.isActiveAndEnabled=false;parentTag.isActiveAndEnabled=true;parentTag.impactKind=IntercomImpactKind.Robot;
        Check(IntercomProjectileImpact.Resolve(collider,point,normal,true).RobotHit,"future robot child collider inherits enabled parent category");
        childTag.isActiveAndEnabled=true;childTag.impactKind=IntercomImpactKind.Surface;childTag.kind=IntercomSurfaceKind.Wood;
        Check(!IntercomProjectileImpact.Resolve(collider,point,normal,true).RobotHit,"nearest explicit surface child overrides robot parent");
        Console.WriteLine("Projectile material routing: "+checks+" assertions passed. Production resolver/surface lookup with narrow Unity stand-ins; no PhysX execution.");
    }
}
