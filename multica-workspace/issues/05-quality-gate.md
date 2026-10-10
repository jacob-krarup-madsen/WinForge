# Quality gate: zero-warning Release + full xUnit v3 suite

Assignee: dotnet-quality | Priority: high | Labels: area:quality

## Goal
Bring the repo to a green gate and keep it there: `TreatWarningsAsErrors`
clean Release build + full test suite, failures triaged to owners.

## Context
- `Directory.Build.props`, `WinForge.sln`, `WinForge.csproj`, manifests.
- `WingetStore.Tests/*` (xUnit v3). Never commit `bin/` / `obj/`.

## Acceptance criteria
- [ ] `dotnet restore WinForge.sln` + `dotnet build WinForge.sln -c Release` clean (list warning IDs fixed, or confirm none).
- [ ] `dotnet test WinForge.sln -c Release --logger "console;verbosity=normal"` green; failures triaged to @winget-core / @vive-features / @optimizer-safety / @winui-mvvm.
- [ ] Report comment: commands run, pass/fail counts, new/updated tests, warnings fixed by ID.

## Safety
Build/test only. `dotnet run` / F5 only if the issue explicitly asks for
a live launch (Developer Mode + VS 2022 17.12+ prerequisites apply).
