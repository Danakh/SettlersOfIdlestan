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
hex, then a Corruption zone of `AbyssGate.RequiredCorruptionLevel` or higher to be *fully cleared* on
the current island (anywhere on the map, by any mechanism — Spire decay, Temple, Dominion annulation;
tracked in `RunRecord.MaxCorruptionLevelCleared`, which resets at every prestige, so the condition must
be met again on each island even after a Gate was opened in a previous run) before the Gate itself can
be placed and invested in — expect many prestige cycles, and treat the default `endless-abyss-gate.json`
strategy as a starting point to tune (per the workflow above), not a finished answer.

### Race gauntlet mode

`--race-gauntlet` answers a different question from the two modes above: **can every race actually
play the game?** It plays the first N islands (4 by default) once per race and prints a PASS/FAIL table.
It is the race-wide counterpart of `SOITests`' `FullIslandTest`, which only ever exercises Humans — but
deliberately *not* a test: it takes minutes, its per-island outcome depends on the seed, and a FAIL is
something to read and judge, not to gate a build on.

```bash
dotnet run --project SOIStrategyTester -c Release -- --race-gauntlet --seed 1
# one race, one island, to check the plumbing (~2 s):
dotnet run --project SOIStrategyTester -c Release -- --race-gauntlet --races Human --islands 1 --seed 1
```

Or double-click `RaceGauntlet.bat`. Exit code is 0 only if every race passed.

**End-game round — `--islands 5 --final-island-points 100`.** Past four islands, "did it prestige"
stops discriminating: every race that isn't outright blocked clears them. The question on island 5 —
the first one a civilization enters with a real prestige-vertex inheritance behind it — is whether it
still *produces* at an end-game rate. `--final-island-points <n>` adds that second condition to the
verdict: clear all N islands **and** be worth n prestige points on island N. It also raises that
island's own points target to n (`EndlessRunOptions.LastCyclePointsFloor`) — without which the island
would prestige at its computed target (2× the previous island's actual, ~80–120 in practice) and the
criterion would be measuring that target rather than the race.

```bash
dotnet run --project SOIStrategyTester -c Release -- --race-gauntlet --seed 1 \
  --islands 5 --final-island-points 100 --gauntlet-output race-gauntlet-endgame
```

Or double-click `RaceGauntletEndGame.bat`. A failure here reads in three distinct ways, and the
summary keeps them apart:
- **never got there** — the race couldn't even reach island N (a blockage, like the 4-island run's).
- **hit the `--max-island-hours` cap** with too few points — still working when the clock ran out;
  more time would help.
- **"N pts hasn't moved in 8 passes"** — the stagnation valve. The island stopped producing well
  before the time cap, so more time would change nothing; the strategy has run out of things to do.

**Each race starts where a player would actually first pick it** —
`GameStateFactory.NewGameForRace` unlocks the divine powers its tier requires (first row of the
ascension grid for a Base race, first two rows for an Advanced one, derived from
`AscensionPowerDefinitions` exactly the way `AscensionController.IsRaceSelectionUnlocked` /
`AreAdvancedRacesUnlocked` check for them) and then goes through the **real**
`AscensionController.PerformAscension`. That last part matters: it is the only path that regenerates
island 1 *for that race* (start terrain, Underworld start for the Dark Elves) and that grants the free
prestige vertices which come with Faith and with race selection being unlocked (central vertex + its 3
neighbours, plus `RaceDefinition.FreePrestigeVertices`). Poking `AscensionState.SelectedRace` instead
would measure every race on a Human map with a Human prestige map. Side effect worth knowing: the run
therefore starts with research already unlocked and a Market vertex bought, so it is *not* comparable
to `StepIslandScenarios`' island-1 numbers.

**The verdict is only "did it reach the next prestige, N times".** That is the one goal every race
shares. FullIslandTest's per-stage checkpoints (12 cities, Library everywhere) are race-hostile by
construction — Giants cannot reach 12 cities at minimum distance 4, Mermaid cities away from the water
cap at Town Hall 2 — so failing them would say nothing about playability. How far each island actually
got (cities, buildings, points, research, Wonder) is reported next to the verdict instead; that is
where a race being *weak* rather than *blocked* shows up.

Islands are driven by `EndlessRunner` (same per-cycle prestige trigger as `--endless`), with two
gauntlet-specific behaviours:
- `EndlessRunOptions.TimeCapAllIslands` — `--max-island-hours` (default **8** here, not 24) caps
  *every* island, including the ones covered by a fixed `--prestige-point-targets` entry. Without it a
  target a race can't reach turns into an unbounded grind. In practice islands 3–4 end on this cap
  rather than on their 500/1000-point target.
