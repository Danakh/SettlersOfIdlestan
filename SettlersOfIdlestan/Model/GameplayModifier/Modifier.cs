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
            FORGE_DOUBLE_PROD_BONUS,
            MINE_GOLD_CHANCE_PERCENT,
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
