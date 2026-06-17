#requires -Version 5
<#
  StandReminder task runner — "npm scripts" pre tento repozitár.

  Použitie:
    .\tasks.ps1               # vypíše zoznam taskov (ako `npm run`)
    .\tasks.ps1 <task> [args] # spustí task

  Príklady:
    .\tasks.ps1 build
    .\tasks.ps1 deploy
    .\tasks.ps1 release 1.0.7
    .\tasks.ps1 gh-release 1.0.7

  Ak PowerShell blokuje spustenie skriptu (ExecutionPolicy), spusti raz:
    powershell -ExecutionPolicy Bypass -File .\tasks.ps1 <task>
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Task,
    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

$App    = 'StandReminder'
$Csproj = Join-Path $Root 'StandReminder.csproj'
$Repo   = 'robkrzn/StandReminder'

# ---------- helpers ----------
function Info($m) { Write-Host $m -ForegroundColor Cyan }
function Ok($m)   { Write-Host $m -ForegroundColor Green }
function Warn($m) { Write-Host $m -ForegroundColor Yellow }
function Die($m)  { Write-Host "✖ $m" -ForegroundColor Red; exit 1 }

function Get-AppVersion {
    $xml = [xml](Get-Content $Csproj -Raw)
    return [string]$xml.Project.PropertyGroup.Version
}

function Stop-App {
    $p = Get-Process $App -ErrorAction SilentlyContinue
    if ($p) {
        Info "Zastavujem bežiacu inštanciu ($App)…"
        $p | Stop-Process -Force
        Start-Sleep -Milliseconds 600
    }
}

function Start-App([string]$exe) {
    Start-Process -FilePath $exe
    Start-Sleep -Milliseconds 800
    $p = Get-Process $App -ErrorAction SilentlyContinue
    if ($p) { Ok "$App beží (PID $($p.Id), v$(Get-AppVersion))" }
    else { Warn "$App sa nezdá byť spustený — skontroluj crash.log." }
}

function Invoke-Publish([string]$outDir, [switch]$Standalone) {
    $a = @('publish', '-c', 'Release', '-r', 'win-x64', '-p:PublishSingleFile=true')
    if ($Standalone) {
        $a += '-p:SelfContained=true', '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true'
    } else {
        $a += '-p:SelfContained=false'
    }
    $a += '-o', $outDir
    dotnet @a
    if ($LASTEXITCODE) { Die "publish zlyhal ($outDir)" }
}

function Compress-Exe([string]$srcDir, [string]$zipPath) {
    $exe = Join-Path $srcDir 'StandReminder.exe'
    if (-not (Test-Path $exe)) { Die "exe sa nenašiel: $exe" }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    # zabalí LEN exe (žiadne .pdb), v koreni zipu
    Compress-Archive -Path $exe -DestinationPath $zipPath
    $mb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Ok "  → $([IO.Path]::GetFileName($zipPath))  ($mb MB)"
}

function Require-CleanTree {
    $dirty = git status --porcelain
    if ($dirty) {
        Warn "Pracovný strom nie je čistý:"
        $dirty | ForEach-Object { Write-Host "    $_" }
        Die "Commitni / odlož zmeny pred releasom (okrem dist/ ktoré je v .gitignore)."
    }
}

# ---------- tasks ----------
# poradie = poradie vo výpise; popis je prvý riadok scriptblocku za '#'
$Tasks = [ordered]@{
    'build' = @{
        desc = 'Build v Release (overenie, že kód kompiluje).'
        run  = {
            dotnet build -c Release
            if ($LASTEXITCODE) { Die 'build zlyhal' }
            Ok 'Build OK.'
        }
    }
    'run' = @{
        desc = 'Spustí appku z kódu (dotnet run, na popredí — Ctrl+C ukončí).'
        run  = { dotnet run -c Release }
    }
    'publish' = @{
        desc = 'Publish framework-dependent single-file exe do .\publish (bez spustenia).'
        run  = {
            Stop-App
            Invoke-Publish 'publish'
            Ok "Hotovo: .\publish\StandReminder.exe (v$(Get-AppVersion))"
        }
    }
    'deploy' = @{
        desc = 'Zastav appku → publish do .\publish → spusti. (alias: restart)'
        run  = {
            Stop-App
            Invoke-Publish 'publish'
            Start-App (Join-Path $Root 'publish\StandReminder.exe')
        }
    }
    'stop' = @{
        desc = 'Zastaví bežiacu inštanciu appky.'
        run  = { Stop-App; Ok 'Zastavené.' }
    }
    'icon' = @{
        desc = 'Pregeneruje Assets\app.ico (tools\generate-icon.ps1).'
        run  = { & (Join-Path $Root 'tools\generate-icon.ps1'); Ok 'Ikona pregenerovaná.' }
    }
    'version' = @{
        desc = 'Vypíše aktuálnu verziu z csproj.'
        run  = { Write-Host (Get-AppVersion) }
    }
    'clean' = @{
        desc = 'Zmaže build výstupy (bin, obj, publish, dist\fd-*, dist\sc-*). Zipy a release notes ostávajú.'
        run  = {
            'bin', 'obj', 'publish' | ForEach-Object {
                if (Test-Path $_) { Remove-Item $_ -Recurse -Force; Info "  zmazané $_" }
            }
            Get-ChildItem 'dist' -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^(fd|sc)-' } |
                ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Info "  zmazané dist\$($_.Name)" }
            Ok 'Vyčistené.'
        }
    }
    'release' = @{
        desc = 'Lokálna časť releasu: bump verzie + commit + publish oboch variantov + zipy + stub release notes. Použitie: release <X.Y.Z>'
        run  = { param($a) Invoke-Release $a }
    }
    'gh-release' = @{
        desc = 'GitHub časť releasu: push + gh release create (oba zipy + notes) + overenie digestu. Použitie: gh-release <X.Y.Z>'
        run  = { param($a) Invoke-GhRelease $a }
    }
}

