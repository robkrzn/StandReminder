# Feature: automatické aktualizácie (self-update z GitHub Releases)

> Stav: **IMPLEMENTOVANÉ (Fázy 1 + 2).** Detekcia, `UpdateWindow` s changelogom, tray
> položka „Skontrolovať aktualizácie…", 1-klik self-update + reštart, throttle a skip.
> Zhrnutie v hotovej podobe je v `doc/PROJECT.md` → sekcia *Automatické aktualizácie*.
> Zostáva Fáza 3 (doladenie: elevácia pri chránenom priečinku, overenie hashu).
>
> Zvolený prístup (rozhodnutie používateľa): **plná self-update (1 klik)** — app
> stiahne novú verziu, pomocný skript ju nainštaluje a appku reštartuje.
>
> Súbory: `Core/UpdateChecker.cs`, `Core/UpdateInstaller.cs`, `Views/UpdateWindow.xaml(.cs)`,
> nastavenia v `Core/AppSettings.cs`, napojenie v `App.xaml.cs`.

## Cieľ

Keď na GitHube vyjde nový release, aplikácia si to **sama všimne**, upozorní
používateľa (verzia + changelog) a na jeden klik sa **stiahne, nainštaluje a
reštartuje** do novej verzie. Žiadne ručné sťahovanie/nahradzovanie.

Repo: **`robkrzn/StandReminder`** (potvrdené z `git remote`). Verejný → GitHub API
netreba autentifikovať (limit 60 req/h na IP, kontrola raz denne je hlboko pod tým).

## Ako to funguje (workflow v krokoch)

```
[štart appky + raz denne]
      │
      ▼
1. GET api.github.com/repos/robkrzn/StandReminder/releases/latest
      │  (HttpClient + User-Agent; JSON cez System.Text.Json)
      ▼
2. Porovnaj tag_name (napr. "v1.0.4") s vlastnou verziou (Assembly.Version)
      │
      ├─ nie je novšia / skipnutá / bez internetu → ticho nič
      ▼
3. UpdateWindow: "Dostupná v1.0.4" + release notes + [Aktualizovať] [Neskôr] [Preskočiť]
      │  (klik Aktualizovať)
      ▼
4. Stiahni správny .zip asset do %TEMP%, rozbaľ
      ▼
5. Zapíš updater skript do %TEMP%, spusti ho odpojene (detached), a Shutdown() appky
      ▼
6. Skript: počká na exit procesu → prepíše StandReminder.exe → spustí novú verziu → hotovo
```

## Kľúčový problém: bežiaci .exe sa nevie prepísať sám

Windows drží bežiaci `.exe` zamknutý — nedá sa prepísať počas behu. Preto
**pomocný updater skript** beží ako samostatný proces (`powershell.exe`), počká na
ukončenie StandReminder, až potom prepíše súbor (vtedy je už odomknutý) a appku
znova spustí.

Náčrt skriptu (zapíše sa do `%TEMP%\StandReminder.update.ps1`):

```powershell
param([int]$Pid, [string]$NewExe, [string]$TargetExe)
try { Wait-Process -Id $Pid -Timeout 30 -ErrorAction SilentlyContinue } catch {}
Start-Sleep -Milliseconds 500                 # poistka, kým sa uvoľní zámok
Copy-Item -Path $NewExe -Destination $TargetExe -Force
Start-Process -FilePath $TargetExe            # reštart do novej verzie
```

Spustenie z appky (odpojene, skryté okno):
```
powershell -ExecutionPolicy Bypass -WindowStyle Hidden -File <skript> -Pid <PID> -NewExe <...> -TargetExe <Environment.ProcessPath>
```
Potom `Application.Current.Shutdown()`.

## Ktorý asset stiahnuť — rozhodnutie

Release má dva zipy:
- `StandReminder-v{ver}-win-x64.zip` — framework-dependent (~120 KB, vyžaduje .NET runtime)
- `StandReminder-v{ver}-win-x64-standalone.zip` — self-contained (~66 MB, funguje vždy)

