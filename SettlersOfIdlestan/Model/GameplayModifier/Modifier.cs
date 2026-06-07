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
            /// <summary>Flags que les armes en acier sont déverrouillées pour les casernes (1 Acier supplémentaire → 5 soldats d'un coup).</summary>
            UNLOCK_STEEL_WEAPONS,
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
