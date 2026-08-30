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

        /// <summary>
        /// Types de bâtiments uniques, résolus une fois pour toutes. IsUnique est une constante par
        /// type : scanner l'enum entier — et instancier un prototype par type pour le lire — n'a besoin
        /// d'être fait qu'au chargement, pas à chaque appel de
        /// <see cref="GetBuildableUniqueBuildings"/>, que l'autoplayer emprunte à chaque tick.
        /// </summary>
        private static readonly BuildingType[] _uniqueBuildingTypes =
            _allBuildingTypes.Where(bt => CreateBuilding(bt)?.IsUnique == true).ToArray();

        /// <summary>
        /// Bâtiments non-uniques sans automatisation existante : ni Watchtower (verrouillé par
        /// défaut, GetDefaultMaxLevel() == 0, tant qu'un vertex de prestige ne le débloque pas) ni
        /// AdventurersWaypost (coût dépendant d'un état civ-wide, GetBuildCost ne peut pas être
        /// appelé sans passer par le chemin spécial de BuildBuilding) ne doivent apparaître dans le
        /// tableau des presets d'automatisation — voir TechnologyId.AutomationPreset.
        /// </summary>
        private static readonly BuildingType[] _excludedFromPresetTable =
            [BuildingType.Watchtower, BuildingType.AdventurersWaypost];

        /// <summary>
        /// Types de bâtiments affichés dans le tableau d'édition des presets d'automatisation :
        /// tous les bâtiments non-uniques à l'exception de <see cref="_excludedFromPresetTable"/>.
        /// </summary>
        public static readonly BuildingType[] PresetTableBuildingTypes =
            _allBuildingTypes.Except(_uniqueBuildingTypes).Except(_excludedFromPresetTable).ToArray();

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

            // Nouvelle île (prestige/ascension) : les index de civ sont réutilisés, mais rien ne
            // garantit que le cache "rien à construire" d'une civ de l'île précédente reste valide
            // pour son homonyme sur la nouvelle — on repart propre plutôt que de risquer un faux
            // positif qui bloquerait silencieusement une automatisation.
            _guildNothingToDoCache.Clear();
            _guildResourceRetryTick.Clear();
        }

        /// <summary>
        /// Purge les caches d'automatisation de guilde d'une civilisation retirée du monde — voir
        /// <see cref="WorldState.CivilizationRemoved"/>. Une entrée par type de guilde.
        /// </summary>
        internal void PurgeCivilizationCaches(int civilizationIndex)
        {
            foreach (var key in _guildNothingToDoCache.Keys.Where(k => k.CivIndex == civilizationIndex).ToList())
                _guildNothingToDoCache.Remove(key);
            foreach (var key in _guildResourceRetryTick.Keys.Where(k => k.CivIndex == civilizationIndex).ToList())
                _guildResourceRetryTick.Remove(key);
        }

        private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
        {
            try { PerformHarvestersGuildProductionAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformHarvestersGuildProductionAutomation), ex); }
            try { PerformArtisansGuildAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformArtisansGuildAutomation), ex); }
            try { PerformAcademyAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformAcademyAutomation), ex); }
            try { PerformTraderGuildAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformTraderGuildAutomation), ex); }
            try { PerformImperialPortSeaportAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformImperialPortSeaportAutomation), ex); }
            try { PerformWarRoomAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformWarRoomAutomation), ex); }
            try { PerformTownHallGuildAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformTownHallGuildAutomation), ex); }
            try { PerformGrandTempleAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformGrandTempleAutomation), ex); }
            try { PerformVolcanicForgeAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformVolcanicForgeAutomation), ex); }
            try { PerformArcaneTowerAutomation(); }
            catch (Exception ex) { GameLog.Error(nameof(BuildingController), nameof(PerformArcaneTowerAutomation), ex); }
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
                TickGuildAutomation(civ, ref tick, guild.GetAutoProductionCooldownTicks(), enabled, targets, now, GuildAutomationKind.Harvesters);
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
                TickGuildAutomation(civ, ref tick, guild.GetAutoArtisanCooldownTicks(), enabled, targets, now, GuildAutomationKind.Artisans);
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
                TickGuildAutomation(civ, ref tick, academy.GetAutoLibraryCooldownTicks(), enabled, targets, now, GuildAutomationKind.Academy);
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
                TickGuildAutomation(civ, ref tick, guild.GetAutoMarketCooldownTicks(), enabled, targets, now, GuildAutomationKind.Trader);
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
                TickGuildAutomation(civ, ref tick, warRoom.GetAutoMilitaryCooldownTicks(), enabled, targets, now, GuildAutomationKind.WarRoom);
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
                TickGuildAutomation(civ, ref tick, guild.GetAutoTownHallCooldownTicks(), enabled, targets, now, GuildAutomationKind.TownHall);
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
                TickGuildAutomation(civ, ref tick, grandTemple.GetAutoTempleCooldownTicks(), enabled, targets, now, GuildAutomationKind.GrandTemple);
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
                TickGuildAutomation(civ, ref tick, forge.GetAutoMithrilMineCooldownTicks(), enabled, targets, now, GuildAutomationKind.VolcanicForge);
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
                TickGuildAutomation(civ, ref tick, arcaneTower.GetAutoMagicCooldownTicks(), enabled, targets, now, GuildAutomationKind.ArcaneTower);
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
            TickGuildAutomation(civ, ref tick, imperialPort.GetAutoSeaportCooldownTicks(), enabled, [BuildingType.Seaport], now, GuildAutomationKind.ImperialPort);
            imperialPort.LastSeaportBuildTick = tick;
        }

        /// <summary>
        /// Une entrée par méthode d'automatisation de guilde (voir les 10 <c>PerformXAutomation</c>
        /// ci-dessus) — sert de clé au cache <see cref="_guildNothingToDoCache"/>, indépendant du
        /// tableau <c>targets</c> passé à chaque appel (qui n'a pas d'identité stable à comparer).
        /// </summary>
        private enum GuildAutomationKind
        {
            Harvesters, Artisans, Academy, Trader, WarRoom, TownHall, GrandTemple, VolcanicForge, ArcaneTower, ImperialPort
        }

        /// <summary>
        /// Mémorise, par (civ, guilde), le triplet de versions (bâtiments/modifiers/presets) sous
        /// lequel un dernier passage n'a rien trouvé à construire/améliorer pour cette guilde. Tant
        /// qu'aucun des trois n'a bougé, un nouveau passage donnerait exactement le même résultat :
        /// <see cref="TryPerformOneGuildAction"/> (balayage ville×type, coûteux en fin de partie avec
        /// des centaines de villes) peut être sauté. Volontairement pas de suivi par abonnement à un
        /// événement : les civs PNJ peuvent apparaître/disparaître en cours de partie
        /// (AutoExtendController), et un abonnement oublié à la désinscription fuirait.
        ///
        /// <para>Un échec bloqué uniquement par un coût non couvert (voir <c>blockedByResources</c>
        /// dans <see cref="TickGuildAutomation"/>) est mis en cache comme les autres — sans quoi le
        /// rattrapage hors-ligne (des dizaines/centaines de milliers d'événements <c>Advanced</c>
        /// rejoués d'affilée, voir <c>GameClock.SimulateAdvance</c>) refait le balayage complet à
        /// chaque événement pendant toute la durée de l'absence, ce qui fige le jeu au chargement —
        /// mais avec une expiration en ticks (<see cref="_guildResourceRetryTick"/>) plutôt
        /// qu'indéfiniment : les ressources s'accumulent sans faire bouger aucune des trois versions
        /// suivies ici, donc un cache sans expiration figerait l'automatisation dès la première
        /// tentative trop précoce jusqu'à ce qu'un événement sans rapport (nouvelle ville, recherche,
        /// preset) l'invalide enfin (bug d'origine de l'automatisation de l'Hôtel de Ville).</para>
        /// </summary>
        private readonly Dictionary<(int CivIndex, GuildAutomationKind Kind), (int Buildings, int Modifiers, int Presets)> _guildNothingToDoCache = new();

        /// <summary>
        /// Tick de simulation à partir duquel une entrée de <see cref="_guildNothingToDoCache"/>
        /// posée pour un échec bloqué par les ressources doit être réévaluée malgré un triplet de
        /// versions inchangé. Absent (ou expiré) pour une entrée posée pour un échec structurel : ce
        /// cas-là ne peut se résoudre que par un changement de version, jamais par le temps qui passe.
        /// </summary>
        private readonly Dictionary<(int CivIndex, GuildAutomationKind Kind), long> _guildResourceRetryTick = new();

        /// <summary>
        /// Espacement minimal (en ticks de simulation, 100/s) entre deux réévaluations d'une
        /// automatisation de guilde bloquée uniquement par manque de ressources — le plancher du
        /// cooldown de base (identique pour toutes les guildes, voir les <c>GetAutoXCooldownTicks</c>
        /// dans <c>Model/Buildings/</c>), pour ne pas retarder une guilde non boostée par rapport au
        /// rythme de relance déjà attendu d'elle (voir
        /// <c>TownHall_AutomationRetriesAfterInsufficientResourcesOnFirstAttempt</c>). Sans ce
        /// plancher, <c>GUILD_AUTOMATION_SPEED_PER_CITY</c> réduit l'<c>effectiveCooldown</c> en fin de
        /// partie au point de retrouver le rythme d'un balayage complet par événement <c>Advanced</c>
        /// pendant tout un rattrapage hors-ligne — voir le commentaire de <see cref="_guildNothingToDoCache"/>.
        /// </summary>
        private const long GuildResourceBlockRetryFloorTicks = 1000;

        private void TickGuildAutomation(
            Model.Civilization.Civilization civ,
            ref long lastTick,
            long cooldown,
            bool enabled,
            BuildingType[] targets,
            long now,
            GuildAutomationKind kind)
        {
            if (!enabled) { lastTick = now; return; }
            if (lastTick == 0) { lastTick = now; return; }
            if (_state == null) return;

            double guildSpeedBonus = civ.ModifierAggregator.ApplyModifiers(ECategory.GUILD_AUTOMATION_SPEED_PER_CITY, "", 0.0) * civ.Cities.Count;
            long effectiveCooldown = guildSpeedBonus > 0 ? (long)(cooldown / (1.0 + guildSpeedBonus)) : cooldown;

            long cycles = TickCooldown.ConsumeElapsedCycles(now, ref lastTick, effectiveCooldown);
            if (cycles <= 0) return;

            var presets = _state.AutomationSettings;

            var cacheKey = (civ.Index, kind);
            var versions = (civ.BuildingsVersion, civ.ModifierAggregator.Version, presets.PresetsVersion);
            if (_guildNothingToDoCache.TryGetValue(cacheKey, out var cachedVersions) && cachedVersions == versions)
            {
                // Une entrée posée pour un échec structurel n'a pas de tick de relance : seul un
                // changement de version peut la faire tomber, déjà exclu par la comparaison ci-dessus.
                if (!_guildResourceRetryTick.TryGetValue(cacheKey, out long retryTick) || now < retryTick)
                    return;
            }

            // Un cycle = une construction/amélioration (comportement inchangé : au plus une action par
            // cooldown écoulé), rejoué `cycles` fois pour rattraper un saut de temps. S'arrête dès qu'un
            // cycle ne trouve plus rien à faire — les cycles suivants échoueraient pour la même raison
            // (aucune ressource supplémentaire n'est produite entre deux cycles de cette boucle).
            for (long i = 0; i < cycles; i++)
                if (!TryPerformOneGuildAction(civ, presets, targets, out bool blockedByResources))
                {
                    // Rien trouvé à ce triplet de versions (celui d'après un éventuel build réussi
                    // plus tôt dans cette même rafale de cycles, cf. BuildingsVersion incrémenté par
                    // BuildBuilding) : un futur appel peut sauter le balayage tant que rien de ça ne
                    // change — nouvelle ville, modifier (recherche/prestige/rituel...) ou preset. Un
                    // échec bloqué par un coût non couvert est mis en cache aussi (rattrapage hors-ligne,
                    // voir le commentaire de _guildNothingToDoCache), mais avec un tick de relance : les
                    // ressources s'accumulent sans bouger ces versions, et un cache sans expiration
                    // figerait l'automatisation dès la première tentative trop tôt.
                    _guildNothingToDoCache[cacheKey] = (civ.BuildingsVersion, civ.ModifierAggregator.Version, presets.PresetsVersion);
                    if (blockedByResources)
                        _guildResourceRetryTick[cacheKey] = now + Math.Max(effectiveCooldown, GuildResourceBlockRetryFloorTicks);
                    else
                        _guildResourceRetryTick.Remove(cacheKey);
                    break;
                }
        }

        /// <summary>
        /// Un cycle de <see cref="TickGuildAutomation"/> : construit le premier bâtiment cible manquant
        /// (dans l'ordre des villes/types), sinon améliore le bâtiment existant de plus bas niveau parmi
        /// les cibles. Retourne false si rien n'a pu être fait (rien à construire/améliorer, ou coût non
        /// couvert). Un plafond de preset à 0 empêche toute construction du type ; un plafond atteint
        /// arrête son amélioration (voir AutomationPresetSettings / TechnologyId.AutomationPreset).
        /// </summary>
        private bool TryPerformOneGuildAction(Model.Civilization.Civilization civ, AutomationSettings presets, BuildingType[] targets, out bool blockedByResources)
        {
            blockedByResources = false;

            foreach (var city in civ.Cities)
                foreach (var type in targets)
                {
                    if (presets.GetActivePresetCap(type, civ) <= 0 || city.Buildings.Any(b => b.Type == type))
                        continue;
                    if (BuildBuilding(city, type, out bool resourceBlocked))
                        return true;
                    blockedByResources |= resourceBlocked;
                }

            var lowestLevelFirst = civ.Cities
                .SelectMany(city => city.Buildings
                    .Where(b => targets.Contains(b.Type) && b.Level < presets.GetActivePresetCap(b.Type, civ))
                    .Select(b => (city, b.Type, b.Level)))
                .OrderBy(x => x.Level);

            foreach (var (city, type, _) in lowestLevelFirst)
            {
                if (BuildBuilding(city, type, out bool resourceBlocked))
                    return true;
                blockedByResources |= resourceBlocked;
            }

            return false;
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
                {
                    var fresh = CreateBuilding(bt); // instance neuve : l'appelant peut la conserver/muter
                    // Coût affiché dépendant du nombre de Relais déjà construits (voir BuildBuilding) :
                    // renseigné ici aussi pour que l'aperçu de coût de l'UI corresponde à celui appliqué
                    // au moment de l'achat.
                    if (fresh is AdventurersWaypost waypost && civ != null)
                        waypost.PriorWaypostCount = CountAdventurersWayposts(civ);
                    return fresh;
                }
            }

            return null;
        }

        /// <summary>Nombre de Relais des Aventuriers déjà construits (niveau &gt; 0) dans la civilisation, pour le coût progressif d'<see cref="AdventurersWaypost"/> et l'affichage sur la Guilde des Aventuriers.</summary>
        public static int CountAdventurersWayposts(Model.Civilization.Civilization civ)
        {
            int count = 0;
            var cities = civ.Cities;
            for (int i = 0; i < cities.Count; i++)
            {
                var buildings = cities[i].Buildings;
                for (int j = 0; j < buildings.Count; j++)
                    if (buildings[j].Type == BuildingType.AdventurersWaypost && buildings[j].Level > 0)
                        count++;
            }
            return count;
        }

        /// <summary>
        /// Construit (ou am�liore) un b�timent dans la ville sp�cifi�e.
        /// Lance InvalidOperationException si pas assez de ressources ou si l'action n'est pas permise.
        /// </summary>
        public bool BuildBuilding(City city, BuildingType type) => BuildBuilding(city, type, out _);

        /// <summary>
        /// Surcharge utilisée par l'automatisation de guilde (<see cref="TryPerformOneGuildAction"/>)
        /// pour distinguer un échec « rien à construire structurellement » (prérequis non remplis,
        /// unique déjà bâti, plafond de niveau atteint) d'un échec « coût non couvert » via
        /// <paramref name="blockedByInsufficientResources"/> : seul le premier cas peut être mis en
        /// cache sans risque, le second se résout de lui-même à mesure que les ressources s'accumulent,
        /// sans qu'aucune des versions suivies par <see cref="_guildNothingToDoCache"/> ne bouge.
        /// </summary>
        private bool BuildBuilding(City city, BuildingType type, out bool blockedByInsufficientResources)
        {
            blockedByInsufficientResources = false;
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            var existing = city.Buildings.FirstOrDefault(b => b.Type == type);

            ResourceSet cost;
            Building resultBuilding;
            if (existing == null)
            {
                var prototype = CreateBuilding(type) ?? throw new ArgumentException("Unknown building type", nameof(type));

                // Bâtiments verrouillés par défaut (GetDefaultMaxLevel() == 0, ex. Smelter/MushroomFarm)
                // tant qu'un vertex de prestige ne relève pas leur plafond : la liste UI
                // (GetBuildingOrBuildableEntry) filtre déjà sur ce plafond, mais les appelants qui
                // construisent directement — l'automatisation de guilde (TickGuildAutomation) en
                // tête — passaient outre et posaient le bâtiment en douce dès le niveau d'Hôtel de
                // Ville requis atteint, sans jamais consulter le prestige. Miroir de la garde sur
                // l'amélioration ci-dessous (existing.Level >= GetMaxLevel(...)).
                if (GetMaxLevel(prototype, civ) <= 0)
                    return false;

                // Les deux tests qui suivent lisent les prérequis, donc passent par le contexte allégé
                // quand la civilisation en réduit les seuils (voir BuildReducedPrerequisiteContext).
                var buildContext = prototype.IsUnique ? BuildReducedPrerequisiteContext(city, civ) : city;

                if (_state.GetMapFor(city.Position) is not { } map1 ||
                    !prototype.IsBuildingAvailableForCity(map1, buildContext, civ))
                    return false;

                if (!prototype.HasBuildPrerequisites(buildContext, _state))
                    return false;

                // civ.UniqueBuildings is a permanent "ever built" flag (never cleared on city loss,
                // used e.g. by PrestigeController.HasImperialPort) — checking it here would block
                // rebuilding forever once the city holding it is destroyed. The cache is the live source
                // of truth and is correctly refreshed by Civilization.RemoveCity.
                if (prototype.IsUnique && civ.GetUniqueBuilding(type) != null)
                    return false;

                if (prototype.IsUnique && city.Buildings.Any(b => b.IsUnique))
                    return false;

                // Coût progressif : renseigné juste avant l'appel à GetBuildCost() (voir
                // AdventurersWaypost.GetBuildCost), sur le même modèle que GetBuildingOrBuildableEntry.
                if (prototype is AdventurersWaypost waypost)
                    waypost.PriorWaypostCount = CountAdventurersWayposts(civ);

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
                    blockedByInsufficientResources = true;
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

                // La Guilde des Aventuriers accorde automatiquement un Relais dans sa propre ville :
                // ajout direct (bâtiment gratuit), même patron que PrestigeMapController.GrantBuildingToCity.
                if (type == BuildingType.AdventurersGuild && !city.Buildings.Any(b => b.Type == BuildingType.AdventurersWaypost))
                    city.AddBuilding(new AdventurersWaypost { Level = 1 });
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

            // Résolu une fois pour toute la liste : les seuils réduits ne dépendent que de la
            // civilisation et de la ville (voir BuildReducedPrerequisiteContext).
            var buildContext = BuildReducedPrerequisiteContext(city, civ);

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
                         !prototype.HasBuildPrerequisites(buildContext, _state))
                {
                    continue;
                }
                else if (_state.GetMapFor(city.Position) is { } map2 &&
                         prototype.IsBuildingAvailableForCity(map2, buildContext))
                {
                    result.Add(prototype);
                }
            }

            return result;
        }

        /// <summary>
        /// Bâtiments uniques que cette ville peut réellement poser maintenant, ressources mises à part :
        /// débloqués pour la civilisation (niveau max > 0), pas déjà bâtis ailleurs, disponibles pour la
        /// ville (niveau/terrain/couche) et dont les prérequis sont remplis — le tout vu à travers le
        /// même contexte allégé que <see cref="BuildBuilding"/> (voir
        /// <see cref="BuildReducedPrerequisiteContext"/>). Liste vide si la ville héberge déjà un unique :
        /// elle ne peut en accueillir qu'un seul.
        ///
        /// <para>Là où <see cref="GetUniqueBuildingsAndBuildables"/> alimente l'affichage et inclut
        /// délibérément les uniques déjà bâtis comme ceux montrés grisés faute de prérequis, celle-ci ne
        /// rend que des candidats sur lesquels <see cref="BuildBuilding"/> peut aboutir. C'est ce qui
        /// permet à l'autoplay de ne jamais s'enfermer à farmer le coût d'un unique qu'il ne pourrait de
        /// toute façon pas poser (voir <c>UniqueBuildingsObjective</c>).</para>
        /// </summary>
        public List<Building> GetBuildableUniqueBuildings(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var result = new List<Building>();
            if (city.Buildings.Any(b => b.IsUnique)) return result;

            var civ = _state.GetCivilization(city.CivilizationIndex)
                      ?? throw new ArgumentException("Civilization not found", nameof(city.CivilizationIndex));

            if (_state.GetMapFor(city.Position) is not { } map) return result;

            var buildContext = BuildReducedPrerequisiteContext(city, civ);

            foreach (var bt in _uniqueBuildingTypes)
            {
                // Same live-cache reasoning as BuildBuilding(): civ.UniqueBuildings never clears on
                // city loss, so it must not be treated as "currently built" here either.
                if (civ.GetUniqueBuilding(bt) != null) continue;

                var prototype = CreateBuilding(bt);
                if (prototype == null || GetMaxLevel(prototype, civ) <= 0) continue;
                if (!prototype.IsBuildingAvailableForCity(map, buildContext, civ)) continue;
                if (!prototype.HasBuildPrerequisites(buildContext, _state)) continue;

                result.Add(prototype);
            }

            return result;
        }

        /// <summary>
        /// La ville telle que doivent la voir les tests de construction d'un bâtiment <b>unique</b>, une
        /// fois appliquée la réduction de prérequis de la civilisation
        /// (UNIQUE_BUILDING_PREREQUISITE_REDUCTION, aujourd'hui le seul Grand Terrier gobelin). Rend la
        /// ville inchangée quand il n'y a rien à réduire — le cas de toutes les races sauf une, sur un
        /// chemin que l'autoplayer emprunte des centaines de fois par passe.
        ///
        /// <para>La réduction est portée par la <i>ville présentée</i> plutôt que par le seuil de chaque
        /// bâtiment : les seuils sont éparpillés entre <c>AvailableAtLevel</c>, des surcharges de
        /// <c>IsBuildingAvailableForCity</c> qui comparent <c>city.Level</c> à une constante écrite en
        /// dur (Port Impérial, Haut-Fourneau) et onze <c>HasBuildPrerequisites</c>. Un seul point de
        /// vérité les couvre tous, y compris les bâtiments uniques à venir.</para>
        /// </summary>
        /// <inheritdoc cref="BuildReducedPrerequisiteContext(City, Civilization)"/>
        /// <remarks>
        /// Public pour l'UI : elle interroge les prérequis d'un bâtiment pour le griser et pour dire ce
        /// qui manque (voir CityBuildingService), et doit poser exactement la même question que
        /// <see cref="BuildBuilding"/> — sans quoi le panneau grise un bâtiment que le contrôleur
        /// accepterait de bâtir.
        /// </remarks>
        public IBuildingContext BuildPrerequisiteContext(City city)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            var civ = _state.GetCivilization(city.CivilizationIndex);
            return civ == null ? city : BuildReducedPrerequisiteContext(city, civ);
        }

        private static IBuildingContext BuildReducedPrerequisiteContext(City city, Civilization civ)
        {
            int reduction = civ.ModifierAggregator.ApplyModifiers(ECategory.UNIQUE_BUILDING_PREREQUISITE_REDUCTION, "", 0);
            return reduction > 0 ? new ReducedPrerequisiteContext(city, reduction) : city;
        }

        /// <summary>
        /// Vue d'une ville dont les seuils de prérequis sont abaissés de <c>Reduction</c> : son niveau
        /// paraît d'autant plus haut, et tout niveau de bâtiment exigé d'autant plus bas. Ne quitte
        /// jamais le test de constructibilité — ni le niveau réel de la ville, ni celui de ses bâtiments,
        /// ni aucun coût n'en dépendent.
        ///
        /// <para>Le plancher à 1 sur les bâtiments exigés est ce qui garde « exiger un Temple » de
        /// devenir « n'exiger aucun Temple » : un seuil ramené à 0 serait satisfait par une ville qui
        /// n'a pas le bâtiment du tout. La réduction allège un prérequis, elle ne le supprime pas.</para>
        /// </summary>
        private sealed class ReducedPrerequisiteContext : IBuildingContext
        {
            private readonly City _city;
            private readonly int _reduction;

            public ReducedPrerequisiteContext(City city, int reduction)
            {
                _city = city;
                _reduction = reduction;
            }

            public int Level => _city.Level + _reduction;
            public Vertex Position => _city.Position;
            public IReadOnlyList<Building> Buildings => _city.Buildings;

            public bool HasBuildingAtLevel(BuildingType type, int minLevel)
            {
                int required = Math.Max(1, minLevel - _reduction);
                var buildings = _city.Buildings;
                for (int i = 0; i < buildings.Count; i++)
                    if (buildings[i].Type == type && buildings[i].Level >= required)
                        return true;
                return false;
            }

            public int CountBuildingsAtLevel(IReadOnlyList<BuildingType> types, int minLevel)
            {
                int required = Math.Max(1, minLevel - _reduction);
                int count = 0;
                var buildings = _city.Buildings;
                for (int i = 0; i < buildings.Count; i++)
                    if (buildings[i].Level >= required && types.Contains(buildings[i].Type))
                        count++;
                return count;
            }
        }

        public int GetMaxLevel(Building building, int civilizationIndex)
        {
            if (_state == null) throw new InvalidOperationException("WorldState has not been initialized.");

            // Civilizations[civilizationIndex] supposait Index == position dans la liste — plus vrai
            // depuis que les civs PNJ éliminées (0 ville) sont retirées de WorldState.Civilizations,
            // ce qui décale les positions suivantes. GetCivilization fait la recherche par Index.
            var civ = _state.GetCivilization(civilizationIndex)
                ?? throw new InvalidOperationException($"No civilization with index {civilizationIndex}.");
            return GetMaxLevel(building, civ);
        }

        public int GetMaxLevel(Building building, Civilization civ)
        {
            if (civ.TryGetCachedMaxLevel(building.Type, out int cached))
                return cached;

            string subCategory = BuildingTypeNames.Of(building.Type);
            var modifiers = civ.ModifierAggregator.GetActiveModifiersUnfiltered(ECategory.BUILDING_MAX_LEVEL);

            // Bonus et malus additifs sommés séparément : les malus raciaux (RaceDefinitions, ex.
            // Gobelins -1 sur les bâtiments standards) s'appliquent en dernier et sont plafonnés pour
            // ne jamais rendre inconstructible un bâtiment qui aurait été atteignable sans eux — un
            // bâtiment jamais débloqué par ailleurs (base + bonus <= 0, ex. Bibliothèque avant sa
            // recherche) reste à 0, inchangé par le malus.
            int bonus = 0, malus = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (!modifier.IsActive || modifier.SubCategory != subCategory) continue;
                int amount = (int)modifier.Value;
                if (amount < 0) malus -= amount; else bonus += amount;
            }

            int beforeMalus = building.GetDefaultMaxLevel() + bonus;
            int value = beforeMalus > 0 ? Math.Max(1, beforeMalus - malus) : beforeMalus;

            civ.SetCachedMaxLevel(building.Type, value);
            return value;
        }

        /// <summary>
        /// Niveau max pour un bâtiment d'une ville précise. Identique à <see cref="GetMaxLevel(Building, Civilization)"/>
        /// (civ-wide, caché) pour tout bâtiment autre que l'Hôtel de Ville. Pour l'Hôtel de Ville,
        /// applique en plus INLAND_CITY_LEVEL_CAP (Sirènes) : plafonne le résultat si la ville ne
        /// touche pas directement le terrain requis — mécanique par ville, donc non cachée au niveau
        /// civ.
        ///
        /// <para>Écrit pour être appelable par ville et par tick : c'est la surcharge dont dépend
        /// BuildingLevelObjective.IsDone, sans quoi l'autoplay se croit capable de monter une Mairie
        /// que BuildBuilding refusera (voir la garde sur GetMaxLevel(…, city) plus haut) et bloque sa
        /// liste de priorités pour toujours. Les trois retours anticipés couvrent tout le monde sauf
        /// une Mairie de surface chez une race à plafond ; le dernier ne coûte qu'un lookup de
        /// dictionnaire, la liste étant rendue sans allocation ni filtrage (d'où le test sur
        /// <see cref="Modifier.IsActive"/> ici) et vide pour les huit races qui n'en ont pas.</para>
        /// </summary>
        public int GetMaxLevel(Building building, Civilization civ, City city)
        {
            int result = GetMaxLevel(building, civ);
            if (building.Type != BuildingType.TownHall) return result;
            if (city.Position.Z != IslandMap.SurfaceLayer) return result;

            var caps = civ.ModifierAggregator.GetActiveModifiersUnfiltered(ECategory.INLAND_CITY_LEVEL_CAP);
            if (caps.Count == 0) return result;

            var map = _state?.GetMapFor(city.Position);
            if (map == null) return result;

            for (int i = 0; i < caps.Count; i++)
            {
                var modifier = caps[i];
                if (!modifier.IsActive) continue;
                if (Enum.TryParse<TerrainType>(modifier.SubCategory, out var terrain) &&
                    !map.VertexHasTerrainType(city.Position, terrain))
                    result = Math.Min(result, (int)modifier.Value);
            }

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
                BuildingType.AdventurersWaypost => new AdventurersWaypost(),
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
