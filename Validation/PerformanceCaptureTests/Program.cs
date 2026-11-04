using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using IntercomMvp;
using UnityEngine;
using Unity.Profiling;
static class Program {
 static int checks;
 static void Check(bool c,string s){checks++;if(!c)throw new Exception(s);}
 static void Call(object c,string n,params object[] p)=>c.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,p);
 static void Main(){
  var ring=new IntercomFrameCapture(3);
  for(int i=0;i<10;i++)ring.Add(new IntercomFrameCapture.Sample{frame=i,wallMilliseconds=16});
  Check(ring.Count==3&&ring.GetOldest(0).frame==7&&ring.GetOldest(2).frame==9,"bounded chronology after wrap");
  ring.Add(new IntercomFrameCapture.Sample{wallMilliseconds=double.NaN});
  ring.Add(new IntercomFrameCapture.Sample{wallMilliseconds=double.PositiveInfinity});
  ring.Add(new IntercomFrameCapture.Sample{wallMilliseconds=-1});
  Check(ring.Count==3&&ring.GetOldest(2).frame==9,"invalid timing rejected");
  bool rejected=false;try{ring.GetOldest(3);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"index bounds");
  ring.Clear();Check(ring.Count==0,"clear history");
  Application.persistentDataPath=Path.Combine(Path.GetTempPath(),"CareProjectCaptureTest-"+Guid.NewGuid());
  var c=new IntercomPerformanceCapture{mission=new IntercomMissionDirector(),navigation=new IntercomNpcNavigation(),effects=new IntercomCombatEffects(),saveWhenStopped=false};
  Call(c,"OnEnable");
  var capture=(IntercomFrameCapture)typeof(IntercomPerformanceCapture).GetField("capture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
  var tick=(Action)Delegate.CreateDelegate(typeof(Action),c,typeof(IntercomPerformanceCapture).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
  Time.frameCount=100;c.mission.SpawnedEnemies=2;c.mission.AliveEnemies=2;c.effects.ActiveEffectCount=5;tick();
  Check(capture.Count==0,"first frame cannot fabricate prior timing");
  Time.frameCount=101;c.mission.SpawnedEnemies=3;ProfilerRecorder.Cpu=42000000;ProfilerRecorder.Gc=128;Time.unscaledDeltaTime=.042f;tick();
  var sample=capture.GetOldest(0);
  Check(sample.frame==100&&sample.spawned==2&&sample.effects==5,"completed metric correlates with previous game frame");
  Check(sample.mainThreadNanoseconds==42000000&&sample.gcAllocatedBytes==128&&Math.Abs(sample.wallMilliseconds-42)<.001,"record counters with exact units");
  ProfilerRecorder.Supported=false;Time.frameCount++;tick();sample=capture.GetOldest(1);
  Check(sample.mainThreadNanoseconds==-1&&sample.gcAllocatedBytes==-1,"unsupported counters marked unavailable, not zero");
  ProfilerRecorder.Supported=true;
  int count=capture.Count;Call(c,"OnApplicationFocus",false);tick();Call(c,"OnApplicationFocus",true);tick();
  Check(capture.Count==count,"focus resume skips long gap");
  Time.timeScale=0;tick();Time.timeScale=1;tick();Check(capture.Count==count,"pause gap skipped");
  Call(c,"OnApplicationPause",true);tick();Call(c,"OnApplicationPause",false);tick();Check(capture.Count==count,"application pause gap skipped");
  for(int i=0;i<20;i++){Time.frameCount++;tick();}
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++){Time.frameCount++;tick();}
  long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(allocated==0,"recording loop must not allocate managed memory");
  Check(capture.Count==3600,"history bounded to last minute at60fps");
  CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ru-RU");
  c.ExportRecentFrames();
  var lines=File.ReadAllLines(IntercomPerformanceCapture.CapturePath);
  Check(lines.Length==3601,"CSV complete header+bounded history");
  Check(lines[1].Split(',').Length==14,"CSV numeric culture cannot split milliseconds");
  Check(int.Parse(lines[1].Split(',')[0])==capture.GetOldest(0).frame,"export chronology");
  Call(c,"OnDisable");Check(ProfilerRecorder.disposed==2,"native recorders disposed");
  File.Delete(IntercomPerformanceCapture.CapturePath);Directory.Delete(Application.persistentDataPath);
  Console.WriteLine($"Performance capture: {checks} assertions PASS; production history+development component with narrow Unity/recorder stand-ins. 10,000 recording frames allocated {allocated} managed bytes. No Unity profiler execution.");
 }
}
