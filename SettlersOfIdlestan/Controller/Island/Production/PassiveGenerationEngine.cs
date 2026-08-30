using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island.Production;

/// <summary>
/// Génération passive de ressources, indépendante de toute ville ou bâtiment particulier
/// (<c>PASSIVE_RESOURCE_GENERATION</c> : Grotte aux Perles, Arbre-Cœur, vertex de prestige…), plus
/// les Cristaux produits par Laboratoire (<c>CRYSTAL_GENERATION_PER_LABORATORY</c>).
/// </summary>
internal sealed class PassiveGenerationEngine
{
    private WorldState? _state;

    private long _lastPassiveGenTick;
    private long _lastPassiveCrystalGenTick;

    // Reste fractionnaire de cristaux non encore distribué par CRYSTAL_GENERATION_PER_LABORATORY, par civilisation
    // (valeur < 1 : perLab × nb labos n'est presque jamais un entier — voir PerformLaboratoryCrystalGeneration).
    private readonly Dictionary<int, double> _laboratoryCrystalCarry = new();

    internal void Initialize(WorldState? state, GameClock? clock)
    {
        _state = state;

        // Non persistés (recréés/réinitialisés à chaque Initialize, y compris au chargement d'une
        // sauvegarde) : les seeder au tick courant plutôt qu'à 0, sinon TickCooldown calcule un
        // nombre de cycles de rattrapage proportionnel à tout le tick courant sur une partie déjà
        // avancée (potentiellement des millions) au lieu du léger différé attendu en début de partie.
        _lastPassiveGenTick = clock?.CurrentTick ?? 0;
        _lastPassiveCrystalGenTick = clock?.CurrentTick ?? 0;
    }

    /// <summary>Voir <c>HarvestController.PurgeCivilizationCaches</c>.</summary>
    internal void PurgeCivilizationCaches(int civilizationIndex)
        => _laboratoryCrystalCarry.Remove(civilizationIndex);

    internal void Tick(long currentTick)
    {
        if (_state == null) return;

        long generalLast = _lastPassiveGenTick;
        long generalCycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref generalLast, HarvestController.PassiveResourceGenerationIntervalTicks);
        _lastPassiveGenTick = generalLast;

        long crystalLast = _lastPassiveCrystalGenTick;
        long crystalCycles = TickCooldown.ConsumeElapsedCycles(currentTick, ref crystalLast, HarvestController.PassiveCrystalGenerationIntervalTicks);
        _lastPassiveCrystalGenTick = crystalLast;

        if (generalCycles <= 0 && crystalCycles <= 0) return;

        foreach (var civ in _state.Civilizations)
        {
            foreach (Resource resource in Enum.GetValues<Resource>())
            {
                long cycles = resource == Resource.Crystal ? crystalCycles : generalCycles;
                if (cycles <= 0) continue;

                int amount = civ.ModifierAggregator.ApplyModifiers(
                    ECategory.PASSIVE_RESOURCE_GENERATION, resource.ToString(), 0);
                if (amount > 0)
                {
                    try { civ.AddResource(resource, amount * (int)cycles); }
                    catch (Exception ex) { GameLog.Error(nameof(HarvestController), $"AddResource {resource}", ex); }
                }
            }

            if (crystalCycles > 0)
                PerformLaboratoryCrystalGeneration(civ, crystalCycles);
        }
    }

    /// <summary>
    /// Applique CRYSTAL_GENERATION_PER_LABORATORY (ex. vertex de prestige Distillation Magique) : valeur agrégée
    /// (typiquement &lt; 1) × nombre de Laboratoires construits (niveau ≥ 1) × cycles écoulés. Le reste
    /// fractionnaire est reporté au cycle suivant par civilisation, pour ne jamais perdre de production même
    /// avec peu de Laboratoires.
    /// </summary>
    private void PerformLaboratoryCrystalGeneration(Civilization civ, long cycles)
    {
        double perLaboratory = civ.ModifierAggregator.ApplyModifiers(ECategory.CRYSTAL_GENERATION_PER_LABORATORY, "", 0.0);
        if (perLaboratory <= 0) return;

        int laboratoryCount = civ.Cities.Sum(c => c.Buildings.Count(b => b.Type == BuildingType.Laboratory && b.Level >= 1));
        if (laboratoryCount == 0) return;

        _laboratoryCrystalCarry.TryGetValue(civ.Index, out double carry);
        carry += perLaboratory * laboratoryCount * cycles;
        int whole = (int)carry;
        if (whole > 0)
        {
            try { civ.AddResource(Resource.Crystal, whole); }
            catch (Exception ex) { GameLog.Error(nameof(HarvestController), "AddResource Crystal (laboratory)", ex); }
            carry -= whole;
        }
        _laboratoryCrystalCarry[civ.Index] = carry;
    }
}
