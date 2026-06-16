# StandReminder — projektová dokumentácia

> Účel tohto súboru: rýchly kontext pre AI asistenta / vývojára pri ďalšej práci na projekte.
> Aktualizuj ho pri každej významnej zmene architektúry alebo správania.

## Čo je to

Windows tray aplikácia, ktorá v pracovnom čase pripomína striedanie sedenia a státia
pri výškovo nastaviteľnom stole. Beží na pozadí v systémovej lište, žiadne hlavné okno.
UI je dvojjazyčná — slovenčina (default) a angličtina, prepína sa v nastaveniach.

**Používateľ:** jeden konkrétny používateľ, aplikácia beží len v práci (pracovný čas
predvolene 07:00–16:00). Odporúčaný pomer sedenie : státie je 2:1 až 3:1 — predvolené
intervaly sú 45 min sedenie / 15 min státie.

## Technológie

- **C# / .NET 10**, WPF (`UseWPF`) + WinForms len kvôli `NotifyIcon` (`UseWindowsForms`)
- Žiadne NuGet závislosti — všetko je BCL
- V csproj sú odstránené implicitné usings `System.Windows.Forms` a `System.Drawing`
  (kolidovali s WPF typmi `Application`, `MessageBox`, `Color`, `Brush`).
  V `App.xaml.cs` sa používajú aliasy `WinForms = System.Windows.Forms` a `Drawing = System.Drawing`.

## Build & spustenie

```powershell
dotnet build -c Release
# distribučný single-file exe (vyžaduje .NET 10 Desktop Runtime na cieľovom PC, ~220 KB):
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false -o publish
# standalone exe bez nutnosti runtime (~75 MB):
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/sc
```

**Pozor:** `--self-contained false` (CLI tvar) v kombinácii s `-p:PublishSingleFile=true`
SDK ignoruje a potichu pribalí celý runtime (~165 MB exe). Vždy používaj MSBuild tvar
`-p:SelfContained=false`.

Výstup: `publish/StandReminder.exe`. Pred republish treba ukončiť bežiacu inštanciu
(`Stop-Process -Name StandReminder`), inak je exe zamknutý.

## Architektúra a súbory

Štruktúra priečinkov: `Views/` = okná (XAML + code-behind), `Core/` = logika bez UI,
`Ui/` = vizuálne pomocné triedy, root = len vstupný bod (`App.xaml`). Všetky súbory
zdieľajú jeden namespace `StandReminder` (zámerné — malá app, žiadne sub-namespaces;
pri presune súboru sa nemení kód). SDK-style csproj globuje súbory automaticky.

