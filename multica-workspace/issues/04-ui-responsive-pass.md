# Responsive layout + navigation pass (Mica/Fluent intact)

Assignee: winui-mvvm | Priority: medium | Labels: area:ui

## Goal
Verify responsive behavior and navigation state across Home, Installed,
Updates, Details, Features, Optimizer, Settings, About (x86/x64/ARM64
assumptions, Mica backdrop, sort/filter state preserved).

## Context
- `Pages/*.xaml*`, `Controls/PackageProgressControl`, `Controls/ResponsivePageContainer.cs`, `Services/GridCalculator.cs`, `Services/NavigationHelper.cs`, `Pages/SortGlyphUpdater.cs`.
- ViewModels use `[ObservableProperty]` / `[RelayCommand]`; code-behind stays navigation/lifecycle-only.

## Acceptance criteria
- [ ] Narrow/wide layouts verified (or enumerated manual F5 steps) with no business logic in code-behind.
- [ ] Back-stack, sort-glyph, and filter state preserved across navigation.
- [ ] No new analyzer warnings; relevant `WingetStore.Tests` suites green.
- [ ] Comment with files changed + manual check steps (F5 path per page).

## Safety
UI-only. Hand service-internals findings to the owning agent via the
squad leader instead of reaching into Optimizer/ViVe/Winget internals.
