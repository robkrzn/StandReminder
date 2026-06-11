# StandReminder 🧍🪑

Ľahká Windows aplikácia v systémovej lište, ktorá ti počas pracovného času pripomína striedať **sedenie a státie** pri výškovo nastaviteľnom stole.

> UI v slovenčine aj angličtine (prepínateľné v nastaveniach). Postavené na WPF / .NET 10, bez externých závislostí.

## Funkcie

- ⏱️ Striedanie fáz sedenia a státia podľa nastaviteľných intervalov (predvolene **45 min sedenie / 15 min státie**, čo zodpovedá odporúčanému pomeru sedenie : státie 2:1 až 3:1)
- 🕖 Aktívna len počas nastaveného pracovného času (predvolene 07:00–16:00) — mimo neho je ticho
- 🔔 Nenápadný tmavý popup v pravom dolnom rohu, keď je čas zmeniť pozíciu, s možnosťou **odložiť o 5 minút** — nikdy nekradne fokus klávesnice
- 📊 Flyout v lište (ľavý klik na ikonu): aktuálna pozícia, uplynutý čas, progress bar, zostávajúci čas
- 🎨 Ikona v lište ukazuje aktuálnu pozíciu na prvý pohľad — modrá sediaca postavička, zelená stojaca, sivá pri pauze alebo mimo pracovného času
- ⚙️ Nastavenia: intervaly, pracovný čas, zvuk, spustenie so štartom Windows, jazyk (slovenčina / angličtina)
- 🩺 Crash log + health check v `%APPDATA%\StandReminder\crash.log`

## Ako začať

Vyžaduje [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — alebo si stiahni standalone verziu z [Releases](https://github.com/robkrzn/StandReminder/releases), ktorá nepotrebuje nič.

```powershell
git clone https://github.com/robkrzn/StandReminder.git
cd StandReminder
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false -o publish
.\publish\StandReminder.exe
```

Aplikácia žije v systémovej lište:

| Akcia | Výsledok |
|---|---|
| Ľavý klik na ikonu | Stavový flyout (pozícia, odpočet, nastavenia) |
| Pravý klik na ikonu | Menu: zmeniť pozíciu, pauza, nastavenia, ukončiť |

Nastavenia sa ukladajú do `%APPDATA%\StandReminder\settings.json`.

## Vývoj

Architektúra a poznámky pre vývoj sú v [doc/PROJECT.md](doc/PROJECT.md).

```powershell
dotnet build -c Release
```

## Licencia

[MIT](LICENSE)

---

# English

A lightweight Windows system tray app that reminds you to alternate between **sitting and standing** at a height-adjustable desk during your work hours.

> UI available in Slovak and English (switchable in settings). Built with WPF / .NET 10, no external dependencies.

## Features

- ⏱️ Alternates sit/stand phases on a configurable schedule (default **45 min sitting / 15 min standing**, matching the commonly recommended 2:1–3:1 sit-to-stand ratio)
- 🕖 Active only during configurable work hours (default 07:00–16:00) — silent otherwise
- 🔔 Non-intrusive dark popup in the bottom-right corner when it's time to switch, with a **+5 min snooze** — it never steals keyboard focus
- 📊 Tray flyout (left-click the icon): current position, elapsed time, progress bar, time remaining
- 🎨 Tray icon shows your current position at a glance — blue sitting figure, green standing figure, gray when paused or outside work hours
- ⚙️ Settings: intervals, work hours, sound, start with Windows, language (Slovak / English)
- 🩺 Crash log + health check in `%APPDATA%\StandReminder\crash.log`

## Getting started

Requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — or grab the standalone build from [Releases](https://github.com/robkrzn/StandReminder/releases) which needs nothing.

```powershell
git clone https://github.com/robkrzn/StandReminder.git
cd StandReminder
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false -o publish
.\publish\StandReminder.exe
```

The app lives in the system tray:

| Action | Result |
|---|---|
| Left-click tray icon | Status flyout (position, countdown, settings) |
| Right-click tray icon | Menu: switch now, pause, settings, exit |

Settings are stored in `%APPDATA%\StandReminder\settings.json`.

## Development

Architecture and contributor notes live in [doc/PROJECT.md](doc/PROJECT.md) (in Slovak).

```powershell
dotnet build -c Release
```

## License

[MIT](LICENSE)