- `--abandon-island-hours` (default 24) — if an island still isn't `PrestigeIsAvailable()` by then, the
  race is declared blocked, and the reason distinguishes *no Imperial Port* (usually structural: no
  coastal city this race can build on this map) from *points short* (pacing).

Output: `<--gauntlet-output>/race-<Race>.csv` (one endless-mode CSV per race), plus `summary.csv`
(one row per race per island) and `summary.json`. The default `race-gauntlet/` directory is
gitignored — these are run artifacts, so the reference verdict lives here instead:

**Last run — seed 1, 4 islands, 9/9 races clear** (points/cities/hours per island):

| Race | Tier | | Sim h | Island 1 | Island 2 | Island 3 | Island 4 | Total pts |
|---|---|---|---|---|---|---|---|---|
| Human | Base | PASS | 16.82 | 47p/12c/0.49h | 198p/12c/3.19h | 579p/12c/5.13h | 765p/12c/8.00h | 1589 |
| Elf | Base | PASS | 26.18 | 35p/8c/7.18h | 99p/9c/3.00h | 331p/8c/8.00h | 765p/12c/8.00h | 1230 |
| Dwarf | Base | PASS | 26.42 | 30p/7c/8.00h | 158p/12c/2.42h | 393p/9c/8.00h | 765p/12c/8.00h | 1346 |
| Goblin | Base | PASS | 24.00 | 61p/12c/6.73h | 122p/12c/2.28h | 550p/12c/7.00h | 599p/12c/8.00h | 1332 |
| Orc | Base | PASS | 18.02 | 47p/12c/0.49h | 158p/12c/2.97h | 662p/12c/6.56h | 765p/12c/8.00h | 1632 |
| Giant | Advanced | PASS | 23.89 | 22p/5c/8.00h | 92p/6c/5.00h | 79p/4c/8.00h | 39p/4c/2.89h | 232 |
| Garuda | Advanced | PASS | 14.82 | 47p/12c/0.47h | 118p/12c/1.35h | 579p/12c/5.00h | 351p/11c/8.00h | 1095 |
| Mermaid | Advanced | PASS | 26.13 | 34p/12c/8.00h | 191p/12c/4.13h | 545p/12c/6.00h | 34p/12c/8.00h | 804 |
| DarkElf | Advanced | PASS | 24.34 | 47p/12c/3.56h | 284p/12c/5.42h | 745p/12c/7.35h | 765p/12c/8.00h | 1841 |

Giants (4–6 cities throughout, Wonder stuck at level 1–2) and Mermaid (Wonder level 0 on island 4)
are *weak*, not blocked — that is the distinction this table exists to make.

This run is the first fully green one, and it also carries the DarkElf fixes (see that section). Two
races drifted against the Wonder-gate reference further down, both on island 4 only: **Giant 206 → 39**
(it gave up on the stagnation valve at 2.89h with 4 cities) and **Garuda 382 → 351**. Everything else
is unchanged to the point. Whichever monument hex `OrderByLeastSacrifice` now prefers is the most
likely cause for a race that only ever has four cities' worth of hexes to choose from; it has not been
chased down, and a single seed does not make it a regression.

**End-game round — seed 1, `--islands 5 --final-island-points 100`: 9/9.** Every race clears the five
islands *and* is worth 100+ points on the last one. Islands 1–4 are point-identical to the 4-island
table above (same seed, same run up to that point), so the column that carries information here is
island 5:

| Race | Island 5 | Cities | Ended on |
|---|---|---|---|
| DarkElf | **972** | 15 | 8h time cap |
| Human / Orc | 786 | 12 | 8h time cap |
| Garuda | 717 | 12 | reached its 702 target @ 6.00h |
| Goblin | 615 | 12 | 8h time cap |
| Dwarf | 331 | 10 | 8h time cap |
| Mermaid | 133 | 12 | reached the 100 floor @ 2.48h |
| Elf | 113 | 4 | 8h time cap |
| Giant | 110 | 5 | reached the 100 floor @ 6.00h |

Three of these lines are the three failures this round used to have. **DarkElf tops the table** — the
race that could not leave its starting city posts the best island-5 score and the highest city count of
the run. **Elf prestiges on island 5 at all** (it used to be abandoned after 24h): still walled in at
4 cities, but the road-cost fix gives it the roads it was missing — weak, no longer blocked. **Giant
clears the 100 bar** at 110, against the 80/100 that was its last standing failure.

