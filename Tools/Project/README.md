# V22 full-project verification

This delivery is a complete Unity project and a self-contained Git bundle.
Open the ZIP contents in a fresh folder or clone the bundle. No patch installer
or older CareProject delivery is required.

`RepairProject.cmd` optionally verifies a fully extracted V22 inventory while
Unity is closed. `RepairProject.ps1 -ProjectPath 'C:\Games\CareProject' -VerifyOnly`
performs a read-only check. Missing/changed files are reported; the command does
not download the release or compile Unity. For actual repairs, preserve a copy
of your own work before applying any external package.

`PROJECT_FILES.json` records the V22 content hashes. `KnownScriptsV22.json`
retains older recognised script hashes and adds current sources. Historic
V18/V19/V20/V21 registries are retained for existing backup receipts. The installer
can restore receipts for V18 through V22, preserving subsequently edited files.

After FBX import the Unity editor prepares the standard Udarnik controller
using imported AnimationClip objects, retaining its GUID and backing up the
old recognised graph. Use Tools → CareProject → Проверить и восстановить проект
if import preparation was deferred. Custom user controllers are preserved.
