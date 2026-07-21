# CLAUDE.md — SOIStrategyTester

Guidance for Claude Code when working in this subdirectory.

## What this project is for

`SOIStrategyTester` is a CLI tool that loads a game state (a save file, or a brand-new game) and
races one or more **autoplay strategies** against it, measuring how many game **ticks** each one
takes to reach a given **objective**. It exists to find optimal play sequences offline, so that
`SOITests/IslandMapTests/StepIslandTest/StepIslandScenarios.cs` can eventually be rewritten with the
fastest known strategies — and so we can estimate how many ticks a good player needs to reach each
prestige.

It depends only on `SettlersOfIdlestanCore` (the core model/controller library) — never on `SOITests`.
The actual strategy-driving primitives it wires up live in the core library:
- `SettlersOfIdlestan.Controller.CivilizationAutoplayer` — coarse-grained moves (`TryStep1Once`,
  `TryStep2Once`, `TryStep3Once`, `TryMilitaryStepOnce`, `TryWonderInvestmentOnce`, `TryPrestigeOnce`).
- `SettlersOfIdlestan.Controller.PriorityAutoplayStrategy` / `IAutoplayObjective` /
  `BuildingLevelObjective` / `CityCountObjective` / `ImperialPortObjective` — fine-grained sequential
  priorities (never touches objective N+1 while objective N still has actionable work).
  `ImperialPortObjective` wraps `CivilizationAutoplayer.TryBuildImperialPortOnce` — unique buildings
  (`IsUnique`) are never returned as buildable by `BuildingController.GetBuildingOrBuildable`, so
  `BuildingLevelObjective` can't drive them regardless of which building types are listed.

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

### Endless mode

`--endless` drives a single strategy (the first one in `--strategies`, if the file has several) across
**many prestige cycles**, until the run's global `--objective` is met. Unlike normal mode, `EndlessRunner`
(not the strategy JSON) decides *when* to prestige each island — the strategy must contain **no `Prestige`
phase** (it errors out if it finds one); it should only describe how to build an island up. Each cycle,
`EndlessRunner` re-enters the strategy's phases from phase 0 as many times as needed (a "pass") until
this cycle's prestige trigger fires, then prestiges (greedy vertex purchase, like `TryPrestigeOnce()`
with no priority list) and moves to the next cycle. See `Data/Strategies/endless-abyss-gate.json`, a
single `UnifiedAggressive` phase (build/expand/research, attacking as soon as expansion is blocked while
an enemy is visible, then pivoting onto the Wonder only once every NPC civilization is gone — see
`CivilizationAutoplayerPriorities.Unified`'s `aggressive` parameter and `WonderInvestmentObjective`).
It's a single non-terminating phase (no `until`) deliberately: an `until: NoEnemyCivilizations` phase
boundary is a trap here — on a map with zero NPCs to begin with (e.g. island 1), that objective is
trivially true before a single tick runs, so the phase would end on iteration 0 without ever laying down
an economy. `Unified`'s own priority ordering handles the pivot instead, so early objectives (production,
expansion, research) always get first refusal every call, and the Wonder only takes a turn once nothing
higher up the list is actionable.

```bash
dotnet run --project SOIStrategyTester -- --new-game --seed 42 --endless \
  --objective Data/Objectives/abyss-gate-unlocked.json \
  --strategies Data/Strategies/endless-abyss-gate.json \
  --csv-output run_current.csv --checkpoint-hours 1
```

Or just double-click `SOIStrategyTester/Endless.bat` — it `cd`s into the project, runs the command
above with sane defaults, and `pause`s at the end so the summary stays on screen.

```
--endless                     Loop the first strategy in --strategies instead of racing every strategy once
--csv-output <path>           Where to continuously append progress rows (default: run_current.csv)
--checkpoint-hours <n>        Simulated-hours interval between checkpoint rows/console lines (default: 1)
--max-cycles <n>              Safety cap on the number of prestige cycles (default: 100000)
--prestige-point-targets <a,b,c,...>
                               Comma-separated prestige-point target for the 1st, 2nd, 3rd... prestige
                               (default: 35,80,500,1000). Past the list, each island's target instead
                               doubles the previous island's *actual* points at prestige time.
--max-island-hours <n>        Once past --prestige-point-targets, each island prestiges as soon as it
                               reaches its doubled target OR this many simulated hours pass, whichever
                               comes first (default: 24). The fixed targets have no time cap — see below.
```

