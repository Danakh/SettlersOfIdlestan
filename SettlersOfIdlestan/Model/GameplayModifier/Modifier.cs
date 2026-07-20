using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.GameplayModifier
{
    public class Modifier
    {
        [JsonConverter(typeof(JsonStringEnumConverter<ECategory>))]
        public enum ECategory
        {
            BUILDING_MAX_LEVEL,
            BUILDING_PRODUCTION,
            HARVEST_SPEED,
            /// <summary>Multiplicateur de vitesse de génération des points de recherche (Bibliothèque, Laboratoire). Base = 1.0.</summary>
            RESEARCH_PRODUCTION_SPEED,
            /// <summary>Multiplicateur de vitesse d'investissement des points de recherche dans la recherche active. Base = 1.0.</summary>
            RESEARCH_INVESTMENT_SPEED,
            UNIT_PRODUCTION_SPEED,
            RESEARCH_COST_REDUCTION,
            STORAGE_CAPACITY_BASIC,
            STORAGE_CAPACITY_ADVANCED,
            TRADE_GOLD_PACKAGES,
            /// <summary>Chance (in %) to double automatic harvest yield. SubCategory = BuildingType name (empty = applies to all).</summary>
            HARVEST_PRODUCTION_BONUS,
            /// <summary>Flat bonus (in %) added to the Forge's double-harvest chance.</summary>
            FORGE_DOUBLE_HARVEST_BONUS,
            MINE_GOLD_CHANCE_PERCENT,
            /// <summary>SubCategory = BuildingType name. Granted to the initial city at run start.</summary>
            STARTING_CITY_BUILDING,
            /// <summary>SubCategory = BuildingType name. Granted to every new outpost, including the initial city.</summary>
            NEW_CITY_BUILDING,
            /// <summary>Flat bonus added to the city defense score. SubCategory unused.</summary>
            CITY_DEFENSE,
            /// <summary>SubCategory = TechnologyId name. Signals that this vertex unlocks the given technology.</summary>
            UNLOCK_RESEARCH,
            /// <summary>Flags that maritime routes (water-water edges) are unlocked for the civilization.</summary>
            UNLOCK_MARITIME_ROUTES,
            /// <summary>Flat bonus added to the city attack range (in edges). SubCategory unused.</summary>
            CITY_ATTACK_RANGE,
            /// <summary>Flat bonus added to the reinforcement range (in edges). SubCategory unused.</summary>
            REINFORCEMENT_RANGE,
            /// <summary>Flags that wonder construction is unlocked for the civilization.</summary>
            UNLOCK_WONDERS,
            /// <summary>Flags that an advanced resource is discovered and visible. SubCategory = Resource enum name.</summary>
            UNLOCK_RESOURCE,
            /// <summary>Multiplicateur de vitesse de régénération de défense des villes. Base = 1.0.</summary>
            CITY_DEFENSE_REGEN_SPEED,
            /// <summary>Bonus plat ajouté à la capacité maximale de soldats de chaque ville. SubCategory unused.</summary>
            CITY_MAX_SOLDIERS_BONUS,
            /// <summary>Or supplémentaire par tranche de 10 unités vendues en une seule vente groupée. SubCategory unused.</summary>
            TRADE_BULK_GOLD_BONUS,
            /// <summary>Quand un bâtiment contribuant à MaxDefense est construit/amélioré, ajoute aussi sa valeur à CurrentDefense. SubCategory unused.</summary>
            BUILDING_DEFENSE_ON_CONSTRUCT,
            /// <summary>Flags que le système de recherche est déverrouillé pour la civilisation.</summary>
            UNLOCK_RESEARCH_SYSTEM,
            /// <summary>Flags que la file de recherche est déverrouillée pour la civilisation.</summary>
            UNLOCK_RESEARCH_QUEUE,
            /// <summary>Flags que le renforcement automatique est déverrouillé pour la civilisation.</summary>
            UNLOCK_AUTO_REINFORCEMENT,
            /// <summary>Flags que la patrouille automatique (raid anti-monstres près des villes) est déverrouillée pour la civilisation.</summary>
            UNLOCK_PATROL,
            /// <summary>Bonus fixe de génération de ressource par cycle (1 000 ticks). SubCategory = Resource enum name.</summary>
            PASSIVE_RESOURCE_GENERATION,
            /// <summary>Multiplicateur additif sur les points de prestige gagnés. Base = 0.0; +0.1 = +10%.</summary>
            PRESTIGE_GAIN,
            /// <summary>Flags que les Forges peuvent produire des ArmeAcier (consommable) au rythme de 1/10 s accéléré par niveau.</summary>
            UNLOCK_STEEL_WEAPONS,
            /// <summary>Multiplicateur de vitesse de production de la Fonderie. Base = 1.0; +0.15 = +15%.</summary>
            SMELTER_SPEED,
            /// <summary>Bonus (négatif) appliqué au coût en minerai du cycle de la Fonderie. Base = Smelter.OreInputPerCycle.</summary>
            SMELTER_ORE_INPUT,
            /// <summary>Obsolète — plus utilisé depuis l'introduction des consommables. Conservé pour compatibilité des sauvegardes.</summary>
            STEEL_WEAPONS_SOLDIER_COUNT,
            /// <summary>Flags que les Forges peuvent produire des ArmureAcier (consommable) ; chaque armure consommée donne 50 % de chance de sauver un soldat.</summary>
            UNLOCK_STEEL_ARMOR,
            /// <summary>Obsolète — fusionné dans UNLOCK_INTERMEDIATE_TRADE. Conservé pour compatibilité des sauvegardes.</summary>
            UNLOCK_STEEL_TRADE,
            /// <summary>Flags que la vente automatique du surplus est déverrouillée pour les villes possédant un Marché niv.4+.</summary>
            UNLOCK_AUTO_MARKET_TRADE,
            /// <summary>Flags que toutes les ressources de base se vendent au taux de 4:1 pour la civilisation.</summary>
            UNLOCK_MARKET_SPECIALIZATION,
            /// <summary>Flags que l'achat automatique de la ressource de base la plus rare avec l'or excédentaire est déverrouillé pour les villes possédant un Marché niv.4+.</summary>
            UNLOCK_AUTO_BUY_TRADE,
            /// <summary>Multiplicateur de vitesse d'envoi des renforts. Base = 1.0; +1.0 = intervalle divisé par 2.</summary>
            REINFORCEMENT_SPEED,
            /// <summary>Flags que la Mine Profonde (accès à l'Inframonde) est déverrouillée pour la civilisation.</summary>
            UNLOCK_DEEPEST_MINE,
            /// <summary>Chance (en %) supplémentaire qu'un trésor apparaisse sur un nouvel hexagone de l'Inframonde.</summary>
            UNDERWORLD_TREASURE_CHANCE_PERCENT,
            /// <summary>Flags que la magie est déverrouillée (Tours de Mages, écran Rituels, branche de recherche).</summary>
            UNLOCK_MAGIC,
            /// <summary>Flags cosmétique pour le tooltip du vertex Invocations (Tour de Mages, invocations, écran magique).</summary>
            UNLOCK_INVOCATIONS,
            /// <summary>SubCategory = RitualId name. Signale que le rituel est connu (débloqué par recherche).</summary>
            UNLOCK_RITUAL,
            /// <summary>Rituels actifs simultanés supplémentaires (base = nombre de Tours de Mages).</summary>
            RITUAL_MAX_COUNT,
            /// <summary>Multiplicateur du budget de puissance des rituels. Base = 1.0; +0.05 = +5%.</summary>
            RITUAL_TOTAL_POWER,
            /// <summary>Fraction de réduction du coût d'entretien des rituels (0.2 = -20%).</summary>
            RITUAL_UPKEEP_REDUCTION,
            /// <summary>Nombre de features magiques présentes sur l'île. SubCategory = "FairyCircle".</summary>
            MAGIC_FEATURE_COUNT,
            /// <summary>Multiplicateur additif de vitesse de génération d'or des marchés. Base = 1.0; +0.1 = +10%.</summary>
            MARKET_GOLD_SPEED,
            /// <summary>Flags que la Faille des Abysses est déverrouillée (réservé pour une future fonctionnalité).</summary>
            UNLOCK_ABYSS,
            /// <summary>Barbacane : si la défense courante de la cité est > 20 quand un soldat défenseur devrait mourir, perd 1 défense à la place. SubCategory unused.</summary>
            CITY_DEFENSE_PROTECTS_SOLDIERS,
            /// <summary>Flags que le Port Impérial peut automatiser la construction des Ports maritimes.</summary>
            UNLOCK_SEAPORT_AUTOMATION,
            /// <summary>Bonus additif de prestige par Port maritime au niveau max. Base = 0.0 ; +0.05 = +5% par port niv. 4.</summary>
            PRESTIGE_GAIN_PER_SEAPORT_LEVEL4,
            /// <summary>Réduction du coût de route de base de l'Inframonde. Chaque point réduit la pierre de base de 1 et le minerai de base de 0,5 (arrondi inférieur), avant multiplication par arrivalDist.</summary>
            UNDERWORLD_ROAD_BASE_REDUCTION,
            /// <summary>Flags que l'action Raid est déverrouillée pour la civilisation du joueur.</summary>
            UNLOCK_RAID,
            /// <summary>Nombre de soldats par ville dont la nourriture d'entretien est offerte chaque cycle. SubCategory unused.</summary>
            SOLDIER_FOOD_FREE_PER_CITY,
            /// <summary>Flags que la Hutte d'Alchimie peut produire des Potions de Soin (consommable) ; chaque potion consommée donne 50 % de chance de sauver un soldat.</summary>
            UNLOCK_HEALING_POTION,
            /// <summary>SubCategory = SpellId name. Signale que le sort instantané est connu (débloqué par recherche).</summary>
            UNLOCK_SPELL,
            /// <summary>Fraction de réduction du coût en cristaux d'un sort. SubCategory = SpellId name (vide = s'applique à tous les sorts) (0.25 = -25%).</summary>
            SPELL_COST_REDUCTION,
            /// <summary>Flags que les villes peuvent attaquer activement une MonsterFeature à distance 2 si elles possèdent une Tour de guet. SubCategory unused.</summary>
            UNLOCK_RANGED_MONSTER_ATTACK,
            /// <summary>Multiplicateur de vitesse d'attaque, contre les monstres et contre les villes ennemies. Base = 1.0; +1.0 = intervalle divisé par 2.</summary>
            ATTACK_SPEED,
            /// <summary>Fraction de réduction du coût de montée de niveau de la Merveille (uniquement). Base = 0.0; 0.1 = -10%.</summary>
            WONDER_COST_REDUCTION,
            /// <summary>Multiplicateur de vitesse d'investissement des Monuments (Merveille, Mine Profonde, Spire de Corruption), actif uniquement pour une ressource investie dont le stock dépasse 50% de sa capacité maximale. Base = 1.0; +1.0 = vitesse doublée.</summary>
            INVESTMENT_SPEED_HIGH_STOCK_BONUS,
            /// <summary>Flags que l'action de relocalisation de ville est déverrouillée pour la civilisation du joueur.</summary>
            UNLOCK_RELOCATION,
            /// <summary>Bonus de vitesse de récolte par niveau de Dominion sur l'hex où a lieu la récolte. Base = 0.0; agrégé via PerVertexModifiers (valeur × vertex de prestige adjacents achetés), puis multiplié par le niveau de Dominion au moment de la récolte.</summary>
            DOMINION_HARVEST_SPEED_PER_LEVEL,
            /// <summary>Flags que le Dominion est déverrouillé pour la civilisation (octroyé par le pouvoir divin Foi). Remplace l'ancien verrou générique RequiresGodPoint/HasEverHadGodPoint.</summary>
            UNLOCK_DOMINION,
            /// <summary>Multiplicateur appliqué à la capacité totale de stockage (basique + avancée), après les bonus additifs. Base = 1.0; +10.0 = +1000%.</summary>
            STORAGE_CAPACITY_MULTIPLIER,
            /// <summary>Obsolète — fusionné dans UNLOCK_INTERMEDIATE_TRADE. Conservé pour compatibilité des sauvegardes.</summary>
            UNLOCK_ORE_GLASS_TRADE,
            /// <summary>Flags que la vente des ressources intermédiaires (Minerai, Verre, Acier) au marché est déverrouillée (recherche Comptoirs Avancés), au prix de 1/5 du coût d'achat.</summary>
            UNLOCK_INTERMEDIATE_TRADE,
            /// <summary>Flags que la construction du Grand Phare est déverrouillée pour la civilisation.</summary>
            UNLOCK_GREAT_LIGHTHOUSE,
            /// <summary>Flags que la récolte est possible sur les Territoires Contestés (à demi-vitesse, voir ContestedTerritory.GetHarvestTimeMultiplier).</summary>
            UNLOCK_CONTESTED_HARVEST,
            /// <summary>Bonus additif sur le taux de récupération des points investis lors de l'annulation d'une recherche. Base = 0.5 (50%); +0.125 = +12,5%.</summary>
            RESEARCH_CANCEL_REFUND_BONUS,
            /// <summary>Flags que les raids automatiques sur une civilisation ciblée (Vendetta) sont déverrouillés pour la civilisation.</summary>
            UNLOCK_VENDETTA,
            /// <summary>Multiplicateur additif sur l'intervalle entre deux tentatives d'apparition de monstre de bordure dans l'Inframonde. Base = 1.0; +0.75 = intervalle ×1,75 (apparitions plus rares).</summary>
            UNDERWORLD_MONSTER_SPAWN_INTERVAL,
            /// <summary>Nombre de niveaux retranchés au niveau effectif de la Corruption pour le malus de récolte (plancher : niveau 1 — seule la présence du Dominion peut réellement contrer la corruption). Base = 0.</summary>
            CORRUPTION_LEVEL_REDUCTION,
            /// <summary>Flags que les routes du Vide (arêtes entre deux hexagones de Vide) sont déverrouillées pour la civilisation, comme les routes maritimes pour l'eau.</summary>
            UNLOCK_VOID_ROUTES,
            /// <summary>Flags que la feature Os Divins (DivineBones) est révélée et investissable sur les îles des Abysses générées après la première.</summary>
            UNLOCK_DIVINE_BONES,
            /// <summary>Fraction de réduction du coût de Purification des Os Divins (Cristal et points de recherche). Base = 0.0; 0.05 = -5%.</summary>
            DIVINE_BONES_COST_REDUCTION,
            /// <summary>Bonus de vitesse de régénération de défense par point de Dominion sur les 3 hexs de l'emplacement. Base = 0.0; agrégé via PerVertexModifiers (valeur × vertex de prestige adjacents achetés), puis multiplié par la somme des niveaux de Dominion autour de la ville.</summary>
            DOMINION_DEFENSE_REGEN_PER_LEVEL,
            /// <summary>Bonus additif au plafond de Dominion que la production d'un Temple peut atteindre, par niveau de Temple (base : 2/niveau, voir CorruptionController.ProcessTempleProduction). Base = 0.</summary>
            TEMPLE_DOMINION_CAP,
            /// <summary>Points de pourcentage de chance de débordement supplémentaires par niveau, pour le Dominion uniquement (base : 10%/niveau, voir CorruptionController.ProcessSpread). Base = 0.</summary>
            DOMINION_SPREAD_CHANCE,
            /// <summary>Probabilité (0-1) qu'un Dominion situé sur un hex d'une ville possédant un Temple ne perde pas de niveau lors d'une annulation mutuelle avec la Corruption (la Corruption, elle, perd toujours le sien). Base = 0.0.</summary>
            TEMPLE_DOMINION_PROTECTION_CHANCE,
            /// <summary>Flags que les routes du Vide déjà bâties ne comptent que pour moitié dans le coût exponentiel de la suivante (voir RoadController.GetVoidRouteResearchCost).</summary>
            VOID_ROUTE_COST_REDUCTION,
            /// <summary>Flags que chaque Temple ajoute un bonus fixe de défense à sa ville selon son niveau (voir Temple.GetDefenseBonusForLevel).</summary>
            TEMPLE_DEFENSE_BONUS,
            /// <summary>Distance minimale (en arêtes) entre deux villes de la même civilisation. Base = 3 (voir CityBuilderController) ; REPLACER 2 (Gobelins) ou 4 (Géants).</summary>
            CITY_MIN_DISTANCE,
            /// <summary>Restriction raciale de placement : tout nouveau vertex de ville en surface doit toucher au moins un hex du terrain indiqué. SubCategory = nom du TerrainType (voir CityBuilderController.GetBuildableVertices).</summary>
            CITY_PLACEMENT_REQUIRES_TERRAIN,
            /// <summary>Flags que chaque Temple construit ou amélioré produit instantanément du Dominion sur les 3 hexs de sa ville, jusqu'à Ziggurat.MaxTriggersPerCity fois par ville (Ziggourat — voir CorruptionController.ApplyZigguratInstantProduction).</summary>
            TEMPLE_INSTANT_DOMINION,
            /// <summary>Fraction de réduction du coût en ressources des nouvelles villes. Base = 0.0 ; 0.25 = -25% (voir CityBuilderController.NewCityBuildingCostFor).</summary>
            NEW_CITY_COST_REDUCTION,
            /// <summary>Bonus additif de vitesse de construction automatique des bâtiments par les guildes (Artisans, Récolteurs, Marchands, etc.), par ville possédée. Base = 0.0 ; agrégé puis multiplié par le nombre de villes de la civilisation (voir BuildingController.TickGuildAutomation). 0.1 = +10% par ville.</summary>
            GUILD_AUTOMATION_SPEED_PER_CITY,
            /// <summary>Vol (Garudas) : autorise la fondation de villes en surface sans connexion routière, jusqu'à Value arêtes d'une ville de la civilisation ; le vertex doit toucher au moins un hex terrestre (le survol de l'eau est permis, pas l'atterrissage en pleine mer). Les autres règles (occupation, distances, terrain) s'appliquent normalement, et l'Inframonde/l'Abysse sont exclus (voir CityBuilderController.AddFlightCandidateVertices). Base = 0 (pas de vol).</summary>
            CITY_PLACEMENT_FLYING,
        }

        [JsonConverter(typeof(JsonStringEnumConverter<EType>))]
        public enum EType
        {
            ADDITIVE,
            MULTIPLICATIVE,
            REPLACER
        }


        public ECategory Category { get; set; }
        public string SubCategory { get; set; } = "";
        public EType Type { get; set; }
        public double Value { get; set; }
        public bool IsActive { get; set; } = true;

        public Modifier() { }

        public Modifier(ECategory category, EType type, double value)
        {
            Category = category;
            Type = type;
            Value = value;
        }

        public Modifier(ECategory category, string subCategory, EType type, double value)
        {
            Category = category;
            SubCategory = subCategory;
            Type = type;
            Value = value;
        }

        public bool AppliesTo(ECategory category, string subCategory)
        {
            return IsActive 
                && (Category == category)
                && ((SubCategory == "") || (SubCategory == subCategory));
        }

        public int Apply(int baseValue)
        {
            return Type switch
            {
                EType.ADDITIVE => baseValue + (int)Value,
                EType.MULTIPLICATIVE => (int)(baseValue * Value),
                EType.REPLACER => (int)Value,
                _ => throw new InvalidOperationException("Unknown modifier type")
            };
        }

        public double Apply(double baseValue)
        {
            return Type switch
            {
                EType.ADDITIVE => baseValue + Value,
                EType.MULTIPLICATIVE => baseValue * Value,
                EType.REPLACER => Value,
                _ => throw new InvalidOperationException("Unknown modifier type")
            };
        }
    }
}
