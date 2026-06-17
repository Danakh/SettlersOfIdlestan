using System;
using System.Collections.Generic;
using System.Text;

namespace SettlersOfIdlestan.Model.GameplayModifier
{
    public class Modifier
    {
        public enum ECategory
        {
            BUILDING_MAX_LEVEL,
            BUILDING_PRODUCTION,
            HARVEST_SPEED,
            RESEARCH_SPEED,
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
            /// <summary>Flags que l'attaque automatique est déverrouillée pour la civilisation.</summary>
            UNLOCK_AUTO_ATTACK,
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
            /// <summary>Flags que la vente d'Acier au marché est déverrouillée (prix premium).</summary>
            UNLOCK_STEEL_TRADE,
            /// <summary>Flags que la vente automatique du surplus est déverrouillée pour les villes possédant un Marché niv.4+.</summary>
            UNLOCK_AUTO_MARKET_TRADE,
            /// <summary>Flags que la spécialisation des Marchés (taux de vente 4:1) est déverrouillée pour la civilisation.</summary>
            UNLOCK_MARKET_SPECIALIZATION,
            /// <summary>Multiplicateur de vitesse d'envoi des renforts. Base = 1.0; +1.0 = intervalle divisé par 2.</summary>
            REINFORCEMENT_SPEED,
            /// <summary>Flags que la Mine Profonde (accès à l'Inframonde) est déverrouillée pour la civilisation.</summary>
            UNLOCK_DEEPEST_MINE,
            /// <summary>Chance (en %) supplémentaire qu'un trésor apparaisse sur un nouvel hexagone de l'Inframonde.</summary>
            UNDERWORLD_TREASURE_CHANCE_PERCENT,
            /// <summary>Flags que la magie est déverrouillée (Tours de Mages, écran Rituels, branche de recherche).</summary>
            UNLOCK_MAGIC,
            /// <summary>SubCategory = RitualId name. Signale que le rituel est connu (débloqué par recherche).</summary>
            UNLOCK_RITUAL,
            /// <summary>Rituels actifs simultanés supplémentaires (base = nombre de Tours de Mages).</summary>
            RITUAL_MAX_COUNT,
            /// <summary>Multiplicateur du budget de puissance des rituels. Base = 1.0; +0.05 = +5%.</summary>
            RITUAL_TOTAL_POWER,
            /// <summary>Fraction de réduction du coût d'entretien des rituels (0.2 = -20%).</summary>
            RITUAL_UPKEEP_REDUCTION,
            /// <summary>Nombre de features magiques présentes sur l'île. SubCategory = "FairyCircle" ou "Dolmen".</summary>
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
        }

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