| Súbor | Zodpovednosť |
|---|---|
| `App.xaml` | Globálne resources: farebná paleta, štýly `PrimaryButton`, `GhostButton`, `DarkSlider`, `DarkCheckBox`, `DarkCombo`. `ShutdownMode=OnExplicitShutdown`, žiadne StartupUri. |
| `App.xaml.cs` | **Jadro aplikácie.** Tray ikona + kontextové menu, stavový automat fáz, `DispatcherTimer` (1 s tick), kreslenie tray ikon cez GDI+, autostart cez registry, single-instance mutex (`StandReminder_SingleInstance`). |
| `Core/AppSettings.cs` | Model nastavení + JSON load/save do `%APPDATA%\StandReminder\settings.json`. |
| `Views/ReminderWindow.xaml(.cs)` | Popup notifikácia pri zmene fázy (pravý dolný roh, `Topmost`, `ShowActivated=False` — nekradne fokus). Tlačidlá: potvrdiť zmenu / `+5 min` snooze. Eventy `Accepted`, `Snoozed`. |
| `Views/StatusWindow.xaml(.cs)` | Flyout po **ľavom kliku** na tray ikonu: aktuálna pozícia, uplynutý čas, progress bar, zostávajúci čas, tlačidlo ⚙ (nastavenia) a „Zmeniť pozíciu teraz". Zavrie sa pri `Deactivated`. Eventy `SettingsRequested`, `SwitchRequested`. |
| `Views/SettingsWindow.xaml(.cs)` | Okno nastavení: slidery intervalov (sedenie 10–120 min, státie 5–60 min), pracovný čas (ComboBoxy 05:00–21:30 po 30 min), zvuk, autostart. Event `Saved`. Tmavý title bar cez `UiNative.UseDarkTitleBar` (volané v `SourceInitialized`). |
| `Ui/UiNative.cs` | P/Invoke na `dwmapi.dll` (`DwmSetWindowAttribute`): `UseDarkTitleBar` (attr 20) a `UseRoundedCorners` (attr 33, Windows 11) pre zaoblené rohy popupov. |
| `Ui/DarkMenuRenderer.cs` | Dark theme pre WinForms `ContextMenuStrip` tray menu — `ToolStripProfessionalRenderer` s vlastnou `ProfessionalColorTable`, zaoblený hover highlight, vlastné separátory. Farby zrkadlia WPF paletu. |
| `Core/CrashLog.cs` | Crash logging + health check (pozri sekciu nižšie). |
| `Core/Stats.cs` | Denná štatistika sedenia/státia → `%APPDATA%\StandReminder\stats.json`. História **max 7 kalendárnych dní** — `Prune()` pri každom load/save maže staršie záznamy, súbor nemôže rásť. Počíta sa v `Tick()` (1 tick = 1 s do aktuálnej fázy, len keď nie je pauza/Idle), flush na disk každých 60 s + pri ukončení (`OnExit`). Zobrazenie: sekcia v StatusWindow flyoute — dnešné súčty + stacked bar graf 7 dní (modrá sedenie dole, zelená státie hore, tooltip s detailom). |
| `Core/Loc.cs` | Lokalizácia: statický slovník všetkých UI stringov ako dvojice `(sk, en)`, prístup cez `Loc.T(key)` / `Loc.F(key, args)`. `Loc.Lang` sa nastavuje z `AppSettings.Language` pri štarte a po uložení nastavení (vtedy sa volá aj `App.ApplyMenuLanguage()` na tray menu; okná si texty naplnia pri vytvorení). Žiadne .resx — pri pridávaní UI textu vždy pridaj kľúč do `Loc.cs`, nie literál do kódu/XAML. |
| `Core/UpdateChecker.cs` | Detekcia aktualizácií. `CheckAsync(current, skippedTag)` zavolá `GET releases/latest` (`HttpClient` + `User-Agent`, JSON cez `JsonDocument`), porovná tag s vlastnou verziou a vráti `UpdateCheckResult` so stavom (`UpToDate` / `UpdateAvailable` / `Skipped` / `Failed`). Asset = ten, čo končí `-standalone.zip`. Akákoľvek chyba → `Failed` (ticho, neloguje sa). |
| `Core/UpdateInstaller.cs` | Self-update. `DownloadAndApplyAsync(info, progress)` stiahne zip do `%TEMP%\StandReminderUpdate`, rozbalí, **zip hneď zmaže** (~66 MB), rekurzívne nájde `StandReminder.exe`, zapíše updater `.ps1` a spustí ho **odpojene** (`powershell.exe`, skryté okno, cez `ArgumentList`). Volajúci hneď spraví `ExitApp()`. Updater počká na exit procesu (`Wait-Process` podľa PID), prepíše exe (10× retry, lebo bežiaci exe je zamknutý), reštartuje novú verziu a zmaže rozbalený priečinok. Cleanup je trojstupňový: (1) zip po rozbalení, (2) rozbalený priečinok v skripte po kopírovaní, (3) `CleanupTemp()` pri štarte novej verzie zmaže celý temp vrátane samotného skriptu (ten sa za behu nevie zmazať sám). |
| `Views/UpdateWindow.xaml(.cs)` | Dark okno (rovnaké tokeny ako ostatné popupy, vpravo dole ako ReminderWindow). Ukáže novú verziu, changelog (`body`) v scrollovateľnej karte so slim dark scrollbarom, odkaz „Otvoriť stránku releasu". Tlačidlá: **Aktualizovať** / **Neskôr** / **Preskočiť túto verziu**. Pri sťahovaní prepne na progress bar (`SetDownloading`/`SetProgress`). Eventy `UpdateRequested`, `Skipped`. Ak release nemá standalone asset, tlačidlo Aktualizovať sa skryje a ostane odkaz na stránku. |
| `Views/ToastWindow.xaml(.cs)` | Ľahký tmavý toast v pravom dolnom rohu — neaktivuje sa (`ShowActivated=False`, `ShowInTaskbar=False`), po 6 s sám zmizne, klikom sa zavrie. Konštruktor `(emoji, title, message, positive)`. Použitý na potvrdenie po self-update (zelený/jantárový badge). Znovupoužiteľný aj pre budúce notifikácie. |
| `Assets/app.ico` | Ikona aplikácie (exe, hlavička okna, taskbar) — zelený kruh so stojacou postavičkou, rovnaká geometria ako tray ikona. Zapojená cez `<ApplicationIcon>` v csproj. **Negeneruje sa pri builde** — pri zmene dizajnu ju treba pregenerovať skriptom `tools/generate-icon.ps1` a commitnúť. |
| `tools/generate-icon.ps1` | PowerShell skript, ktorý nakreslí logo cez System.Drawing (veľkosti 16–256 px) a zloží ICO kontajner s PNG frame-ami. Spúšťa sa z koreňa repa: `powershell -File tools\generate-icon.ps1`. |

