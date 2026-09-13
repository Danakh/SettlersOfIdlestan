using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Military;

/// <summary>
/// Gère l'action Raid : redirige tous les flux de la civilisation du joueur vers une cible — un
/// emplacement militaire ennemi (ville ou Flotte de Guerre — voir IMilitaryVertex) ou une
/// MonsterFeature. Les emplacements à portée d'attaque attaquent directement; les autres renforcent
/// l'allié le plus proche de la cible.
///
/// <para><b>Un raid par layer, en parallèle.</b> Un raid ne réquisitionne que les emplacements
/// militaires du layer de sa cible : rien n'empêche donc plusieurs raids de coexister, un par layer
/// (voir <see cref="AutomationSettings.RaidsByLayer"/>). Chacun a sa propre cible, son propre
/// entretien qui escalade pour son compte et s'arrête indépendamment des autres. Côté joueur, le
/// bouton Raid ne parle que du layer regardé : il n'est actif (rouge) que si ce layer-là est en train
/// de raider, et le recliquer n'annule que ce raid — les guerres des autres layers continuent. C'est
/// le même découpage que la Vendetta (<see cref="AutomationSettings.VendettaTargetCivIndexByLayer"/>,
/// une civilisation ciblée par layer), qui alimente désormais un raid par layer simultanément.</para>
/// </summary>
internal class RaidEngine
{
    private WorldState? _state;
    private GameClock? _clock;
    private CityAttackEngine? _cityAttackEngine;
    private ReinforcementEngine? _reinforcementEngine;
    private MonsterCombatEngine? _monsterCombatEngine;
    private SoldierProductionEngine? _productionEngine;

    /// <summary>Entretien en or débité à la première seconde d'un raid ; il croît ensuite de 2 par seconde (voir PayUpkeep).</summary>
    internal const int InitialUpkeep = 10;

    private const long RaidCheckIntervalTicks = 100L;

    /// <summary>Dernier cycle d'entretien facturé, par layer raidé. Purement runtime (non sérialisé) :
    /// une entrée absente vaut 0, ce qui fait simplement payer le raid dès le cycle suivant.</summary>
    private readonly Dictionary<int, long> _lastRaidCheckTickByLayer = new();

    /// <summary>Tampon des layers à examiner dans <see cref="Update"/>, réutilisé d'un événement
    /// d'horloge à l'autre : itérer directement sur les clés du dictionnaire est impossible
    /// (StopRaid en retire une entrée), et le copier à chaque tick allouerait sur le chemin chaud.</summary>
    private readonly List<int> _raidLayerScratch = new();

    private long _lastPlayerAutoVendettaTick = 0;

    private long _lastPlayerBlitzTick = 0;

    internal void Initialize(WorldState? state, GameClock? clock, CityAttackEngine cityAttackEngine, ReinforcementEngine reinforcementEngine, MonsterCombatEngine monsterCombatEngine, SoldierProductionEngine productionEngine)
    {
        _state = state;
        _clock = clock;
        _cityAttackEngine = cityAttackEngine;
        _reinforcementEngine = reinforcementEngine;
        _monsterCombatEngine = monsterCombatEngine;
        _productionEngine = productionEngine;
        _lastRaidCheckTickByLayer.Clear();
    }

    internal bool IsRaidUnlocked(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_RAID);

