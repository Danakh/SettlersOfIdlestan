using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Controller.Military;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Drives NPC civilizations during actual gameplay:
/// - Runs autoplayer steps periodically to let NPCs build and expand.
/// - Escalates aggressivity to Warlike for non-Pacifist civs that get attacked.
/// - Passes encounter status (enemy spotted in visibility) so Warlike/Expansionist
///   civs expand freely until they spot an enemy, then stop expanding.
/// </summary>
public class NpcGameController
{
    private WorldState? _state;
    private GameClock? _clock;
    private MilitaryController? _militaryController;
    private AutoplayControllers? _autoplayControllers;

    /// <summary>Interval between NPC autoplayer turns (100 ticks = 1 s at normal speed).</summary>
    public const long NpcStepIntervalTicks = 100L;

    /// <summary>
    /// Décalage appliqué au tick de réflexion de chaque NPC (civ.Index * ce décalage), pour que
    /// toutes les civilisations NPC ne recalculent pas leur étape d'autoplay sur la même frame —
    /// répartit le coût de calcul (expansion notamment) sur plusieurs frames consécutives.
    /// </summary>
    public const long NpcStepStaggerTicks = 10L;

    private readonly Dictionary<int, long> _nextStepTickByCivIndex = new();

    /// <summary>
    /// Autoplayer conservé d'un tour à l'autre par civ NPC. En recréer un à chaque tour repartait
    /// systématiquement d'un cache froid — notamment celui des vertex prospectifs de
    /// <see cref="CivilizationAutoplayer"/>, qui reparcourt toute la carte visible — et réallouait la
    /// stratégie complète. Les cooldowns internes (clic, expansion) valent
    /// <see cref="NpcStepIntervalTicks"/>, soit exactement l'intervalle entre deux tours : les
    /// conserver ne bloque donc aucune action qui passait auparavant.
    /// Invalidé quand la civ change d'instance (nouvelle île) ou que son agressivité évolue
    /// (escalade vers Warlike dans <see cref="OnCityAttacked"/>), la stratégie en dépendant.
    /// </summary>
    private readonly Dictionary<int, (Civilization Civ, NpcAggressivityLevel Aggressivity, NpcCivilizationAutoplayer Autoplayer)> _autoplayerByCivIndex = new();

    public void Initialize(
        WorldState state,
        GameClock? clock,
        MilitaryController militaryController,
        AutoplayControllers autoplayControllers)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;
        if (_militaryController != null)
            _militaryController.SoldierAttackedCity -= OnCityAttacked;

