---
description: Run WinForge xUnit v3 suite and triage failures
agent: dotnet-quality
---

Run tests and triage to owning agent:

1. Run `dotnet test WinForge.sln -c Release --logger "console;verbosity=normal" $ARGUMENTS`
2. Report pass/fail counts, failing suites, and route: winget parsing → @winget-core, ViVe → @vive-features, optimizer → @optimizer-safety, ViewModel/UI helper → @winui-mvvm.
3. Never claim green without full-suite or explicitly scoped filter run.