**Per-cycle prestige trigger.** Every iteration, regardless of which phase is active, `EndlessRunner`
checks `PrestigeController.CalculatePrestigePoints() >= pointsTarget` (and, once past the fixed target
list, `island age >= --max-island-hours`) — the moment either is true *and* `PrestigeIsAvailable()`
(points ≥ 20 **and** an Imperial Port built — not just points), it prestiges immediately, wherever the
strategy currently is. `pointsTarget` for cycle N is `--prestige-point-targets[N-1]` while N is within
the list, else `2 × <actual points the previous prestige had>`.

**Stagnation safety valve — the fixed targets are not a guarantee.** A cycle *can* plateau below its
fixed target (all reachable building levels maxed, no Wonder/Imperial-Port headroom yet, map exhausted)
— `CivilizationAutoplayer.TryExpandOnce()` in particular can keep returning `true` forever building roads
that never resolve into a new city, so "the phase is still doing something" is not a reliable progress
signal. `EndlessRunner` tracks `CalculatePrestigePoints()` pass over pass (a "pass" = one full loop
through every phase) and, once it's been flat for `StagnantPassLimit` (8) consecutive passes, force-
prestiges with whatever points it has rather than hang — logged as `gave up — N pts hasn't moved in 8
passes`. If `PrestigeIsAvailable()` is *still* false at that point (no Imperial Port at all), the whole
endless run aborts — grinding harder won't fix that. A phase's own non-null `until` that's never reached
gets the same "warn and move on to the next phase" treatment as the stall-tolerance below (not a hard
abort — the outer pass loop retries the whole sequence).

**Background systems run automatically, every iteration, regardless of phase kind or mode**
(`StrategyRunner.ExecutePhaseOnce` calls `CivilizationAutoplayer.TryDeepestMineInvestmentOnce` /
`TryCorruptionSpireInvestmentOnce` / `TryAbyssGateInvestmentOnce` / `TryResearchOnce` unconditionally
alongside whatever the phase itself does). Each is a no-op until unlocked — `TryResearchOnce` needs
`ResearchController.IsResearchUnlocked()`, the abyss-chain methods need their prestige-vertex unlocks
(`UNLOCK_DEEPEST_MINE`, `UNLOCK_ABYSS` ×3), which the greedy vertex-buying in `TryPrestigeOnce` handles
on its own over enough cycles — so this is safe in every mode, not just `--endless`. Note `TryResearchOnce`
was previously dead code: no `PhaseKind` called it, so no strategy built purely from `Priority` phases
ever actually researched anything, including whatever tech unlocks higher building-level caps and
`UNLOCK_WONDERS` — a likely contributor to points plateauing well below 500-1000 on early islands (see
`PrestigeController.CalculatePrestigePoints` — the Wonder gives a *multiplier*, `level × (1 + hours
played on this island)`, by far the strongest lever for high targets, but it's gated behind unlocking it
first). Reaching the Abyss Gate is still fundamentally slow on top of that: it needs the Underworld
unlocked (Deepest Mine dug), then a Corruption Spire built on the most-corrupted reachable Underworld
hex, then that hex's corruption to reach `AbyssGate.RequiredCorruptionLevel` (a probabilistic tug-of-war
between `CorruptionController.ProcessSpread` growing it and `ProcessMonumentCorruptionDecay` shrinking
it right back once the Spire is built) before the Gate itself can be placed and invested in — expect
many prestige cycles, and treat the default `endless-abyss-gate.json` strategy as a starting point to
tune (per the workflow above), not a finished answer.

All strategies in one run start from an **identical** fresh copy of the starting state (a new
`MainGameController` is built per strategy), so ticks-to-objective are directly comparable.