        _state = state;
        _clock = clock;
        _militaryController = militaryController;
        _autoplayControllers = autoplayControllers;
        _nextStepTickByCivIndex.Clear();
        _autoplayerByCivIndex.Clear();

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
        _militaryController.SoldierAttackedCity += OnCityAttacked;
    }

    /// <summary>
    /// Purge ce que ce contrôleur garde au nom d'une civilisation retirée du monde — voir
    /// <see cref="WorldState.CivilizationRemoved"/>. L'autoplayer conservé retient la civilisation
    /// elle-même et toute sa stratégie (dont un cache de vertex prospectifs couvrant la carte
    /// visible) : sans cette purge, chaque civilisation PNJ éliminée en laisse un derrière elle pour
    /// le reste de l'île.
    /// </summary>
    internal void PurgeCivilizationCaches(int civilizationIndex)
    {
        _autoplayerByCivIndex.Remove(civilizationIndex);
        _nextStepTickByCivIndex.Remove(civilizationIndex);
    }

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try { Update(e.CurrentTick); }
        catch (Exception ex) { GameLog.Error(nameof(NpcGameController), nameof(Update), ex); }
    }

    /// <summary>
    /// Copie des civs PNJ à traiter ce tour, réutilisée d'un tick à l'autre. La copie reste
    /// nécessaire — un tour d'IA peut faire apparaître une civilisation (AutoExtend) et donc muter
    /// <c>WorldState.Civilizations</c> en cours d'itération — mais la reconstruire par LINQ à chaque
    /// tick allouait une liste et sa chaîne d'itérateurs pour rien.
    /// </summary>
    private readonly List<Civilization> _npcCivsBuffer = new();

    /// <summary>
    /// Laissé volontairement à une seule étape de décision par événement <c>Advanced</c> (pas de
    /// rattrapage par cycles via <see cref="Model.Game.TickCooldown"/>, contrairement aux
    /// comportements de production/consommation). <see cref="RunNpcStep"/> choisit UNE action
    /// stratégique (construire, étendre…) pour la civ PNJ, exactement comme le ferait un joueur
    /// humain à ce rythme — la simuler "cycles fois" en rafale pendant un saut de temps
    /// (TimeJumpService) ferait jouer plusieurs coups à la suite sans que rien de nouveau ne se
    /// soit produit entre eux, ce qui n'a pas de sens pour une décision plutôt qu'un taux.
    /// </summary>
    private void Update(long currentTick)
    {
        if (_state == null || _autoplayControllers == null) return;

        _npcCivsBuffer.Clear();
        var civilizations = _state.Civilizations;
        for (int i = 0; i < civilizations.Count; i++)
            if (civilizations[i].IsNpc)
                _npcCivsBuffer.Add(civilizations[i]);

        foreach (var civ in _npcCivsBuffer)
        {
            // Décale le premier tick de réflexion de chaque civ selon son index, pour que toutes les
            // civs NPC ne recalculent pas leur étape (expansion incluse) sur la même frame.
            if (!_nextStepTickByCivIndex.TryGetValue(civ.Index, out var nextStepTick))
                nextStepTick = civ.Index * NpcStepStaggerTicks;

            if (currentTick < nextStepTick) continue;

            _nextStepTickByCivIndex[civ.Index] = currentTick + NpcStepIntervalTicks;
            RunNpcStep(civ);
        }
    }

    private void RunNpcStep(Civilization civ)
    {
        if (_state == null || _autoplayControllers == null) return;

        var aggressivity = civ.NpcParameters?.AggressivityLevel ?? NpcAggressivityLevel.Cautious;

        // Aggressive civs stop expanding once they can see an enemy.
        bool hasEncounteredEnemy = aggressivity >= NpcAggressivityLevel.Expansionist
            && HasEncounteredEnemy(civ);

        var autoplayer = GetOrCreateAutoplayer(civ, aggressivity);
        UpdateNpcMilitaryFlows(civ, aggressivity, autoplayer.Inner);
        autoplayer.TryStepOnce(shouldExpand: !hasEncounteredEnemy);
    }

    /// <summary>Autoplayer de la civ, recréé uniquement si la civ ou son agressivité a changé (voir <see cref="_autoplayerByCivIndex"/>).</summary>
    private NpcCivilizationAutoplayer GetOrCreateAutoplayer(Civilization civ, NpcAggressivityLevel aggressivity)
    {
        if (_autoplayerByCivIndex.TryGetValue(civ.Index, out var cached) &&
            ReferenceEquals(cached.Civ, civ) && cached.Aggressivity == aggressivity)
            return cached.Autoplayer;

        var autoplayer = new NpcCivilizationAutoplayer(
            civ, _state!.GetMapForZ(IslandMap.SurfaceLayer)!, _autoplayControllers!, aggressivity, _militaryController,
            expandCooldownTicks: NpcStepIntervalTicks);
        _autoplayerByCivIndex[civ.Index] = (civ, aggressivity, autoplayer);
        return autoplayer;
    }

    /// <summary>
    /// Assigne les flux militaires (attaque / renfort) via PriorityTargetCivilization :
    /// - Warlike avec war enemies → attaque l'ennemi qui a attaqué en premier
    /// - Warlike sans war enemies → attaque le premier ennemi repéré (visibilité)
    /// - Cautious / Expansionist → pas d'attaque tant qu'ils n'ont pas été attaqués
    ///   (l'escalade vers Warlike dans OnCityAttacked prend le relais)
    /// </summary>
    private void UpdateNpcMilitaryFlows(Civilization civ, NpcAggressivityLevel aggressivity, CivilizationAutoplayer autoplayer)
    {
        if (_militaryController == null) return;

        var target = GetNpcPriorityTarget(civ, aggressivity);
        autoplayer.PriorityTargetCivilization = target;

        if (target != null)
        {
            autoplayer.TryUpdatePriorityTargetFlowsOnce();
        }
        else
        {
            foreach (var city in civ.Cities)
                _militaryController.SetCityFlow(city, null);
            _militaryController.UpdateCivilizationReinforcementFlows(civ);
        }
    }

    /// <summary>
    /// Détermine la civ cible à attaquer pour un NPC Warlike.
    /// Warlike avec war enemies → l'ennemi qui les a attaqués.
    /// Warlike sans war enemies → l'ennemi le plus proche déjà repéré (HasEncounteredEnemy).
    /// Toute autre aggressivité → null (Cautious/Expansionist n'attaquent qu'après escalade).
    /// </summary>
    private Civilization? GetNpcPriorityTarget(Civilization npcCiv, NpcAggressivityLevel aggressivity)
    {
        if (_state == null || aggressivity != NpcAggressivityLevel.Warlike) return null;

        var warEnemies = npcCiv.WarEnemyCivIndices;
        if (warEnemies.Count > 0)
            return _state.Civilizations.FirstOrDefault(c => warEnemies.Contains(c.Index) && c.MilitaryVertices.Count > 0);

        return HasEncounteredEnemy(npcCiv) ? FindNearestVisibleEnemy(npcCiv) : null;
    }

    private Civilization? FindNearestVisibleEnemy(Civilization npcCiv)
    {
        if (_state == null) return null;
        var z = npcCiv.Cities.FirstOrDefault()?.Position.Z ?? IslandMap.SurfaceLayer;
        if (!_state.Visibility.GetForZ(z).TryGetValue(npcCiv.Index, out var visibleMap)) return null;

        // Restreint aux villes de la même couche z : EdgeDistanceTo lève si on compare des vertex de
        // couches différentes, et une civ peut avoir des villes sur plusieurs couches (surface +
        // Inframonde/Abysse).
        var npcCitiesOnZ = npcCiv.Cities.Where(nc => nc.Position.Z == z).ToList();
        if (npcCitiesOnZ.Count == 0) return null;

        // Emplacements militaires adverses (villes, Flottes de Guerre, Camps Mobiles) : un Camp Mobile
        // repéré doit pouvoir déclencher le ciblage tout comme une ville, sans quoi un ennemi qui n'a
        // encore qu'un Camp Mobile visible ne serait jamais pris pour cible.
        return _state.Civilizations
            .Where(c => c.Index != npcCiv.Index)
            .Select(c => (Civ: c, VisibleVertices: c.MilitaryVertices.Where(v => v.Position.Z == z && visibleMap.IsVertexVisible(v.Position)).ToList()))
            .Where(x => x.VisibleVertices.Count > 0)
            .OrderBy(x => npcCitiesOnZ.Min(nc => x.VisibleVertices.Min(ev => nc.Position.EdgeDistanceTo(ev.Position))))
            .Select(x => x.Civ)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns true if the NPC civ's visible map already contains any hex belonging to a military
    /// vertex (city, War Fleet or Mobile Camp) from a different civilization.
    /// </summary>
    private bool HasEncounteredEnemy(Civilization npcCiv)
    {
        if (_state == null) return false;
        var z = npcCiv.Cities.FirstOrDefault()?.Position.Z ?? IslandMap.SurfaceLayer;
        if (!_state.Visibility.GetForZ(z).TryGetValue(npcCiv.Index, out var visibleMap)) return false;

        // Boucles indexées plutôt que Where/SelectMany/Any : appelé jusqu'à deux fois par tour d'IA
        // et par civilisation PNJ, sur tous les emplacements militaires de la carte — en fin de partie
        // plusieurs centaines. La chaîne LINQ allouait trois itérateurs et deux fermetures à chaque appel.
        var civilizations = _state.Civilizations;
        for (int i = 0; i < civilizations.Count; i++)
        {
            var other = civilizations[i];
            if (other.Index == npcCiv.Index) continue;

            var vertices = other.MilitaryVertices;
            for (int j = 0; j < vertices.Count; j++)
                if (visibleMap.IsVertexVisible(vertices[j].Position))
                    return true;
        }

        return false;
    }

    /// <summary>
    /// When a military vertex (city, War Fleet or Mobile Camp — <see cref="e"/>'s "City"-named
    /// positions are generic, see <see cref="CityAttackEventArgs"/>) is attacked: escalates a
    /// non-Pacifist NPC's aggressivity to Warlike, and records the attacker in the target's
    /// <see cref="Civilization.WarEnemyCivIndices"/> — including for the player, so their autoplayer
    /// can see who to retaliate against. Pacifist NPCs never retaliate, so they don't record war
    /// enemies either.
    /// </summary>
    private void OnCityAttacked(object? sender, CityAttackEventArgs e)
    {
        if (_state == null) return;

        var targetCiv = _state.Civilizations.FirstOrDefault(c =>
            c.MilitaryVertices.Any(v => v.Position.Equals(e.TargetCity)));
        if (targetCiv == null) return;

        if (targetCiv.IsNpc)
        {
            var npcParams = targetCiv.NpcParameters;
            if (npcParams == null || npcParams.AggressivityLevel == NpcAggressivityLevel.Pacifist) return;
            npcParams.AggressivityLevel = NpcAggressivityLevel.Warlike;
        }

        var attackerCiv = _state.Civilizations.FirstOrDefault(c =>
            c.MilitaryVertices.Any(v => v.Position.Equals(e.SourceCity)));
        if (attackerCiv != null && !targetCiv.WarEnemyCivIndices.Contains(attackerCiv.Index))
            targetCiv.WarEnemyCivIndices.Add(attackerCiv.Index);
    }

    private static void FillNpcResources(Civilization civ)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            int max = civ.GetResourceMaxQuantity(resource);
            if (max <= 0) continue;
            try { civ.AddResource(resource, max); }
            catch (Exception ex) { GameLog.Error(nameof(NpcGameController), $"AddResource {resource}", ex); }
        }
    }
}
