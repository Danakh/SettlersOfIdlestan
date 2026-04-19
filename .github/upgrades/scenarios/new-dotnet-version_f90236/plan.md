# .NET 10 Migration Plan

**Solution:** SettlersOfIdlestan  
**Target Framework:** .NET 10.0 (Long Term Support)  
**Current Branch:** upgrade-to-NET10  
**Created:** $(date)

---

## Table of Contents

- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Risk Management](#risk-management)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

---

## Executive Summary

### Scope Overview

This plan upgrades the **SettlersOfIdlestan** solution from **.NET 8.0** to **.NET 10.0** in a single atomic operation.

| Metric | Value |
|--------|-------|
| **Total Projects** | 6 |
| **Projects Requiring Upgrade** | 5 |
| **Projects Already on .NET 10** | 1 (SOITests) |
| **Estimated Complexity** | 🟢 Low |
| **Migration Approach** | All-At-Once (atomic) |
| **Primary Challenge** | API behavioral changes (13 occurrences), source incompatibilities (5 occurrences) |
| **Package Updates Required** | 5 out of 10 packages |
| **Estimated Code Impact** | 18+ LOC (~0.3% of codebase) |

### Solution Classification

**✅ Simple Solution** - Ideal for All-At-Once Strategy:
- **6 projects** - small solution size
- **Linear dependency structure** - no circular dependencies or complex relationships
- **Low API impact** - behavioral changes are primary concern, not binary incompatibilities
- **Limited package updates** - 5 updates across multiple projects
- **Good test coverage** - dedicated test project available (SOITests)

### Key Findings from Assessment

**Mandatory Issues:**
- All 5 projects (except SOITests) require target framework updates to net10.0

**Primary API Challenges:**
- `System.Text.Json.JsonDocument` - 8 behavioral change occurrences (44.4%)
- `System.TimeSpan.FromSeconds()` - 5 source incompatibilities (27.8%)
- `System.Uri` - 3 behavioral change occurrences (16.7%)

**Package Updates Required:**
- `Microsoft.AspNetCore.Components.WebAssembly`: 8.0.0 → 10.0.6 (SettlersOfIdlestanGame)
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer`: 8.0.0 → 10.0.6 (SettlersOfIdlestanGame)
- `Microsoft.AspNetCore.Components.WebAssembly`: 10.0.5 → 10.0.6 (SettlersOfIdlestanWeb)
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer`: 10.0.5 → 10.0.6 (SettlersOfIdlestanWeb)
- `Microsoft.Extensions.Logging.Debug`: 10.0.0 → 10.0.6 (SettlersOfIdlestanDesktop)

### Selected Strategy: All-At-Once

**Rationale:**
- Small, cohesive solution with clear dependency structure
- All dependencies available for target framework version
- No blocking issues or complex multi-targeting requirements
- Enables rapid, coordinated upgrade with single validation phase
- Simpler source control strategy (one commit for entire upgrade)

**Approach:**
- Update all project files simultaneously to net10.0
- Update all package references in single operation
- Restore dependencies and build entire solution at once
- Fix all compilation errors in single pass
- Execute comprehensive test validation

**Benefits:**
- ✅ Fastest completion time
- ✅ No intermediate build states to manage
- ✅ Clear dependency resolution
- ✅ Simpler merge strategy
- ✅ All projects benefit simultaneously

---

## Migration Strategy

### Approach: All-At-Once Atomic Upgrade

**Philosophy:** Update all projects in a single coordinated operation with no intermediate states.

**Key Characteristics:**
- ✅ All projects updated simultaneously to net10.0
- ✅ Single unified package upgrade pass
- ✅ One comprehensive build and fix cycle
- ✅ Single test validation phase
- ✅ One source control commit (after completion)

### Execution Phases

#### Phase 0: Prerequisite Validation
**Objective:** Ensure environment is ready for upgrade

**Activities:**
1. Verify .NET 10 SDK is installed on build machine
2. Validate global.json compatibility (if present)
3. Confirm all dependencies are available for net10.0
4. Back up current solution state

**Success Criteria:**
- ✅ SDK version supports net10.0 compilation
- ✅ No environmental blockers identified

#### Phase 1: Atomic Upgrade Operation
**Objective:** Execute all framework and package updates simultaneously

**Activities:**
1. **Update all project files (TargetFramework)**
   - SettlersOfIdlestan.csproj: net8.0 → net10.0
   - SettlersOfIdlestanGame.csproj: net8.0 → net10.0
   - SettlersOfIdlestanSkia.csproj: net8.0 → net10.0
   - SettlersOfIdlestanDesktop.csproj: Update net8.0 references to net10.0 in multi-target string
   - SettlersOfIdlestanWeb.csproj: net8.0 → net10.0
   - SOITests.csproj: Already net10.0 (no change needed)

2. **Update all NuGet packages simultaneously**
   - Apply all 5 recommended package upgrades (see Package Update Reference)
   - Maintain compatible versions across all projects

3. **Restore and Build**
   - Run `dotnet restore` for entire solution
   - Run `dotnet build` for entire solution
   - Capture all compilation errors

4. **Fix Compilation Errors**
   - Address API behavioral changes (System.Text.Json, TimeSpan, Uri)
   - Address source incompatibilities (TimeSpan.FromSeconds signature)
   - Update affected code per Breaking Changes Catalog

5. **Rebuild and Verify**
   - Run `dotnet build` again to confirm all fixes applied
   - Verify solution builds with 0 errors, 0 warnings

**Success Criteria:**
- ✅ All 6 projects target net10.0 (or compatible net8.0/net10.0 multi-target)
- ✅ All 5 package updates applied
- ✅ Solution builds without errors
- ✅ Solution builds without warnings

#### Phase 2: Test Validation
**Objective:** Ensure upgrade didn't introduce regressions

**Activities:**
1. Execute all tests in SOITests.csproj
2. Verify test pass rate matches baseline
3. Manual smoke test of applications (if applicable)

**Success Criteria:**
- ✅ All tests pass
- ✅ No new test failures introduced
- ✅ Application functionality verified

### Rationale for All-At-Once

**Why simultaneous upgrade is optimal for this solution:**

1. **Small Solution Size** - 6 projects fit easily into single operation
2. **Clear Dependencies** - No circular relationships or complex orchestration
3. **Cohesive Codebase** - All projects target similar frameworks
4. **Limited API Risk** - Issues are behavioral changes, not blockers
5. **Good Test Coverage** - Comprehensive tests available for validation
6. **Simpler Source Control** - One commit encompasses entire upgrade

### Parallel vs Sequential Execution

**Execution will be sequential within each category:**
- All file updates can be done in any order (no dependencies)
- Package updates must be coordinated to ensure consistency
- Build is sequential (dependencies require bottom-up compilation)
- Testing is sequential (depends on successful build)

### Risk Mitigation in All-At-Once Approach

**Mitigation Strategies:**
- ✅ Pre-migration backup ensures rollback capability
- ✅ Clean dependency structure prevents cascading failures
- ✅ Comprehensive test project validates regression
- ✅ Source control isolation (dedicated upgrade branch)
- ✅ Staged validation (build → test → merge)

---

## Detailed Dependency Analysis

### Dependency Graph Structure

The solution follows a **clean, layered architecture** with three dependency levels:

```
Level 0 (Foundation)
├─ SettlersOfIdlestan.csproj (ClassLibrary)
│  └─ Core business logic library
│  └─ No dependencies on other projects
│  └─ Used by: SettlersOfIdlestanGame, SettlersOfIdlestanSkia, SOITests

Level 1 (Direct Consumers)
├─ SettlersOfIdlestanGame.csproj (AspNetCore/Blazor WebAssembly)
│  └─ Depends on: SettlersOfIdlestan
├─ SettlersOfIdlestanSkia.csproj (ClassLibrary)
│  └─ Depends on: SettlersOfIdlestan
│  └─ Used by: SettlersOfIdlestanDesktop, SettlersOfIdlestanWeb
└─ SOITests.csproj (Test Project)
   └─ Depends on: SettlersOfIdlestan
   └─ Already on .NET 10.0 ✅

Level 2 (Application Layer)
├─ SettlersOfIdlestanDesktop.csproj (MAUI Desktop App)
│  └─ Depends on: SettlersOfIdlestanSkia
└─ SettlersOfIdlestanWeb.csproj (Blazor Web)
   └─ Depends on: SettlersOfIdlestanSkia
```

### Migration Order (All-At-Once)

Since this is an **All-At-Once upgrade**, all projects are updated simultaneously without phasing. However, understanding the dependency structure is important for validation:

**Group 1 (Foundation - 1 project):**
- SettlersOfIdlestan.csproj

**Group 2 (Consumers - 3 projects):**
- SettlersOfIdlestanGame.csproj
- SettlersOfIdlestanSkia.csproj
- SOITests.csproj

**Group 3 (Applications - 2 projects):**
- SettlersOfIdlestanDesktop.csproj
- SettlersOfIdlestanWeb.csproj

### No Circular Dependencies

✅ **Clean dependency flow** - All dependencies flow downward with no cycles:
- No project depends on projects that depend on it
- Safe for simultaneous upgrade
- No orchestration complexity needed

### Critical Path

**Foundation Project (must validate first):**
- SettlersOfIdlestan.csproj - Core library with highest API compatibility risk (14 API issues)

**Top-Level Applications (final validation):**
- SettlersOfIdlestanDesktop.csproj
- SettlersOfIdlestanGame.csproj
- SettlersOfIdlestanWeb.csproj

**Test Validation:**
- SOITests.csproj - Already on .NET 10.0, used for regression validation

---

## Project-by-Project Plans

### SettlersOfIdlestan.csproj (Foundation Library)

**Current State**
- **Framework:** net8.0
- **Type:** ClassLibrary
- **Dependants:** 3 (SettlersOfIdlestanGame, SettlersOfIdlestanSkia, SOITests)
- **Files:** 52 | **LOC:** 4,155 | **Issues:** 15 (1 mandatory)
- **Risk Level:** 🟡 Medium (highest API complexity in solution)

**Target State**
- **Framework:** net10.0
- **Expected Changes:** 14+ LOC impacted

**Migration Steps**

1. **Update project file**
   - Change: `<TargetFramework>net8.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
   - Location: SettlersOfIdlestan\SettlersOfIdlestan.csproj

2. **API Changes to Address**
   - [To be detailed in Breaking Changes Catalog]
   - Primary focus: System.TimeSpan.FromSeconds source incompatibility

3. **Package Updates**
   - No direct package dependencies in this project
   - Affected by dependency versions when consumed

4. **Testing**
   - Unit tests via SOITests.csproj
   - Validate all public API surfaces compile correctly

---

### SettlersOfIdlestanDesktop.csproj (MAUI Desktop Application)

**Current State**
- **Framework:** net8.0-windows10.0.19041.0;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0 (multi-target)
- **Type:** DotNetCoreApp (MAUI)
- **Dependencies:** SettlersOfIdlestanSkia
- **Files:** 11 | **LOC:** 169 | **Issues:** 2 (1 mandatory)
- **Risk Level:** 🟢 Low

**Target State**
- **Framework:** net8.0-windows10.0.19041.0;net10.0-ios;net10.0-maccatalyst;net10.0-windows
- **Expected Changes:** Minimal LOC impact

**Migration Steps**

1. **Update project file**
   - Update multi-target string to align with net10.0 Windows platform
   - Add: `net10.0-windows` to target string
   - Remove/consolidate: Duplicate `net10.0-windows10.0.19041.0` references

2. **Package Updates**
   - `Microsoft.Extensions.Logging.Debug`: 10.0.0 → 10.0.6

3. **Dependencies**
   - Depends on SettlersOfIdlestanSkia (validates after Skia upgrade)

4. **Testing**
   - Verify MAUI application builds for all target platforms
   - Test Windows platform specifically (net10.0-windows)

---

### SettlersOfIdlestanGame.csproj (Blazor WebAssembly)

**Current State**
- **Framework:** net8.0
- **Type:** AspNetCore (Blazor WebAssembly)
- **Dependencies:** SettlersOfIdlestan
- **Files:** 22 | **LOC:** 15 | **Issues:** 6 (1 mandatory)
- **Risk Level:** 🟢 Low (small LOC, API behavioral changes only)

**Target State**
- **Framework:** net10.0
- **Expected Changes:** 3+ LOC impacted

**Migration Steps**

1. **Update project file**
   - Change: `<TargetFramework>net8.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
   - Location: SettlersOfIdlestanGame\SettlersOfIdlestanGame.csproj

2. **Package Updates**
   - `Microsoft.AspNetCore.Components.WebAssembly`: 8.0.0 → 10.0.6
   - `Microsoft.AspNetCore.Components.WebAssembly.DevServer`: 8.0.0 → 10.0.6

3. **API Changes**
   - [To be detailed in Breaking Changes Catalog]
   - Focus: JsonDocument behavior changes

4. **Testing**
   - Build WebAssembly project and verify IL output is valid
   - Test Blazor component rendering if applicable

---

### SettlersOfIdlestanSkia.csproj (Skia Library)

**Current State**
- **Framework:** net8.0
- **Type:** ClassLibrary
- **Dependencies:** SettlersOfIdlestan
- **Dependants:** 2 (SettlersOfIdlestanDesktop, SettlersOfIdlestanWeb)
- **Files:** 1 | **LOC:** 7 | **Issues:** 1 (1 mandatory)
- **Risk Level:** 🟢 Low (minimal code)

**Target State**
- **Framework:** net10.0
- **Expected Changes:** Minimal

**Migration Steps**

1. **Update project file**
   - Change: `<TargetFramework>net8.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
   - Location: SettlersOfIdlestanSkia\SettlersOfIdlestanSkia.csproj

2. **Dependencies**
   - SkiaSharp 3.119.2 - Already compatible, no update needed

3. **Testing**
   - Verify library compiles and loads correctly
   - Test in dependent projects (Desktop, Web)

---

### SettlersOfIdlestanWeb.csproj (Blazor Web)

**Current State**
- **Framework:** net8.0
- **Type:** AspNetCore (Blazor Web)
- **Dependencies:** SettlersOfIdlestanSkia
- **Files:** 14 | **LOC:** 11 | **Issues:** 4 (1 mandatory)
- **Risk Level:** 🟢 Low (small LOC, 1 behavioral change)

**Target State**
- **Framework:** net10.0
- **Expected Changes:** 1+ LOC impacted

**Migration Steps**

1. **Update project file**
   - Change: `<TargetFramework>net8.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
   - Location: SettlersOfIdlestanWeb\SettlersOfIdlestanWeb.csproj

2. **Package Updates**
   - `Microsoft.AspNetCore.Components.WebAssembly`: 10.0.5 → 10.0.6
   - `Microsoft.AspNetCore.Components.WebAssembly.DevServer`: 10.0.5 → 10.0.6

3. **API Changes**
   - [To be detailed in Breaking Changes Catalog]
   - Review URI handling changes

4. **Testing**
   - Build Blazor Web project
   - Verify web components load and render correctly

---

### SOITests.csproj (Test Project)

**Current State**
- **Framework:** net10.0 ✅ Already upgraded
- **Type:** DotNetCoreApp (xUnit test project)
- **Dependencies:** SettlersOfIdlestan
- **Files:** 18 | **LOC:** 1,666 | **Issues:** 0
- **Risk Level:** ✅ None (already on target framework)

**Target State**
- **Framework:** net10.0 (no change)
- **Status:** Ready for validation

**Testing Role**
- Comprehensive test suite for regression validation
- Tests SettlersOfIdlestan.csproj API surface
- Validates API behavioral changes don't break expected functionality

**Note:** No code changes required in test project itself, but tests will validate the upgraded core library.

---

## Package Update Reference

### NuGet Package Updates Summary

**Total Packages to Update:** 5 out of 10  
**Status:** 5 compatible, 0 incompatible  
**Approach:** Update all simultaneously in single operation

### Package Details

| Package | Current | Target | Affected Projects | Reason | Impact |
|---------|---------|--------|-------------------|--------|--------|
| Microsoft.AspNetCore.Components.WebAssembly | 8.0.0 | 10.0.6 | SettlersOfIdlestanGame | Framework compatibility | High - WebAssembly runtime |
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.5 | 10.0.6 | SettlersOfIdlestanWeb | Version alignment | Medium - Minor version update |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 8.0.0 | 10.0.6 | SettlersOfIdlestanGame | Framework compatibility | Medium - Dev-time only |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 10.0.5 | 10.0.6 | SettlersOfIdlestanWeb | Version alignment | Medium - Dev-time only |
| Microsoft.Extensions.Logging.Debug | 10.0.0 | 10.0.6 | SettlersOfIdlestanDesktop | Version alignment | Low - Extension package |

### Compatible (No Update Required)

| Package | Version | Affected Projects | Status |
|---------|---------|-------------------|--------|
| Microsoft.Maui.Controls | (latest) | SettlersOfIdlestanDesktop | ✅ Fully compatible with net10.0 |
| Microsoft.NET.Test.Sdk | 17.10.0 | SOITests | ✅ Fully compatible with net10.0 |
| SkiaSharp | 3.119.2 | SettlersOfIdlestanSkia | ✅ Fully compatible with net10.0 |
| xunit | 2.5.0 | SOITests | ✅ Fully compatible with net10.0 |
| xunit.runner.visualstudio | 2.5.0 | SOITests | ✅ Fully compatible with net10.0 |

### Update Strategy

**All package updates will be applied simultaneously:**
1. During atomic upgrade phase, all PackageReference version attributes updated
2. `dotnet restore` will fetch new versions
3. Build validation will confirm compatibility

**Breaking Changes by Package:**
- ✅ **Blazor 10.0.6** - No major breaking changes, incremental updates from 8.0.0 or 10.0.5
- ✅ **Logging.Debug 10.0.6** - Fully compatible with earlier 10.0.x versions
- ✅ **MAUI Controls** - .NET 10 support is native, no breaking changes
- ✅ **Test packages** - No breaking changes expected

---

## Breaking Changes Catalog

### Overview

This section documents expected breaking changes and behavioral changes when upgrading to .NET 10.0.

**Key Statistics from Assessment:**
- ✅ **0 Binary Incompatibilities** - No APIs were removed entirely
- 🟡 **5 Source Incompatibilities** - Require code changes to compile
- 🔵 **13 Behavioral Changes** - May require runtime validation
- ✅ **4,897 Compatible APIs** - Vast majority of APIs unchanged

### Source Incompatibilities (Require Code Changes)

#### 1. System.TimeSpan.FromSeconds() Signature Change
**Severity:** 🟡 Medium | **Occurrences:** 5 | **Affected Projects:** SettlersOfIdlestan

**Issue:**
The `TimeSpan.FromSeconds()` method signature has changed in .NET 10.

**Current Usage Pattern (net8.0):**
```csharp
// Likely pattern in current code
double seconds = ...;
TimeSpan span = TimeSpan.FromSeconds(seconds);
```

**Required Change:**
Verify that all `TimeSpan.FromSeconds()` calls pass numeric types (double, int, etc.) and not derived types or special values.

**Action Items:**
- [ ] Search codebase for `TimeSpan.FromSeconds` usage
- [ ] Verify all call sites pass compatible numeric types
- [ ] Update any calls with incompatible type conversions
- [ ] Recompile to verify changes

**Files to Review:**
- Check SettlersOfIdlestan.csproj code for TimeSpan usage

---

### Behavioral Changes (May Require Runtime Validation)

#### 1. System.Text.Json.JsonDocument Behavior
**Severity:** 🔵 Low | **Occurrences:** 8 | **Affected Projects:** SettlersOfIdlestan, SettlersOfIdlestanGame, SettlersOfIdlestanWeb

**Issue:**
JSON parsing behavior has changed in .NET 10. Document parsing, serialization options, or error handling may behave differently.

**Expected Behaviors:**
- More strict validation of JSON structure
- Different error messages for invalid JSON
- Potential changes in whitespace handling
- Unicode handling improvements

**Mitigation:**
- [ ] Review any JSON file parsing code
- [ ] Test with actual JSON data from production
- [ ] Verify error handling still works correctly
- [ ] Check if any edge cases need adjustment
- [ ] Validate serialization/deserialization output

**Recommendation:** Run integration tests that exercise JSON parsing paths.

---

#### 2. System.Uri Behavior Changes
**Severity:** 🔵 Low | **Occurrences:** 3 | **Affected Projects:** SettlersOfIdlestanGame, SettlersOfIdlestanWeb

**Issue:**
URI parsing and handling has been refined in .NET 10.

**Specific Changes:**
- More strict RFC compliance for URI validation
- Changes to handling of relative URIs
- Different behavior for special characters in URIs

**Affected APIs:**
- `Uri.#ctor(String)` constructor
- URI parsing methods
- Relative/absolute URI determination

**Mitigation:**
- [ ] Identify all URI construction code
- [ ] Validate URIs used in the application
- [ ] Test with actual application URLs
- [ ] Check for any relative URL handling
- [ ] Verify query string parsing still works

**Recommendation:** Execute smoke tests for any web/URL-related functionality.

---

#### 3. System.Text.Json.JsonSerializer.Deserialize() Behavior
**Severity:** 🔵 Low | **Occurrences:** 1 | **Affected Projects:** SettlersOfIdlestan

**Issue:**
Deserialization behavior with JsonSerializerOptions has changed.

**Potential Changes:**
- Default options handling
- Type converter behavior
- Error reporting on deserialization failure

**Mitigation:**
- [ ] Review JsonSerializerOptions usage
- [ ] Verify deserialization still produces expected results
- [ ] Check error handling for malformed JSON
- [ ] Test with real data

---

### No Blocking Issues Found

✅ **No Binary Incompatibilities** - No APIs were removed  
✅ **No Circular Dependencies** - Clean upgrade path  
✅ **All Packages Available** - No blockers on package availability  
✅ **Small LOC Impact** - Only 18+ lines estimated to change  

### Validation Strategy for Breaking Changes

**Compile-Time Validation:**
1. Build solution → Compiler will identify source incompatibilities
2. Fix TimeSpan.FromSeconds() issues as compiler directs
3. Rebuild until 0 errors

**Runtime Validation:**
1. Run SOITests.csproj tests → Validates core library behavior
2. Manual smoke tests for JSON parsing paths
3. Manual smoke tests for URL/URI handling
4. Check application initialization and basic workflows

**Recommended Testing Checklist:**
- [ ] Solution builds without errors
- [ ] Solution builds without warnings
- [ ] All unit tests pass
- [ ] Application starts successfully
- [ ] Basic user workflows execute
- [ ] JSON-based operations work correctly
- [ ] URI operations work correctly

---

## Complexity & Effort Assessment

### Complexity Ratings by Project

| Project | Complexity | Risk | Effort | Justification |
|---------|-----------|------|--------|----------------|
| SettlersOfIdlestan | 🟡 Medium | 🟡 Medium | Standard | 4,155 LOC, 14 API issues, foundation library |
| SettlersOfIdlestanDesktop | 🟢 Low | 🟢 Low | Minimal | 169 LOC, mostly MAUI framework updates |
| SettlersOfIdlestanGame | 🟢 Low | 🟢 Low | Minimal | 15 LOC, framework update + package version bump |
| SettlersOfIdlestanSkia | 🟢 Low | 🟢 Low | Minimal | 7 LOC, framework update only |
| SettlersOfIdlestanWeb | 🟢 Low | 🟢 Low | Minimal | 11 LOC, framework update + package version bump |
| SOITests | ✅ None | ✅ None | None | Already on net10.0, no changes needed |

### Overall Solution Assessment

**Solution Complexity:** 🟢 **LOW**

**Justification:**
- **Small Solution:** 6 projects, ~6,000 LOC total
- **Simple Dependencies:** Linear hierarchy, no cycles
- **Low API Risk:** No binary incompatibilities, mostly behavioral changes
- **Limited Updates:** 5 package updates, all available
- **Good Test Coverage:** Comprehensive xUnit test project available
- **Straightforward Patterns:** Standard Blazor, MAUI, library structure

### Effort Estimation

**Relative Effort Breakdown:**

| Activity | Relative Effort | Duration | Notes |
|----------|-----------------|----------|-------|
| **Project File Updates** | Very Low | Quick | Update 5 TargetFramework entries |
| **Package Updates** | Very Low | Quick | Edit 5 PackageReference versions |
| **Dependency Restore** | Very Low | Auto | `dotnet restore` handles it |
| **Compilation** | Low | Quick | 6 projects, small codebase |
| **Fix TimeSpan Issues** | Low | Quick | ~5 occurrences, straightforward fix |
| **JSON Parsing Review** | Medium | Moderate | Need code inspection + testing |
| **Unit Tests** | Low | Quick | Already exist, just run them |
| **Smoke Tests** | Low | Quick | Basic app functionality check |
| **Integration Validation** | Low | Quick | Verify project interactions work |

**Total Effort:** 🟢 **LOW** - Suitable for single upgrade session

### Resource Requirements

**Skill Level:** Mid-level developer
- Understanding of C# and .NET build system
- Familiar with project file structure
- Can navigate API documentation for changes
- Can run and interpret unit tests

**Tools Needed:**
- .NET 10 SDK installed
- Visual Studio 2022+ (or VS Code with C# extensions)
- Git for version control
- Access to NuGet package sources

**Estimated Team Size:** 1-2 developers

### Risk Assessment per Project

#### SettlersOfIdlestan.csproj - 🟡 MEDIUM RISK
**Risk Factors:**
- Foundation library used by 3 other projects
- Highest API complexity (14 API issues)
- TimeSpan source incompatibility must be fixed
- JSON parsing behavior changes need validation

**Mitigation:**
- ✅ Fix compilation errors immediately after build
- ✅ Run comprehensive unit tests in SOITests
- ✅ Code review for TimeSpan.FromSeconds() changes
- ✅ Manual testing of JSON parsing paths

**Impact if Failed:**
- 🔴 HIGH - All dependent projects won't compile

---

#### SettlersOfIdlestanDesktop.csproj - 🟢 LOW RISK
**Risk Factors:**
- MAUI multi-targeting adds complexity
- Need to verify Windows platform support
- Small codebase limits issues

**Mitigation:**
- ✅ Test build for each target platform
- ✅ Verify platform-specific code compiles
- ✅ Package version alignment with logging

**Impact if Failed:**
- 🟡 MEDIUM - Desktop app won't build/run, but web/game unaffected

---

#### SettlersOfIdlestanGame.csproj - 🟢 LOW RISK
**Risk Factors:**
- Major Blazor package update (8.0.0 → 10.0.6)
- Behavioral changes in JsonDocument
- Small codebase (15 LOC)

**Mitigation:**
- ✅ Package updates straightforward
- ✅ JsonDocument behavior review (limited code)
- ✅ Build and basic rendering test

**Impact if Failed:**
- 🟡 MEDIUM - WebAssembly app won't work, but core library OK

---

#### SettlersOfIdlestanSkia.csproj - 🟢 LOW RISK
**Risk Factors:**
- Minimal LOC (7 lines)
- No package updates needed
- Used by 2 downstream projects

**Mitigation:**
- ✅ Framework update only
- ✅ SkiaSharp compatibility already verified
- ✅ Compile check validates update

**Impact if Failed:**
- 🔴 HIGH - Blocks Desktop and Web projects

---

#### SettlersOfIdlestanWeb.csproj - 🟢 LOW RISK
**Risk Factors:**
- Minor package version updates
- URI behavioral changes possible
- Small codebase (11 LOC)

**Mitigation:**
- ✅ Package updates are patch versions
- ✅ URI handling review for edge cases
- ✅ Build and render test

**Impact if Failed:**
- 🟡 MEDIUM - Web app won't work, but core/desktop unaffected

---

#### SOITests.csproj - ✅ NO RISK
**Status:** Already on net10.0 ✅
**Role:** Validation mechanism
**Impact:** Validates upgrade success

---

## Risk Management

### High-Risk Items

| Item | Risk | Description | Mitigation |
|------|------|-------------|-----------|
| SettlersOfIdlestan TimeSpan API | 🟡 Medium | Source incompatibility must be fixed for compilation | Immediate code fix during build phase + code review |
| SettlersOfIdlestanSkia.csproj | 🟡 Medium | Foundation for Desktop/Web; if broken blocks 2 apps | Priority validation after update + focused testing |
| Blazor 10.0.6 Upgrade (Game) | 🟡 Medium | Major version jump (8.0.0 → 10.0.6) | Package compatibility verified + WebAssembly build validation |

### Contingency Plans

#### If Build Fails

**Scenario:** Compilation errors beyond TimeSpan issues

**Response:**
1. Capture detailed error messages
2. Match errors against Breaking Changes Catalog
3. Consult .NET 10 migration guide for specific error
4. Make targeted code fixes per error
5. Rebuild to verify fix
6. Repeat until 0 errors

**Escalation:** If errors don't match known patterns, consult .NET documentation or community resources

#### If Tests Fail

**Scenario:** Unit tests fail after upgrade

**Response:**
1. Review test failure messages in detail
2. Identify which component(s) are failing
3. Distinguish between test logic issues vs. actual code problems:
   - Test-only failures: Update test expectations/patterns
   - Code failures: Fix underlying implementation
4. Re-run tests to confirm fix

**Priority:**
- 🔴 Core library tests failing: Block release until fixed
- 🟡 Component tests failing: Investigate before release
- 🟢 UI/rendering tests: May be acceptable if functionality verified manually

#### If URI or JSON Issues Appear at Runtime

**Scenario:** Application runs but JSON/URI operations fail unexpectedly

**Response:**
1. Reproduce issue with minimal test case
2. Determine if issue is behavioral change or code defect
3. Review .NET 10 release notes for that specific API
4. Adjust code to work with new behavior or submit upstream issue

**Verification:**
- Test with production-like data
- Validate error messages changed but not behavior
- Consider adding regression tests for edge cases

#### If Package Compatibility Issues Arise

**Scenario:** Package update causes compatibility problems

**Response:**
1. Try updating to latest available version of package
2. If latest doesn't work, check package release notes
3. Revert to compatible version if necessary
4. File issue with package maintainer if appropriate

**Acceptable Resolution:**
- Keep package on older compatible version if required
- Document exception with justification

### Environment & Infrastructure Risks

**Risk:** Build machine doesn't have .NET 10 SDK

**Mitigation:**
- ✅ Verify SDK installation before starting (Phase 0)
- ✅ Document required SDK version (net10.0)
- ✅ Test SDK by building Hello World app

**Risk:** NuGet package sources unavailable

**Mitigation:**
- ✅ Verify internet connectivity to nuget.org
- ✅ Check NuGet.config for package source configuration
- ✅ Test package restore with single package before full upgrade

### Rollback Plan

If the upgrade becomes unworkable:

1. **Immediate Rollback:**
   - Revert all changes from git: `git reset --hard main`
   - Return to main branch: `git checkout main`
   - Delete upgrade branch: `git branch -D upgrade-to-NET10`

2. **Root Cause Analysis:**
   - Document what went wrong
   - Identify which breaking change caused issue
   - Plan for alternative approach if needed

3. **Resolution Options:**
   - Fix root cause and retry upgrade
   - Upgrade to different .NET version
   - Implement phased/incremental upgrade strategy instead

### Success Factors for Risk Mitigation

✅ All-At-Once approach reduces risk by:
- Completing upgrade in one coordinated session
- Catching all issues together in one build cycle
- Avoiding intermediate broken states
- Single, focused testing phase

✅ Clean architecture reduces risk by:
- No circular dependencies
- Clear layering allows focused validation
- Foundation library isolated for testing

✅ Comprehensive tests reduce risk by:
- xUnit test suite available for regression validation
- Can quickly identify breaking changes
- Good coverage of API surface

---

## Testing & Validation Strategy

### Multi-Level Testing Approach

#### Level 1: Compilation Validation

**Objective:** Ensure all code compiles without errors or warnings

**Activities:**
1. Run `dotnet build` on entire solution
2. Verify 0 compiler errors
3. Verify 0 compiler warnings
4. Document any warnings for future cleanup

**Success Criteria:**
- ✅ All 6 projects compile successfully
- ✅ No build errors blocking further testing
- ✅ All packages resolved correctly

**Tools:** `dotnet build`

---

#### Level 2: Unit Test Validation

**Objective:** Verify API behavioral changes don't break expected functionality

**Test Project:** SOITests.csproj

**Test Coverage:**
- All public methods in SettlersOfIdlestan.csproj
- Core business logic validation
- API surface regression testing

**Activities:**
1. Run `dotnet test SOITests.csproj`
2. Capture test results (pass/fail counts)
3. Review any failed tests for root cause
4. Fix code issues if tests reveal problems
5. Re-run tests to verify fixes

**Success Criteria:**
- ✅ All tests pass (100% pass rate)
- ✅ No new test failures introduced
- ✅ Test coverage remains >80% (if measured)

**Tools:** xUnit test runner

---

#### Level 3: Component Build Validation

**Objective:** Verify each project builds independently and together

**Activities by Project:**

**SettlersOfIdlestan.csproj:**
1. Build core library alone: `dotnet build SettlersOfIdlestan\SettlersOfIdlestan.csproj`
2. Verify no warnings
3. ✅ Success: Zero build errors

**SettlersOfIdlestanSkia.csproj:**
1. Build Skia library: `dotnet build SettlersOfIdlestanSkia\SettlersOfIdlestanSkia.csproj`
2. Verify SkiaSharp package loads correctly
3. ✅ Success: Zero build errors

**SettlersOfIdlestanGame.csproj:**
1. Build Blazor WebAssembly: `dotnet build SettlersOfIdlestanGame\SettlersOfIdlestanGame.csproj`
2. Verify WebAssembly IL generation succeeds
3. ✅ Success: Zero build errors, valid IL output

**SettlersOfIdlestanWeb.csproj:**
1. Build Blazor Web app: `dotnet build SettlersOfIdlestanWeb\SettlersOfIdlestanWeb.csproj`
2. Verify server components compile
3. ✅ Success: Zero build errors

**SettlersOfIdlestanDesktop.csproj:**
1. Build MAUI app: `dotnet build SettlersOfIdlestanDesktop\SettlersOfIdlestanDesktop.csproj`
2. Verify multi-target platforms resolve correctly
3. ✅ Success: Zero build errors for all platforms

**SettlersOfIdlestanWeb.csproj:**
1. Already net10.0 compatible
2. Re-run tests: `dotnet test SOITests\SOITests.csproj`
3. ✅ Success: All tests pass

---

#### Level 4: Integration Validation (Manual)

**Objective:** Verify applications function end-to-end

**Test Cases by Application:**

**Blazor WebAssembly (SettlersOfIdlestanGame):**
- [ ] WebAssembly app loads in browser
- [ ] UI renders correctly
- [ ] Basic interactions respond (buttons, input)
- [ ] No JavaScript console errors
- [ ] Network calls succeed (if applicable)

**Blazor Web (SettlersOfIdlestanWeb):**
- [ ] Web app starts successfully
- [ ] Home page renders without errors
- [ ] Page navigation works
- [ ] Components render correctly
- [ ] No server-side errors in console

**MAUI Desktop (SettlersOfIdlestanDesktop):**
- [ ] Application launches
- [ ] Main window displays
- [ ] UI elements render properly
- [ ] Basic functionality works
- [ ] No runtime exceptions

**Success Criteria:**
- ✅ All applications launch without crashes
- ✅ Basic user workflows execute
- ✅ No unhandled exceptions
- ✅ UI renders correctly on all platforms

---

#### Level 5: Behavioral Change Validation

**Objective:** Verify API behavioral changes don't break functionality

**Areas to Test:**

**JSON Operations:**
- [ ] Parse sample JSON files (if used in app)
- [ ] Serialize objects to JSON
- [ ] Deserialize JSON to objects
- [ ] Verify error handling for malformed JSON
- [ ] Compare output with baseline (if available)

**TimeSpan Operations:**
- [ ] Any TimeSpan.FromSeconds() calls work correctly
- [ ] Time calculations remain accurate
- [ ] Comparisons and arithmetic work
- [ ] No precision loss

**URI Operations:**
- [ ] Construct URIs from parts
- [ ] Parse URIs correctly
- [ ] Handle relative URIs
- [ ] Relative/absolute conversions work
- [ ] Query string parsing works

**Success Criteria:**
- ✅ All operations produce expected results
- ✅ No unexpected exceptions
- ✅ Output matches expectations

---

### Validation Checklist

**Pre-Upgrade:**
- [ ] .NET 10 SDK installed and verified
- [ ] All dependencies available for net10.0
- [ ] Source code backed up / on clean branch

**Upgrade Phase:**
- [ ] All 5 project files updated to net10.0
- [ ] All 5 package versions updated
- [ ] Dependencies restored successfully
- [ ] Initial build attempted

**Post-Upgrade:**
- [ ] Solution builds with 0 errors
- [ ] Solution builds with 0 warnings
- [ ] All unit tests pass
- [ ] Each project builds independently
- [ ] Blazor apps load and render
- [ ] Desktop app launches
- [ ] Manual functional tests pass
- [ ] JSON operations work correctly
- [ ] URI operations work correctly

**Approval:**
- [ ] All validation criteria met
- [ ] No outstanding issues
- [ ] Ready for merge to main branch

---

### Testing Timeline

**Compilation Validation:** ~5-10 minutes  
**Unit Test Execution:** ~2-5 minutes  
**Component Build Validation:** ~5-10 minutes  
**Manual Integration Tests:** ~15-30 minutes  
**Behavioral Testing:** ~10-20 minutes  

**Total Estimated Testing Time:** ~50-75 minutes  
(Does not include code fixing time if issues found)

---

## Source Control Strategy

### Branch Management

**Upgrade Branch:** `upgrade-to-NET10` (already created and active)

**Branching Strategy:**
```
main
  ↑
  ├─ (at commit: "Pre-upgrade commit...")
  │
  └─→ upgrade-to-NET10 (current branch)
       ↑
       └─ Upgrade work happens here
       └─ Tests run here
       └─ PR created from here back to main
```

### Commit Strategy

**Single-Commit Approach (Recommended for All-At-Once):**

Since this is an atomic upgrade, a single commit is recommended:

**Commit Message:**
```
Upgrade solution to .NET 10.0

- Update all projects from .NET 8.0 to .NET 10.0
- Update Blazor packages: 8.0.0 → 10.0.6 (SettlersOfIdlestanGame)
- Update Blazor packages: 10.0.5 → 10.0.6 (SettlersOfIdlestanWeb)
- Update logging package: 10.0.0 → 10.0.6 (SettlersOfIdlestanDesktop)
- Fix API incompatibilities:
  - TimeSpan.FromSeconds() source incompatibility (5 occurrences)
  - JsonDocument behavioral changes (verify at runtime)
  - Uri behavioral changes (verify at runtime)
- All tests pass
- Solution builds with 0 errors, 0 warnings
```

**What's Included:**
- ✅ Updated .csproj files (all 5 projects)
- ✅ Code fixes for API breaking changes
- ✅ Package reference updates

**What's NOT Included:**
- ❌ No unrelated code changes
- ❌ No refactoring during upgrade
- ❌ No dependency updates outside assessment scope

### Code Review Checklist

**Before Merge, Verify:**
- [ ] All project files updated to net10.0
- [ ] All package versions correct per assessment
- [ ] Code changes limited to breaking change fixes
- [ ] No spurious whitespace changes
- [ ] Commit message clearly describes changes
- [ ] All tests pass on CI/CD

### Pull Request Process

**PR Checklist:**
1. Create PR from `upgrade-to-NET10` → `main`
2. PR title: "Upgrade to .NET 10.0"
3. PR description includes:
   - List of projects upgraded
   - Summary of breaking changes fixed
   - Test results
   - Known issues (if any)

**Approval Criteria:**
- ✅ All CI/CD checks pass
- ✅ All tests pass
- ✅ Code review approved
- ✅ No conflicts with main branch

### Merge Strategy

**Fast-Forward Merge Preferred:**
```
git checkout main
git pull origin main
git merge upgrade-to-NET10
git push origin main
```

**If Conflicts Occur:**
1. Identify conflicting files
2. Resolve conflicts preferring upgrade-to-NET10 changes
3. Test merged code
4. Commit merge resolution
5. Push to main

### Cleanup

**After Successful Merge:**
```
git branch -d upgrade-to-NET10
git push origin --delete upgrade-to-NET10
```

**Tag the Release (Optional):**
```
git tag -a v10.0.0 -m "Upgrade to .NET 10.0"
git push origin v10.0.0
```

---

## Success Criteria

### The upgrade is complete and successful when:

#### Technical Criteria (MANDATORY)

**1. Target Framework Update ✅**
- [ ] SettlersOfIdlestan.csproj: net10.0
- [ ] SettlersOfIdlestanGame.csproj: net10.0
- [ ] SettlersOfIdlestanSkia.csproj: net10.0
- [ ] SettlersOfIdlestanDesktop.csproj: net8.0-windows10.0.19041.0;net10.0-ios;net10.0-maccatalyst;net10.0-windows (updated)
- [ ] SettlersOfIdlestanWeb.csproj: net10.0
- [ ] SOITests.csproj: net10.0 (already compliant)

**2. Package Updates ✅**
- [ ] Microsoft.AspNetCore.Components.WebAssembly: 10.0.6 (SettlersOfIdlestanGame)
- [ ] Microsoft.AspNetCore.Components.WebAssembly.DevServer: 10.0.6 (SettlersOfIdlestanGame)
- [ ] Microsoft.AspNetCore.Components.WebAssembly: 10.0.6 (SettlersOfIdlestanWeb)
- [ ] Microsoft.AspNetCore.Components.WebAssembly.DevServer: 10.0.6 (SettlersOfIdlestanWeb)
- [ ] Microsoft.Extensions.Logging.Debug: 10.0.6 (SettlersOfIdlestanDesktop)

**3. Build Success ✅**
- [ ] Solution builds without errors: `dotnet build` → 0 errors
- [ ] Solution builds without warnings: All compiler warnings resolved
- [ ] All projects build independently
- [ ] All projects build together

**4. Test Success ✅**
- [ ] SOITests passes 100%: All unit tests pass
- [ ] No new test failures introduced
- [ ] Test coverage maintained at previous baseline

**5. No Unresolved Issues ✅**
- [ ] All API incompatibilities resolved (TimeSpan.FromSeconds fixed)
- [ ] No outstanding compiler errors or warnings
- [ ] No blocking runtime issues discovered
- [ ] No package dependency conflicts

**6. Code Quality ✅**
- [ ] Changes limited to breaking change fixes
- [ ] No unrelated refactoring included
- [ ] Code style consistent with existing codebase
- [ ] Comments added where necessary to explain changes

---

#### Functional Criteria (VALIDATION)

**Application Functionality:**
- [ ] Blazor WebAssembly app (SettlersOfIdlestanGame) loads without errors
- [ ] Blazor Web app (SettlersOfIdlestanWeb) starts successfully
- [ ] MAUI Desktop app (SettlersOfIdlestanDesktop) launches
- [ ] All applications render UI correctly
- [ ] Basic user interactions work as expected

**API Behavior:**
- [ ] JSON parsing operations produce expected output
- [ ] URI operations handle URLs correctly
- [ ] TimeSpan calculations accurate
- [ ] No unexpected runtime exceptions

---

#### Process Criteria (GOVERNANCE)

**Source Control:**
- [ ] Changes committed to `upgrade-to-NET10` branch
- [ ] Commit message clearly describes upgrade
- [ ] PR created and reviewed before merge
- [ ] All CI/CD checks pass
- [ ] Code review approved

**Documentation:**
- [ ] This plan followed as specified
- [ ] Issues encountered documented
- [ ] Fixes applied match breaking changes catalog
- [ ] Post-upgrade notes recorded for future reference

---

### Validation Sign-Off

**Ready for Production When:**

1. ✅ All Technical Criteria met
2. ✅ All Functional Criteria verified
3. ✅ All Process Criteria satisfied
4. ✅ Code reviewed and approved
5. ✅ Tests pass on CI/CD pipeline

**Final Steps Before Production:**
- [ ] Merge upgrade branch to main
- [ ] Deploy to staging environment (if applicable)
- [ ] Final smoke test in production-like environment
- [ ] Monitor for issues post-deployment

---

### Known Limitations & Future Work

**Out of Scope for This Upgrade:**
- Refactoring or architectural changes
- Feature additions or improvements
- Performance optimization (though .NET 10 may provide benefits)
- Documentation updates (beyond what's needed for upgrade)

**Recommendations for Post-Upgrade:**
1. Monitor production for any runtime issues related to JSON parsing or URI handling
2. Review performance metrics after deployment
3. Update team documentation with .NET 10 development best practices
4. Plan future dependency updates as needed

---

### Success Metrics Summary

| Metric | Target | Status |
|--------|--------|--------|
| Projects Successfully Upgraded | 5/5 | TBD |
| Compilation Success Rate | 100% | TBD |
| Test Pass Rate | 100% | TBD |
| Critical Issues Resolved | 100% (5/5) | TBD |
| Breaking Changes Fixed | 100% | TBD |
| Applications Functional | 100% | TBD |
| Code Review Approved | ✅ Required | TBD |
| PR Merged to Main | ✅ Required | TBD |

**Overall Status:** 🟢 Ready for Execution
