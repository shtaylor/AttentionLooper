# AttentionLooper

A minimal desktop app that plays a periodic chime to help you maintain focus and awareness of passing time. Set an interval, pick a sound, and let it run in the background.

![Windows](https://img.shields.io/badge/platform-Windows-blue)

## Features

- **Periodic chime** -- set any interval (hours, minutes, seconds) and a sound plays on repeat
- **Sound selection** -- ships with wood-block and chime sounds; drop additional `.mp3` or `.wav` files into the `AudioLibrary` folder and they appear automatically
- **Waveform display** -- visualizes the selected sound file with an animated ripple effect during playback
- **Volume control** -- slider with mute toggle
- **Dark / Light / System themes** -- switches instantly, follows system preference when set to System
- **Settings persistence** -- selected sound, volume, period, and theme are remembered across sessions
- **Compact UI** -- stays out of the way at 480x320

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for building from source)
- Windows 10 or later

### Build and Run

```
git clone <repo-url>
cd AttentionLooper
dotnet run --project src/AttentionLooper
```

### Publish a Portable Build

To produce a self-contained single-file executable that requires no .NET installation:

```
dotnet publish src/AttentionLooper/AttentionLooper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Output is in `src/AttentionLooper/bin/Release/net10.0-windows/win-x64/publish/`. Zip the `publish` folder to distribute -- the `AudioLibrary` subfolder must stay alongside the executable.

## Adding Custom Sounds

Place `.mp3` or `.wav` files in the `AudioLibrary` folder next to the executable (or in `src/AttentionLooper/AudioLibrary/` during development). Click the sound dropdown to see them. New files are picked up on the next dropdown open or app restart.

## Tech Stack

- **WPF** (.NET 10)
- **CommunityToolkit.Mvvm** -- MVVM source generators
- **NAudio** -- audio playback and waveform analysis

## License

See [LICENSE](LICENSE) for details.
