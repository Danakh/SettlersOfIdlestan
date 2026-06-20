# CLAUDE.md — SOIStrategyTester

Guidance for Claude Code when working in this subdirectory.

## What this project is for

`SOIStrategyTester` is a CLI tool that loads a game state (a save file, or a brand-new game) and
races one or more **autoplay strategies** against it, measuring how many game **ticks** each one
takes to reach a given **objective**. It exists to find optimal play sequences offline, so that
`SOITests/IslandMapTests/FullIslandTest/FullIslandScenarios.cs` can eventually be rewritten with the
fastest known strategies — and so we can estimate how many ticks a good player needs to reach each
prestige.

It depends only on `SettlersOfIdlestanCore` (the core model/controller library) — never on `SOITests`.
The actual strategy-driving primitives it wires up live in the core library:
- `SettlersOfIdlestan.Controller.CivilizationAutoplayer` — coarse-grained moves (`TryStep1Once`,
  `TryStep2Once`, `TryStep3Once`, `TryMilitaryStepOnce`, `TryWonderInvestmentOnce`, `TryPrestigeOnce`).
- `SettlersOfIdlestan.Controller.PriorityAutoplayStrategy` / `IAutoplayObjective` /
  `BuildingLevelObjective` / `CityCountObjective` — fine-grained sequential priorities (never touches
  objective N+1 while objective N still has actionable work).

This CLAUDE.md is the one you should read before being asked to "find a better strategy for X" — it
explains the JSON vocabulary and the iteration loop, so you can run experiments without re-deriving
the schema from the C# source every time.

## Building and running

```bash
dotnet build SOIStrategyTester/SOIStrategyTester.csproj
dotnet run --project SOIStrategyTester -- <args>
```

CLI arguments (see `Program.cs` / `--help`):

```
--save <path.json>            Load a save (same encrypted format as MainGameController.ExportMainState)
--new-game                    Start a fresh game instead (default if --save is omitted)
--world-id <n>                World id for --new-game (default: first island)
--seed <n>                    PRNG seed for --new-game (default: random — pass a fixed seed for
                               reproducible comparisons across runs)
--objective <objective.json>  Required. The global stopping condition.
--strategies <strategies.json> Required. A JSON array of StrategyDefinition to race.
--output <path>                Default: results.json
--best-output <path>           Default: Data/Best/<strategies-file-name>.best.json
--max-iterations <n>           Default max iterations per phase (default: 20000)
--time-step <seconds>          Simulated seconds advanced per iteration (default: 0.5)
```

All strategies in one run start from an **identical** fresh copy of the starting state (a new
`MainGameController` is built per strategy), so ticks-to-objective are directly comparable.

⚠️ **Use a fresh save, not a stale fixture.** There is a legacy, pre-rename, unencrypted fixture at
`saves/5HexsMapWithTwoCities.json` (repo root) that predates the `IslandState`→`WorldState` rename and
will silently deserialize into an empty world state. The save tests actually exercise lives at
`SOITests/saves/5HexsMapWithTwoCities.json`. When in doubt, generate your own save with `--new-game
--seed <n>`, or point `--save` at a save you just produced via `FullIslandSaveGeneratorTests` /
`SaveUtils`.

## Directory layout

```
SOIStrategyTester/
  Program.cs, StrategyRunner.cs, ObjectiveEvaluator.cs, GameStateFactory.cs
  Model/                       ObjectiveSpec, StrategyDefinition, StrategyPhase, StrategyRunResult
  Data/
    Objectives/                One JSON file per stopping condition (reusable across strategies)
    Strategies/                One JSON file per *experiment* — an array of StrategyDefinition to compare
    Best/                      Winning strategy + result, written by --best-output. Check these in
                                once you're happy with a result — they're the running record of the
                                fastest known strategy for each objective.
```

## JSON vocabulary

### ObjectiveSpec (`Data/Objectives/*.json`)

One object, `kind` plus the fields it needs. These mirror the `Condition` lambdas in
`FullIslandScenarios.cs` exactly:

| kind | fields | mirrors |
|---|---|---|
| `CityCountWithBuilding` | `cityCount`, `requiredBuilding` (BuildingType name) | `TwoCitiesStep` |
| `CityCount` | `cityCount` | `SixCitiesStep` / `TenCitiesStep` |
| `PrestigePointsAtLeast` | `points` | `PrestigePointsStep` |
| `PrestigeAvailable` | — | `PrestigeAvailableStep` |
| `NoSurfaceMonsters` | — | `ExterminateMonstersStep` |
| `NoEnemyCivilizations` | — | `ExterminateCivilizationsStep` |
| `WonderPlaced` | — | `WonderPlacedStep` |
| `WonderLevelAtLeast` | `level` | `WonderLevelStep` |
| `PrestigeRunCountAtLeast` | `count` | the `IsPrestigeStep` steps (RunHistory.Count) |

### StrategyDefinition (`Data/Strategies/*.json` — an array of these)

