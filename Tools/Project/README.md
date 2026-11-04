# V19 package verification and recovery

The update is intended for V15–V18 projects; the installer verifies the base
before making changes. Edited or incomplete bases can fail preflight and require
the full project in a fresh folder.

The update ZIP must keep these items together: `RepairProject.cmd`,
`RepairProject.ps1`, `UpdateManifest.json`, `Tools/Project/PackageInstaller.cs`
and the complete `Payload` folder. The inventory and retirement/known-script
registries are inside `Payload`. Do not run the launcher from inside `Payload`.

Close Unity, extract the complete update ZIP outside the working project and run
`RepairProject.cmd`. Select the Unity root containing `Assets`, `Packages` and
`ProjectSettings`. The tool checks every payload hash and any required unchanged
base file before changing the project. It archives replaced files and known old
scripts under `ProjectBackups`, then installs the current sources.

From PowerShell, `RepairProject.ps1 -ProjectPath 'C:\Games\CareProject' -VerifyOnly`
checks without changing files. `-RestoreBackup '...\ProjectBackups\V19_...'`
uses the saved receipt; both V18 and V19 receipts are accepted, and files edited
after installation are preserved.
Unknown edited legacy scripts are preserved and stop preflight.

The full project can simply be opened from a fresh folder. Its same repair script
can verify that folder and quarantine recognised old copies left by a previous
manual extraction. It cannot restore user files already overwritten manually
before the script was run.

After the external step, open that exact project in Unity, wait for import and
use the CareProject repair window to check scene links and build imported robot
presentation assets. The external installer does not delete Unity caches, edit
Git history or compile Unity/Android builds.

The update manifest schema stays at 1. V19 payloads use `version: V19` and the
full inventory uses `CareProject V19`. Historical V18 retirement paths/hashes
remain in `Tools/RetirementManifestV18.json`. The full-project wrapper prefers
`Tools/Project/KnownScriptsV19.json`, falling back to V18 if necessary.
