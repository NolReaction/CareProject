using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using IntercomMvp.Editor;

internal static class Program
{
    private static int assertions;
    private const string ScenePath="Assets/IntercomMVP/Scenes/TutorialYard/IntercomMVP.unity";
    private static void Check(bool value,string why){assertions++;if(!value)throw new Exception(why);}
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"CareProjectRepair_"+Guid.NewGuid().ToString("N"));
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings"})Directory.CreateDirectory(Path.Combine(Root,directory));
            Write("Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs","public const string BuildVersion = \"V18\";");
            Write("Assets/IntercomMVP/Runtime/Presentation/UI/IntercomWorkshopHud.cs","HeartRed DrawPause");
            Write("Assets/IntercomMVP/Runtime/Player/Input/IntercomPlayerInput.cs","PassiveHudContains");
            Write(ScenePath,"--- !u!114 &5\nMonoBehaviour:\n  m_EditorClassIdentifier: Assembly-CSharp::IntercomMvp.IntercomFrontEnd\n"+
                "  mission: {fileID: 1}\n  input: {fileID: 2}\n  roomCamera: {fileID: 3}\n  homeDoodle: {fileID: 2800000, guid: a}\n  rescueDoodle: {fileID: 2800000, guid: b}\n"+
                "--- !u!1 &6\nGameObject:\n  m_Name: MVP_MenuHub\n");
        }
        internal void Write(string path,string text){string file=Path.Combine(Root,path);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text);}
        internal IntercomProjectRepairAudit.Report Scan()=>IntercomProjectRepairAudit.Inspect(Root,"V18",ScenePath);
        public void Dispose(){Directory.Delete(Root,true);}
    }
    private static void Main()
    {
        using(var f=new Fixture())
        {
            string[] files=Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories);
            var hashes=files.ToDictionary(x=>x,x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
            var r=f.Scan();Check(r.Errors.Count==0 && r.Warnings.Count==0,"current complete package accepted");
            Check(r.Facts.Any(x=>x.Contains(f.Root)),"actual root visible for user to compare UnityHub projects");
            Check(files.All(x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x)))==hashes[x]),"diagnostics never rewrite source or scene");
            Check(Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Length==files.Length,"diagnostics create no manifests/cache");
        }
        using(var f=new Fixture())
        {
            f.Write("Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs","old frontend without build stamp");
            f.Write("Assets/IntercomMVP/Runtime/Presentation/UI/IntercomWorkshopHud.cs","old full panel HUD");
            var r=f.Scan();Check(r.Errors.Count==2,"old source package is diagnosed even with current scene");
            Check(r.Errors.Any(x=>x.Contains("старый HUD")),"old-HUD cause explicit");
            Check(r.Errors.Any(x=>x.Contains("Переимпорт")),"reimport is not presented as installing missing source");
        }
        using(var f=new Fixture())
        {
            f.Write("Assets/IntercomMVP/Runtime/Old/IntercomPlayerInput.cs","PassiveHudContains");
            var r=f.Scan();Check(r.Errors.Any(x=>x.Contains("IntercomPlayerInput.cs")&&x.Contains("2")),"duplicate imported input is detected before pretending repair works");
        }
        using(var f=new Fixture())
        {
            f.Write("CareProject_Update_V17/Assets/IntercomMVP/Runtime/a.cs","new UI in wrong folder");
            f.Write("SomeUnpack/Deep/CareProject/Assets/IntercomMVP/a.cs","another nested full ZIP");
            var r=f.Scan();Check(r.Warnings.Count(x=>x.Contains("вложенной"))==2,"real nested overlay and full-archive paths detected");
            Check(r.Warnings.Any(x=>x.Contains("CareProject_Update_V17")),"actionable nested-folder name reported");
            Check(r.Errors.Count==0,"nested copied archive is a warning without corrupting valid root source");
        }
        using(var f=new Fixture())
        {
            f.Write("Source/AnExample/Assets/IntercomMVP/a.cs","valid source example");
            f.Write("ProjectBackups/Previous/Assets/IntercomMVP/a.cs","expected recovery backup");
            Check(f.Scan().Warnings.Count==0,"source/backups are not falsely treated as installation mistakes");
        }
        using(var f=new Fixture())
        {
            Directory.CreateDirectory(Path.Combine(f.Root,"OtherUnity/Assets"));Directory.CreateDirectory(Path.Combine(f.Root,"OtherUnity/ProjectSettings"));
            Check(f.Scan().Warnings.Any(x=>x.Contains("OtherUnity")),"nested other Unity project detected without requiring our exact Assets child");
        }
        using(var f=new Fixture())
        {
            const string guid="11111111111111111111111111111111";
            f.Write("Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs.meta","fileFormatVersion: 2\nguid: "+guid+"\n");
            f.Write("Assets/OldUpdate/Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs","old imported duplicate");
            f.Write("Assets/OldUpdate/Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs.meta","fileFormatVersion: 2\nguid: "+guid+"\n");
            var r=f.Scan();Check(r.Warnings.Any(x=>x.Contains("Assets/OldUpdate")),"nested ZIP inside imported Assets is diagnosed");
            Check(r.Warnings.Any(x=>x.Contains("совпадает GUID")),"exact duplicated asset identity called out");
            Check(File.Exists(Path.Combine(f.Root,"Assets/OldUpdate/Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs")),"read-only audit never deletes an ambiguous imported copy");
            Check(r.Errors.Count==0,"canonical current source remains recognized separately from duplicate-copy warning");
        }
        using(var f=new Fixture())
        {
            string p=Path.Combine(f.Root,ScenePath);File.WriteAllText(p,File.ReadAllText(p).Replace("  roomCamera: {fileID: 3}","  roomCamera: {fileID: 0}"));
            var r=f.Scan();Check(r.Errors.Count==0,"missing optional link is repairable rather than missing-source failure");
            Check(r.Warnings.Any(x=>x.Contains("roomCamera")),"missing actual serialized camera reference reported");
        }
        using(var f=new Fixture())
        {
            string p=Path.Combine(f.Root,ScenePath);File.WriteAllText(p,File.ReadAllText(p).Replace("MVP_MenuHub","Old prototype environment"));
            Check(f.Scan().Errors.Any(x=>x.Contains("MVP_MenuHub")),"legacy generated scene cannot pass current-room inspection");
        }
        using(var f=new Fixture())
        {
            File.Delete(Path.Combine(f.Root,ScenePath));var r=f.Scan();Check(r.Errors.Any(x=>x.Contains("готовой игровой сцены")),"missing authored scene clearly diagnosed");
            Check(!File.Exists(Path.Combine(f.Root,ScenePath)),"missing authored scene is never replaced by an obsolete generated prototype");
        }
        using(var f=new Fixture())
        {
            string old=Path.Combine(f.Root,"Assets/IntercomMVP/Runtime/Frontend/IntercomFrontEnd.cs");
            f.Write("Assets/IntercomMVP/Runtime/UserMoved/IntercomFrontEnd.cs",File.ReadAllText(old));File.Delete(old);
            Check(f.Scan().Errors.Count==0,"single moved source remains valid");
        }
        using(var f=new Fixture())
        {
            string p=Path.Combine(f.Root,ScenePath);File.WriteAllText(p,File.ReadAllText(p).Replace("\n","\r\n"));
            Check(f.Scan().Errors.Count==0&&f.Scan().Warnings.Count==0,"Windows CRLF scene syntax recognized");
        }
        Console.WriteLine("Project repair diagnostics: "+assertions+" assertions PASS; production read-only disk policy, actual filesystem fixtures. No Unity Editor scene-repair execution.");
    }
}
