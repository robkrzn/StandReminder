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
# distribučný single-file exe (vyžaduje .NET 10 runtime na cieľovom PC):
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Výstup: `publish/StandReminder.exe`. Pred republish treba ukončiť bežiacu inštanciu
(`Stop-Process -Name StandReminder`), inak je exe zamknutý.

## Architektúra a súbory

| Súbor | Zodpovednosť |
|---|---|
| `App.xaml` | Globálne resources: farebná paleta, štýly `PrimaryButton`, `GhostButton`, `DarkSlider`, `DarkCheckBox`, `DarkCombo`. `ShutdownMode=OnExplicitShutdown`, žiadne StartupUri. |
| `App.xaml.cs` | **Jadro aplikácie.** Tray ikona + kontextové menu, stavový automat fáz, `DispatcherTimer` (1 s tick), kreslenie tray ikon cez GDI+, autostart cez registry, single-instance mutex (`StandReminder_SingleInstance`). |
| `AppSettings.cs` | Model nastavení + JSON load/save do `%APPDATA%\StandReminder\settings.json`. |
| `ReminderWindow.xaml(.cs)` | Popup notifikácia pri zmene fázy (pravý dolný roh, `Topmost`, `ShowActivated=False` — nekradne fokus). Tlačidlá: potvrdiť zmenu / `+5 min` snooze. Eventy `Accepted`, `Snoozed`. |
| `StatusWindow.xaml(.cs)` | Flyout po **ľavom kliku** na tray ikonu: aktuálna pozícia, uplynutý čas, progress bar, zostávajúci čas, tlačidlo ⚙ (nastavenia) a „Zmeniť pozíciu teraz". Zavrie sa pri `Deactivated`. Eventy `SettingsRequested`, `SwitchRequested`. |
| `SettingsWindow.xaml(.cs)` | Okno nastavení: slidery intervalov (sedenie 10–120 min, státie 5–60 min), pracovný čas (ComboBoxy 05:00–21:30 po 30 min), zvuk, autostart. Event `Saved`. Tmavý title bar cez `UiNative.UseDarkTitleBar` (volané v `SourceInitialized`). |
| `UiNative.cs` | P/Invoke na `dwmapi.dll` (`DwmSetWindowAttribute`): `UseDarkTitleBar` (attr 20) a `UseRoundedCorners` (attr 33, Windows 11) pre zaoblené rohy popupov. |
| `DarkMenuRenderer.cs` | Dark theme pre WinForms `ContextMenuStrip` tray menu — `ToolStripProfessionalRenderer` s vlastnou `ProfessionalColorTable`, zaoblený hover highlight, vlastné separátory. Farby zrkadlia WPF paletu. |
| `CrashLog.cs` | Crash logging + health check (pozri sekciu nižšie). |
| `Loc.cs` | Lokalizácia: statický slovník všetkých UI stringov ako dvojice `(sk, en)`, prístup cez `Loc.T(key)` / `Loc.F(key, args)`. `Loc.Lang` sa nastavuje z `AppSettings.Language` pri štarte a po uložení nastavení (vtedy sa volá aj `App.ApplyMenuLanguage()` na tray menu; okná si texty naplnia pri vytvorení). Žiadne .resx — pri pridávaní UI textu vždy pridaj kľúč do `Loc.cs`, nie literál do kódu/XAML. |

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
(Zmeniť pozíciu teraz / Pozastaviť / Nastavenia… / Ukončiť). Tooltip ikony ukazuje
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
  "Language": "sk"
}
```

- Časy sú stringy `"HH:mm"`, parsované cez `TimeSpan.TryParse` (properties `WorkStartTime/WorkEndTime`)
- Autostart: registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, hodnota `StandReminder`
- Po uložení nastavení sa aktuálna fáza reštartuje s novým intervalom

## Crash log a health check (CrashLog.cs)

- Log: `%APPDATA%\StandReminder\crash.log` — appenduje sa, formát `[timestamp] LEVEL správa` + stack trace
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

- Štatistiky: koľko času denne prestojím/presedím
- Auto-dismiss pripomienky po X minútach + detekcia nečinnosti (používateľ nie je pri PC)
- Vlastné zvuky, voľba dňa v týždni (víkendy)
