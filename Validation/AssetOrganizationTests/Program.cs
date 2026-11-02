using System;
using System.IO;
using System.Text.Json;
using IntercomMvp.Editor;
using UnityEditor;
using UnityEngine;
static class Program {
 const string Old="Assets/IntercomMVP/Audio/Pistol.wav", New="Assets/IntercomMVP/Game/Weapons/Pistols/Audio/Pistol.wav";
 const string Guid="abcdef0123456789abcdef0123456789", Tomb="1234567890abcdef1234567890abcdef";
 static string root;
 static int checks;
 static string P(string p)=>Path.Combine(root,p);
 static void Put(string p,string text){Directory.CreateDirectory(Path.GetDirectoryName(P(p)));File.WriteAllText(P(p),text);}
 static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;}
 static void Fixture(){
  if(root!=null) Directory.Delete(root,true);
  root=Path.Combine(Path.GetTempPath(),"intercom-assets-"+System.Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  Application.dataPath=P("Assets");
  Put(New,"real asset");Put(New+".meta","fileFormatVersion: 2\nguid: "+Guid+"\n");
  Put(Old,"valid placeholder");Put(Old+".meta","fileFormatVersion: 2\nguid: "+Tomb+"\n");
  var entry=new {legacyPath=Old,destination=New,guid=Guid,tombstoneGuid=Tomb,tombstoneSha256=IntercomAssetCatalog.Digest(P(Old)),tombstoneMetaSha256=IntercomAssetCatalog.Digest(P(Old)+".meta")};
  Put(IntercomAssetCatalog.ManifestPath,JsonSerializer.Serialize(new {revision="foundation-v4",files=new[]{entry}}));
 }
 static void Main(){try {
  Fixture();Check(IntercomAssetCatalog.Resolve(Old)==New,"resolve must ignore old placeholder");
  IntercomAssetOrganization.FinishUpgrade();Check(!File.Exists(P(Old))&&File.Exists(P(New)),"first overlay cleanup");
  IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(New)),"idempotent cleanup");
  Put(Old,"valid placeholder");Put(Old+".meta","fileFormatVersion: 2\nguid: "+Tomb+"\n");IntercomAssetOrganization.FinishUpgrade();Check(!File.Exists(P(Old)),"repeat zip overlay cleanup");
  Fixture();Put(Old,"user edit");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(Old)),"modified placeholder must survive");
  Fixture();File.AppendAllText(P(Old)+".meta","userData: importer edit\n");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(Old)),"modified importer must survive");
  Fixture();File.Delete(P(New));File.Delete(P(New)+".meta");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(Old)),"no original available: preserve placeholder");
  Fixture();Put("Assets/User.prefab","reference: {guid: "+Tomb+"}");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(Old)),"referenced placeholder must survive");
  Fixture();File.Delete(P(New));File.Delete(P(New)+".meta");Put(Old+".meta","guid: "+Guid+"\n");IntercomAssetOrganization.FinishUpgrade();Check(!File.Exists(P(Old))&&AssetDatabase.AssetPathToGUID(New)==Guid,"partial migration preserves GUID");
  Fixture();Put(Old+".meta","guid: "+Guid+"\n");Put(New+".meta","guid: other\n");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(Old))&&File.ReadAllText(P(New))=="real asset","destination conflict must survive");
  Fixture();string custom="Assets/IntercomMVP/UserChosen/Pistol.wav";Directory.CreateDirectory(Path.GetDirectoryName(P(custom)));File.Move(P(New),P(custom));File.Move(P(New)+".meta",P(custom)+".meta");Check(IntercomAssetCatalog.Resolve(Old)==custom,"respect user's relocated original");IntercomAssetOrganization.FinishUpgrade();Check(File.Exists(P(custom))&&!File.Exists(P(Old)),"cleanup with user relocation");
  Check(!IntercomAssetCatalog.SafePath("Assets/IntercomMVP/../outside"),"path traversal");
  Console.WriteLine("PASS: "+checks+" production asset migration/resolution checks (Unity API filesystem stubs).");
 }finally {if(root!=null&&Directory.Exists(root))Directory.Delete(root,true);}}
}
