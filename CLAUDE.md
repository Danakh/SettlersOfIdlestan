# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SettlersOfIdlestan** is an idle management game in C# (.NET 10.0) where players lead a civilization on a procedurally generated hex-grid island. Core systems include resource management, city building, trading, and a prestige/meta-progression system.

## Commands

```bash
# Build
dotnet build SettlersOfIdlestan.slnx

# Run desktop app
dotnet run --project SettlersOfIdlestanDesktop

# Run all tests
dotnet test SOITests

# Run a single test class
dotnet test SOITests --filter "FullyQualifiedName~HarvestControllerTests"
```

## Solution Structure

| Project | Role |
|---|---|
| `SettlersOfIdlestan` | Core model + controller library — no UI dependencies |
| `SettlersOfIdlestanSkia` | SkiaSharp rendering engine + game loop |
| `SettlersOfIdlestanDesktop` | MAUI desktop shell (Windows / macCatalyst / iOS) |
| `SettlersOfIdlestanWeb` | Blazor WebAssembly |
| `SOITests` | xUnit test suite for model and controller layers |

## Architecture

The project follows a strict MVC separation:

### Model (`SettlersOfIdlestan/Model/`)

Pure data — no rendering or UI logic.

- **`Game/`** — `MainGameState` is the root state object. Contains `GodState` (prestige wrapper) and `GameClock`.
- **`IslandMap/`** — `IslandState` holds the current playthrough board. `IslandMap` is the hex grid. `HexTile` carries terrain (`TerrainType`) and resources (`Resource` enum).
- **`Civilization/`** — `Civilization` owns a list of `City` objects, roads, and a `TechnologyTree`. Cities sit at hex `Vertex` positions.
- **`Buildings/`** — Abstract `Building` base class with 13 concrete subtypes (TownHall, Market, Sawmill, etc.). Building level drives city level.
- **`HexGrid/`** — Axial coordinate system (`HexCoord` with q, r). `Vertex` and `Edge` are derived positions used for city/road placement.
- **`PrestigeMap/`** — `GodState` → `PrestigeState` → `PrestigeMap`. Meta-progression between runs.
- **`GameplayModifier/`** — `Modifier` objects that adjust game rules and balancing.

### Controller (`SettlersOfIdlestan/Controller/`)

Business logic orchestrators operating on model objects.

- `MainGameController` — central entry point; delegates to sub-controllers.
- `HarvestController`, `BuildingController`, `CityBuilderController`, `RoadController`, `TradeController`, `PrestigeController`
- `Generator/IslandMapGenerator` — procedural island creation via seeded `GamePRNG` (deterministic).
- `CivilizationAutoplayer` — AI for automated opponent actions.

### Rendering (`SettlersOfIdlestanSkia/`)

- **`Services/SkiaGameRuntime`** — central game loop (16 ms / 60 FPS target), owns `RenderService`, `CameraService`, `InputHandlingService`, `HarvestService`, `ConstructionInteractionService`.
- **`Renderers/`** — One renderer per concern: `GameBoardRenderer`, `CityRenderer`, `RoadRenderer`, `HarvestRenderer`, `TradeRenderer`, `PrestigeRenderer`, panel/overlay renderers, etc. All register through `RenderService`.
- **`Services/GameControllerService`** — thin wrapper around `MainGameController` for use in the Skia layer.

### Desktop entry point (`SettlersOfIdlestanDesktop/`)

- `MainPage.xaml` — MAUI page hosting the SkiaSharp canvas.
- `MauiProgram.cs` — DI container and MAUI configuration.
- `DesktopFileSystemService` — file I/O for save/load (auto-save every 5 s during gameplay).

### Services (`SettlersOfIdlestan/Services/`)

- `SerializationService` — JSON serialization with custom converters for polymorphic types (`Building` subtypes, `HexCoord`, `Vertex`, `Edge`).
- `LocalizationService` / `ILocalizationService` — JSON-based i18n (French/English).

## Key Patterns

- **Hex coordinates**: axial (q, r) system; cubic `s = -q - r` computed on demand. All grid math goes through `HexCoord` helpers.
- **Polymorphic buildings**: `Building` is abstract; subtypes are serialized via a custom JSON discriminator — update the converter when adding a new building type.
- **State persistence**: the full `MainGameState` tree is JSON-serialized; custom converters live in `Services/`. Do not make model fields non-serializable without updating converters.
- **Deterministic generation**: `GamePRNG` is seeded — keep generation logic in `Generator/` and avoid `System.Random` elsewhere.
- **Rendering registration**: new renderers must be registered in `RenderService`; render order matters for layering.

## Testing

Tests live in `SOITests/`:
- `ControllerTests/` — one file per controller (MainGameController, Building, Harvest, Trade, Prestige, Road, Autoplayer)
- `HexGridTests/` — HexCoord, Edge, Vertex, direction math
- `SkiaTests/` — folder exists, currently empty