⚠️ **Use a fresh save, not a stale fixture.** There is a legacy, pre-rename, unencrypted fixture at
`saves/5HexsMapWithTwoCities.json` (repo root) that predates the `IslandState`→`WorldState` rename and
will silently deserialize into an empty world state. The save tests actually exercise lives at
`SOITests/saves/5HexsMapWithTwoCities.json`. When in doubt, generate your own save with `--new-game
--seed <n>`, or point `--save` at a save you just produced via `StepIslandSaveGeneratorTests` /
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
`StepIslandScenarios.cs` exactly:

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
| `AbyssGateUnlocked` | — | live `AbyssGate.Built` on the current island, or cross-prestige `GameRecord.HasBuiltAbyssGate` |

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
  "kind": "Step1 | Step2 | Step3 | Military | UnifiedAggressive | ExterminateMonsters | ExterminateCivilizations | Wonder | Prestige | Priority",
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
{ "kind": "ImperialPort" }
```

`ImperialPort` needs no extra fields — it wraps `CivilizationAutoplayer.TryBuildImperialPortOnce`, which
focuses exclusively on the first coastal city (Seaport 4, Warehouse 4, TownHall 4, then the unique
Imperial Port). `BuildingLevel` can never drive this regardless of which building types are listed,
since `IsUnique` buildings are never returned as buildable by `BuildingController.GetBuildingOrBuildable`.

⚠️ **Put `CityCount` (and any other open-ended growth objective) last, or cap it conservatively.**
`PriorityAutoplayStrategy` never touches objective N+1 while objective N still has actionable work — if
an early `CityCount` target turns out to be more than a given map can actually support, every objective
after it (e.g. the Temple/TownHall stages that actually generate prestige points) never even starts,
and the run hangs until `maxIterations`. This isn't hypothetical: an Island1 experiment that put
`CityCount` first worked great on a fresh seed-42 game but deadlocked against the `release-1.0` fixture,
whose map plateaus at 13 cities — see `Island1PrestigePointsStep` in `StepIslandScenarios.cs` for the
fix (build first, expand only as an uncapped-but-rarely-needed fallback).

A building unavailable to a city (terrain/prerequisites) or already at max level counts as done for
that city — it never blocks the objective forever.

## Workflow for finding better strategies (do this autonomously when asked)

1. **Pick the objective.** Reuse a file under `Data/Objectives/`, or add a new one if the goal isn't
   covered yet (extend `SOIStrategyTester.Model.ObjectiveKind` + `ObjectiveEvaluator` first if the
   condition kind itself is new — check `StepIslandScenarios.cs` for the exact semantics to mirror).

2. **Write or extend a strategies file** under `Data/Strategies/`. Put every variant you want to
   compare in the *same* array so they race from an identical starting state in one run. Good places
   to introduce variation:
   - Reorder phases (e.g. expand before vs. after maxing production).
   - Swap a coarse `Step1`/`Step2` phase for a `Priority` phase with a hand-picked building list/order
     (this is the main lever for "per-step" optimization — it lets you express things StepIslandTest's
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
   feed the StepIslandTest rewrite (translate the winning phases back into
   `CivilizationAutoplayerRunner` calls, or extend the runner to execute `PriorityAutoplayStrategy`
   phases directly).

## Gotchas

- A `Prestige` phase invalidates the previous `CivilizationAutoplayer`'s civ/map references (a new
  island is generated). `StrategyRunner` already rebuilds the autoplayer at the start of every phase,
  so you don't need to do anything special — just be aware a "global" multi-phase strategy that
  crosses a prestige transition is exercising that rebuild.
- **Never list more than one building type per `BuildingLevel` objective.** `BuildingLevelObjective.
  TryAdvanceOnce` calls `TryBuildBuildingOnce(..., withGrind: true)` (the default) for *every* not-yet-done
  (city, building) pair within a single call, unlike `CivilizationAutoplayer.TryStepOnce`'s deliberate
  "grind once per step" discipline. With trade enabled, each failed attempt's grind can chase a
  *different* missing resource than the previous one in the same tick, churning the stockpile and
  stalling forever — reproduced directly: a `["Sawmill","Brickworks","Mill","Market","Seaport"]` list
  hung for 300k+ iterations on a 2-resource thrash, while the same five buildings as five separate
  single-building stages finished in ~120. Split every multi-building list into one stage per building.
- See the `CityCount`-ordering warning above the `ImperialPort` example — it's the same family of bug
  (an early objective with unmet/unreachable preconditions silently blocks everything after it) but for
  expansion targets instead of cross-building trade.
- `ExterminateCivilizations`/`ExterminateMonsters` and large `CityCount` targets can legitimately need
  tens or hundreds of thousands of iterations (see `StepIslandScenarios.cs`'s `maxIterations` overrides
  for precedent) — use the phase-level `maxIterations` override rather than inflating the global default.
- Comparisons are only fair when every strategy in a run starts from the *same* state — always pass
  the same `--seed`/`--save` for an entire comparison, never mix.
- **`TryResearchOnce` only starts/queues research — it never builds the Library that produces the
  research points research actually needs to progress.** `ResearchController.ProduceResearchPoints`
  only ticks up `ResearchPoints` from a built `Library` (or `Laboratory`) in a city; without one in the
  strategy's own `BuildingLevel` lists, research starts (visible as `InProgress`) but never advances —
  `ResearchCompleted` stays 0 forever even across dozens of prestige cycles. `Library` needs `TownHall`
  level 2 and its own max-level unlock (the first prestige vertex, which also grants
  `UNLOCK_RESEARCH_SYSTEM`) before it's buildable, so it's a safe no-op to list early. Any hand-written
  `Priority` strategy meant to run long enough for research/Wonder to matter (not just
  `CivilizationAutoplayerPriorities.Unified`, which already includes `Library`) must list `Library` in
  a `BuildingLevel` objective itself — see `endless-abyss-gate.json`.