Read the two narrow ones with care: Giant at 110 and Elf at 113 are thin margins on a single seed, and
neither should be called solid until a second seed agrees.

Historical, for contrast — the same round measured 0/9 *before* the Wonder gate was opened (best was
Goblin at 82, with six of the seven that got there plateauing on the stagnation valve between 1.9h and
4.1h), then 7/9 with the gate open and Giant and DarkElf still failing. One measurement from that era
is still worth not re-running: rerunning Human and Goblin with `--max-island-hours 24
--abandon-island-hours 48` produced *identical* points, every island having ended on stagnation rather
than on the cap. When a race plateaus, more time buys nothing — the ceiling is the strategy's.

### Why islands 3+ never build the Wonder

Islands 3–5 land in the 40–82 point range while island 2 reaches 120–198. Island 2 is the last one where
the Wonder gets built, and `PrestigeController.CalculatePrestigePoints` makes it a *multiplier*
(`level × (1 + hours on this island)`) — by far the strongest lever there is. The `WondersUnlocked`,
`WonderPlaced` and `NpcCivsAlive` CSV columns exist to pin down which of the three steps fails, and the
answer is unambiguous:

| Island | WondersUnlocked | WonderPlaced | NPC civs alive at prestige | Points |
|---|---|---|---|---|
| 1 | True | True | 0 | 30–61 |
| 2 | True | True | 0 | 122–198 |
| 3 | True | **False** | **1** | 40–110 |
| 4 | True | **False** | **2** | 43–62 |
| 5 | True | **False** | **10** | 40–82 |

**It is not a research problem.** `Architecture` (tier 0, cost 100, no prerequisites) grants
`UNLOCK_WONDERS` and the technology tree persists across prestiges, so `WondersUnlocked` is True on every
island from island 1 onward. The Wonder is simply never *placed*.

The gate is `CivilizationAutoplayerPriorities.Unified`'s
`new WonderInvestmentObjective(auto, () => aggressive && allNpcsEliminated())`. In `--race-gauntlet`
(`UnifiedAggressive`) the Wonder objective stays a no-op until **every** NPC civilization is wiped out.
Islands 1–2 have none, so it opens immediately; islands 3, 4 and 5 ship 1, 2 and 10 NPC civilizations and
the run plateaus with them still standing, so the strongest points multiplier in the game is never even
placed. `WonderPlaced` tracks `NpcCivsAlive == 0` exactly, across every race.

That is self-reinforcing: no Wonder → few points → a weaker start on the next island → even less able to
clear a larger NPC count. It is why island 5 stalls at 40–82 rather than 100.

### Can the autoplay just win those wars instead? No — measured

The obvious counter-move is to make the strategy declare war rather than relax the Wonder gate. It does
not work, and the reason is worth knowing before anyone tries it again.

In aggressive mode the war trigger is `hasVisibleThreats() && !HasBuildableExpansion()`, and
`HasBuildableExpansion()` counts **a single buildable road**. Since the road network can nearly always
grow one more step, that trigger essentially never fires: the default gauntlet run never declares war at
all, whatever its city count. `StrategyPhase.AttackNeighborsAtCities` exists to bypass it — see
`Data/Strategies/race-gauntlet-warlike.json`, which declares war at 12 cities. Result:

- **Zero NPC civilizations killed in 24 simulated hours.** `NpcCivsAlive` sits flat at 2 across the whole
  island for both Human (20 cities) and Goblin (48 cities).
- **The Imperial Port is lost.** Islands 4+ never reach `PrestigeIsAvailable()` at all — the run is
  abandoned after 24h with points in hand (56 / 105 / 43) but no Port, so it is strictly worse than the
  baseline: Human and Goblin drop from 5/5 islands to 3/5.

The blocked-state dump says why (`militaire:` line, added for this):

```
militaire : 44 soldats, 2 civ PNJ vivantes (11 villes, 4 villes), cible prioritaire : 11 villes, à portée : False
```

The army exists (44 soldiers), an enemy is picked, and it is **never in range**.
`AttackNeighborsObjective` only re-points the attack flows of cities that *already* have a target within
`CityAttackRange`; nothing in the priority list advances the front. The declared-war fallback is a plain
`CityCountObjective(int.MaxValue)`, which chooses vertices by nearest-prospective, not toward the enemy —
so the civilization sprawls (Goblin to 48 cities, Human laying 98 roads) while both NPC civilizations
stand untouched, and meanwhile every new city re-opens objective #1 (`Step1Buildings → 1`), which is what
starves the Imperial Port stage far down the list.

