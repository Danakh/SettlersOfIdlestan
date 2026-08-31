# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Workflow

- Never run `git commit` (or `git push`) unless explicitly instructed to in the current request. The user commits their own work — preparing/staging changes and describing them is fine, but do not create the commit yourself by default.

## Project Overview

**SettlersOfIdlestan** is an idle management game in C# (.NET 10.0) where players lead a civilization on a procedurally generated hex-grid island. Core systems: resource management, city building, trading, prestige/meta-progression.

## Commands

```bash
dotnet build SettlersOfIdlestan.slnx
dotnet run --project SettlersOfIdlestanAvalonia.Desktop
dotnet run --project SettlersOfIdlestanAvalonia.Browser
dotnet test SOITests
dotnet test SOIUITests
dotnet test SOITests --filter "FullyQualifiedName~HarvestControllerTests"
```

## Solution Structure

| Project | Role |
|---|---|
| `SettlersOfIdlestan` | Core model + controller library — no UI |
| `SettlersOfIdlestanSkia` | Hex map rendering (SkiaSharp) + game loop |
| `SettlersOfIdlestanUI` | Avalonia overlay — controls, view models, `GameRuntimeHost` |
| `SettlersOfIdlestanAvalonia.Desktop` | Desktop head (Windows/Linux/macOS, Steam) |
| `SettlersOfIdlestanAvalonia.Browser` | WebAssembly head |
| `SettlersOfIdlestanAvalonia.iOS` | iOS head |
| `SOITests` | xUnit tests — model and controllers |
| `SOIUITests` | xUnit v3 + Avalonia.Headless — overlay tests |

**UI split.** The hex map is still drawn in SkiaSharp inside an Avalonia control; everything
laid over it (top bar, panels, popups, title screen) is made of real Avalonia controls. Click
arbitration is the visual tree's job — never reintroduce a hand-maintained hit-test structure.

**Threading.** Avalonia renders on the render thread while the game loop and input live on the
UI thread, but the runtime and the whole model are single-threaded. Every access to the
runtime, reads included, must go through `GameRuntimeHost.Read`/`Invoke`.

---

## How to Add a Building