## Stavový automat (App.xaml.cs)

```
enum Phase { Idle, Sitting, Standing }
```

- `Tick()` beží každú sekundu:
  - `Idle` + sme v pracovnom čase → `StartPhase(Sitting)` (deň začína sedením)
  - aktívna fáza + koniec pracovného času → `Idle`, zavrie sa pripomienka
  - aktívna fáza + `now >= _phaseEnd` a popup nie je otvorený → `ShowReminder(opačná fáza)`
- `StartPhase(phase)` nastaví `_phaseStart` a `_phaseEnd = start + interval` z nastavení
- **Snooze** posunie `_phaseEnd` o 5 min (fáza pokračuje, popup sa zavrie)
- **Pauza** (`_paused`): tiky sa ignorujú; pri obnovení sa fáza reštartuje s plným intervalom
- Popup zostáva otvorený, kým ho používateľ nepotvrdí (zámerné — pri odchode od PC)
- `PushStatus()` aktualizuje otvorený StatusWindow flyout pri každom ticku

## Tray ikona

Tri ikony kreslené runtime cez GDI+ (`MakeIcon` + `CreateSitIcon/CreateStandIcon/CreateIdleIcon`),
32×32 px, farebný kruh + biely piktogram:

- 🔵 modrá `#5B8DEF` — sediaca postavička (profil) — fáza Sitting
- 🟢 zelená `#46C28E` — stojaca postavička — fáza Standing
- ⚪ sivá `#787E94` — pause symbol (‖) — Idle alebo pozastavené

Interakcie: **ľavý klik** = StatusWindow flyout, **pravý klik** = menu
(Zmeniť pozíciu teraz / Pozastaviť / Nastavenia… / Skontrolovať aktualizácie… / Ukončiť). Tooltip ikony ukazuje
stav a odpočet (limit 63 znakov). Flyout má 300 ms guard proti znovuotvoreniu
pri zatváracom kliku na ikonu.

Tray menu je tmavé (`DarkMenuRenderer`) a má zaoblené rohy cez DWM
(`UiNative.UseRoundedCorners` na `menu.HandleCreated`).

## Dizajn (dark theme)

| Token | Hodnota | Použitie |
|---|---|---|
| `BgBrush` | `#1B1E28` | pozadie okien |
| `CardBrush` | `#232734` | karty, popupy |
| `TextBrush` | `#E8EAF2` | hlavný text |
| `SubTextBrush` | `#9AA0B4` | sekundárny text |
| `AccentSitBrush` | `#5B8DEF` | modrá — sedenie |
| `AccentStandBrush` | `#46C28E` | zelená — státie |
| `BorderBrush` | `#3A4054` | okraje, ghost buttony |

Popupy: `WindowStyle=None`, `AllowsTransparency`, `CornerRadius` 14–16, `DropShadowEffect`,
fade-in animácia, pozícia pravý dolný roh `SystemParameters.WorkArea`.

Ovládacie prvky majú **vlastné ControlTemplates v App.xaml** (defaultné WPF vyzerali zastaralo):
`DarkSlider` (akcentová výplň + ring thumb à la Win11), `DarkCheckBox` (zaoblený box,
akcent pri zaškrtnutí), `DarkCombo` (zaoblené pole, otočná šípka, tmavý zaoblený dropdown
s vlastným slim scrollbarom v `Border.Resources` popupu). Pribudli brushe `InputBrush`
`#2A2F3F` (polia) a `HoverBrush` `#323848` (hover stavy).

## Nastavenia (settings.json)

```json
{
  "SitMinutes": 45,
  "StandMinutes": 15,
  "WorkStart": "07:00",
  "WorkEnd": "16:00",
  "PlaySound": true,
  "StartWithWindows": false,
  "Language": "sk",
  "AutoUpdateCheck": true,
  "SkippedVersion": "",
  "LastUpdateCheck": null,
  "PendingUpdateVersion": ""
}
```