App nevie spoľahlivo zistiť, ktorý variant beží. **Návrh: vždy aktualizovať na
`standalone` variant** — funguje bez ohľadu na to, či je na PC runtime, takže update
nikdy nerozbije appku. Cena: ~66 MB sťahovanie (zriedkavé, akceptovateľné).

> Alternatíva: detegovať variant (napr. podľa veľkosti exe / prítomnosti runtime) a
> sťahovať zodpovedajúci. Zložitejšie a krehké → standalone-always je bezpečnejšie.

## Návrh architektúry (zapadnutie do kódu)

Konvencie: namespace `StandReminder`, logika do `Core/`, okná do `Views/`, UI texty
do `Core/Loc.cs` (žiadne literály), nastavenia do `Core/AppSettings.cs`. **Bez NuGet**
— `HttpClient` aj `System.Text.Json` sú v BCL, rozbalenie zipu cez
`System.IO.Compression.ZipFile`.

### `Core/UpdateChecker.cs` (nový)

```csharp
public record UpdateInfo(Version Version, string TagName, string Notes, string DownloadUrl);

public static class UpdateChecker
{
    // GET releases/latest, parse JSON, porovnaj s Assembly verziou.
    // Vráti UpdateInfo ak je novšia a nie je skipnutá, inak null.
    public static Task<UpdateInfo?> CheckAsync(Version current, string? skippedTag);
}
```
- Vlastná verzia: `typeof(App).Assembly.GetName().Version` (z csproj `<Version>` → `1.0.3.0`).
- Tag „v1.0.4" → strip „v" → `Version.Parse`. Porovnanie cez `Version` (rieši poradie správne).
- Asset URL: z `assets[]` vyber ten, čo končí `-standalone.zip` (`browser_download_url`).
- Všetko v `try/catch` — bez internetu / chyba API → vráť `null`, neotravuj, neloguj ako chybu (max INFO).

### `Core/UpdateInstaller.cs` (nový)

```csharp
public static class UpdateInstaller
{
    // Stiahni zip do %TEMP%, rozbaľ, nájdi StandReminder.exe,
    // zapíš updater .ps1, spusti ho detached, vráť (volajúci spraví Shutdown()).
    public static Task DownloadAndApplyAsync(UpdateInfo info, IProgress<double>? progress = null);
}
```
- Sťahovanie cez `HttpClient` s priebehom (na progress bar v okne).
- Rozbalenie: `ZipFile.ExtractToDirectory` do temp podpriečinka.
- Pozor na install-location: ak je appka v chránenom priečinku (Program Files),
  `Copy-Item` zlyhá bez elevácie. App je per-user (autostart cez HKCU), takže typicky
  beží z user-writable miesta — ale **ošetriť zlyhanie** (oznámiť a ponúknuť ručné stiahnutie).

### `Views/UpdateWindow.xaml(.cs)` (nový)

- Dark theme okno (rovnaké štýly/tokeny ako `ReminderWindow`/`StatusWindow`).
- Obsah: nová verzia, release notes (`body` ako čitateľný text), progress bar pri sťahovaní.
- Tlačidlá: **Aktualizovať** / **Neskôr** / **Preskočiť túto verziu** (uloží `SkippedVersion`).
- Eventy napr. `UpdateRequested`, `Skipped`.

### Zmeny v `Core/AppSettings.cs`

```jsonc
{
  "AutoUpdateCheck": true,    // zapnuté checkovanie (default zap.)
  "SkippedVersion": "",       // verzia, ktorú používateľ „preskočil"
  "LastUpdateCheck": null     // ISO timestamp poslednej kontroly (throttle na 1×/deň)
}
```

### Napojenie v `App.xaml.cs`

- Na `OnStartup` (po inicializácii, s malým oneskorením aby nezdržalo štart) spusti
  `await UpdateChecker.CheckAsync(...)` ak `AutoUpdateCheck` a uplynul deň od `LastUpdateCheck`.
- Periodická kontrola: využi existujúci 1 s `DispatcherTimer` — raz za deň porovnaj čas.
- Položka v tray menu „Skontrolovať aktualizácie…" na manuálne vyvolanie (ignoruje throttle aj skip).
- Pri dostupnom update → otvor `UpdateWindow`.