**4 touch points:**

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
    public override Resource? AutomaticHarvestCapability(TerrainType terrain, Civilization? civ) => ...;

    // Optional — if the building has prerequisites:
    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) => ...;
    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state) => "tooltip_requires_X";
}
```
See `ArtisansGuild.cs`, `Sawmill.cs`, `ImperialPort.cs` for examples.  
If the building provides gameplay bonuses, also implement `IModifierProvider`.

### 3. Factory — `Model/Buildings/BuildingFactory.cs`
```csharp
[BuildingType.MyBuilding] = () => new MyBuilding(),
```
Single source of truth for `BuildingType` → concrete type: `BuildingController.CreateBuilding`
(instantiation) and `BuildingJsonConverter` (polymorphic deserialization) both read this table, so
there is nothing else to register. `BuildingFactoryTests` fails if a `BuildingType` value is missing.

### 4. Localization — `Resources/Localization/fr.json` + `en.json`
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

**Modifier categories:** `HARVEST_SPEED`, `RESEARCH_PRODUCTION_SPEED` (point generation), `RESEARCH_INVESTMENT_SPEED` (consumption into active research), `BUILDING_MAX_LEVEL` (SubCategory = BuildingType name), `BUILDING_PRODUCTION`, `STORAGE_CAPACITY_BASIC/ADVANCED`, `TRADE_GOLD_PACKAGES`, `FORGE_DOUBLE_PROD_BONUS`, `MINE_GOLD_CHANCE_PERCENT`, `STARTING_CITY_BUILDING` / `NEW_CITY_BUILDING` (SubCategory = BuildingType), `CITY_DEFENSE`, `RESEARCH_COST_REDUCTION`, `UNLOCK_RESEARCH` (SubCategory = TechnologyId name).  
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

- **Polymorphic buildings**: `Building` is abstract; subtypes must be registered in `Model/Buildings/BuildingFactory` — the single table read by both `BuildingController.CreateBuilding` and `BuildingJsonConverter`. Forgetting it makes any save containing the building unreadable; `BuildingFactoryTests` guards against it.
- **Une seule signature par question posée à un bâtiment** : `IsBuildingAvailableForCity`, `HasBuildPrerequisites`, `GetMissingPrerequisiteKey`, `GetBuildWarningKey` et `AutomaticHarvestCapability` n'ont chacune **qu'une** méthode virtuelle. Ne jamais en ajouter une variante « allégée » sans la `Civilization` ou le `WorldState` : c'était le cas avant, la riche retombant sur la pauvre, et un appelant qui prenait la pauvre sautait silencieusement toute redéfinition portée par l'autre — bâtiment constructible là où sa propre règle l'interdit, sans erreur ni trace. Quand l'appelant n'a pas la donnée, il passe `null` **explicitement** (voir `AutoExtendController.PopulateAggressiveCity`), et la redéfinition décide elle-même — en général en refermant la règle. `BuildingHookOverloadTests` échoue si une seconde surcharge réapparaît, sur `Building` comme sur un type concret.
- **State persistence**: `MainGameState` is fully JSON-serialized; don't make model fields non-serializable without updating converters in `Services/`.
- **Collections du modèle encapsulées** : `City.Buildings`, `Civilization.Cities/Roads/Fleets/MaritimeBeacons/MobileCamps/LandingSites` sont exposées en lecture seule. Passer par `AddBuilding`/`RemoveBuilding`/`ClearBuildings`, `AddCity`/`RemoveCity`, etc. — jamais par la liste. C'est ce qui rend les caches dérivés corrects : toute mutation de bâtiments lève `City.BuildingsChanged`, auquel la civilisation propriétaire s'abonne pour invalider les siens (`Civilization.HasMarket`, cache d'Hôtel de Ville et de garnison de la ville). Un cache recalculé « à la construction » depuis `BuildingController.BuildBuilding` serait faux : plusieurs chemins ajoutent des bâtiments sans y passer (bâtiments de départ d'une nouvelle ville, bâtiment racial de l'Ascension, générateur de PNJ). Après un changement de `Building.Level` sans ajout ni retrait, l'invalidation reste manuelle (`City.InvalidateLevelCache`, `Civilization.InvalidateBuildingDerivedCaches`).
- **Lecture des collections sur les chemins chauds** : ces propriétés sont typées `IReadOnlyList<T>`, dont `foreach` boxe l'énumérateur et dont l'indexeur est un appel d'interface. Dans une boucle exécutée à chaque tick, utiliser une boucle `for` indexée (et le champ privé quand on est dans la classe). Mesuré : ~3 % du temps de simulation en fin de partie sur le seul passage de `List` à `IReadOnlyList`.
- **Deterministic generation**: `GamePRNG` is seeded — keep generation logic in `Generator/`, avoid `System.Random` elsewhere.
- **PRNG instance unique** : en dehors des tests, ne jamais faire `new GamePRNG()` dans un contrôleur ou moteur de jeu. Toujours utiliser le `GamePRNG` unique de `MainGameState.PRNG`, récupéré et câblé pendant `Initialize()` (voir `MainGameController.InitializeControllersForCurrentIsland`). Un `new GamePRNG()` local désynchronise la partie de son seed et casse le déterminisme.
- **Rendering**: new renderers must be registered in `RenderService`; render order matters for layering.
- **Hex coordinates**: axial (q, r) system; cubic `s = -q - r` computed on demand.
- **Modifier tooltips**: toute nouvelle `ECategory` **portée par un vertex ou un hexagone de prestige** nécessite obligatoirement (1) un cas dans `FormatModifier()` dans `PrestigeMapRenderer.cs`, (2) les clés de localisation correspondantes dans `fr.json` et `en.json`. `PrestigeMapRendererFormatModifierTests` (SOIUITests) balaie la carte de prestige et échoue si l'un des deux manque — sans cas, le modificateur s'affiche comme un nombre nu ; sans clé, la clé brute apparaît dans l'infobulle. Les catégories qui ne viennent que des recherches ou des races sont affichées ailleurs et ne sont pas concernées.
- **Enum serialization**: tout enum du projet `SettlersOfIdlestan` (modèle/contrôleurs, potentiellement persisté dans `MainGameState`) doit être décoré de `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` — jamais sérialisé par entier. Un enum encodé en int se décale silencieusement si une valeur est insérée/retirée ailleurs qu'en fin de liste, corrompant les sauvegardes existantes sans erreur. La sérialisation par nom échoue de façon explicite (`JsonException`) si une valeur est renommée/supprimée, ce qui reste corrigeable. Correction du décodage pour les anciennes valeurs supprimées ou renommées : dans le converter concerné (`BuildingJsonConverter`, etc.) pour les enums avec converter dédié, ou via un remap de la chaîne lue avant `Enum.TryParse` sinon — chaque remap doit porter un commentaire indiquant la version qui a introduit le besoin (ex. `[Legacy remap v0.11]`).

## Testing

`SOITests/ControllerTests/` — one file per controller. `SOITests/HexGridTests/` — coordinate math.
