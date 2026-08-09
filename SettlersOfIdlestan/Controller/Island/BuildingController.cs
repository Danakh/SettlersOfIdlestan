using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Controller.Island
{
    public class BuildingBuiltEventArgs : EventArgs
    {
        public City City { get; }
        public BuildingType BuildingType { get; }
        public int Level { get; }
        public bool IsNewBuilding { get; }

        public BuildingBuiltEventArgs(City city, BuildingType type, int level, bool isNewBuilding)
        {
            City = city;
            BuildingType = type;
            Level = level;
            IsNewBuilding = isNewBuilding;
        }
    }

    /// <summary>
    /// Contr�le la logique de construction et d'am�lioration des b�timents pour une ville donn�e.
    /// API similaire � RoadController / CityBuilderController.
    /// </summary>
    public class BuildingController
    {
        private WorldState? _state;
        private GameClock? _clock;

        private static readonly BuildingType[] _allBuildingTypes = (BuildingType[])Enum.GetValues(typeof(BuildingType));

        public event EventHandler<BuildingBuiltEventArgs>? OnBuildingBuilt;

        internal BuildingController(WorldState? state = null)
        {
            _state = state;
        }

        /// <summary>
        /// Initialize or update the WorldState for this controller.
        /// </summary>
        internal void Initialize(WorldState state, GameClock? clock = null)
        {
            if (_clock != null)
                _clock.Advanced -= OnClockAdvanced;

            _state = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock;

            if (_clock != null)
                _clock.Advanced += OnClockAdvanced;

            foreach (var civ in _state.Civilizations)
            {
                RecalculateStorageCapacity(civ);
                civ.RebuildUniqueBuildingCache();
            }
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformHarvestersGuildProductionAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformHarvestersGuildProductionAutomation)}: {ex}"); }
            try { PerformArtisansGuildAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformArtisansGuildAutomation)}: {ex}"); }
            try { PerformAcademyAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformAcademyAutomation)}: {ex}"); }
            try { PerformTraderGuildAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformTraderGuildAutomation)}: {ex}"); }
            try { PerformImperialPortSeaportAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformImperialPortSeaportAutomation)}: {ex}"); }
            try { PerformWarRoomAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformWarRoomAutomation)}: {ex}"); }
            try { PerformTownHallGuildAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformTownHallGuildAutomation)}: {ex}"); }
            try { PerformGrandTempleAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformGrandTempleAutomation)}: {ex}"); }
            try { PerformVolcanicForgeAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformVolcanicForgeAutomation)}: {ex}"); }
            try { PerformArcaneTowerAutomation(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BuildingController] {nameof(PerformArcaneTowerAutomation)}: {ex}"); }
        }

        private void PerformHarvestersGuildProductionAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill, BuildingType.MushroomFarm, BuildingType.Mine];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.HarvestersGuild) is not HarvestersGuild guild || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsProductionBuildingAutomationActive;
                long tick = guild.LastProductionBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoProductionCooldownTicks(), enabled, targets, now);
                guild.LastProductionBuildTick = tick;
            }
        }

        private void PerformArtisansGuildAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Forge, BuildingType.Warehouse, BuildingType.GlassWorks, BuildingType.Smelter];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.ArtisansGuild) is not ArtisansGuild guild || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsArtisanBuildingAutomationActive;
                long tick = guild.LastArtisanBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoArtisanCooldownTicks(), enabled, targets, now);
                guild.LastArtisanBuildTick = tick;
            }
        }

        private void PerformAcademyAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Library, BuildingType.Laboratory];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.Academy) is not Academy academy || academy.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsLibraryBuildingAutomationActive;
                long tick = academy.LastLibraryBuildTick;
                TickGuildAutomation(civ, ref tick, academy.GetAutoLibraryCooldownTicks(), enabled, targets, now);
                academy.LastLibraryBuildTick = tick;
            }
        }

        private void PerformTraderGuildAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Market];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.TraderGuild) is not TraderGuild guild || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsMarketBuildingAutomationActive;
                long tick = guild.LastMarketBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoMarketCooldownTicks(), enabled, targets, now);
                guild.LastMarketBuildTick = tick;
            }
        }

        private void PerformWarRoomAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Barracks, BuildingType.Garrison, BuildingType.Arsenal, BuildingType.WeaponSmith, BuildingType.ArmorSmith, BuildingType.Palisade];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.WarRoom) is not WarRoom warRoom || warRoom.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsMilitaryBuildingAutomationActive;
                long tick = warRoom.LastMilitaryBuildTick;
                TickGuildAutomation(civ, ref tick, warRoom.GetAutoMilitaryCooldownTicks(), enabled, targets, now);
                warRoom.LastMilitaryBuildTick = tick;
            }
        }

        private void PerformTownHallGuildAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.TownHall];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.BuildersGuild) is not BuildersGuild guild || guild.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsTownHallAutomationActive;
                long tick = guild.LastTownHallBuildTick;
                TickGuildAutomation(civ, ref tick, guild.GetAutoTownHallCooldownTicks(), enabled, targets, now);
                guild.LastTownHallBuildTick = tick;
            }
        }

        private void PerformGrandTempleAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.Temple];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.GrandTemple) is not GrandTemple grandTemple || grandTemple.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsTempleAutomationActive;
                long tick = grandTemple.LastTempleBuildTick;
                TickGuildAutomation(civ, ref tick, grandTemple.GetAutoTempleCooldownTicks(), enabled, targets, now);
                grandTemple.LastTempleBuildTick = tick;
            }
        }

        private void PerformVolcanicForgeAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.MithrilMine];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.VolcanicForge) is not VolcanicForge forge || forge.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsMithrilMineBuildingAutomationActive;
                long tick = forge.LastMithrilMineBuildTick;
                TickGuildAutomation(civ, ref tick, forge.GetAutoMithrilMineCooldownTicks(), enabled, targets, now);
                forge.LastMithrilMineBuildTick = tick;
            }
        }

        private void PerformArcaneTowerAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;
            BuildingType[] targets = [BuildingType.MageTower, BuildingType.AlchimistHut];

            foreach (var civ in _state.Civilizations)
            {
                if (civ.GetUniqueBuilding(BuildingType.ArcaneTower) is not ArcaneTower arcaneTower || arcaneTower.Level == 0) continue;

                bool isPlayer = civ.Index == _state.PlayerCivilization.Index;
                bool enabled = !isPlayer || _state.AutomationSettings.IsArcaneTowerBuildingAutomationActive;
                long tick = arcaneTower.LastMagicBuildTick;
                TickGuildAutomation(civ, ref tick, arcaneTower.GetAutoMagicCooldownTicks(), enabled, targets, now);
                arcaneTower.LastMagicBuildTick = tick;
            }
        }

        private void PerformImperialPortSeaportAutomation()
        {
            if (_state == null || _clock == null) return;
            long now = _clock.CurrentTick;

            var civ = _state.PlayerCivilization;
            if (!civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_SEAPORT_AUTOMATION)) return;

            if (civ.GetUniqueBuilding(BuildingType.ImperialPort) is not ImperialPort imperialPort) return;

            bool enabled = _state.AutomationSettings.IsSeaportBuildingAutomationActive;
            long tick = imperialPort.LastSeaportBuildTick;
            TickGuildAutomation(civ, ref tick, imperialPort.GetAutoSeaportCooldownTicks(), enabled, [BuildingType.Seaport], now);
            imperialPort.LastSeaportBuildTick = tick;
        }

        private void TickGuildAutomation(
            Model.Civilization.Civilization civ,
            ref long lastTick,
            long cooldown,
            bool enabled,
            BuildingType[] targets,
            long now)
        {
            if (!enabled) { lastTick = now; return; }
            if (lastTick == 0) { lastTick = now; return; }

            double guildSpeedBonus = civ.ModifierAggregator.ApplyModifiers(ECategory.GUILD_AUTOMATION_SPEED_PER_CITY, "", 0.0) * civ.Cities.Count;
            long effectiveCooldown = guildSpeedBonus > 0 ? (long)(cooldown / (1.0 + guildSpeedBonus)) : cooldown;
            if (now - lastTick < effectiveCooldown) return;

            lastTick = now;

            // New builds first, then upgrade lowest-level existing buildings
            foreach (var city in civ.Cities)
                foreach (var type in targets)
                    if (!city.Buildings.Any(b => b.Type == type) && BuildBuilding(city, type))
                        return;

            var lowestLevelFirst = civ.Cities
                .SelectMany(city => city.Buildings
                    .Where(b => targets.Contains(b.Type))
                    .Select(b => (city, b.Type, b.Level)))
                .OrderBy(x => x.Level);

            foreach (var (city, type, _) in lowestLevelFirst)
                if (BuildBuilding(city, type))
                    return;
        }

        /// <summary>
        /// Retourne la liste des b�timents constructibles ou am�liorables pour la ville sp�cifi�e.
        /// La m�thode renvoie des instances prototypes de niveau 0 pour les b�timents non construits.
        /// </summary>
        public List<Building> GetBuildingsAndBuildables(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var existingByType = new Dictionary<BuildingType, Building>(city.Buildings.Count);
            foreach (var b in city.Buildings)
                existingByType[b.Type] = b;

            var map = _state.GetMapFor(city.Position);

            var result = new List<Building>(_allBuildingTypes.Length);

            foreach (var bt in _allBuildingTypes)
            {
                var entry = GetBuildingOrBuildableEntry(city, bt, existingByType, map);
                if (entry != null)
                    result.Add(entry);
            }

            // sort the result by available level
            result.Sort((a, b) => a.AvailableAtLevel.CompareTo(b.AvailableAtLevel));

            return result;
        }

        /// <summary>
        /// Retourne le bâtiment existant ou le prototype constructible pour ce type précis dans la
        /// ville donnée, ou null si ce type n'est pas disponible. Évite de reconstruire la liste
        /// complète de <see cref="GetBuildingsAndBuildables"/> quand on ne s'intéresse qu'à un seul type.
        /// </summary>
        public Building? GetBuildingOrBuildable(City city, BuildingType type)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var existing = city.FindBuilding(type);
            return GetBuildingOrBuildableEntry(city, type, existing, _state.GetMapFor(city.Position));
        }

        private Building? GetBuildingOrBuildableEntry(City city, BuildingType bt, Dictionary<BuildingType, Building> existingByType, IslandMap? map)
        {
            existingByType.TryGetValue(bt, out var existing);
            return GetBuildingOrBuildableEntry(city, bt, existing, map);
        }

        /// <summary>
        /// Instances de sondage, une par type, servant uniquement à répondre aux questions qui ne
        /// dépendent que du type (unicité, prérequis, niveau max par défaut, disponibilité pour une
        /// ville). Elles ne sont jamais rendues à l'appelant ni ajoutées à une ville : seule une
        /// instance fraîche l'est. Sans elles, chaque test de disponibilité — l'autoplayer en fait des
        /// centaines par passe de stratégie, la plupart concluant « indisponible » — allouait un
        /// bâtiment complet pour le jeter aussitôt. À traiter comme immuable.
        /// </summary>
        private static readonly Dictionary<BuildingType, Building> _probesByType = new();

        private static Building? GetProbe(BuildingType bt)
        {
            if (_probesByType.TryGetValue(bt, out var probe))
                return probe;

            probe = CreateBuilding(bt);
            if (probe != null)
                _probesByType[bt] = probe;
            return probe;
        }

        private Building? GetBuildingOrBuildableEntry(City city, BuildingType bt, Building? existing, IslandMap? map)
        {
            if (existing != null)
                return existing.IsUnique ? null : existing;

            var prototype = GetProbe(bt);
            if (prototype == null || prototype.IsUnique)
                return null;

            // Alchimist Hut / Mage Tower : prérequis lié à une feature de carte (Cercle de Fées) ou
            // à un terrain (Grotte de Cristal) découvert, pas à un autre bâtiment construisible —
            // reste masquée tant que le prérequis n'est pas rempli, plutôt qu'affichée grisée avec
            // tooltip (voir GetUniqueBuildingsAndBuildables pour le même traitement des bâtiments
            // uniques équivalents comme la Forge Volcanique).
            if ((bt == BuildingType.AlchimistHut || bt == BuildingType.MageTower) &&
                _state != null && !prototype.HasBuildPrerequisites(city, _state))
                return null;

            if (GetMaxLevel(prototype, city.CivilizationIndex) > 0 && map != null)
            {
                var civ = _state?.GetCivilization(city.CivilizationIndex);
                bool available = civ != null
                    ? prototype.IsBuildingAvailableForCity(map, city, civ)
                    : prototype.IsBuildingAvailableForCity(map, city);
                if (available)
                    return CreateBuilding(bt); // instance neuve : l'appelant peut la conserver/muter
            }

            return null;
        }

        /// <summary>
        /// Construit (ou am�liore) un b�timent dans la ville sp�cifi�e.
        /// Lance InvalidOperationException si pas assez de ressources ou si l'action n'est pas permise.
        /// </summary>
        public bool BuildBuilding(City city, BuildingType type)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            var existing = city.Buildings.FirstOrDefault(b => b.Type == type);

            ResourceSet cost;
            Building resultBuilding;
            if (existing == null)
            {
                var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

                if (_state.GetMapFor(city.Position) is not { } map1 ||
                    !prototype.IsBuildingAvailableForCity(map1, city, civ))
                    return false;

                if (!prototype.HasBuildPrerequisites(city, _state))
                    return false;

                // civ.UniqueBuildings is a permanent "ever built" flag (never cleared on city loss,
                // used e.g. by PrestigeController.HasImperialPort) — checking it here would block
                // rebuilding forever once the city holding it is destroyed. The cache is the live source
                // of truth and is correctly refreshed by Civilization.RemoveCity.
                if (prototype.IsUnique && civ.GetUniqueBuilding(type) != null)
                    return false;

                if (prototype.IsUnique && city.Buildings.Any(b => b.IsUnique))
                    return false;

                cost = prototype.GetBuildCost();
                resultBuilding = prototype;
            }
            else
            {
                if (existing.Level >= GetMaxLevel(existing, civ, city))
                    return false;

                cost = existing.GetUpgradeCost(existing.Level + 1);
                resultBuilding = existing;
            }

            // check resources
            foreach (var kv in cost)
            {
                if (civ.GetResourceQuantity(kv.Key) < kv.Value)
                {
                    return false;
                }
            }

            // consume resources
            foreach (var kv in cost)
            {
                civ.RemoveResource(kv.Key, kv.Value);
            }

            if (existing == null)
            {
                resultBuilding.Level = 1;

                // Si le joueur a désactivé la production de ce type de bâtiment partout (tous
                // désactivés), les nouveaux bâtiments construits doivent suivre ce réglage global
                // plutôt que de démarrer actifs par défaut (voir ActivationStatus, ToggleAll/AreAllActiveNullable).
                if (resultBuilding.ActivationStatus != ActivationStatus.NON_ACTIVABLE)
                {
                    var sameTypeExisting = civ.Cities.SelectMany(c => c.Buildings)
                        .Where(b => b.Type == type && b.Level >= 1)
                        .ToList();
                    if (sameTypeExisting.Count > 0 && sameTypeExisting.All(b => b.ActivationStatus == ActivationStatus.INACTIVE))
                        resultBuilding.ActivationStatus = ActivationStatus.INACTIVE;
                }

                city.AddBuilding(resultBuilding);
                if (type == BuildingType.TownHall) city.InvalidateLevelCache();
                int defBonus = resultBuilding.GetDefenseBonus();
                if (defBonus > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                    city.CurrentDefense += defBonus;
                if (resultBuilding.IsUnique)
                {
                    civ.RegisterUniqueBuildingInCache(resultBuilding);
                    if (!civ.UniqueBuildings.Contains(resultBuilding.Type))
                        civ.AddUniqueBuilding(resultBuilding.Type);
                }
                if (resultBuilding is IUniqueBuilding)
                    civ.RebuildUniqueBuildingsModifiers();
            }
            else
            {
                int oldDefBonus = existing.GetDefenseBonus();
                existing.Level += 1;
                int defDelta = existing.GetDefenseBonus() - oldDefBonus;
                if (defDelta > 0 && civ.ModifierAggregator.HasModifier(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT))
                    city.CurrentDefense += defDelta;
                if (existing is IUniqueBuilding)
                    civ.RebuildUniqueBuildingsModifiers();
            }

            if (type == BuildingType.Watchtower)
                _state.Visibility.RecalculateFor(city.CivilizationIndex);

            city.InvalidateMaxSoldiersCache();
            RecalculateStorageCapacity(civ);

            OnBuildingBuilt?.Invoke(this, new BuildingBuiltEventArgs(
                city, type, resultBuilding.Level, existing == null));

            return true;
        }

        /// <summary>
        /// Retourne la liste des bâtiments uniques disponibles ou déjà construits pour la ville spécifiée.
        /// Les bâtiments déjà construits (dans n'importe quelle ville de la civ) sont toujours inclus.
        /// Les bâtiments non construits sont inclus uniquement si la ville sélectionnée est niveau 4.
        /// </summary>
        public List<Building> GetUniqueBuildingsAndBuildables(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            var result = new List<Building>();

            foreach (var bt in _allBuildingTypes)
            {
                var prototype = CreateBuilding(bt);
                if (prototype == null || !prototype.IsUnique || GetMaxLevel(prototype, civ) <= 0)
                    continue;

                // Same live-cache reasoning as BuildBuilding(): civ.UniqueBuildings never clears on
                // city loss, so it must not be treated as "currently built" here either.
                var existingInstance = civ.GetUniqueBuilding(bt);

                if (existingInstance != null)
                {
                    result.Add(existingInstance);
                }
                // Ziggourat : reste cachée tant que le pouvoir divin Foi (UNLOCK_DOMINION) n'est pas
                // débloqué, même verrou que les recherches/vertex du Dominion (voir ResearchController.IsDominionRequirementMet).
                else if (bt == BuildingType.Ziggurat &&
                         !civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_DOMINION))
                {
                    continue;
                }
                // Alchimist Hut / Volcanic Forge : prérequis lié à une feature de carte (Cercle de
                // Fées / Volcan) découverte, pas à un autre bâtiment construisible — reste masqué
                // tant que la feature n'est pas trouvée, plutôt qu'affiché grisé avec tooltip.
                else if ((bt == BuildingType.AlchimistHut || bt == BuildingType.VolcanicForge) &&
                         !prototype.HasBuildPrerequisites(city, _state))
                {
                    continue;
                }
                else if (_state.GetMapFor(city.Position) is { } map2 &&
                         prototype.IsBuildingAvailableForCity(map2, city))
                {
                    result.Add(prototype);
                }
            }

            return result;
        }

        public int GetMaxLevel(Building building, int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.Civilizations[civilizationIndex];
            return GetMaxLevel(building, civ);
        }

        public int GetMaxLevel(Building building, Civilization civ)
        {
            if (civ.TryGetCachedMaxLevel(building.Type, out int cached))
                return cached;

            int value = civ.ModifierAggregator.ApplyModifiers(
                ECategory.BUILDING_MAX_LEVEL, BuildingTypeNames.Of(building.Type), building.GetDefaultMaxLevel());
            civ.SetCachedMaxLevel(building.Type, value);
            return value;
        }

        /// <summary>
        /// Niveau max pour un bâtiment d'une ville précise. Identique à <see cref="GetMaxLevel(Building, Civilization)"/>
        /// (civ-wide, caché) pour tout bâtiment autre que l'Hôtel de Ville. Pour l'Hôtel de Ville,
        /// applique en plus INLAND_CITY_LEVEL_CAP (Sirènes) : plafonne le résultat si la ville ne
        /// touche pas directement le terrain requis — mécanique par ville, donc non cachée au niveau
        /// civ (un seul lookup de terrain, chemin non chaud : amélioration/affichage de Mairie).
        /// </summary>
        public int GetMaxLevel(Building building, Civilization civ, City city)
        {
            int result = GetMaxLevel(building, civ);
            if (building.Type != BuildingType.TownHall) return result;
            if (city.Position.Z != IslandMap.SurfaceLayer) return result;

            var map = _state?.GetMapFor(city.Position);
            if (map == null) return result;

            foreach (var modifier in civ.ModifierAggregator.GetActiveModifiers(ECategory.INLAND_CITY_LEVEL_CAP))
                if (Enum.TryParse<TerrainType>(modifier.SubCategory, out var terrain) &&
                    !map.VertexHasTerrainType(city.Position, terrain))
                    result = Math.Min(result, (int)modifier.Value);

            return result;
        }

        /// <summary>
        /// Recalcule intégralement la capacité de stockage (ressources de base / avancées) de la
        /// civilisation et met à jour son cache. À appeler après toute construction/amélioration/
        /// destruction de bâtiment, ajout/retrait de ville, ou changement de l'agrégateur de modificateurs.
        /// </summary>
        /// <summary>Niveau minimum de Marché requis pour débloquer l'Achat Automatique (voir <see cref="TradeController.IsAutoBuyUnlocked"/>).</summary>
        private const int AutoBuyMinMarketLevel = 4;

        public static void RecalculateStorageCapacity(Model.Civilization.Civilization civ)
        {
            int basic = 10 * civ.Cities.Count;
            int advanced = 0;
            bool hasHighLevelMarket = false;

            // Boucles indexées : City.Buildings est typée IReadOnlyList, dont l'énumérateur est boxé à
            // chaque foreach. Ce recalcul est déclenché par chaque construction.
            var cities = civ.Cities;
            for (int c = 0; c < cities.Count; c++)
            {
                var buildings = cities[c].Buildings;
                for (int b = 0; b < buildings.Count; b++)
                {
                    var building = buildings[b];
                    basic += building.GetStorageCapacityBonusBasic();
                    advanced += building.GetStorageCapacityBonusAdvanced();
                    if (building.Type == BuildingType.Market && building.Level >= AutoBuyMinMarketLevel)
                        hasHighLevelMarket = true;
                }
            }

            basic += civ.ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0);
            advanced += civ.ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_ADVANCED, "", 0);

            double multiplier = civ.ModifierAggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_MULTIPLIER, "", 1.0);
            basic = (int)(basic * multiplier);
            advanced = (int)(advanced * multiplier);

            civ.SetStorageCapacityCache(basic, advanced);
            civ.SetAutoBuyUnlockedCache(hasHighLevelMarket && civ.ModifierAggregator.HasModifier(ECategory.UNLOCK_AUTO_BUY_TRADE));
        }

        public static Building? CreateBuilding(BuildingType type)
        {
            return type switch
            {
                BuildingType.TownHall => new TownHall(),
                BuildingType.Market => new Market(),
                BuildingType.Sawmill => new Sawmill(),
                BuildingType.Brickworks => new Brickworks(),
                BuildingType.Mill => new Mill(),
                BuildingType.Mine => new Mine(),
                BuildingType.Quarry => new Quarry(),
                BuildingType.Seaport => new Seaport(),
                BuildingType.Warehouse => new Warehouse(),
                BuildingType.Forge => new Forge(),
                BuildingType.Library => new Library(),
                BuildingType.Temple => new Temple(),
                BuildingType.BuildersGuild => new BuildersGuild(),
                BuildingType.Laboratory => new Laboratory(),
                BuildingType.Barracks => new Barracks(),
                BuildingType.GlassWorks => new GlassWorks(),
                BuildingType.Palisade => new Palisade(),
                BuildingType.ImperialPort => new ImperialPort(),
                BuildingType.HarvestersGuild => new HarvestersGuild(),
                BuildingType.ArtisansGuild => new ArtisansGuild(),
                BuildingType.Watchtower => new Watchtower(),
                BuildingType.Academy => new Academy(),
                BuildingType.TraderGuild => new TraderGuild(),
                BuildingType.Garrison => new Garrison(),
                // BuildingType.DeepestMine : legacy — la Mine Profonde est désormais une IslandFeature
                BuildingType.Smelter => new Smelter(),
                BuildingType.BlastFurnace => new BlastFurnace(),
                BuildingType.Arsenal => new Arsenal(),
                BuildingType.MushroomFarm => new MushroomFarm(),
                BuildingType.MithrilMine => new MithrilMine(),
                BuildingType.MageTower => new MageTower(),
                BuildingType.WarRoom => new WarRoom(),
                BuildingType.AlchimistHut => new AlchimistHut(),
                BuildingType.WeaponSmith => new WeaponSmith(),
                BuildingType.ArmorSmith => new ArmorSmith(),
                BuildingType.AdventurersGuild => new AdventurersGuild(),
                BuildingType.VolcanicForge => new VolcanicForge(),
                BuildingType.Ziggurat => new Ziggurat(),
                BuildingType.HeartTree => new HeartTree(),
                BuildingType.RunicForge => new RunicForge(),
                BuildingType.GreatBurrow => new GreatBurrow(),
                BuildingType.ColossusWorkshop => new ColossusWorkshop(),
                BuildingType.SkullPit => new SkullPit(),
                BuildingType.ThroneOfWinds => new ThroneOfWinds(),
                BuildingType.PearlGrotto => new PearlGrotto(),
                BuildingType.GrandTemple => new GrandTemple(),
                BuildingType.ArcaneTower => new ArcaneTower(),
                BuildingType.SpiderShrine => new SpiderShrine(),
                _ => null,
            };
        }
    }
}
