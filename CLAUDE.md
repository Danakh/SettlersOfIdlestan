# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SettlersOfIdlestan** is an idle management game in C# (.NET 10.0) where players lead a civilization on a procedurally generated hex-grid island. Core systems: resource management, city building, trading, prestige/meta-progression.

## Commands

```bash
dotnet build SettlersOfIdlestan.slnx
dotnet run --project SettlersOfIdlestanDesktop
dotnet test SOITests
dotnet test SOITests --filter "FullyQualifiedName~HarvestControllerTests"
```

## Solution Structure

| Project | Role |
|---|---|
| `SettlersOfIdlestan` | Core model + controller library — no UI |
| `SettlersOfIdlestanSkia` | SkiaSharp rendering engine + game loop |
| `SettlersOfIdlestanDesktop` | MAUI desktop shell |
| `SettlersOfIdlestanWeb` | Blazor WebAssembly |
| `SOITests` | xUnit tests |

---

## How to Add a Building

**5 touch points:**

### 1. Enum — `Model/Buildings/Building.cs`
Add value to `BuildingType` enum.

### 2. Class — `Model/Buildings/MyBuilding.cs`
```csharp
public class MyBuilding : Building
{
    public MyBuilding() : base(BuildingType.MyBuilding) { AvailableAtLevel = 1; }

    public override ResourceSet GetBuildCost() => new() { { Resource.Wood, 10 }, { Resource.Brick, 5 } };
    public override ResourceSet GetUpgradeCost(int level) => new() { { Resource.Wood, level * 5 } };
    public override int GetDefaultMaxLevel() => 5;

    // Optional — if the building harvests automatically:
    public override HarvestCapability AutomaticHarvestCapability(TerrainType terrain) => ...;

    // Optional — if the building has prerequisites:
    public override bool HasBuildPrerequisites(City city) => ...;
    public override string GetMissingPrerequisiteKey() => "tooltip_requires_X";
}
```
See `ArtisansGuild.cs`, `Sawmill.cs`, `ImperialPort.cs` for examples.  
If the building provides gameplay bonuses, also implement `IModifierProvider`.

### 3. JSON converter — `Model/Buildings/BuildingJsonConverter.cs`
Add the type mapping (two places in the switch — Read and Write).

### 4. Factory — `Controller/Island/BuildingController.cs`, `CreateBuilding()`
```csharp
BuildingType.MyBuilding => new MyBuilding(),
```

### 5. Localization — `Resources/Localization/fr.json` + `en.json`
```json
"building_mybuilding_name": "Mon Bâtiment",
"building_mybuilding_desc": "Description courte.",
"tooltip_requires_X": "⚠ Nécessite ..."
```

---

## How to Add a Technology (Research)

**3 touch points:**

### 1. Enum — `Model/Civilization/Technology.cs`
Add value to `TechnologyId` enum (organized by tier).

### 2. Definition — `Model/Civilization/TechnologyDefinitions.cs`
```csharp
new(TechnologyId.MyTechnology,
    "tech_my_technology_name", "tech_my_technology_desc",
    cost: 100,
    prerequisites: new[] { TechnologyId.SomePrerequsite },
    modifiers: new Modifier[]
    {
        new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1),
        new(ECategory.BUILDING_MAX_LEVEL, "Sawmill", EType.ADDITIVE, 1),
    }),
```

**Modifier categories:** `HARVEST_SPEED`, `RESEARCH_SPEED`, `BUILDING_MAX_LEVEL` (SubCategory = BuildingType name), `BUILDING_PRODUCTION`, `STORAGE_CAPACITY_BASIC/ADVANCED`, `TRADE_GOLD_PACKAGES`, `FORGE_DOUBLE_PROD_BONUS`, `MINE_GOLD_CHANCE_PERCENT`, `STARTING_CITY_BUILDING` / `NEW_CITY_BUILDING` (SubCategory = BuildingType), `CITY_DEFENSE`, `RESEARCH_COST_REDUCTION`, `UNLOCK_RESEARCH` (SubCategory = TechnologyId name).  
**Modifier types:** `ADDITIVE`, `MULTIPLICATIVE`, `REPLACER`.

### 3. Localization — `fr.json` + `en.json`
```json
"tech_my_technology_name": "Nom de la Recherche",
"tech_my_technology_desc": "Description."
```

