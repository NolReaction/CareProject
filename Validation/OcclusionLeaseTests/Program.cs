using System;
using System.Collections;
using System.Reflection;
using IntercomMvp;
using UnityEngine;
static class Program {
 static int assertions,cases;
 static Shader shader=new Shader(); static Material template=new Material(shader);
 static void Check(bool v,string m){assertions++;if(!v)throw new Exception(m);}
 static void Near(float a,float b,string m)=>Check(Math.Abs(a-b)<.00001,m);
 static void Reset()=>typeof(IntercomOcclusionFade).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
 static int Leases=>((IDictionary)typeof(IntercomOcclusionFade).GetField("Leases",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Count;
 static Renderer R()=>new Renderer{sharedMaterials=new[]{new Material(shader)}};
 static void Case(string n,Action f){Reset();f();cases++;Console.WriteLine("PASS "+n);}
 static void Main(){
 Case("material fade preserves source and never toggles visibility",()=>{var r=R();var original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.25f,template);Check(r.sharedMaterials[0]!=original,"clone assigned");Near(r.sharedMaterials[0].GetColor("_BaseColor").a,.25f,"alpha");Near(original.GetColor("_BaseColor").a,1,"source alpha");Check(!r.forceRenderingOff,"visibility untouched");var clone=r.sharedMaterials[0];IntercomOcclusionFade.Release(r,1);Check(r.sharedMaterials[0]==original,"restored");Check(clone.destroyed,"clone disposed");Check(Leases==0,"released");});
 Case("most transparent camera wins until last lease ends",()=>{var r=R();var original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.5f,template);var clone=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,2,.25f,template);Near(clone.GetColor("_BaseColor").a,.25f,"minimum");IntercomOcclusionFade.Release(r,2);Near(clone.GetColor("_BaseColor").a,.5f,"remaining owner");IntercomOcclusionFade.Release(r,1);Check(r.sharedMaterials[0]==original,"last restores");});
 Case("updates reuse same clone rather than allocate each frame",()=>{var r=R();IntercomOcclusionFade.Set(r,1,.9f,template);var clone=r.sharedMaterials[0];for(int i=0;i<100;i++)IntercomOcclusionFade.Set(r,1,.4f,template);Check(r.sharedMaterials[0]==clone,"same clone");Check(Leases==1,"one entry");});
 Case("external hidden flags and preexisting alpha survive",()=>{var r=R();r.forceRenderingOff=true;r.sharedMaterials[0].SetColor("_BaseColor",new Color(1,1,1,.5f));IntercomOcclusionFade.Set(r,1,.25f,template);Near(r.sharedMaterials[0].GetColor("_BaseColor").a,.125f,"multiplicative");IntercomOcclusionFade.Release(r,1);Check(r.forceRenderingOff,"prior hidden");Near(r.sharedMaterials[0].GetColor("_BaseColor").a,.5f,"prior alpha");});
 Case("unsupported shaders and MPB owners remain solid",()=>{var r=R();r.sharedMaterials=new[]{new Material(new Shader())};var original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.2f,template);Check(r.sharedMaterials[0]==original,"unknown solid");IntercomOcclusionFade.Release(r,1);r=R();r.hasBlock=true;original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.2f,template);Check(r.sharedMaterials[0]==original,"property block solid");Check(Leases==0,"no MPB lease");});
 Case("external slot replacement is preserved during restore",()=>{var r=R();IntercomOcclusionFade.Set(r,1,.2f,template);var replacement=new Material(shader);r.sharedMaterials=new[]{replacement};IntercomOcclusionFade.Release(r,1);Check(r.sharedMaterials[0]==replacement,"external change survives");});
 Case("cutout cannot suddenly disappear below its cutoff",()=>{var r=R();r.sharedMaterials[0].keywords.Add("_ALPHATEST_ON");IntercomOcclusionFade.Set(r,1,.2f,template);Check(!r.sharedMaterials[0].keywords.Contains("_ALPHATEST_ON"),"alpha test removed");Check(r.sharedMaterials[0].keywords.Contains("_SURFACE_TYPE_TRANSPARENT"),"transparent variant");});
 Case("subsystem reset disposes and restores",()=>{var r=R();var original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.2f,template);var clone=r.sharedMaterials[0];Reset();Check(r.sharedMaterials[0]==original,"restore");Check(clone.destroyed,"destroy");Check(Leases==0,"clear");});
 Case("destroyed renderer removes lease and disposes clone",()=>{var r=R();IntercomOcclusionFade.Set(r,1,.2f,template);var clone=r.sharedMaterials[0];r.destroyed=true;IntercomOcclusionFade.Release(r,1);Check(clone.destroyed,"dispose orphan");Check(Leases==0,"remove orphan");});
 Case("missing renderer or template has no effect",()=>{IntercomOcclusionFade.Set(null,1,.2f,template);var r=R();var original=r.sharedMaterials[0];IntercomOcclusionFade.Set(r,1,.2f,null);IntercomOcclusionFade.Release(null,1);Check(r.sharedMaterials[0]==original,"unchanged");Check(Leases==0,"empty");});
 Case("fade changes alpha only, preserving authored colour and texture ownership",()=>{var r=R();var original=r.sharedMaterials[0];original.SetColor("_BaseColor",new Color(.13f,.37f,.21f,.7f));IntercomOcclusionFade.Set(r,1,.3f,template);var color=r.sharedMaterials[0].GetColor("_BaseColor");Near(color.r,.13f,"red preserved");Near(color.g,.37f,"green preserved");Near(color.b,.21f,"blue preserved");Near(color.a,.21f,"alpha multiplies");IntercomOcclusionFade.Release(r,1);Check(r.sharedMaterials[0]==original,"exact original restored");});
 Case("250 repeated entry and exit cycles never retain old materials or leases",()=>{var r=R();var original=r.sharedMaterials[0];for(int i=0;i<250;i++){IntercomOcclusionFade.Set(r,1,.25f,template);var clone=r.sharedMaterials[0];Check(Leases==1,"only active lease");IntercomOcclusionFade.Release(r,1);Check(clone.destroyed,"transient clone disposed");Check(r.sharedMaterials[0]==original,"original survives cycle");Check(Leases==0,"no accumulating leases");}});
 Reset();Console.WriteLine($"PASS {cases} cases / {assertions} assertions; material leases against Unity fakes, not a Unity renderer test.");
 }
}
