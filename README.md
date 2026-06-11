# StandReminder 🧍🪑

A lightweight Windows system tray app that reminds you to alternate between **sitting and standing** at a height-adjustable desk during your work hours.

> UI language is Slovak. Built with WPF / .NET 10, no external dependencies.

## Features

- ⏱️ Alternates sit/stand phases on a configurable schedule (default **45 min sitting / 15 min standing**, matching the commonly recommended 2:1–3:1 sit-to-stand ratio)
- 🕖 Active only during configurable work hours (default 07:00–16:00) — silent otherwise
- 🔔 Non-intrusive dark popup in the bottom-right corner when it's time to switch, with a **+5 min snooze** — it never steals keyboard focus
- 📊 Tray flyout (left-click the icon): current position, elapsed time, progress bar, time remaining
- 🎨 Tray icon shows your current position at a glance — blue sitting figure, green standing figure, gray when paused or outside work hours
- ⚙️ Settings: intervals, work hours, sound, start with Windows
- 🩺 Crash log + health check in `%APPDATA%\StandReminder\crash.log`

## Getting started

Requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/robkrzn/StandReminder.git
cd StandReminder
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
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
