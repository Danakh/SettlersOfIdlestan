# SettlersOfIdlestan .NET 10 Upgrade Tasks

## Overview

This document tracks execution of the atomic upgrade of the `SettlersOfIdlestan` solution from .NET 8.0 to .NET 10.0 per the Plan. All projects and package updates will be applied in a single coordinated operation, followed by testing and a final commit.

**Progress**: 1/4 tasks complete (25%) ![0%](https://progress-bar.xyz/25)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-04-19 13:11)*
**References**: Plan §Phase 0 (Prerequisite Validation)

- [✓] (1) Verify .NET 10 SDK is installed on the build machine per Plan §Phase 0
- [✓] (2) .NET 10 SDK version meets minimum requirements (**Verify**)
- [✓] (3) Validate `global.json` compatibility (update if present) per Plan §Phase 0
- [✓] (4) `global.json` compatible or updated (**Verify**)
- [✓] (5) Verify required NuGet packages are available for `net10.0` per Plan §Package Update Reference
- [✓] (6) Package availability confirmed for all updates (**Verify**)

### [▶] TASK-002: Atomic framework and package upgrade with compilation fixes
**References**: Plan §Phase 1 (Atomic Upgrade), Plan §Project-by-Project Plans, Plan §Package Update Reference, Plan §Breaking Changes Catalog

- [ ] (1) Update TargetFramework in all project files per Plan §Project-by-Project Plans:
  - `SettlersOfIdlestan\SettlersOfIdlestan.csproj`
  - `SettlersOfIdlestanGame\SettlersOfIdlestanGame.csproj`
  - `SettlersOfIdlestanSkia\SettlersOfIdlestanSkia.csproj`
  - `SettlersOfIdlestanDesktop\SettlersOfIdlestanDesktop.csproj`
  - `SettlersOfIdlestanWeb\SettlersOfIdlestanWeb.csproj`
  (Note: `SOITests.csproj` already targets `net10.0`)
- [ ] (2) All project files updated to `net10.0` where required (**Verify**)
- [ ] (3) Update PackageReference versions per Plan §Package Update Reference (apply all package updates simultaneously; key updates: `Microsoft.AspNetCore.Components.WebAssembly`, `Microsoft.AspNetCore.Components.WebAssembly.DevServer`, `Microsoft.Extensions.Logging.Debug`)
- [ ] (4) All PackageReference version attributes updated (**Verify**)
- [ ] (5) Run `dotnet restore` for the solution
- [ ] (6) All dependencies restored successfully (**Verify**)
- [ ] (7) Build solution to identify compilation errors: run `dotnet build` for the entire solution
- [ ] (8) Fix all compilation errors found, addressing source incompatibilities and breaking changes per Plan §Breaking Changes Catalog (notably `TimeSpan.FromSeconds`, `System.Text.Json` and `System.Uri` changes)
- [ ] (9) Rebuild solution to verify fixes applied
- [ ] (10) Solution builds with 0 errors and 0 warnings (**Verify**)

### [ ] TASK-003: Run test suite and validate upgrade
**References**: Plan §Phase 2 Testing, Plan §Testing & Validation Strategy, Plan §Breaking Changes Catalog

- [ ] (1) Run unit tests: `dotnet test SOITests\SOITests.csproj`
- [ ] (2) Fix any test failures (reference Plan §Breaking Changes Catalog for common issues)
- [ ] (3) Re-run `dotnet test` after fixes
- [ ] (4) All tests pass with 0 failures (**Verify**)

### [ ] TASK-004: Final commit
**References**: Plan §Source Control Strategy (Commit Strategy)

- [ ] (1) Commit all remaining changes with message: "TASK-004: Upgrade solution to .NET 10.0 — update project targets and package references; fix breaking changes"



