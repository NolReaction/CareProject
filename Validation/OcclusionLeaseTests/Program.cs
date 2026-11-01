using System;
using System.Collections;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
static class Program {
 static int assertions,cases;
 static void Check(bool value,string message) { assertions++; if(!value)throw new Exception(message); }
 static void Case(string name,Action a){ Reset(); a(); cases++; Console.WriteLine("PASS "+name); }
 static void Reset() => typeof(IntercomOcclusionVisibility).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
 static int Leases => ((IDictionary)typeof(IntercomOcclusionVisibility).GetField("Leases",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Count;
 static void Main(){
  Case("one camera releases exactly its own visibility change",()=>{
   var r=new Renderer(); IntercomOcclusionVisibility.Acquire(r);Check(r.forceRenderingOff,"must hide");Check(Leases==1,"must track");
   IntercomOcclusionVisibility.Release(r);Check(!r.forceRenderingOff,"must restore");Check(Leases==0,"must release");
  });
  Case("second camera prevents premature restoration",()=>{
   var r=new Renderer(); IntercomOcclusionVisibility.Acquire(r); IntercomOcclusionVisibility.Acquire(r);
   IntercomOcclusionVisibility.Release(r);Check(r.forceRenderingOff,"camera B still owns it");Check(Leases==1,"one registry entry");
   IntercomOcclusionVisibility.Release(r);Check(!r.forceRenderingOff,"last camera restores it");Check(Leases==0,"released");
  });
  Case("a renderer already hidden remains hidden after every owner releases",()=>{
   var r=new Renderer{forceRenderingOff=true};IntercomOcclusionVisibility.Acquire(r);IntercomOcclusionVisibility.Acquire(r);
   IntercomOcclusionVisibility.Release(r);IntercomOcclusionVisibility.Release(r);Check(r.forceRenderingOff,"original hidden must survive");Check(Leases==0,"released");
  });
  Case("independent objects retain independent original state",()=>{
   var a=new Renderer();var b=new Renderer{forceRenderingOff=true};IntercomOcclusionVisibility.Acquire(a);IntercomOcclusionVisibility.Acquire(b);
   IntercomOcclusionVisibility.Release(a);Check(!a.forceRenderingOff,"A restored");Check(b.forceRenderingOff,"B still hidden");Check(Leases==1,"B remains");
   IntercomOcclusionVisibility.Release(b);Check(b.forceRenderingOff,"B original remains");Check(Leases==0,"released");
  });
  Case("subsystem reset restores live flags without relying on owner callbacks",()=>{
   var a=new Renderer();var b=new Renderer{forceRenderingOff=true};IntercomOcclusionVisibility.Acquire(a);IntercomOcclusionVisibility.Acquire(a);IntercomOcclusionVisibility.Acquire(b);
   Reset();Check(!a.forceRenderingOff,"reset restores false");Check(b.forceRenderingOff,"reset restores true");Check(Leases==0,"reset clears");
   IntercomOcclusionVisibility.Release(a);Check(!a.forceRenderingOff,"late owner callback safe");
  });
  Case("destroyed renderer reference can still release the registry lease",()=>{
   var r=new Renderer();IntercomOcclusionVisibility.Acquire(r);r.destroyed=true;IntercomOcclusionVisibility.Release(r);Check(Leases==0,"destroyed reference removed");
  });
  Case("missing and unregistered renderer operations are harmless",()=>{
   IntercomOcclusionVisibility.Acquire(null);IntercomOcclusionVisibility.Release(null);IntercomOcclusionVisibility.Release(new Renderer());Check(Leases==0,"null operations clear");
  });
  Reset();Console.WriteLine($"PASS {cases} cases / {assertions} assertions. Production visibility lease registry with narrow Unity API doubles; NOT Unity compilation/rendering.");
 }
}