# release tasky sú dlhšie → vlastné funkcie
function Invoke-Release([string]$Version) {
    if (-not $Version) { Die 'Použitie: .\tasks.ps1 release <X.Y.Z>' }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { Die "Verzia musí byť v tvare X.Y.Z (dostal som '$Version')." }

    $nnn = $Version -replace '\.', ''
    $current = Get-AppVersion

    if ($current -ne $Version) {
        Require-CleanTree
        Info "Bump verzie $current → $Version"
        $raw = Get-Content $Csproj -Raw
        $raw = $raw -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
        [System.IO.File]::WriteAllText($Csproj, $raw)
        git add StandReminder.csproj
        git commit -m "Bump version to $Version" | Out-Null
        Ok "Commit: Bump version to $Version"
    } else {
        Info "Verzia je už $Version — bump preskakujem."
    }

    Stop-App

    Info 'Publish framework-dependent…'
    Invoke-Publish "dist/fd-$nnn"
    Info 'Publish standalone…'
    Invoke-Publish "dist/sc-$nnn" -Standalone

    Info 'Balím zipy (len exe)…'
    Compress-Exe "dist/fd-$nnn" "dist/$App-v$Version-win-x64.zip"
    Compress-Exe "dist/sc-$nnn" "dist/$App-v$Version-win-x64-standalone.zip"

    $notes = "dist/RELEASE_NOTES_$nnn.md"
    if (-not (Test-Path $notes)) {
        $prev = (git tag --sort=-v:refname | Select-Object -First 1)
        if (-not $prev) { $prev = "v$current" }
        $tpl = @"
Jednoriadkové zhrnutie releasu.

## What's changed

- ✨ **Názov** — popis zmeny.

## Downloads

| Asset | Size | Notes |
|---|---|---|
| ``$App-v$Version-win-x64.zip`` | ~0.3 MB | Requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| ``$App-v$Version-win-x64-standalone.zip`` | ~66 MB | No runtime needed — unzip and run (this is what the auto-updater installs) |

Windows 10/11, x64. Settings and statistics are stored in ``%APPDATA%\StandReminder``.

**Full changelog:** https://github.com/$Repo/compare/$prev...v$Version
"@
        [System.IO.File]::WriteAllText((Join-Path $Root $notes), $tpl)
        Warn "Vytvoril som stub release notes: $notes"
    }

    Ok 'Lokálna časť releasu hotová.'
    Write-Host ''
    Info "Ďalší krok:"
    Write-Host "  1) uprav $notes" -ForegroundColor Gray
    Write-Host "  2) .\tasks.ps1 gh-release $Version" -ForegroundColor Gray
}

function Invoke-GhRelease([string]$Version) {
    if (-not $Version) { Die 'Použitie: .\tasks.ps1 gh-release <X.Y.Z>' }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { Die "Verzia musí byť v tvare X.Y.Z." }

    $nnn   = $Version -replace '\.', ''
    $zipFd = "dist/$App-v$Version-win-x64.zip"
    $zipSc = "dist/$App-v$Version-win-x64-standalone.zip"
    $notes = "dist/RELEASE_NOTES_$nnn.md"

    foreach ($f in @($zipFd, $zipSc, $notes)) {
        if (-not (Test-Path $f)) { Die "Chýba '$f' — spusti najprv: .\tasks.ps1 release $Version" }
    }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Die "gh CLI nie je nainštalované / v PATH." }

    Info 'Push na origin/main…'
    git push origin main
    if ($LASTEXITCODE) { Die 'git push zlyhal' }

    Info "Vytváram GitHub release v$Version…"
    gh release create "v$Version" $zipFd $zipSc `
        --title "$App v$Version" `
        --notes-file $notes `
        --target main
    if ($LASTEXITCODE) { Die 'gh release create zlyhal' }

    Info 'Overujem latest + digest standalone assetu…'
    gh api "repos/$Repo/releases/latest" --jq '{tag: .tag_name, assets: [.assets[] | {name, size, digest}]}'
    Ok "Release v$Version vydaný."
}

# ---------- dispatch ----------
function Show-Help {
    Write-Host ''
    Write-Host "StandReminder — tasky" -ForegroundColor White
    Write-Host "  .\tasks.ps1 <task> [args]" -ForegroundColor DarkGray
    Write-Host ''
    $w = ($Tasks.Keys | Measure-Object -Maximum -Property Length).Maximum
    foreach ($k in $Tasks.Keys) {
        $name = $k.PadRight($w)
        Write-Host "  $name  " -ForegroundColor Cyan -NoNewline
        Write-Host $Tasks[$k].desc -ForegroundColor Gray
    }
    Write-Host ''
    Write-Host "  Aktuálna verzia: $(Get-AppVersion)" -ForegroundColor DarkGray
    Write-Host ''
}

if (-not $Task) { Show-Help; return }

# aliasy
$alias = @{ 'restart' = 'deploy' }
if ($alias.ContainsKey($Task)) { $Task = $alias[$Task] }

if (-not $Tasks.Contains($Task)) {
    Warn "Neznámy task: '$Task'"
    Show-Help
    exit 1
}

$arg = if ($Rest) { $Rest[0] } else { $null }
& $Tasks[$Task].run $arg
