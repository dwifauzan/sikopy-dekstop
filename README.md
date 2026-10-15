# SIKOPY

A cross-platform application built with [Avalonia UI](https://avaloniaui.net/) and .NET 10.
One shared codebase runs on **Desktop** (Windows, macOS, Linux), **Android**, **iOS**, and **Browser** (WebAssembly).

> What the app does, its features and requirements are described in [docs/SPECIFICATION.md](docs/SPECIFICATION.md).

---

## Tech stack

| Area | Technology | Version |
|---|---|---|
| Runtime | .NET | 10.0 |
| UI framework | Avalonia UI (Fluent theme, Inter font) | 12.1.4 |
| MVVM | CommunityToolkit.Mvvm | 8.4.2 |
| Debug tooling | AvaloniaUI.DiagnosticsSupport (Debug builds only) | 2.2.3 |
| Package versions | NuGet Central Package Management (`Directory.Packages.props`) | — |

## Project structure

```
SIKOPY/
├── SIKOPY/                  # Shared app: all UI, ViewModels, logic — most work happens here
│   ├── App.axaml(.cs)       # Application entry, theme, picks window vs. single view per platform
│   ├── ViewLocator.cs       # Maps XxxViewModel -> XxxView by naming convention
│   ├── ViewModels/          # ViewModels (CommunityToolkit.Mvvm)
│   ├── Views/               # .axaml views + code-behind
│   └── Assets/              # Images, icons (embedded as AvaloniaResource)
├── SIKOPY.Desktop/          # Desktop launcher (Windows / macOS / Linux)
├── SIKOPY.Android/          # Android launcher (min API 23)
├── SIKOPY.iOS/              # iOS launcher (min iOS 15.0)
├── SIKOPY.Browser/          # WebAssembly launcher (wwwroot/ holds the host page)
├── docs/                    # Architecture & specification docs
├── Directory.Packages.props # Central NuGet package versions
├── SIKOPY.sln / SIKOPY.slnx # Solution files
└── CONTRIBUTING.md          # How to contribute
```

The platform projects are thin launchers. **Put features in `SIKOPY/`**, not in the platform projects, unless the code really is platform-specific.
See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for details.

## Prerequisites

### Everyone

1. **.NET 10 SDK.** Check it with `dotnet --version` (should print `10.0.x`).
   Download: https://dotnet.microsoft.com/download/dotnet/10.0
2. **An IDE.** JetBrains Rider (recommended), Visual Studio 2026, or VS Code + C# Dev Kit.
   For `.axaml` previews install the Avalonia plugin/extension for your IDE.
3. **Workloads** for the mobile targets:
   ```bash
   dotnet workload install android ios
   # optional, for the Browser target:
   dotnet workload install wasm-tools
   ```

### Android (any OS)

| Requirement | Notes |
|---|---|
| **JDK 21** | .NET Android does **not** support newer JDKs (for example JDK 26 fails with `XA0030`). Android Studio bundles one: `/Applications/Android Studio.app/Contents/jbr/Contents/Home` (macOS). |
| **Android SDK platform 36** | `android-36` exactly, because `android-36.1` alone is not enough. Install it with the command below. |

```bash
dotnet build SIKOPY.Android/SIKOPY.Android.csproj -t:InstallAndroidDependencies -f net10.0-android \
  "-p:AndroidSdkDirectory=$HOME/Library/Android/sdk" \
  "-p:JavaSdkDirectory=/Applications/Android Studio.app/Contents/jbr/Contents/Home" \
  -p:AcceptAndroidSDKLicenses=true
```
(Adjust both paths for your OS and where you installed things.)

**Rider:** *Settings → Build, Execution, Deployment → Android* and set **Java Development Kit location** to your JDK 21.

### iOS (macOS only)

Windows and Linux developers cannot build the iOS target, so they can skip this section.

```bash
# 1. Install Xcode from the App Store, then point the toolchain at it (not the Command Line Tools):
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
# 2. Accept the Xcode license:
sudo xcodebuild -license accept
# 3. Install the iOS simulator runtime:
xcodebuild -downloadPlatform iOS
```

## Getting started

```bash
git clone <repo-url>
cd SIKOPY
dotnet restore
dotnet run --project SIKOPY.Desktop     # quickest way to see the app
```

Open `SIKOPY.sln` (or `SIKOPY.slnx`) in your IDE.

## Running each platform

| Platform | Command | Notes |
|---|---|---|
| Desktop | `dotnet run --project SIKOPY.Desktop` | Fastest dev loop. Use this for most UI work. |
| Browser | `dotnet run --project SIKOPY.Browser` | Serves at `https://localhost:7169` / `http://localhost:5235`. |
| Android | `dotnet build SIKOPY.Android -t:Run -f net10.0-android` | Needs a running emulator or connected device. |
| iOS | `dotnet build SIKOPY.iOS -t:Run -f net10.0-ios` | macOS only. Starts the simulator. |

In Rider/Visual Studio you can also pick the startup project and press Run.

**Debug tools:** in Debug builds, press **F12** in the running app to open Avalonia DevTools (visual tree, styles, bindings).

## Documentation

| Document | Contents |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the app is structured, startup flow, MVVM, how to add screens |
| [CONTRIBUTING.md](docs/CONTRIBUTING.md) | Git workflow, coding conventions |
