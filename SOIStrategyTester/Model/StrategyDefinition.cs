using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;

namespace SOIStrategyTester.Model;

/// <summary>
/// One action the autoplayer repeatedly performs during a phase. Mirrors the methods already exposed
/// by CivilizationAutoplayer / CivilizationAutoplayerRunner, plus a Priority kind that drives the
/// sequential-objective PriorityAutoplayStrategy for fine-grained, single-step tuning.
/// </summary>
public enum PhaseKind
{
    /// <summary>auto.TryStep1Once(ShouldExpand).</summary>
    Step1,
    /// <summary>auto.TryStep2Once(ShouldExpand).</summary>
    Step2,
    /// <summary>auto.TryStep3Once(ShouldExpand).</summary>
    Step3,
    /// <summary>auto.TryMilitaryStepOnce().</summary>
    Military,
    /// <summary>auto.TryMilitaryStepOnce() + auto.TryStep2Once(true) — the high-level autoplayer loop
    /// StepIslandScenarios' ExterminateMonstersStep used before being replaced by a Priority phase
    /// (see SOIStrategyTester/Data/Best/island2-step2ter.best.json). Kept here for comparison baselines.</summary>
    ExterminateMonsters,
    /// <summary>Same as ExterminateMonsters, plus pointing idle cities' attack flow at the nearest enemy
    /// city — mirrors RunStepExterminateCivilizationsUntil.</summary>
    ExterminateCivilizations,
    /// <summary>auto.TryStep2Once(false) + auto.TryWonderInvestmentOnce() + auto.TryTradeForResourceOnce(Gold)
    /// — mirrors RunStepWonderUntil.</summary>
    Wonder,
    /// <summary>auto.TryPrestigeOnce(PrestigePriorityVertexNames) — performs the prestige transition.</summary>
    Prestige,
    /// <summary>Drives a PriorityAutoplayStrategy built from PriorityObjectives.</summary>
    Priority,
}

public enum PriorityObjectiveKind
{
    /// <summary>BuildingLevelObjective(Buildings, TargetLevel).</summary>
    BuildingLevel,
    /// <summary>CityCountObjective(TargetCityCount).</summary>
    CityCount,
    /// <summary>ImperialPortObjective — focuses the first coastal city on Seaport/Warehouse/TownHall 4
    /// then the Imperial Port itself. Needs no extra fields.</summary>
    ImperialPort,
}

public class PriorityObjectiveSpec
{
    public PriorityObjectiveKind Kind { get; set; }
    public List<BuildingType>? Buildings { get; set; }
    public int? TargetLevel { get; set; }
    public int? TargetCityCount { get; set; }
}

public class StrategyPhase
{
    public PhaseKind Kind { get; set; }

    /// <summary>Passed through to Step1/Step2/Step3.</summary>
    public bool ShouldExpand { get; set; } = true;

    /// <summary>Names of public static Vertex fields on SettlersOfIdlestan.Model.Prestige.PrestigeMap.PrestigeMap
    /// (e.g. "CentralVertex", "BarracksVertex") to purchase first, deterministically, during a Prestige phase.</summary>
    public List<string>? PrestigePriorityVertexNames { get; set; }

    /// <summary>Required when Kind == Priority.</summary>
    public List<PriorityObjectiveSpec>? PriorityObjectives { get; set; }

    /// <summary>The condition that ends this phase and moves on to the next one. Leave null on the last
    /// phase of a strategy — it then simply runs until the run's global objective is satisfied.</summary>
    public ObjectiveSpec? Until { get; set; }

    /// <summary>Overrides the run's default max-iterations-per-phase budget for this phase specifically
    /// (some phases, like extermination, legitimately need tens of thousands of iterations).</summary>
    public int? MaxIterations { get; set; }
}

public class StrategyDefinition
{
    public string Name { get; set; } = "";
    public List<StrategyPhase> Phases { get; set; } = new();
}