```json
{
  "name": "string, must be unique within the file",
  "phases": [ StrategyPhase, ... ]
}
```

### StrategyPhase

```json
{
  "kind": "Step1 | Step2 | Step3 | Military | ExterminateMonsters | ExterminateCivilizations | Wonder | Prestige | Priority",
  "shouldExpand": true,                  // Step1/Step2/Step3 only
  "prestigePriorityVertexNames": [...],  // Prestige only — names of public static Vertex fields on
                                          // SettlersOfIdlestan.Model.Prestige.PrestigeMap.PrestigeMap,
                                          // e.g. "CentralVertex", "BarracksVertex" — purchased first,
                                          // deterministically, before the remaining points are spent greedily
  "priorityObjectives": [...],           // Priority only — see below
  "until": ObjectiveSpec | null,         // ends this phase, moves to the next one. null on the LAST
                                          // phase = "run until the run's global --objective is met"
  "maxIterations": 20000                 // optional override of --max-iterations for this phase
}
```

`Priority` phases drive `PriorityAutoplayStrategy` and take a `priorityObjectives` list, evaluated
**in order** — phase N+1 is never touched while phase N still has actionable work:

```json
{ "kind": "BuildingLevel", "buildings": ["TownHall", "Sawmill", ...], "targetLevel": 1 }
{ "kind": "CityCount", "targetCityCount": 5 }
```

A building unavailable to a city (terrain/prerequisites) or already at max level counts as done for
that city — it never blocks the objective forever.

## Workflow for finding better strategies (do this autonomously when asked)

1. **Pick the objective.** Reuse a file under `Data/Objectives/`, or add a new one if the goal isn't
   covered yet (extend `SOIStrategyTester.Model.ObjectiveKind` + `ObjectiveEvaluator` first if the
   condition kind itself is new — check `FullIslandScenarios.cs` for the exact semantics to mirror).

2. **Write or extend a strategies file** under `Data/Strategies/`. Put every variant you want to
   compare in the *same* array so they race from an identical starting state in one run. Good places
   to introduce variation:
   - Reorder phases (e.g. expand before vs. after maxing production).
   - Swap a coarse `Step1`/`Step2` phase for a `Priority` phase with a hand-picked building list/order
     (this is the main lever for "per-step" optimization — it lets you express things FullIslandTest's
     fixed `Step1Buildings`/`Step2Buildings` arrays can't, like "Market before Sawmill" or "skip Mill").
   - Change `targetLevel`/`targetCityCount` checkpoints inside a `Priority` phase's objective list.
   - Vary `shouldExpand`, `prestigePriorityVertexNames`, or where a `Prestige` phase sits relative to
     production phases (global, multi-phase experiments — see `island1-global-variants.json`).

3. **Run it** with a fixed `--seed` (or a real `--save`) so every variant in the file is judged fairly:
   ```bash
   dotnet run --project SOIStrategyTester -- --new-game --seed 42 \
     --objective Data/Objectives/<x>.json --strategies Data/Strategies/<experiment>.json \
     --output /tmp/results.json --best-output Data/Best/<experiment>.best.json
   ```

4. **Read `results.json`** (sorted: successes first, then by ascending ticks) to see the ranking, and
   the console output for which phase (if any) timed out on a failing variant — that tells you whether
   to raise `maxIterations` or fix the strategy itself (e.g. it genuinely can't afford to expand,
   the way a TownHall-only strategy with no production buildings never can — manual harvest alone
   cannot fund roads/outposts).

5. **Iterate.** Add the next variant (e.g. perturb the winner further) to the same strategies file and
   re-run, or start a fresh experiment file for a different phase of the game. Several rounds of
   "tweak the current best, re-race" is the expected loop — there's no built-in search/optimizer, you
   are the optimizer.

6. **Promote the winner.** `--best-output` already records the winning `StrategyDefinition` + its
   `StrategyRunResult` next to the objective it was raced against. Once a result is good enough to
   rely on, leave that file under `Data/Best/` (check it in) — it's the artifact that will eventually
   feed the FullIslandTest rewrite (translate the winning phases back into
   `CivilizationAutoplayerRunner` calls, or extend the runner to execute `PriorityAutoplayStrategy`
   phases directly).

## Gotchas

- A `Prestige` phase invalidates the previous `CivilizationAutoplayer`'s civ/map references (a new
  island is generated). `StrategyRunner` already rebuilds the autoplayer at the start of every phase,
  so you don't need to do anything special — just be aware a "global" multi-phase strategy that
  crosses a prestige transition is exercising that rebuild.
- `ExterminateCivilizations`/`ExterminateMonsters` and large `CityCount` targets can legitimately need
  tens or hundreds of thousands of iterations (see `FullIslandScenarios.cs`'s `maxIterations` overrides
  for precedent) — use the phase-level `maxIterations` override rather than inflating the global default.
- Comparisons are only fair when every strategy in a run starts from the *same* state — always pass
  the same `--seed`/`--save` for an entire comparison, never mix.
