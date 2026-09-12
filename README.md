# WinForge

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![WinUI 3](https://img.shields.io/badge/WinUI%203-Windows%20App%20SDK%202.4-blue.svg)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

**WinForge** is a 100% native **WinUI 3 (Windows App SDK)** PowerTools Suite unifying Windows package management, feature velocity control, and system optimization into a single, high-performance desktop interface.

## Features

- **Winget Package Store:** Browse curated applications, search the global winget repository, one-click install, batch upgrades, and uninstalls.
- **Feature Velocity Manager:** Discover, search, and enable or disable Windows 11 velocity feature flags with integrated ViVeTool support and offline catalog.
- **System Optimizer:** Telemetry debloat, cache and disk cleaner, memory working-set flusher, privacy audit, and restore point integration.

## Tech Stack

- **Language & Runtime:** C# 12, .NET 9 (`net9.0-windows10.0.26100.0`)
- **UI Framework:** WinUI 3, Windows App SDK 2.4.0, CommunityToolkit.Mvvm, Mica backdrop, Fluent Design System
- **CLI & Automation:** Windows Package Manager (`winget`), ViVeTool CLI, PowerShell 7/5.1 engine

## Project Structure

```text
WinForge/
├── src/
│   └── WinForge/             # Native WinUI 3 application & xUnit test suite (740+ tests)
│       └── WingetStore.Tests/
└── tools/
    └── cli/                  # ViVeTool, feature catalogs, and PowerShell optimizer suite
```

## Quick Start

```bash
git clone https://github.com/jacob-krarup-madsen/WinForge.git
cd WinForge
dotnet build WinForge.sln -c Release
dotnet test WinForge.sln
```

Run the application directly:
```bash
dotnet run --project src/WinForge
```

## License

[MIT](LICENSE) © 2026 Jacob Krarup Madsen