- Časy sú stringy `"HH:mm"`, parsované cez `TimeSpan.TryParse` (properties `WorkStartTime/WorkEndTime`)
- Autostart: registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, hodnota `StandReminder`
- Po uložení nastavení sa aktuálna fáza reštartuje s novým intervalom
- `AutoUpdateCheck` (default zap.) — checkbox v nastaveniach; `SkippedVersion` = tag, ktorý
  používateľ „preskočil"; `LastUpdateCheck` = ISO timestamp poslednej kontroly (throttle 1×/deň,
  parsovaný cez `[JsonIgnore] LastUpdateCheckTime`); `PendingUpdateVersion` = tag, na ktorý práve
  prebieha update (pred reštartom sa zapíše, nová verzia ho pri štarte vyhodnotí a vyčistí).
  Pozri sekciu *Automatické aktualizácie*.

## Crash log a health check (CrashLog.cs)

- Log: `%APPDATA%\StandReminder\crash.log` — appenduje sa, formát `[timestamp] LEVEL správa` + stack trace
- **Rotácia:** pri prekročení 512 KB sa log presunie do `crash.old.log` (prepíše predošlú zálohu)
  a začne sa nový — celková spotreba disku je ohraničená na ~1 MB
- **Anti-spam:** identický záznam (level + správa + typ výnimky) sa v rámci minúty zapíše len raz;
  pri ďalšom odlišnom zázname sa doplní riadok „Predchádzajúci záznam sa opakoval ešte N×".
  Chráni pred chybou opakujúcou sa v 1 s ticku (86 400 záznamov/deň → ~1 440/deň)
- `CrashLog.Install(app)` sa volá v `OnStartup` (len primárna inštancia — po mutex checku):
  - `DispatcherUnhandledException` → zaloguje **ERROR** a nastaví `Handled = true`, takže
    výnimky na UI vlákne aplikáciu **nezhodia**, len sa zapíšu
  - `AppDomain.UnhandledException` → **FATAL** (proces aj tak končí, ale ostane záznam)
  - `TaskScheduler.UnobservedTaskException` → **ERROR**
- Health flag: `%APPDATA%\StandReminder\running.flag` — vytvorí sa pri štarte, zmaže sa pri
  čistom ukončení (`OnExit` → `MarkCleanExit`, pokrýva aj logoff/shutdown Windows).
  Ak pri štarte flag existuje → **WARN** „nečisté ukončenie predchádzajúceho behu".
- **Pozor pri interpretácii logu:** republish workflow používa `Stop-Process`, čo je nečistý
  kill — WARN záznamy z časov vývoja/republishov nie sú skutočné crashe používateľa.

## Automatické aktualizácie (self-update z GitHub Releases)

Implementované podľa [FEATURE-auto-update.md](FEATURE-auto-update.md). Repo `robkrzn/StandReminder`
je verejné → GitHub API bez autentifikácie.

**Workflow:** `App.Tick()` raz denne (a hneď pri štarte, keď `LastUpdateCheck` nie je dnešný)
volá `UpdateChecker.CheckAsync` — ak je novšia verzia a nie je „preskočená", otvorí `UpdateWindow`.
Tray položka „Skontrolovať aktualizácie…" vyvolá kontrolu manuálne (ignoruje throttle aj
`SkippedVersion`; pri žiadnej novšej verzii / chybe ukáže `MessageBox`). Po kliku **Aktualizovať**
sa zip stiahne, rozbalí a spustí sa odpojený PowerShell updater; appka spraví `ExitApp()`,
updater prepíše exe a reštartuje novú verziu.

**Potvrdenie po update:** pred reštartom sa do `PendingUpdateVersion` zapíše cieľový tag.
Nová verzia pri štarte (`App.ReportPendingUpdate`) tag vyhodnotí a vyčistí — ak bežiaca verzia
dosahuje/prekračuje cieľ, ukáže tmavý toast (`ToastWindow`) „Aktualizácia dokončená", inak
(kopírovanie zlyhalo, beží stará verzia) upozorní na nedokončený update. Toast sa použil namiesto
WinForms balloon tipu — ten je nespoľahlivý (Focus Assist ho zožerie, zlyhá hneď po vytvorení
ikony) a nesedel by s dizajnom. *Pozn.:* notifikácia sa zjaví len pri update **na** build, ktorý
túto logiku obsahuje (od verzie po zavedení featury, t. j. v1.0.4+). Aktuálnu verziu appky vidno
aj v hlavičke okna Nastavenia (`SettingsWindow`, kľúč `SetVersion`).

**Kľúčové rozhodnutia / pasce:**
- **Bežiaci `.exe` sa nevie prepísať sám** (Windows ho drží zamknutý) → preto detached
  `powershell.exe` skript, ktorý `Wait-Process` na PID, až potom kopíruje (s 10× retry) a reštartuje.