**Conclusion: this is not reachable by tuning a strategy file.** Making islands 3+ winnable needs a
capability the autoplayer does not have — expansion *directed at* the priority target (or attack range
that can be projected).

### The fix that was applied: open the Wonder gate

`WonderInvestmentObjective`'s predicate became
`aggressive && (allNpcsEliminated() || !hasTargetInRange())`. The second clause is the one that fires:
if no enemy is actually reachable there is no war to win, so invest in the Wonder instead of sprawling.
When a target *is* in range the war keeps priority, which was the original intent.

Reference run, seed 1, 4 islands — points per island, before → after:

| Race | Island 1 | Island 2 | Island 3 | Island 4 | Total |
|---|---|---|---|---|---|
| Human | 47 | 198 | 70 → **579** | 44 → **765** | 359 → **1589** |
| Elf | 35 | 99 | 20 → **331** | 42 → **765** | 196 → **1230** |
| Dwarf | 30 | 158 | 40 → **393** | 43 → **765** | 271 → **1346** |
| Goblin | 61 | 122 | 110 → **550** | 62 → **599** | 355 → **1332** |
| Orc | 47 | 158 | 67 → **662** | 43 → **765** | 315 → **1632** |
| Giant | 22 | 92 | 47 → **79** | 40 → **206** | 201 → **399** |
| Garuda | 47 | 118 | 73 → **579** | 42 → **382** | 280 → **1126** |
| Mermaid | 34 | 191 | 62 → **545** | 60 → **34** ⚠ | 347 → **804** |

Islands 1–2 are unchanged (they already placed their Wonder). **End-game round: 0/9 → 7/9** races reach
100 points on island 5, with 103–807 points instead of 40–82. The two that still failed at that point
were Giant (80/100, 4–7 cities only, Wonder stuck at level 1) and DarkElf (never left island 1). Both
pass since the DarkElf fixes — see the 9/9 round above.

**Known cost, deliberately accepted.** The objective never yields once active, so the unlimited expansion
below it stops (12 cities on island 4, against 13–23 without the pivot). Mermaid loses 26 points on
island 4 specifically — its Wonder stays level 0 there — while gaining 457 across the four islands.
Three attempts to remove that cost were measured and **all were worse**, so don't re-run them:

| Attempt | Result |
|---|---|
| Pivot only from 20 cities | Dwarf/Orc/Human/Garuda each lose ~700 pts — most races cap at 12–13 cities on island 4, so the threshold removes their Wonder entirely |
| Pivot only from 14 cities | Human stops prestiging on island 4 at all (no Imperial Port after 24h) |
| Make the objective non-blocking once investments are open | Elf −694, Dwarf −573. `TryAdvanceOnce` also pumps Gold every turn, and that is what funds the Wonder levels; freeing the turns restarts expansion, which is cheap activity that does not raise prestige points, so islands end earlier on the stagnation valve (Human island 2: 198 → 42) |

The lesson: more cities is not what makes points here — the Wonder multiplier is. The blocking pivot is
not waste, it *is* the valuable action.

**Elf was a second blocker, visible only at 5 islands**: it cleared 4 then never prestiged on island 5,
abandoned after 24h with `11/20 points; 4 cities, 0 buildable vertices, 15 buildable roads` — walled
in by Forest adjacency on that map while the road network still had somewhere to go. Fixed as a side
effect of the road-cost grind (see the DarkElf section): it now prestiges there with 113 points, still
from only 4 cities.

### DarkElf: was the one open blocker — fixed, four causes deep

The Dark Elves are the only race that starts in the Underworld, and they used to never leave their
starting city: `#19 CityCount(→ 12, 1 now)`, 1 city, **0 roads laid in 20 simulated hours**,
0 buildable vertices. Four distinct causes stacked, each hidden behind the previous one. They are
worth keeping written down, because three of the four are general bugs that merely happened to be
fatal for this race.

