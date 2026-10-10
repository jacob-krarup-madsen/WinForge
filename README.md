# WinForge

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![WinUI 3](https://img.shields.io/badge/WinUI%203-Windows%20App%20SDK%202.4-blue.svg)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

**WinForge** is a 100% native **C# (.NET 9) and WinUI 3 (Windows App SDK)** PowerTools Suite unifying Windows package management, feature velocity control, and reversible system optimization into a single, high-performance desktop application.

## Features

- **Winget Package Store:** Browse curated applications, search the global winget repository, one-click install, batch upgrades, and uninstalls.
- **Feature Velocity Manager:** Discover, filter by release track, and batch enable or disable all 118 Windows 11 velocity feature flags with integrated ViVeTool support and live Pureinfotech catalog synchronization.
- **Native System Optimizer:** 25-phase Windows 11 performance & privacy optimization engine with pre-optimization baseline snapshotting, reversible restoration, Win32 working-set memory flushing (`EmptyWorkingSet`), reparse-point-safe disk & WinSxS cleanup, non-destructive 24-tweak system health auditing, CPU/RAM micro-benchmarking, and JSON profile import/export.

## Tech Stack

- **Language & Runtime:** C# 13, .NET 9 (`net9.0-windows10.0.26100.0`), strict nullable reference types & zero-warning Roslyn static analysis
- **UI Framework:** WinUI 3, Windows App SDK 2.4.0, CommunityToolkit.Mvvm, Mica backdrop, Fluent Design System
- **System Integration:** Native Win32 P/Invoke (`[LibraryImport]`), `Microsoft.Win32.Registry`, `System.Management` (WMI), `Windows.Management.Deployment.PackageManager`, Windows Package Manager (`winget`), and bundled ViVeTool CLI

## Prerequisites

Before building or running WinForge, ensure your host environment meets the following requirements:

1. **Operating System:** Windows 10 (build 19041+) or Windows 11 (build 22000+).
2. **.NET 9.0 SDK:** Required for compilation and runtime dependencies (`net9.0-windows10.0.26100.0`).
   Install via Windows Package Manager:
   ```powershell
   winget install Microsoft.DotNet.SDK.9
   ```
3. **Visual Studio 2022 (v17.12+) or Build Tools:**
   Include the following workloads:
   - *.NET Desktop Development*
   - *Windows application development* (Windows App SDK C# Templates)
4. **Developer Mode:** Recommended to enable unpacked AppX/WinUI 3 execution in Windows Settings (*Settings > System > For developers > Developer Mode*).

## Project Structure

```text
WinForge/
├── Directory.Build.props     # Solution-wide strict C# compiler & analyzer enforcement
├── Directory.Build.targets   # Solution-wide build targets
├── WinForge.sln              # Unified .NET 9 solution
└── src/
    └── WinForge/             # Native WinUI 3 desktop application
        ├── Models/           # Domain & ViVeTool feature models
        ├── Pages/            # WinUI 3 XAML pages
        ├── Services/         # Winget, ViVeTool, and native Optimizer services
        ├── Tools/            # Bundled ViVeTool.exe & Albacore.ViVe.dll
        ├── ViewModels/       # MVVM ViewModels
        └── WingetStore.Tests/# Comprehensive xUnit v3 test suite
```

## Build & Test Instructions

### Building via .NET CLI

Clone the repository and build in Debug or Release configuration:
```powershell
git clone https://github.com/jacob-krarup-madsen/WinForge.git
cd WinForge
dotnet restore WinForge.sln
dotnet build WinForge.sln -c Release
```

### Running Automated Tests

Run the full xUnit v3 test suite across the solution:
```powershell
dotnet test WinForge.sln -c Release --logger "console;verbosity=normal"
```

### Running the WinUI 3 Application

Launch the desktop client directly via `dotnet`:
```powershell
dotnet run --project src/WinForge/WinForge.csproj
```

Alternatively, open `WinForge.sln` in Visual Studio 2022, set `WinForge` as the startup project, and press **F5**.

## License

[MIT](LICENSE) © 2026 Jacob Krarup Madsen