    internal bool IsWarHeraldUnlocked(Civilization civ)
        => civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_WAR_HERALD);

    /// <summary>Vrai si un raid est en cours <b>sur ce layer</b> — les raids des autres layers ne comptent pas.</summary>
    internal bool IsRaidActive(int layerZ)
        => _state?.AutomationSettings.IsRaidActiveOnLayer(layerZ) ?? false;

    internal Vertex? GetRaidTarget(int layerZ)
        => _state?.AutomationSettings.GetRaid(layerZ)?.TargetVertex;

    internal HexCoord? GetRaidTargetHex(int layerZ)
        => _state?.AutomationSettings.GetRaid(layerZ)?.TargetHex;

    internal List<Vertex> GetSelectableTargets(Civilization playerCiv)
    {
        if (_state == null) return new List<Vertex>();
        int currentLayer = _state.CurrentViewedLayer;
        var targets = new List<Vertex>();
        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerCiv.Index) continue;
            foreach (var vertex in civ.MilitaryVertices)
            {
                if (vertex.Position.Z == currentLayer && IsCityVisibleTo(vertex, playerCiv))
                    targets.Add(vertex.Position);
            }
        }
        return targets;
    }

    /// <summary>Emplacements militaires de la civilisation elle-même (villes, flottes, camps mobiles), ciblables par War Herald.</summary>
    internal List<Vertex> GetSelectableAlliedTargets(Civilization civ)
        => civ.MilitaryVertices.Select(v => v.Position).ToList();

    /// <summary>
    /// Monstres ciblables par un raid : sur la couche affichée, et surtout <b>actuellement visibles</b>
    /// pour le joueur. <see cref="IslandFeature.Found"/> ne suffit pas — c'est un
    /// drapeau collant posé à la découverte (voir FeatureController.DiscoverFeatures) : un monstre qui
    /// s'éloigne ensuite dans le brouillard de guerre le garde à true et restait proposé comme cible
    /// alors que son hexagone n'est plus visible.
    /// </summary>
    internal List<HexCoord> GetSelectableMonsterTargets()
    {
        if (_state == null) return new List<HexCoord>();
        int currentLayer = _state.CurrentViewedLayer;
        var playerCiv = _state.PlayerCivilization;
        return _state.Features.OfType<MonsterFeature>()
            .Where(m => m.Found && m.Position.Z == currentLayer && m is not Adventurer && IsHexVisibleTo(m.Position, playerCiv))
            .Select(m => m.Position)
            .ToList();
    }

    private bool IsCityVisibleTo(IMilitaryVertex vertex, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(vertex.Position.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return visibleMap.IsVertexVisible(vertex.Position);
    }

    private bool IsHexVisibleTo(HexCoord hex, Civilization civ)
    {
        var visibleMaps = _state!.Visibility.GetForZ(hex.Z);
        if (!visibleMaps.TryGetValue(civ.Index, out var visibleMap)) return true;
        return visibleMap.HasTile(hex);
    }

    private const int NearestCitiesCheckedForSoldierCapacity = 3;

    /// <summary>
    /// Installe (ou remplace) le raid du layer donné et cale son compteur d'entretien sur l'instant
    /// présent, pour que sa première seconde soit facturée une seconde après son lancement et non au
    /// tick suivant — <see cref="_lastRaidCheckTickByLayer"/> conserverait sinon la date du dernier
    /// cycle du raid précédent de ce layer, voire 0 s'il n'y en a jamais eu.
    /// </summary>
    private void SetRaid(int layerZ, RaidState raid)
    {
        _state!.AutomationSettings.RaidsByLayer[layerZ] = raid;
        _lastRaidCheckTickByLayer[layerZ] = _clock?.CurrentTick ?? 0L;
    }

    internal void StartRaid(Civilization civ, Vertex targetCityVertex)
    {
        if (_state == null) return;
        _state.AutomationSettings.WarHeraldTargetVertex = null;
        SetRaid(targetCityVertex.Z, new RaidState { TargetVertex = targetCityVertex, CurrentUpkeep = InitialUpkeep });
        ApplyRaidFlows(civ, targetCityVertex);

        // Vendetta : un raid manuel du joueur sur une ville ennemie met à jour la civilisation ciblée
        // par les raids automatiques sur le layer de cette ville (voir ResolvePlayerAutoVendetta
        // ci-dessous) — les cibles des autres layers ne sont pas touchées.
        if (civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_VENDETTA))
        {
            var targetCiv = _state.Civilizations.FirstOrDefault(c => c.MilitaryVertices.Any(v => v.Position.Equals(targetCityVertex)));
            if (targetCiv != null)
                _state.AutomationSettings.VendettaTargetCivIndexByLayer[targetCityVertex.Z] = targetCiv.Index;
        }

        var nearestCities = civ.Cities
            .Where(c => c.Position.Z == targetCityVertex.Z)
            .OrderBy(c => c.Position.EdgeDistanceTo(targetCityVertex));
        WarnIfNoSoldierCapacityNearTarget(nearestCities);
    }

    internal void StartMonsterRaid(Civilization civ, HexCoord targetHex)
    {
        if (_state == null) return;
        _state.AutomationSettings.WarHeraldTargetVertex = null;
        SetRaid(targetHex.Z, new RaidState { TargetHex = targetHex, CurrentUpkeep = InitialUpkeep });
        ApplyMonsterRaidFlows(civ, targetHex);

        var nearestCities = civ.Cities
            .Where(c => c.Position.Z == targetHex.Z)
            .OrderBy(c => c.Position.GetHexes().Max(h => h.DistanceTo(targetHex)));
        WarnIfNoSoldierCapacityNearTarget(nearestCities);
    }

    /// <summary>
    /// War Herald : raid gratuit et instantané sur un emplacement militaire allié (ville, Flotte de
    /// Guerre ou Camp Mobile de la civilisation elle-même). Redirige le flux de chaque emplacement
    /// militaire de la civilisation (sur le même layer que la cible) vers la cible si elle est à
    /// portée de renfort, sinon vers l'allié le plus proche de la cible qui est lui-même à portée de
    /// renfort — même logique de relais que le Raid classique (voir ApplyRaidFlows). Les emplacements
    /// ayant un flux d'attaque actif (ville ennemie ou monstre) ne sont pas redirigés — contrairement
    /// au Raid classique, aucun upkeep et aucun suivi dans le temps. La cible elle-même annule son
    /// propre flux de renfort (mais garde un flux d'attaque actif s'il y en a un) : elle ne doit pas
    /// continuer à s'écouler vers un autre allié pendant qu'elle est renforcée.
    /// Réactiver le War Herald sur la cible déjà visée (voir AutomationSettings.WarHeraldTargetVertex)
    /// désactive tous les flux de renfort au lieu de les rediriger, en guise d'interrupteur.
    /// </summary>
    internal void StartWarHeraldRaid(Civilization civ, Vertex target)
    {
        if (_reinforcementEngine == null) return;

        if (_state != null && target.Equals(_state.AutomationSettings.WarHeraldTargetVertex))
        {
            _state.AutomationSettings.WarHeraldTargetVertex = null;
            _reinforcementEngine.ClearReinforcementFlows(civ);
            return;
        }
        if (_state != null) _state.AutomationSettings.WarHeraldTargetVertex = target;

        // L'auto-renfort (UpdateCivilizationReinforcementFlows, voir ReinforcementEngine.ResolvePlayerAutoReinforcement)
        // tourne toutes les AutoReinforcementIntervalTicks (~1s) et redirige tout emplacement dont le
        // flux actuel n'est plus une cible éligible — ce qui annule quasi immédiatement la redirection
        // du War Herald tant que les deux automatisations sont actives en même temps.
        if (_state != null
            && _state.AutomationSettings.IsMilitaryReinforcementAutomationActive
            && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_REINFORCEMENT))
        {
            _state.EventLog.Add(GameEventType.WarHeraldAutoReinforcementConflict, toast: true);
        }

        int targetZ = target.Z;
        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        var verticesInLayer = civ.MilitaryVertices.Where(v => v.Position.Z == targetZ).ToList();

        foreach (var vertex in verticesInLayer)
        {
            bool hasActiveAttackFlow = vertex.MonsterAttackTarget != null
                || (vertex.FlowTarget != null && _reinforcementEngine.IsEnemyCityAt(vertex.FlowTarget, civ));
            if (hasActiveAttackFlow) continue;

            if (vertex.Position.Equals(target))
            {
                if (vertex.FlowTarget != null)
                    _reinforcementEngine.SetCityFlow(vertex, null);
                continue;
            }

            int distToTarget = vertex.Position.EdgeDistanceTo(target);
            if (distToTarget <= reinforcementRange)
            {
                _reinforcementEngine.SetCityFlow(vertex, target);
            }
            else
            {
                // Renforce l'allié le plus proche de la cible qui est aussi à portée de renfort
                var nearestAlly = verticesInLayer
                    .Where(a => a != vertex
                             && a.Position.EdgeDistanceTo(target) < distToTarget
                             && vertex.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => a.Position.EdgeDistanceTo(target))
                    .FirstOrDefault();

                _reinforcementEngine.SetCityFlow(vertex, nearestAlly?.Position);
            }
        }
    }

    /// <summary>
    /// Avertit le joueur si une des villes les plus proches de la cible ne peut accueillir aucun soldat
    /// (vulnérable en cas de contre-attaque). Le critère est la capacité <b>effective</b>
    /// (<see cref="SoldierProductionEngine.GetMaximumSoldierCapacity"/>), pas la présence d'une Caserne :
    /// une capacité nulle n'arrive qu'en tout début de partie, où la Caserne est effectivement le seul
    /// moyen d'ouvrir des places — d'où le libellé du toast (event_raid_missing_barracks_*). Dès le
    /// premier bonus civ-wide (CITY_MAX_SOLDIERS_BONUS), plus aucune ville n'est concernée.
    /// Ne concerne que les villes — une Flotte de Guerre n'a jamais de bâtiment (voir WarFleet) mais a
    /// toujours une capacité fixe non nulle, donc n'est pas prise en compte par cet avertissement.
    /// </summary>
    private void WarnIfNoSoldierCapacityNearTarget(IEnumerable<City> citiesOrderedByDistance)
    {
        if (_state == null) return;
        bool anyWithoutCapacity = citiesOrderedByDistance
            .Take(NearestCitiesCheckedForSoldierCapacity)
            .Any(c => _productionEngine!.GetMaximumSoldierCapacity(c) == 0);
        if (anyWithoutCapacity)
            _state.EventLog.Add(GameEventType.RaidMissingBarracks, toast: true);
    }

    /// <summary>
    /// Arrête le Raid d'un layer à la demande explicite du joueur (bouton Raid recliqué alors que ce
    /// layer est en train de raider). Contrairement à un arrêt automatique (cible détruite/hors de
    /// vue, upkeep impayé — voir Update/StopRaid), oublie aussi la cible Vendetta <b>de ce layer</b>
    /// (voir <see cref="AutomationSettings.VendettaTargetCivIndexByLayer"/>) : après une interruption
    /// volontaire, Vendetta ne doit pas y reprendre automatiquement le même combat mais attendre un
    /// nouveau déclencheur (nouveau raid manuel ou attaque subie). Les guerres des autres layers ne
    /// sont pas touchées — elles se mènent en parallèle et le joueur n'a demandé l'arrêt que de
    /// celle-ci.
    /// </summary>
    internal void CancelRaid(Civilization civ, int layerZ)
    {
        StopRaid(civ, layerZ);
        _state?.AutomationSettings.VendettaTargetCivIndexByLayer.Remove(layerZ);
    }

    /// <summary>
    /// Arrête tous les raids en cours et oublie toutes les cibles Vendetta — bascule de
    /// l'automatisation Vendetta, qui ne vise aucun layer en particulier (voir AutomationRenderer).
    /// </summary>
    internal void CancelAllRaids(Civilization civ)
    {
        if (_state == null) return;
        CollectRaidLayers();
        foreach (int layerZ in _raidLayerScratch)
            StopRaid(civ, layerZ);
        _state.AutomationSettings.VendettaTargetCivIndexByLayer.Clear();
    }

    /// <summary>Arrête le raid du layer donné et libère les flux qu'il avait réquisitionnés. Sans effet si ce layer ne raide pas.</summary>
    internal void StopRaid(Civilization civ, int layerZ)
    {
        if (_state == null) return;
        if (!_state.AutomationSettings.RaidsByLayer.Remove(layerZ, out var raid)) return;
        _lastRaidCheckTickByLayer.Remove(layerZ);

        bool wasMonsterRaid = raid.TargetHex != null;
        foreach (var vertex in civ.MilitaryVertices)
        {
            if (vertex.Position.Z != layerZ) continue;
            if (wasMonsterRaid) vertex.MonsterAttackTarget = null;
            _reinforcementEngine!.SetCityFlow(vertex, null);
        }
    }

    /// <summary>Recopie les layers actuellement raidés dans <see cref="_raidLayerScratch"/>, pour pouvoir les parcourir en modifiant le dictionnaire.</summary>
    private void CollectRaidLayers()
    {
        _raidLayerScratch.Clear();
        foreach (int layerZ in _state!.AutomationSettings.RaidsByLayer.Keys)
            _raidLayerScratch.Add(layerZ);
    }

    /// <summary>
    /// Recherche Vendetta : tant qu'une civilisation est ciblée sur un layer (voir
    /// <see cref="AutomationSettings.VendettaTargetCivIndexByLayer"/>, mis à jour par StartRaid et
    /// CityAttackEngine.ResolveCityAttacks) et qu'aucun Raid n'y est en cours, relance automatiquement
    /// un Raid classique (mêmes upkeep et relais de renfort — voir StartRaid/ApplyRaidFlows) sur la
    /// ville la plus proche de cette civilisation, sans intervention du joueur.
    /// Un raid par layer, tous menés de front : chaque guerre avance chez elle sans attendre les
    /// autres, puisqu'un raid ne réquisitionne que les emplacements militaires de son propre layer.
    /// La cible d'un layer n'est abandonnée que lorsqu'elle n'y a plus aucun emplacement militaire ;
    /// tant qu'elle y survit sans être atteignable (brouillard de guerre, plus aucun emplacement à
    /// nous sur ce layer), la cible est conservée et ce layer attend simplement son heure.
    /// </summary>
    internal void ResolvePlayerAutoVendetta(long currentTick)
    {
        if (_state == null || _cityAttackEngine == null) return;
        if (!_state.AutomationSettings.IsMilitaryVendettaAutomationActive) return;

        // Blitz coché : la guerre éclair remplace entièrement l'enchaînement de raids ci-dessous
        // (voir ResolvePlayerBlitz). Les deux ne peuvent pas tourner ensemble sur un même layer —
        // ApplyRaidFlows réquisitionne tous les emplacements du plan du raid à chaque cycle
        // d'entretien et renverrait en renfort ceux que le Blitz vient de lancer à l'assaut, une fois
        // par seconde ; c'est le Blitz qui cède, layer par layer (voir ResolvePlayerBlitz).
        // Les cibles de Vendetta continuent d'être enregistrées pendant ce temps (voir StartRaid et
        // CityAttackEngine.ResolveCityAttacks) : décocher Blitz reprend la guerre là où elle en est.
        if (_state.AutomationSettings.IsMilitaryBlitzActive
            && _state.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_BLITZ))
        {
            if (currentTick - _lastPlayerBlitzTick < MilitaryController.AutoVendettaIntervalTicks) return;
            _lastPlayerBlitzTick = currentTick;
            ResolvePlayerBlitz(_state.PlayerCivilization);
            return;
        }

        var targetsByLayer = _state.AutomationSettings.VendettaTargetCivIndexByLayer;
        if (targetsByLayer.Count == 0) return;
        if (currentTick - _lastPlayerAutoVendettaTick < MilitaryController.AutoVendettaIntervalTicks) return;
        _lastPlayerAutoVendettaTick = currentTick;

        var playerCiv = _state.PlayerCivilization;
        if (!playerCiv.ModifierAggregator.HasModifier(ECategory.UNLOCK_VENDETTA)) return;

        // Copie des clés : une guerre terminée retire son entrée en cours de parcours. Layers traités
        // du moins profond au plus profond — sans incidence sur le résultat depuis que chacun lance
        // son propre raid, mais l'ordre reste déterministe.
        foreach (int layerZ in targetsByLayer.Keys.OrderBy(z => z).ToList())
        {
            int targetCivIndex = targetsByLayer[layerZ];
            var targetCiv = _state.GetCivilization(targetCivIndex);

            // La cible d'un layer est tenue pour morte — et la guerre de ce layer terminée — dès qu'elle
            // n'y a plus d'emplacement militaire, même si elle survit ailleurs.
            if (targetCiv == null || !HasMilitaryVertexOnLayer(targetCiv, layerZ))
            {
                targetsByLayer.Remove(layerZ);
                continue;
            }

            // Raid déjà en cours sur ce layer (relancé au cycle précédent, ou lancé à la main par le
            // joueur) : on le laisse aller à son terme avant d'en désigner un autre ici.
            if (IsRaidActive(layerZ)) continue;

            // Cherche la ville ennemie de la civilisation ciblée la plus proche de n'importe lequel de nos
            // emplacements de ce layer, sans limite de portée (contrairement à un Raid manuel classique,
            // la cible n'est ici jamais choisie par le joueur).
            var targetCivIndices = new[] { targetCivIndex };
            IMilitaryVertex? nearestEnemy = null;
            int nearestDist = int.MaxValue;
            var playerVertices = playerCiv.MilitaryVertices;
            for (int i = 0; i < playerVertices.Count; i++)
            {
                var vertex = playerVertices[i];
                if (vertex.Position.Z != layerZ) continue;
                var enemy = _cityAttackEngine.FindNearbyEnemyCity(vertex, targetCivIndices, maxRange: int.MaxValue);
                if (enemy == null) continue;
                int dist = vertex.Position.EdgeDistanceTo(enemy.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemy = enemy;
                }
            }
            // Cible encore vivante mais injoignable pour l'instant (brouillard de guerre, aucune ville à
            // nous sur ce layer) : on garde la cible, ce layer reprendra la guerre plus tard.
            if (nearestEnemy == null) continue;

            StartRaid(playerCiv, nearestEnemy.Position);
        }
    }

    /// <summary>
    /// Blitz (recherche du même nom, case cochée à côté de Vendetta) : guerre totale et permanente,
    /// sans déclencheur ni cible désignée. Chaque emplacement militaire du joueur qui n'est pas déjà
    /// engagé — ni flux d'attaque, ni attaque de monstre en cours — prend pour cible la ville ennemie
    /// la plus proche à sa portée d'attaque, quelle que soit la civilisation à qui elle appartient.
    /// Tous les fronts d'un même layer avancent donc en même temps, là où la Vendetta seule concentre
    /// le layer sur un raid unique : en contrepartie le Blitz ne porte qu'à portée d'attaque (aucun
    /// relais de renfort, aucune cible hors de vue) et ne coûte aucun entretien.
    ///
    /// <para>Un emplacement déjà lancé à l'assaut n'est pas réexaminé : c'est CityAttackEngine qui
    /// annule un flux d'attaque devenu impossible (cible détruite, hors de vue, chemin coupé), et le
    /// passage suivant lui trouve alors une nouvelle cible.</para>
    ///
    /// <para>Les layers où un Raid est en cours (lancé à la main par le joueur — la Vendetta, elle,
    /// n'en lance plus tant que le Blitz est coché) sont laissés de côté : le raid y réquisitionne
    /// tous les emplacements à chaque cycle d'entretien et défaire son travail une fois par seconde
    /// ne ferait que faire osciller les flux. Le Blitz y reprend la main dès la fin du raid.</para>
    /// </summary>
    private void ResolvePlayerBlitz(Civilization playerCiv)
    {
        if (_cityAttackEngine == null || _reinforcementEngine == null) return;

        var raids = _state!.AutomationSettings.RaidsByLayer;

        // Boucle indexée et sortie anticipée sur les emplacements déjà engagés : en fin de partie
        // cette passe voit plusieurs centaines d'emplacements, chacun comparé à tous les emplacements
        // ennemis de son plan (voir FindNearbyEnemyCity), une fois par seconde.
        var vertices = playerCiv.MilitaryVertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            if (vertex.MonsterAttackTarget != null) continue;
            if (vertex.FlowTarget != null && _reinforcementEngine.IsEnemyCityAt(vertex.FlowTarget, playerCiv)) continue;
            if (raids.Count > 0 && raids.ContainsKey(vertex.Position.Z)) continue;

            var enemy = _cityAttackEngine.FindNearbyEnemyCity(vertex);
            if (enemy == null) continue;
            _reinforcementEngine.SetCityFlow(vertex, enemy.Position);
        }
    }

    private static bool HasMilitaryVertexOnLayer(Civilization civ, int layerZ)
    {
        var vertices = civ.MilitaryVertices;
        for (int i = 0; i < vertices.Count; i++)
            if (vertices[i].Position.Z == layerZ) return true;
        return false;
    }

    /// <summary>Fait vivre chaque raid en cours, chacun pour son propre compte : ils ne partagent ni cible, ni entretien, ni destin.</summary>
    internal void Update(long currentTick)
    {
        if (_state == null) return;
        var raids = _state.AutomationSettings.RaidsByLayer;
        if (raids.Count == 0) return;

        var playerCiv = _state.PlayerCivilization;

        // Copie des layers : UpdateRaid peut arrêter un raid, donc retirer son entrée du dictionnaire.
        CollectRaidLayers();
        foreach (int layerZ in _raidLayerScratch)
        {
            if (!raids.TryGetValue(layerZ, out var raid)) continue;
            UpdateRaid(currentTick, playerCiv, layerZ, raid);
        }
    }

    /// <summary>
    /// La validité de la cible (existence, visibilité, flux d'attaque toujours actif) est vérifiée
    /// à chaque tick pour que le raid s'arrête dès que sa cible disparaît, au lieu d'attendre le
    /// prochain cycle de facturation d'upkeep — seuls le débit d'upkeep et la ré-application des
    /// flux restent limités à RaidCheckIntervalTicks.
    /// </summary>
    private void UpdateRaid(long currentTick, Civilization playerCiv, int layerZ, RaidState raid)
    {
        var target = raid.TargetVertex;
        if (target != null)
        {
            var targetVertex = FindEnemyVertexAt(target, playerCiv);
            // La cible doit exister et rester visible : hors de vue (brouillard de guerre), le raid est annulé.
            if (targetVertex == null || !IsCityVisibleTo(targetVertex, playerCiv))
            {
                StopRaid(playerCiv, layerZ);
                return;
            }

            if (!HasAttackFlowTo(playerCiv, layerZ, target))
            {
                StopRaid(playerCiv, layerZ);
                return;
            }

            if (currentTick - LastRaidCheckTick(layerZ) < RaidCheckIntervalTicks) return;
            _lastRaidCheckTickByLayer[layerZ] = currentTick;

            if (!PayUpkeep(playerCiv, layerZ, raid)) return;
            ApplyRaidFlows(playerCiv, target);
        }
        else
        {
            var targetHex = raid.TargetHex;
            var monster = _state!.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(targetHex));
            // Même règle que pour une ville : la cible doit exister et rester visible — un monstre qui
            // s'éloigne dans le brouillard de guerre met fin au raid au lieu d'en facturer l'entretien
            // indéfiniment.
            if (monster == null || !IsHexVisibleTo(monster.Position, playerCiv))
            {
                StopRaid(playerCiv, layerZ);
                return;
            }

            if (!HasMonsterAttackFlowTo(playerCiv, layerZ, targetHex!.Value))
            {
                StopRaid(playerCiv, layerZ);
                return;
            }

            if (currentTick - LastRaidCheckTick(layerZ) < RaidCheckIntervalTicks) return;
            _lastRaidCheckTickByLayer[layerZ] = currentTick;

            if (!PayUpkeep(playerCiv, layerZ, raid)) return;
            ApplyMonsterRaidFlows(playerCiv, targetHex.Value);
        }
    }

    private long LastRaidCheckTick(int layerZ)
        => _lastRaidCheckTickByLayer.TryGetValue(layerZ, out var tick) ? tick : 0L;

    // Les trois recherches ci-dessous tournent à chaque événement d'horloge et, depuis les raids
    // parallèles, une fois par raid en cours : boucles indexées, filtrées sur le layer du raid quand
    // c'est possible, plutôt que LINQ sur des IReadOnlyList (énumérateur boxé, fermeture allouée) —
    // en fin de partie elles voient plusieurs centaines d'emplacements militaires.

    /// <summary>Emplacement militaire ennemi occupant ce vertex, ou null s'il n'existe plus.</summary>
    private IMilitaryVertex? FindEnemyVertexAt(Vertex position, Civilization playerCiv)
    {
        var civs = _state!.Civilizations;
        for (int c = 0; c < civs.Count; c++)
        {
            var civ = civs[c];
            if (civ.Index == playerCiv.Index) continue;
            var vertices = civ.MilitaryVertices;
            for (int i = 0; i < vertices.Count; i++)
                if (vertices[i].Position.Equals(position)) return vertices[i];
        }
        return null;
    }

    /// <summary>Un emplacement de ce layer attaque-t-il encore la cible ? C'est ce flux qui maintient le raid en vie.</summary>
    private static bool HasAttackFlowTo(Civilization civ, int layerZ, Vertex target)
    {
        var vertices = civ.MilitaryVertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            if (vertex.Position.Z != layerZ) continue;
            if (vertex.FlowTarget != null && vertex.FlowTarget.Equals(target)) return true;
        }
        return false;
    }

    /// <summary>Idem pour un raid visant une MonsterFeature, dont l'assaut passe par MonsterAttackTarget et non par un flux.</summary>
    private static bool HasMonsterAttackFlowTo(Civilization civ, int layerZ, HexCoord target)
    {
        var vertices = civ.MilitaryVertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            if (vertex.Position.Z != layerZ) continue;
            if (vertex.MonsterAttackTarget != null && vertex.MonsterAttackTarget.Value.Equals(target)) return true;
        }
        return false;
    }

    /// <summary>
    /// Entretien réellement débité chaque seconde par le raid du layer donné : l'entretien courant de
    /// ce raid (<see cref="InitialUpkeep"/> au départ, +2 par cycle) diminué de RAID_UPKEEP_REDUCTION
    /// (Fosse aux Crânes orque), jamais sous 0. 0 si ce layer ne raide pas. Chaque raid escalade pour
    /// son propre compte : deux guerres menées en parallèle sur deux layers ne se facturent pas l'une
    /// l'autre.
    /// La réduction s'applique au paiement plutôt qu'à la valeur stockée, pour qu'un bâtiment construit
    /// ou perdu pendant le raid prenne effet immédiatement sans fausser l'escalade.
    /// </summary>
    internal int EffectiveUpkeep(Civilization civ, int layerZ)
    {
        var raid = _state?.AutomationSettings.GetRaid(layerZ);
        if (raid == null) return 0;
        return Math.Max(0, raid.CurrentUpkeep - UpkeepReduction(civ));
    }

    /// <summary>
    /// Entretien qu'un raid lancé maintenant coûterait à sa première seconde, réductions comprises —
    /// ce qu'annonce l'infobulle de l'action Raid tant qu'aucun raid n'est en cours.
    /// </summary>
    internal int InitialEffectiveUpkeep(Civilization civ)
        => Math.Max(0, InitialUpkeep - UpkeepReduction(civ));

    private static int UpkeepReduction(Civilization civ)
        => (int)civ.ModifierAggregator.ApplyModifiers(ECategory.RAID_UPKEEP_REDUCTION, "", 0.0);

    /// <summary>Débite l'upkeep courant du raid et l'augmente pour le prochain cycle. Retourne false (et arrête ce raid) si les fonds sont insuffisants.</summary>
    private bool PayUpkeep(Civilization playerCiv, int layerZ, RaidState raid)
    {
        int upkeep = Math.Max(0, raid.CurrentUpkeep - UpkeepReduction(playerCiv));
        if (playerCiv.GetResourceQuantity(Resource.Gold) < upkeep)
        {
            StopRaid(playerCiv, layerZ);
            return false;
        }
        // Entretien entièrement absorbé par les réductions : rien à débiter — RemoveResource refuse
        // une quantité nulle (voir Civilization.RemoveResource). L'escalade court quand même.
        if (upkeep > 0)
            playerCiv.RemoveResource(Resource.Gold, upkeep);
        raid.CurrentUpkeep += 2;
        return true;
    }

    private void ApplyRaidFlows(Civilization civ, Vertex target)
    {
        if (_cityAttackEngine == null || _reinforcementEngine == null) return;

        int attackRange = _cityAttackEngine.CityAttackRange(civ);
        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        int targetZ = target.Z;
        var verticesInLayer = civ.MilitaryVertices.Where(v => v.Position.Z == targetZ).ToList();

        foreach (var vertex in verticesInLayer)
        {
            int distToTarget = vertex.Position.EdgeDistanceTo(target);
            if (distToTarget <= attackRange)
            {
                _reinforcementEngine.SetCityFlow(vertex, target);
            }
            else
            {
                // Renforce l'allié le plus proche de la cible qui est aussi à portée de renfort
                var nearestAlly = verticesInLayer
                    .Where(a => a != vertex
                             && a.Position.EdgeDistanceTo(target) < distToTarget
                             && vertex.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => a.Position.EdgeDistanceTo(target))
                    .FirstOrDefault();

                _reinforcementEngine.SetCityFlow(vertex, nearestAlly?.Position);
            }
        }
    }

    private static int DistanceToMonster(IMilitaryVertex vertex, MonsterFeature monster)
        => vertex.Position.GetHexes().Max(h => h.DistanceTo(monster.Position));

    private void ApplyMonsterRaidFlows(Civilization civ, HexCoord targetHex)
    {
        if (_reinforcementEngine == null || _monsterCombatEngine == null || _state == null) return;

        var monster = _state.Features.OfType<MonsterFeature>().FirstOrDefault(m => m.Position.Equals(targetHex));
        if (monster == null) return;

        int reinforcementRange = _reinforcementEngine.ReinforcementRange(civ);
        int targetZ = targetHex.Z;
        var verticesInLayer = civ.MilitaryVertices.Where(v => v.Position.Z == targetZ).ToList();

        foreach (var vertex in verticesInLayer)
        {
            bool canAttack = _monsterCombatEngine.GetAttackAvailability(vertex, monster) == MonsterAttackAvailability.Available;
            if (canAttack)
            {
                vertex.MonsterAttackTarget = targetHex;
                _reinforcementEngine.SetCityFlow(vertex, null);
            }
            else
            {
                // Renforce l'allié le plus proche du monstre qui est aussi à portée de renfort
                int distToTarget = DistanceToMonster(vertex, monster);
                var nearestAlly = verticesInLayer
                    .Where(a => a != vertex
                             && DistanceToMonster(a, monster) < distToTarget
                             && vertex.Position.EdgeDistanceTo(a.Position) <= reinforcementRange)
                    .OrderBy(a => DistanceToMonster(a, monster))
                    .FirstOrDefault();

                vertex.MonsterAttackTarget = null;
                _reinforcementEngine.SetCityFlow(vertex, nearestAlly?.Position);
            }
        }
    }
}