**1. The Surface Breach sterilised the only Mountain of the starting triangle.** `StrategyRunner`
calls `TrySurfaceBreachInvestmentOnce()` every iteration as a background system, and on the very
first one it placed the Breach on `GetPlaceableHexes()[0]` — the only Underworld Mountain adjacent to
the starting city, the one `RaceDefinitions.UnderworldStartTerrains` guarantees. `SurfaceBreach`
derives from `Monument`, so `BlocksHarvest => true`: the Quarry was built, on a real Mountain, and
produced **zero Stone forever**. Fixed by `SurfaceBreachController.MinCitiesToPlace = 4` — a
civilization must have four cities before opening the shaft, by which time other Mountains have been
revealed.

**2. Underworld roads cost Stone and Ore, which nothing was producing.**
`RoadController.ApplyUnderworldRoadCostAdjustments` adds Ore 5 + Stone 9 on top of the Wood/Brick
base. Stone was dead (cause 1) and Ore has no source without a Mine, which needs TownHall 3 — and in
`CivilizationAutoplayerPriorities.Unified` the whole Step 2 block is gated behind `hasStep2Cities`
(**4 cities**). Four cities to produce Ore, and Ore to lay the first road toward the second city.

**3. The grind never saw what was missing.** `TryBuildRoadOnce` called
`TryGrindOnce(GetRoadCost(distance))` — the *base* cost, without the Underworld surcharge. The
auto-trade therefore compared a need for Wood 2 / Brick 2 against a stockpile capped at 275,
concluded nothing was missing, and never bought the Ore and Stone that were actually blocking. Same
bug family as the city surcharge documented in `TryBuildOutpostOnce`. Fixed by
`RoadController.GetRoadCostFor(civ, edge)`, which returns what `BuildRoad` actually charges;
`GetPlayerRoadCost` is now just its player-facing special case.

**4. The Underworld overran the starting area.** With roads finally being laid, the run reached four
cities and then died differently: every one of the twelve hexes of its four cities was occupied by
Trolls and Ogres — up to five on a single hex — all blocking harvest, production flat at +0. The Pact
of the Depths (`MONSTER_ATTACK_IMMUNITY`) does not help: it stops monsters from *taking* cities, not
from sitting on the terrain, and the autoplay has no soldiers to evict them (Barracks → Ore → Mine,
whose Mountain is itself occupied). One island further, aggressive Underworld NPC civilizations wiped
the civilization out entirely (0 cities, 2 NPC civs alive with 6 and 12 cities). Fixed by
`AutoExtendController.UnderworldSafeRadiusBonusByIsland = { 8, 6, 4, 2 }` — hexes *added* to
`MinHexDistanceFromArrival` on the first four islands, a guaranteed empty ring that tightens island
after island. It covers all three Underworld spawn paths (per-hex monsters, border monsters, and
aggressive civilizations — a ring that keeps monsters out but lets a three-city hostile civilization
settle inside it would guarantee nothing). Treasure keeps its exact odds: the ring only closes the
monster windows, and the treasure window is shifted down rather than narrowed.

A **radius rather than a reduced probability** is the deliberate choice: population density stays the
game's own everywhere it matters, and what the player is given is a starting area they *know* is
safe, not an expectation of quiet that one bad roll can deny on the first hex revealed.

Also fixed along the way, and general: `MonumentInvestment.OrderByLeastSacrifice` now orders every
monument's `GetPlaceableHexes()` from cheapest to costliest to sacrifice — fewest cities actually
harvesting the hex first, then the most abundant of the resources lost. Both callers benefit: the
autoplay takes `hexes[0]`, and the player sees candidates best-first.

Result, `--race-gauntlet --races DarkElf --islands 4 --seed 1`: **FAIL 0/4 → PASS 4/4**, with
47 / 284 / 745 / 765 points per island. On the 5-island end-game round it goes further and tops the
table at 972 points and 15 cities on island 5.

### Manches Pandémonium — `--pandemonium` et `--pandemonium-ascended`

L'autre bout de la question d'équilibrage. Le race gauntlet demande « toutes les races savent-elles
jouer les premières îles ? » ; celles-ci demandent **« le dernier contenu du jeu est-il battable, et
avec quoi ? »** — une civilisation arrivée au bout peut-elle abattre le dieu démon du Pandémonium ?

Il y a **deux manches, et elles n'ont pas le même verdict attendu** :

| Manche | État de départ | Attendu | Ce qu'elle mesure |
|---|---|---|---|
| `--pandemonium` | l'Ascension minimale d'un début de race — 7/15 pouvoirs, **pas Poing de Dieu** | **DÉFAITE** | ce que valent les seuls moyens militaires |
| `--pandemonium-ascended` | 20 Ascensions, 200 points divins, 15/15 pouvoirs, 9 races, tous les bâtiments uniques permanents | **VICTOIRE** | que le contenu final reste battable une fois la méta-progression faite |

