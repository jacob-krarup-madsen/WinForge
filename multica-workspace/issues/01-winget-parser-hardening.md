# Harden WingetParser against locale/version variance

Assignee: winget-core | Priority: high | Labels: area:winget

## Goal
Make `WingetParser` resilient to winget output differences across locales
and winget versions, with regression tests per format.

## Context
- Parsing lives ONLY in `src/WinForge/Services/WingetParser.cs`.
- CLI streaming via `IProcessRunner.RunStreamAsync` (`CliProcessRunner.cs`).
- Caching via `CachingWingetService.cs`; test doubles: `MockProcessRunner`,
  `StubWingetService`, `NullLineRunner`, `StatusOnlyLinesRunner`,
  `ThrowingWingetService`.

## Acceptance criteria
- [ ] Null/empty lines, status-only lines, and non-UTF8/encoding quirks handled without exceptions.
- [ ] New xUnit v3 cases in `WingetStore.Tests` per format variant (mirror `PackageFilteringHelper*Tests` / `VersionComparerEdgeCaseTests` style).
- [ ] No duplicate winget invocations introduced (caching intact).
- [ ] `dotnet build WinForge.sln -c Release` clean; relevant test suites green.
- [ ] Report posted as issue comment: variants covered + test results.

## Safety
Read-only work. `winget search/list/show` for diagnosis is fine; no
`install/upgrade/uninstall` against the real system.
