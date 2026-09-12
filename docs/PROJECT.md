# Project: WinForge Architecture & Quality Assurance

WinForge is an enterprise-grade Windows PowerTools Suite combining native package management, Windows velocity features, and system optimization into a single, unified WinUI 3 desktop application.

## 1. System Architecture

```text
WinForge/
├── .github/                  # GitHub Actions (CI, CodeQL, Release, Stale), issue & PR templates
├── docs/                     # Technical specifications and architectural guides
├── src/
│   └── WinForge/             # Main WinUI 3 desktop application
│       ├── Controls/         # ResponsivePageContainer, PackageProgressControl
│       ├── Models/           # WingetPackage, InstallTask, FeatureItem, ViVeTool models
│       ├── Pages/            # HomePage, InstalledPage, UpdatesPage, FeaturesPage, OptimizerPage, SettingsPage, AboutPage
│       ├── Services/         # WingetService, IconService, SettingsService, CliProcessRunner, ViVeTool services
│       ├── Testing/          # UITestRunner in-app integration test harness
│       ├── ViewModels/       # MVVM ViewModels with CommunityToolkit.Mvvm
│       └── WingetStore.Tests/ # xUnit test suite (730+ automated tests)
└── tools/
    └── cli/                  # ViVeTool runner, catalogs, and WindowsOptimizer PowerShell engine
```

## 2. Core Modules

### 2.1 Winget Package Store
- **Catalog & Search:** Live discovery and searching through the official Microsoft Community Winget repositories.
- **Package Management:** One-click installations, upgrades, and uninstalls powered by non-blocking asynchronous stream runners (`CliProcessRunner`).
- **Metadata & Asset Caching:** Resilient icon resolution and caching via `IconService` stored under `%LOCALAPPDATA%\WingetStore\`.

### 2.2 Feature Velocity Manager (ViVeTool)
- **Windows 11 Velocity Features:** Unlocks, toggles, and tracks experimental and staging features across Canary, Dev, and GA Windows builds.
- **Offline Fallback Catalog:** Ships with built-in verified feature definitions for seamless offline discovery.
- **Process Orchestration:** Interacts safely with ViVeTool CLI binaries without freezing the main UI dispatcher.

### 2.3 System Optimizer
- **Modular PowerShell Engine:** 25-phase optimization suite including telemetry debloat, cache cleaning, working set flush, and performance tweaks.
- **Safety First:** Creates automatic Windows System Restore Points (`Checkpoint-Computer`) before executing critical tweaks.
- **Restore & Rollback:** Ships with full undo functionality (`Undo-Optimization.ps1`) to safely restore system defaults.

## 3. Testing Strategy

| Tier | Tooling | Coverage |
|------|---------|----------|
| **Unit Testing** | xUnit v3, Microsoft Testing Platform | 730+ tests exercising parsers, version comparing, model bindings, services, caching, process runners, and sanitizers. |
| **Integration Testing** | In-app `UITestRunner` (`WinForge.exe --run-ui-tests`) | Exercises live WinUI 3 page lifecycle, navigation, and XAML data templates. |
| **Security Hardening** | GitHub CodeQL (`csharp`) | Automated static analysis scanning for command injection, memory safety, and input sanitization on every push. |

## 4. Build & Run Contracts

- **TargetFramework:** `net9.0-windows10.0.26100.0`
- **Minimum OS:** Windows 10 (1809 / Build 17763)
- **Compilation Standard:** 0 errors, 0 warnings under standard `dotnet build WinForge.sln -c Release`

## 5. Primary Service Contracts

### 5.1 `IWingetService` & `IProcessRunner`
- `IProcessRunner`: Asynchronous line-by-line output streaming with cancellation token process tree termination.
- `IWingetService`: High-level operations for discovering packages, running winget queries, installing, upgrading, and querying system status.

### 5.2 `IconService`
- In-memory thread-safe dictionary cache with local image file cache in `%LOCALAPPDATA%\WingetStore\icons\`.
- URL candidate generation from package metadata with content-type and magic-byte header validation.

### 5.3 `IViVeToolRunner` & `IViVeToolLocator`
- Dynamic path discovery across environment PATH, application directory, and repository tool trees.
- Non-blocking execution of velocity commands (`/enable`, `/disable`) with structured result reporting.

## 6. Directory Structure
```text
WinForge/
├── .github/workflows/        # CI, CodeQL, Release, Stale
├── docs/                     # Documentation and architecture
├── src/WinForge/             # WinUI 3 desktop client
│   ├── Controls/
│   ├── Models/
│   ├── Pages/
│   ├── Services/
│   ├── Testing/
│   ├── ViewModels/
│   └── WingetStore.Tests/    # 730+ xUnit tests
└── tools/cli/                # Automation scripts, ViVeTool, WindowsOptimizer
```