Le code de sortie reflète **la conformité à l'attendu**, pas la victoire : la manche de base est un
échec voulu, et la faire échouer le build à chaque exécution en ferait un bruit permanent. Ce qu'on
veut détecter, c'est le jour où l'un des deux résultats change.

```bash
dotnet run --project SOIStrategyTester -c Release -- --pandemonium --seed 1 --max-island-hours 4
dotnet run --project SOIStrategyTester -c Release -- --pandemonium-ascended --seed 1
# verification de plomberie (~2 min) : petite ile, peu de villes
dotnet run --project SOIStrategyTester -c Release -- --pandemonium --seed 1 \
  --world-id 1 --underworld-cities 20 --abyss-cities 20 --max-island-hours 2
```

Ou double-cliquer `Pandemonium.bat` / `PandemoniumAscended.bat`. Sorties dans `--pandemonium-output`
(défaut `pandemonium/`, gitignoré) : `assault.csv`, `summary.json`, et la sauvegarde finale chargeable
dans le Desktop.

⚠️ **C'est lent, à l'échelle demandée.** ~260 villes signifient que chaque événement d'horloge les
parcourt toutes dans chaque contrôleur périodique, et que la seule règle de récolte clique près d'un
millier d'hexes par itération : compter une dizaine de minutes rien que pour fabriquer l'état, puis
autant par heure simulée d'assaut. Le verrouillage de la manche de base est visible dès la première
demi-heure, et se lit à l'identique sur `--world-id 1 --underworld-cities 20 --abyss-cities 20`, qui
donne le même verdict en deux minutes. La manche ascensionnée, elle, se termine en 51 itérations :
seule la fabrication y coûte du temps.

**Pour itérer sur la stratégie, repartir de la sauvegarde plutôt que de refabriquer.** La fabrication
prend une dizaine de minutes (~260 villes posées route par route, puis ~4 500 bâtiments) ; la rejouer à
chaque essai est du temps perdu, et deux fabrications ne sont comparables qu'à seed égal. Le même
siège est disponible en `PhaseKind.PandemoniumSiege`, donc en mode normal sur une sauvegarde :

```bash
dotnet run --project SOIStrategyTester -c Release -- --save pandemonium/pandemonium-Human-final.json \
  --objective Data/Objectives/demon-god-defeated.json --strategies Data/Strategies/pandemonium-siege.json
```

#### L'état de départ est fabriqué, pas joué

`EndGameStateFactory` : 100 % des recherches, 100 % des vertex de prestige, île de surface conquise et
entièrement bâtie, 100 villes dans l'Inframonde et 50 dans l'Abysse, tous bâtiments au niveau plafond,
puis le Pandémonium ouvert. Aucun autoplay ne sait amener une partie jusque-là — le gauntlet met huit
heures simulées à monter douze villes sur une île — et ce n'est pas ce qu'on mesure ici.