- **Vždy sa sťahuje `*-standalone.zip`** (self-contained, ~66 MB) — funguje bez ohľadu na to,
  či je na PC .NET runtime, takže update nikdy nerozbije appku. Appka nevie spoľahlivo zistiť,
  ktorý variant beží, preto sa standalone berie vždy.
- **Verzie:** vlastná = `Assembly.GetName().Version` (z csproj `<Version>` → `1.0.3.0`), tag
  „v1.0.4" → strip „v" → `Version.Parse`. Pred porovnaním sa oba normalizujú (nedefinované
  Build/Revision `-1` → `0`), takže `1.0.4 > 1.0.3.0` aj `v1.0.3 == 1.0.3.0` (rovnaká → neponúkne).
- **Graceful degradation:** bez internetu / chyba API → `Failed`, ticho, neloguje sa ako chyba.
  Zlyhanie *inštalácie* (po kliku Aktualizovať) sa loguje (`CrashLog.Write ERROR`) a okno ukáže chybu.
- **Throttle:** `LastUpdateCheck` sa zapíše po **každom** pokuse (aj neúspešnom), takže auto-kontrola
  beží max 1×/deň aj keď zlyhá. Manuálna kontrola throttle nerešpektuje.
- **Test bez vydávania:** dočasne zníž lokálnu `<Version>` (napr. na `1.0.0`), aby `releases/latest`
  vyzeral ako novší a otestoval sa flow detekcie/sťahovania.
- **Otvorené (Fáza 3):** ak appka beží z chráneného priečinka (Program Files), `Copy-Item` zlyhá
  bez elevácie — zatiaľ updater po 10 retry vzdá kopírovanie a reštartuje pôvodnú verziu
  (per-user inštalácia cez HKCU autostart túto situáciu typicky nemá).

## Opravené chyby (história)

- **2026-06-11 — pád pri kliku na flyout:** WPF vyhodí `InvalidOperationException`, ak sa
  `Close()` zavolá na okno, ktoré sa práve zatvára. `StatusWindow.Deactivated` volal `Close()`
  a deaktivácia nastáva aj počas zatvárania (typicky klik na ⚙ → otvorenie nastavení
  deaktivuje flyout uprostred `Close()`). Fix: `StatusWindow.CloseSafe()` s `_isClosing`
  flagom (nastavuje sa aj v `OnClosing`); App volá výhradne `CloseSafe()`.

## Známe vlastnosti / obmedzenia

- Pracovný čas nesmie prechádzať cez polnoc (validácia: koniec > začiatok)
- `ReminderWindow` si načítava settings znova z disku (`AppSettings.Load()`) — drobná
  nekonzistencia, ale settings sa ukladajú pri každej zmene, takže neškodí
- Zvuk je len `SystemSounds.Exclamation`
- Ikony cez `Icon.FromHandle(bmp.GetHicon())` — GDI handle sa neuvoľňuje, ale vytvárajú sa
  len 3× za život aplikácie, takže to nevadí

## Nápady na ďalší rozvoj (zatiaľ nerealizované)

- **Automatické aktualizácie — Fáza 3 (doladenie):** elevácia / fallback pri chránenom
  priečinku, voliteľné overenie hashu/podpisu stiahnutého assetu. Fázy 1–2 hotové —
  pozri sekciu *Automatické aktualizácie* a [FEATURE-auto-update.md](FEATURE-auto-update.md).
- Auto-dismiss pripomienky po X minútach + detekcia nečinnosti (používateľ nie je pri PC)
- Vlastné zvuky, voľba dňa v týždni (víkendy)

## Prompt na pokračovanie práce

Novú konverzáciu s AI asistentom začni skopírovaním tohto promptu (a doplň úlohu):

```text
Pracujem na projekte StandReminder (Windows tray app, sit/stand pripomienky).
Najprv si prečítaj dokumentáciu doc/PROJECT.md a riaď sa ňou — obsahuje
architektúru, konvencie (lokalizácia cez Core/Loc.cs, dizajnové tokeny,
štruktúru priečinkov), build/publish príkazy aj známe pasce.
Po každej významnej zmene dokumentáciu aktualizuj.
Po úprave over, že build prechádza. Nasadenie a spustenie aplikácie
(publish + reštart, postup v dokumentácii) rob LEN keď si ho vyžiadam —
pri viacerých zmenách za sebou nechcem reštartovať appku po každej.

Úloha: <sem napíš, čo ideme dorobiť>
```