---

## How to Add a Prestige Vertex

**3 touch points** — coordinate in `Model/Prestige/PrestigeMap/PrestigeMap.cs`, definition in `PrestigeMapFactory.cs`.

### 1. Declare the Vertex coordinate — `PrestigeMap.cs`
```csharp
public static readonly Vertex MyVertex = Vertex.Create(new(1, 0), new(2, 0), new(1, 1));
```

### 2. Add to `CreateDefault()` vertices array — `PrestigeMapFactory.cs`
```csharp
new PrestigeVertex(
    PrestigeMap.MyVertex,
    "prestige_vertex_myvertex",
    cost: Cost(PrestigeMap.MyVertex),
    modifiers: new Modifier[]
    {
        new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 1),
    }
),
```
Cost is computed automatically from distance to center. Modifiers work the same as technologies.

### 3. Localization — `fr.json` + `en.json`
```json
"prestige_vertex_myvertex": "Nom du Vertex"
```

---

## How to Add a Prestige Hex

**3 touch points** — coordinate in `PrestigeMap.cs`, definition in `PrestigeMapFactory.cs`.

### 1. Declare the HexCoord — `PrestigeMap.cs`
```csharp
public static readonly HexCoord MyHexCoord = new(2, 0);
```

### 2. Add to `CreateDefault()` hexes array — `PrestigeMapFactory.cs`
```csharp
new PrestigeHex(
    PrestigeMap.MyHexCoord,
    "prestige_hex_myname",
    adjacentVertices: Adjacent(PrestigeMap.MyHexCoord),
    perVertexModifiers: new Modifier[]
    {
        new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1),
    },
    startingResourceBonusPerVertex: 2
),
```
`perVertexModifiers` are applied once per adjacent purchased vertex. `startingResourceBonusPerVertex` gives starting resources (0 if none).

### 3. Localization — `fr.json` + `en.json`
```json
"prestige_hex_myname": "Nom de l'Hexagone"
```

---

## Changelog

The changelog files are at `SettlersOfIdlestanSkia/Resources/changelog/changelog_fr.txt` and `changelog_en.txt`.

**When to update:** Only for significant new gameplay features (new systems, new game mechanics, new content). Do **not** add entries for balance changes, bug fixes, refactors, or UI polish — those changes should not appear in the changelog automatically.

Add content under the latest version block. Do not create a new version entry unless explicitly asked to.

---

## Tooltips & Localization

**Translation files:** `SettlersOfIdlestan/Resources/Localization/fr.json` and `en.json`.  
Both files must always be kept in sync.

**Naming conventions:**
| Content | Key pattern |
|---|---|
| Building name / description | `building_{type}_name` / `building_{type}_desc` |
| Technology name / description | `tech_{id}_name` / `tech_{id}_desc` |
| Prestige vertex | `prestige_vertex_{name}` |
| Prestige hex | `prestige_hex_{name}` |
| Terrain | `terrain_{type}` |
| Build prerequisite tooltips | `tooltip_requires_{condition}` |
| Other UI tooltips | `tooltip_{description}` |

**Formatted strings** use `{0}`, `{1}` placeholders and are retrieved via `ILocalizationService.GetFormated(key, args)`.

---

## Key Architecture Rules

- **Polymorphic buildings**: `Building` is abstract; subtypes need a discriminator entry in `BuildingJsonConverter` — always update the converter.
- **State persistence**: `MainGameState` is fully JSON-serialized; don't make model fields non-serializable without updating converters in `Services/`.
- **Deterministic generation**: `GamePRNG` is seeded — keep generation logic in `Generator/`, avoid `System.Random` elsewhere.
- **Rendering**: new renderers must be registered in `RenderService`; render order matters for layering.
- **Hex coordinates**: axial (q, r) system; cubic `s = -q - r` computed on demand.
- **Modifier tooltips**: toute nouvelle `ECategory` nécessite obligatoirement (1) un cas dans `FormatModifier()` dans `PrestigeMapRenderer.cs`, (2) les clés de localisation correspondantes dans `fr.json` et `en.json`.

## Testing

`SOITests/ControllerTests/` — one file per controller. `SOITests/HexGridTests/` — coordinate math.
