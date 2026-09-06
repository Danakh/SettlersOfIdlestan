using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Entretien de la Grande Forge de Mithril : une fois par seconde, chaque forge active prélève
/// <see cref="MithrilGreatForge.MithrilPerAdventurerPerSecond"/> Mithril par Aventurier en vie de sa
/// civilisation et, en échange, équipe tous ces Aventuriers (+1 dégât, +1 armure).
///
/// <para>Le bonus est porté par l'Aventurier lui-même
/// (<see cref="Adventurer.MithrilForgeAttackDamageBonus"/>) et non par un modificateur de
/// civilisation : les autres bonus d'Aventurier sont figés à l'invocation, alors que celui-ci doit
/// pouvoir tomber en cours de vie — forge désactivée, détruite, ou à court de Mithril.</para>
/// </summary>
internal sealed class MithrilGreatForgeEngine
{
    private WorldState? _state;

    /// <summary>Positions des villes dont les Aventuriers sont équipés au cycle courant.</summary>
    private readonly HashSet<Vertex> _equippedSpawnCities = new();

    /// <summary>
    /// Vrai tant qu'un Aventurier peut encore porter un bonus à retirer. Évite de balayer les features
    /// à chaque événement d'horloge quand aucune forge n'existe — le cas de l'immense majorité des
    /// parties. Vrai à l'initialisation : une sauvegarde rechargée peut contenir des Aventuriers
    /// équipés par une forge qui n'existe plus.
    /// </summary>
    private bool _mayHaveStaleBonuses = true;

    internal void Initialize(WorldState? state)
    {
        _state = state;
        _equippedSpawnCities.Clear();
        _mayHaveStaleBonuses = true;
    }

    internal void ResolveForgeUpkeep(long currentTick)
    {
        if (_state == null) return;

        CollectSuppliedForgeCities(currentTick);

        if (_equippedSpawnCities.Count == 0 && !_mayHaveStaleBonuses) return;

        bool anyEquipped = false;
        var features = _state.Features;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is not Adventurer adventurer) continue;

            bool equipped = adventurer.SpawnCityPosition != null
                         && _equippedSpawnCities.Contains(adventurer.SpawnCityPosition);

            adventurer.MithrilForgeAttackDamageBonus = equipped ? MithrilGreatForge.AdventurerAttackDamageBonus : 0;
            adventurer.MithrilForgeArmorBonus = equipped ? MithrilGreatForge.AdventurerArmorBonus : 0;
            anyEquipped |= equipped;
        }

        _mayHaveStaleBonuses = anyEquipped;
    }

    /// <summary>
    /// Remplit <see cref="_equippedSpawnCities"/> avec les Relais des Aventuriers des civilisations
    /// dont la Grande Forge tourne et vient d'être approvisionnée.
    ///
    /// <para>Le prélèvement n'a lieu qu'une fois par <see cref="MithrilGreatForge.UpkeepIntervalTicks"/>,
    /// et une seule fois par événement d'horloge : comme le tir des Spires de Défense, ce n'est pas un
    /// taux à rattraper — rejouer les cycles manqués d'un saut de temps viderait le stock de Mithril
    /// d'un coup pour un bonus de combat qui, lui, n'a pas été rendu rétroactivement.</para>
    ///
    /// <para>Faute de Mithril, <c>LastUpkeepTick</c> n'avance pas : la forge retente au tick suivant
    /// plutôt qu'à la prochaine seconde ronde, et ses Aventuriers restent désarmés entretemps.</para>
    /// </summary>
    private void CollectSuppliedForgeCities(long currentTick)
    {
        _equippedSpawnCities.Clear();

        var civilizations = _state!.Civilizations;
        for (int c = 0; c < civilizations.Count; c++)
        {
            var civ = civilizations[c];
            if (civ.GetUniqueBuilding(BuildingType.MithrilGreatForge) is not MithrilGreatForge forge) continue;
            if (forge.Level < 1 || forge.ActivationStatus != ActivationStatus.ACTIVE) continue;

            // Un Relais des Aventuriers = au plus un Aventurier vivant (voir
            // MonsterFeatureController.UpdateAdventurerSpawns) : compter les Relais revient à compter
            // les Aventuriers à équiper, sans avoir à remonter de chaque Aventurier à sa civilisation.
            var waypostCities = civ.GetCitiesWith(BuildingType.AdventurersWaypost);
            int adventurerCount = CountLiveAdventurers(waypostCities);

            if (currentTick - forge.LastUpkeepTick >= MithrilGreatForge.UpkeepIntervalTicks)
            {
                int cost = adventurerCount * MithrilGreatForge.MithrilPerAdventurerPerSecond;
                if (cost > 0 && civ.GetResourceQuantity(Resource.Mithril) < cost)
                {
                    civ.RaiseLowStock(Resource.Mithril);
                    continue;
                }

                if (cost > 0) civ.RemoveResource(Resource.Mithril, cost);
                forge.LastUpkeepTick = currentTick;
            }

            for (int i = 0; i < waypostCities.Count; i++)
                _equippedSpawnCities.Add(waypostCities[i].Position);
        }
    }

    /// <summary>Aventuriers réellement en vie invoqués par ces Relais — un Relais dont l'Aventurier vient de mourir ne coûte rien.</summary>
    private int CountLiveAdventurers(List<Model.Civilization.City> waypostCities)
    {
        if (waypostCities.Count == 0) return 0;

        int count = 0;
        var features = _state!.Features;
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is not Adventurer adventurer || adventurer.Hp <= 0) continue;
            if (adventurer.SpawnCityPosition == null) continue;
            for (int c = 0; c < waypostCities.Count; c++)
                if (waypostCities[c].Position.Equals(adventurer.SpawnCityPosition))
                {
                    count++;
                    break;
                }
        }
        return count;
    }
}