Ce qui reste passé par les vrais chemins : l'achat des vertex de prestige (seul chemin qui reconstruit
le cache de `PrestigeModifierProvider`), l'achat des pouvoirs divins (`AscensionController.PurchasePower`,
dans l'ordre des colonnes) et l'Ascension elle-même, le peuplement route par route (donc
l'auto-extension génère les couches comme en partie réelle), la pose du Grand Phare puis des Balises
Maritimes, et l'ouverture du Pandémonium par un Portail bâti auquel `PandemoniumGateController` réagit
sur l'horloge — c'est lui qui calcule le niveau des monstres.

Sont fabriqués, et documentés comme tels sur place : les *niveaux* de bâtiments et du Grand Phare
(leur coût d'amélioration dépasse la capacité de stockage bien avant le plafond) et les points de
recherche des routes du Vide de l'Abysse (1 000 000 puis ×4 à chaque route — cinquante villes
abyssales en demanderaient des dizaines, coût que rien dans le jeu ne finance). Monstres et
civilisations PNJ sont purgés partout **sauf** dans le Pandémonium, et la purge alterne avec
l'expansion (`SettleLayer`) : une civilisation PNJ debout interdit les vertex proches de ses villes, et
l'Abysse en fait naître une par île générée.

⚠️ **Ne jamais appeler `WorldVisibility.Recalculate()` depuis la fabrique.** La version globale
remplace son index par couche puis l'énumère en levant `HexesRevealed`, or l'auto-extension de l'Abysse
réagit à cet événement en faisant pousser une île — ce qui recalcule la visibilité et modifie l'index
en cours d'énumération. La fabrication échouait ainsi sur cinq seeds sur six
(`InvalidOperationException: Collection was modified`). Le jeu n'appelle jamais `Recalculate()` sur ce
chemin : utiliser `RecalculateFor(civIndex)`, comme lui.


#### Les deux manches se jouent sur une île endgame

L'Ascension régénère toujours la **première** île de l'atlas, la plus petite : 20 hexes de terre, 0 à 1
emplacement de Balise Maritime selon le seed, et une surface qui plafonne à 14-16 villes. Une manche de
fin de partie ne peut pas se mesurer là-dessus, donc la fabrique bascule explicitement sur l'île
`--world-id` (défaut **5**, la première que l'atlas marque `IsEndgameIsland`) en réécrivant le numéro
d'île puis en appelant `MainGameController.RestartIsland` — voir `EndGameStateFactory.SwitchToIsland`,
seul point de fabrication de la bascule, tout le reste étant le vrai chemin du jeu.

Ce que ça change, à seed 1 :

| | Île 1 (par défaut de l'Ascension) | Île 5 (`IsEndgameIsland`) |
|---|---|---|
| Hexes de terre | 20 | **165** |
| Eau | 21 | 115 |
| Emplacements de Balise Maritime | 0 | **33** |
| Balises posées par la fabrique | 0 | **33** |
| Villes de surface | 15 | **108** |
| Civilisations PNJ à purger | 0 | 4 à 6 |

C'est là que les Balises Maritimes servent enfin à quelque chose — et elles servent beaucoup : l'île
endgame tire sa forme au hasard et a une chance sur deux d'une île bonus, donc l'essentiel de la
surface n'est atteignable qu'en mer. L'autoplay n'en pose jamais ; la fabrique monte le Grand Phare au
niveau 3 dès que la terre ferme est épuisée, puis pose à chaque fois la balise la plus éloignée du
réseau, ce qui rouvre des routes maritimes vers l'île suivante. Les 33 emplacements de la carte sont
tous utilisés.

La ligne `surface :` du rapport donne ces nombres à chaque manche : une surface qui plafonne se lit
alors comme une limite de la carte, pas comme un manque de la fabrique.

#### La stratégie

`PandemoniumSiege` (aussi disponible en `PhaseKind.PandemoniumSiege`) tient en quatre règles :
**hors du Pandémonium on ne fait que récolter** (les 150 villes des profondeurs sont déjà au maximum,
leur rôle est d'alimenter le stock commun) ; **Poing de Dieu sur la cible dès qu'il est débloqué** ;
**dans l'arène on bâtit au maximum** (Mairie, puis garnison — Palissade/Caserne/Tour de guet —, puis
production, puis tout au plafond) ; **on raid les Tentacules une par une, puis le dieu démon**.

Deux écarts assumés avec l'autoplay standard : l'expansion est réécrite pour tirer la route vers l'hex
visé (celle de `TryExpandOnce` vise le vertex prospectif le plus proche du réseau, jamais l'ennemi —
c'est la limite mesurée au gauntlet), et le Camp Mobile sert de tête de pont jetable là où une ville ne
peut pas être bâtie.

#### Manche de base — DÉFAITE attendue, et le blocage n'est pas le boss

Le dieu démon n'est jamais touché, les huit Tentacules non plus, à aucune des échelles mesurées :

| Manche | Villes dans l'arène | Soldats | Vertex sûrs / constructibles | Cible | Tentacules tuées |
|---|---|---|---|---|---|
| île 5, 108 + 100 + 50, 1 h | 1 (12 routes, 17 bâtiments) | 82 | **0 / 9** | Tentacule à 3 hexes | 0 / 8 |
| île 1, 100 + 50, 2 h | 4 (89 routes, 68 bâtiments) | 328 | **0 / 47** | Tentacule à 3 hexes | 0 / 8 |
| île 1, 20 + 20, 1 h à 6 h | 1 | 82 | **0 / 9** | Tentacule à 3 hexes | 0 / 8 |

Figé dès la première demi-heure simulée, aucune ville perdue : le siège s'arrête, il ne se fait pas
détruire. Le front reste à **3 hexes** de la Tentacule la plus proche — un hex de trop — et la taille
de l'économie derrière n'y change rien : 259 villes et 4 509 bâtiments ne font pas mieux que 22 villes.
Trois faits, dans l'ordre où ils se verrouillent :

- **Aucun emplacement sûr, jamais.** Tous les vertex constructibles de l'arène sont à portée d'un
  monstre : neuf monstres à portée 2 couvrent presque tout un hexagone de 37 cases.
- **Fonder sous le feu est une perte sèche.** Une ville neuve n'a ni défense ni garnison et une
  Tentacule frappe pour 7 + 8 x niveau à deux hexes en ignorant la Palissade : elle tombe avant son
  premier bâtiment. Mesuré sur 6 h simulées : **321 villes fondées et perdues, zéro dégât infligé**,
  chacune au prix d'une fondation en couche profonde (le coût croît au carré du nombre de villes
  abyssales déjà tenues), jusqu'à assécher le Cristal de toute la civilisation. La stratégie s'en
  abstient donc.
- **Le Camp Mobile ne comble pas l'écart.** Il n'est proposé que là où une ville *ne peut pas* être
  bâtie, or les emplacements à portée sont justement constructibles ; et n'ayant aucun bâtiment il ne
  peut pas porter de Tour de guet, donc pas de frappe à distance 2 — il lui faudrait le corps-à-corps
  (distance 1), qu'aucun vertex accessible n'offre.

**C'est un échec attendu, pas un bug** : le Pandémonium est le contenu de fin de méta-progression, et
la manche de base représente un joueur qui ne l'a pas faite. Ce qu'elle établit, et qui mérite d'être
connu avant de toucher aux PV du boss, c'est que le blocage est **géométrique** — on ne peut pas
approcher l'arène — et non une question de dégâts, d'économie ou de durée.

#### Manche ascensionnée — VICTOIRE attendue, en 51 coups

Dieu démon abattu en **51 itérations** (25 secondes simulées), 8/8 Tentacules, sans qu'une seule ville
de l'arène ne soit bâtie ni un seul soldat recruté. Le levier est unique et c'est **Poing de Dieu** :
100 dégâts au monstre visé, réduits par la seule armure, à n'importe quelle distance et sur n'importe
quelle couche. 4 coups par Tentacule (360 PV, armure 1), 19 pour le dieu démon (1 800 PV, armure 4) —
51 en tout, pour 1 275 points de prestige sur les 20 085 en caisse (le coût monte de 1 à chaque usage
depuis le dernier prestige, d'où la dotation d'Ascension Prestigieuse comme nerf de la manche).

État de départ mesuré, seed 1, île 5 : 108 villes de surface + 100 Inframonde + 50 Abysse, 4 496
bâtiments (20 371 niveaux cumulés), 386 975 / 163 645 de stockage.

C'est bien Poing de Dieu et rien d'autre qui décide : la manche de base a elle aussi une Ascension
(`NewGameForRace` en fait une, avec Foi et le premier pouvoir de chaque colonne — 7 sur 15), mais Poing
de Dieu est le <b>deuxième</b> de sa colonne et lui manque. Les autres pouvoirs ne servent pas au
combat ici mais changent l'état : Bras de Dieu (+1 dégât par soldat, soit le double), Courroux de Dieu
(+100 % de cadence d'attaque), Inventaire Divin (x10 de stockage — 21 345 d'avancé sans lui, 163 645
avec, ce qui rend enfin payable une fondation de ville dans l'arène), et 16 bâtiments uniques
permanents contre 0.

**La conclusion d'équilibrage est double, et il faut lire les deux lignes ensemble** : le contenu final
est battable, mais uniquement par le pouvoir divin, et il l'est alors *trivialement* — 25 secondes,
aucune bataille. Le siège militaire que la stratégie sait mener n'entre jamais en jeu, ni dans un sens
ni dans l'autre. Si l'intention de design est que le Pandémonium soit un siège, c'est ce déséquilibre-là
qu'il faut regarder, pas les PV du dieu démon.

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
  Program.cs, StrategyRunner.cs, EndlessRunner.cs, RaceGauntletRunner.cs,
  ObjectiveEvaluator.cs, GameStateFactory.cs
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
| `DemonGodDefeated` | — | plus aucun `DemonGod` vivant — la victoire des manches `--pandemonium` |
| `TentaclesRemainingAtMost` | `count` | Tentacules encore vivantes dans le Pandémonium ≤ count |

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
  "kind": "Step1 | Step2 | Step3 | Military | UnifiedAggressive | ExterminateMonsters | ExterminateCivilizations | Wonder | Prestige | Priority | PandemoniumSiege",
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
