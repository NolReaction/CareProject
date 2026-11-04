#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [switch]$VerifyOnly,
    [string]$RestoreBackup
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-Json([string]$Path) {
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Select-ProjectFolder {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'CareProject V19: выбери существующий проект Unity, где рядом лежат Assets, Packages и ProjectSettings.'
    $dialog.ShowNewFolderButton = $false
    try {
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Отменено. Файлы проекта не изменены.' }
        return $dialog.SelectedPath
    } finally { $dialog.Dispose() }
}

function Inventory-Entries($Inventory) {
    foreach ($property in $Inventory.files.PSObject.Properties) {
        $entry = New-Object CareProject.Packages.PackageFile
        $entry.Path = [string]$property.Name
        $entry.Sha256 = [string]$property.Value.sha256
        $entry.Bytes = [long]$property.Value.bytes
        $entry
    }
}

function Manifest-Entries($Entries) {
    foreach ($item in $Entries) {
        $entry = New-Object CareProject.Packages.PackageFile
        $entry.Path = [string]$item.path
        $entry.Sha256 = [string]$item.sha256
        $entry.Bytes = [long]$item.bytes
        $entry
    }
}

try {
    Write-Host 'CareProject V19 — проверка, установка и восстановление' -ForegroundColor Cyan
    Write-Host 'Закрой Unity. Заменяемые и старые файлы сохранятся в ProjectBackups.'
    if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
        throw 'Unity запущена. Закрой все окна редактора и запусти этот файл ещё раз.'
    }
    $engine = Join-Path $PSScriptRoot 'Tools\Project\PackageInstaller.cs'
    if (-not (Test-Path -LiteralPath $engine -PathType Leaf)) {
        throw 'Нет PackageInstaller.cs. Сначала распакуй весь ZIP, затем запускай RepairProject.cmd.'
    }
    if (-not ('CareProject.Packages.PackageInstaller' -as [type])) { Add-Type -Path $engine }

    if ($RestoreBackup) {
        $backupRoot = (Resolve-Path -LiteralPath $RestoreBackup).ProviderPath
        $receipt = Read-Json (Join-Path $backupRoot 'Receipt.json')
        if ([int]$receipt.schema -ne 1 -or -not ([CareProject.Packages.PackageInstaller]::SupportsVersion([string]$receipt.version))) { throw 'Unsupported restore receipt.' }
        if ($ProjectPath -and [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\') -ne [IO.Path]::GetFullPath([string]$receipt.projectRoot).TrimEnd('\')) {
            throw 'Эта резервная копия относится к другому проекту.'
        }
        $ProjectPath = [string]$receipt.projectRoot
        $restoreEntries = @(foreach ($item in $receipt.entries) {
            $entry = New-Object CareProject.Packages.ReceiptEntry
            $entry.Path = [string]$item.path
            $entry.Existed = [bool]$item.existed
            $entry.PreviousSha256 = $item.previousSha256
            $entry.InstalledSha256 = $item.installedSha256
            $entry
        })
        [CareProject.Packages.PackageInstaller]::Restore($ProjectPath, $backupRoot, [CareProject.Packages.ReceiptEntry[]]$restoreEntries)
        Write-Host ('Восстановлено: ' + $ProjectPath) -ForegroundColor Green
        Write-Host 'Резервная копия сохранена.'
        exit 0
    }

    $patchManifestPath = Join-Path $PSScriptRoot 'UpdateManifest.json'
    $patch = $null
    if (Test-Path -LiteralPath $patchManifestPath -PathType Leaf) {
        $patch = Read-Json $patchManifestPath
        if ([int]$patch.schema -ne 1 -or [string]$patch.version -ne 'V19') { throw 'Unsupported update package.' }
        $payloadRoot = Join-Path $PSScriptRoot 'Payload'
    } else { $payloadRoot = $PSScriptRoot }

    $inventoryPath = Join-Path $payloadRoot 'PROJECT_FILES.json'
    if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) { throw 'Нет PROJECT_FILES.json. Распакуй пакет V19 полностью.' }
    $inventory = Read-Json $inventoryPath
    if ([string]$inventory.version -ne 'CareProject V19') { throw 'This installer requires a V19 source inventory.' }
    $expected = @(Inventory-Entries $inventory)
    if ($patch) { $payload = @(Manifest-Entries $patch.files) }
    else {
        $payload = @($expected)
        $self = New-Object CareProject.Packages.PackageFile
        $self.Path = 'PROJECT_FILES.json'
        $self.Sha256 = [CareProject.Packages.PackageInstaller]::HashFile($inventoryPath)
        $self.Bytes = (Get-Item -LiteralPath $inventoryPath).Length
        $payload += $self
    }

    $retirement = Read-Json (Join-Path $payloadRoot 'Tools\RetirementManifestV18.json')
    if ([int]$retirement.schema -ne 1 -or [string]$retirement.version -ne 'V18') { throw 'Unsupported retirement manifest.' }
    $retired = @(foreach ($item in $retirement.retiredFiles) {
        $entry = New-Object CareProject.Packages.RetiredFile
        $entry.Path = [string]$item.path
        $entry.KnownSha256 = [string[]]$item.knownSha256
        $entry.Guid = [string]$item.guid
        $entry.RequireGuid = $false
        $entry
    })
    foreach ($item in $retirement.legacyAliases) {
        $entry = New-Object CareProject.Packages.RetiredFile
        $entry.Path = [string]$item.path
        $entry.KnownSha256 = [string[]]$item.knownSha256
        $entry.Guid = [string]$item.guid
        $entry.RequireGuid = $true
        $retired += $entry
        $metadata = @($retirement.retiredFiles | Where-Object { $_.path -eq ([string]$item.canonicalRetiredSource + '.meta') })
        if ($metadata.Count -gt 0) {
            $sidecar = New-Object CareProject.Packages.RetiredFile
            $sidecar.Path = [string]$item.path + '.meta'
            $sidecar.KnownSha256 = [string[]]$metadata[0].knownSha256
            $sidecar.Guid = [string]$item.guid
            $sidecar.RequireGuid = $true
            $retired += $sidecar
        }
    }
    $knownItems = @()
    if ($patch -and $patch.PSObject.Properties.Name -contains 'knownScripts') { $knownItems = @($patch.knownScripts) }
    else {
        foreach ($knownName in @('KnownScriptsV19.json', 'KnownScriptsV18.json')) {
            $knownPath = Join-Path $payloadRoot ('Tools\Project\' + $knownName)
            if (Test-Path -LiteralPath $knownPath -PathType Leaf) {
                $knownItems = @((Read-Json $knownPath).knownScripts)
                break
            }
        }
    }
    $known = @(foreach ($item in $knownItems) {
        $entry = New-Object CareProject.Packages.KnownScript
        $entry.Path = [string]$item.path
        $entry.Guid = [string]$item.guid
        $entry.KnownSha256 = [string[]]$item.knownSha256
        $entry
    })
    if (-not $ProjectPath) { $ProjectPath = Select-ProjectFolder }
    $ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
    Write-Host ('Выбранный проект: ' + $ProjectPath) -ForegroundColor Yellow
    $result = [CareProject.Packages.PackageInstaller]::Apply($payloadRoot, $ProjectPath, 'V19',
        [CareProject.Packages.PackageFile[]]$payload, [CareProject.Packages.PackageFile[]]$expected,
        [CareProject.Packages.RetiredFile[]]$retired, [CareProject.Packages.KnownScript[]]$known, [bool]$VerifyOnly)
    if ($VerifyOnly) { Write-Host 'Проверка пройдена. Файлы проекта не изменялись.' -ForegroundColor Green }
    else { Write-Host 'Установка V19 завершена.' -ForegroundColor Green }
    Write-Host ('Файлов к установке / установлено: ' + $result.Copied + '; старых файлов перенесено в копию: ' + $result.Removed)
    if ($result.BackupRoot) {
        Write-Host ('Резервная копия и журнал: ' + $result.BackupRoot)
        Write-Host ('Команда восстановления: powershell -NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -RestoreBackup "' + $result.BackupRoot + '"')
    }
    Write-Host 'Открой именно этот проект в Unity 6000.3.26f1, дождись импорта, затем открой окно проверки в Tools > CareProject.'
    Write-Host 'В окне проверки должна отображаться версия V19. Удалять папку Library не нужно.'
    exit 0
} catch {
    Write-Host ('ОСТАНОВЛЕНО: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Если проверка не прошла, не копируй скрипты по одному. Для неполного или изменённого старого проекта распакуй полный V19 ZIP в новую папку.'
    exit 1
}