## Bezpečnosť a UX

1. Komunikácia len cez **HTTPS** na `api.github.com` a `github.com` (download URL).
2. **Neotravovať:** rešpektuj `SkippedVersion` a kontrolu max 1×/deň; manuálna kontrola tieto ignoruje.
3. Bez internetu / chyba → ticho, appka funguje normálne (graceful degradation).
4. Pred reštartom **ulož stav** (`stats`, settings) — appka to už robí v `OnExit`/flush,
   over že `Shutdown()` cestou cez updater to spustí (`OnExit` sa zavolá).
5. Updater skript po sebe **zmaže** temp súbory (na konci skriptu).
6. **Overenie integrity (implementované):** po stiahnutí sa počíta SHA-256 a porovná s `digest`
   (`sha256:…`) z GitHub API; nezhoda zruší inštaláciu. Plus host-allowlist (HTTPS, len GitHub hosty).
   *Rámec dôvery:* hash z GitHubu rieši integritu (poškodený download, CDN-tamper), nie kompromitáciu
   účtu — public repo neumožňuje hocikomu vydať release, len vlastníkovi/kolaborantom. Plnú autenticitu
   (aj proti kompromitácii účtu) by dal **podpis exe (Authenticode)** + overenie pred spustením — viď Fáza 3.

## Fázovanie (míľniky)

| Fáza | Obsah | Výstup |
|---|---|---|
| **1 — Detekcia** | `UpdateChecker` (API + verzie), tray „Skontrolovať aktualizácie", `UpdateWindow` len s changelogom + tlačidlom „Otvoriť stránku". | App si všimne novú verziu a upozorní. Bez rizika. |
| **2 — Self-update (MVP)** | `UpdateInstaller` (download + extract + updater skript), tlačidlo „Aktualizovať" → 1-klik update + reštart. Settings + throttle + skip. | Plná funkcia. |
| **3 — Doladenie** | Progress bar, ošetrenie chráneného priečinka (elevácia / fallback na ručné), prípadne overenie hashu. | Robustnosť. |

## Testovanie (háčik)

Self-update sa reálne overí len proti novšiemu releasu. Bez vydávania sa dá otestovať:
- **Version-compare + parsing API**: jednotkovo / dočasným znížením lokálnej `<Version>`
  (napr. na 1.0.0), aby `releases/latest` (v1.0.3) vyzeral ako „novší".
- **Updater skript**: samostatne s dvoma dummy exe (počká, prepíše, spustí).
- Plný end-to-end až s testovacím releasom (napr. v1.0.4-test).

## Otvorené otázky — rozhodnuté pri implementácii

- [x] Asset: **standalone-always** — sťahuje sa `*-standalone.zip`.
- [x] Frekvencia: **štart + 1×/deň** (throttle cez `LastUpdateCheck`, hooknuté do 1 s ticku).
- [x] Updater: **PowerShell skript** v `%TEMP%` (Win11), spúšťaný odpojene cez `ArgumentList`.
- [x] Cieľové miesto: rovnaké (`Environment.ProcessPath`).
- [ ] **Fáza 3:** chránený priečinok (Program Files) → elevácia vs. fallback na ručné stiahnutie.
      Zatiaľ: 10× retry kópie, inak reštart pôvodnej verzie (per-user inštalácia to typicky nerieši).
- [x] **Overenie integrity — hash:** SHA-256 voči GitHub `digest` + host-allowlist (HTTPS, GitHub hosty).
- [ ] **Fáza 3 — podpis:** Authenticode podpis exe + overenie podpisu/vydavateľa pred spustením
      (jediná ochrana aj proti kompromitácii GitHub účtu; vyžaduje code-signing certifikát).

## Referencie

- GitHub REST API — [Get the latest release](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)
  (`GET /repos/{owner}/{repo}/releases/latest`)
- `System.IO.Compression.ZipFile` (BCL) na rozbalenie
- Vzor self-update cez „wait-for-exit + copy + relaunch" skript (klasický pattern pre single-file WPF/WinForms appky)
