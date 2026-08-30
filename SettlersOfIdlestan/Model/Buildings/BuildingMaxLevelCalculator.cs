using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Races;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Calcule le niveau max theorique d'un type de batiment : GetDefaultMaxLevel() + tous les bonus
/// BUILDING_MAX_LEVEL additifs jamais atteignables, toutes conditions dynamiques ignorees
/// (recherche non faite, vertex non achete, pouvoir divin non debloque...). Remplace la
/// maintenance manuelle par batiment (l'ancien Building.GetAbsoluteMaxLevel() etait surcharge au
/// cas par cas sur chaque batiment unique) par un calcul unique, couvert par
/// BuildingMaxLevelCalculatorTests.TheoreticalMaxLevel_MatchesSumOfAllBonusSources pour tous les
/// types de batiments, uniques et non-uniques.
///
/// Sources sommees :
///  - Recherche (TechnologyDefinitions.All)
///  - Vertex de prestige (PrestigeMapFactory vertices)
///  - Hexagone de prestige (PrestigeMapFactory hexes : perVertexModifiers x nombre de vertex
///    adjacents, puisque chacun peut etre applique une fois par vertex achete autour de l'hex)
///  - Batiments uniques accordant un bonus a un autre type (Guilde des Marchands -> Marche,
///    Guilde des Artisans -> Forge, Guilde des Recolteurs -> Scierie/Briqueterie/Carriere/Moulin),
///    lus directement via IUniqueBuilding.GetUniqueBuildingModifiers() sur un prototype de niveau 1
///    plutot que dupliques ici.
///  - Pouvoirs divins d'Ascension : un seul aujourd'hui accorde BUILDING_MAX_LEVEL (Foi -> Temple
///    +3, voir AscensionController.GetModifiers()) ; a maintenir a la main si un futur pouvoir en
///    ajoute un autre (couvert par le meme test).
///
///  - Bonus de race (RaceDefinitions) : le MEILLEUR parmi toutes les races pour ce type de
///    batiment, jamais en dessous de 0. Un seul choix de race est actif par partie (mutuellement
///    exclusifs, voir AscensionState.SelectedRace), donc on ne peut jamais cumuler deux bonus de
///    races differentes sur le meme type — mais rien n'oblige non plus a subir le malus d'une race
///    qu'on n'a pas choisie : le plafond theorique retient la race la plus favorable pour CE type
///    de batiment, jamais une combinaison qui ne peut pas exister en jeu. Concretement : le
///    batiment unique racial (Ziggourat -> Humaine +1, Arbre-Coeur -> Elfe +1, etc., chacun
///    disjoint des autres races) retient son unique bonus positif ; un batiment que seule une race
///    penalise (Garuda -1 sur la Tour de Mage/Champignonniere/Scierie/Mine de Mithril/etc., voir
///    RaceDefinitions.GarudaLightBuildings) retient 0, pas -1, puisqu'une autre race l'evite
///    simplement ; un batiment touche par plusieurs races aux effets opposes (Gobelin -1 / Geant
///    +2 sur les memes batiments standards) retient le plus favorable (+2, en jouant Geant). Le
///    plafond reellement atteignable en jeu (qui, lui, depend de la race REELLEMENT choisie) reste
///    calcule par BuildingController.GetMaxLevel a partir des seuls bonus actifs.
///
/// Toutes les sources ci-dessus sont des tables statiques (contenu fixe pour la duree du process) :
/// le resultat est calcule une seule fois par type puis mis en cache, plutot que recalcule a
/// chaque appel — evite de reconstruire PrestigeMapFactory.CreateDefault() et d'instancier tous les
/// types de batiments a chaque rafraichissement du popup de presets (~toutes les 100 ms tant qu'il
/// reste ouvert, voir AutomationPresetPopupViewModel.Refresh).
/// </summary>
public static class BuildingMaxLevelCalculator
{
    private static readonly Lazy<IReadOnlyDictionary<BuildingType, int>> Cache =
        new(ComputeAll);

    public static int GetTheoreticalMaxLevel(BuildingType type) => Cache.Value[type];

    private static IReadOnlyDictionary<BuildingType, int> ComputeAll()
    {
        var prestigeMap = PrestigeMapFactory.CreateDefault();
        var techModifiers = TechnologyDefinitions.All.SelectMany(t => t.Modifiers).ToList();
        var vertexModifiers = prestigeMap.Vertices.SelectMany(v => v.Modifiers).ToList();
        var uniqueBuildingModifiers = GetUniqueBuildingGrantedModifiers();
        var raceBonusBySubCategory = GetBestRaceBonusBySubCategory();

        bool Matches(Modifier m, string subCategory) =>
            m.Category == ECategory.BUILDING_MAX_LEVEL && m.Type == EType.ADDITIVE && m.SubCategory == subCategory;

        var result = new Dictionary<BuildingType, int>();
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            var prototype = BuildingFactory.Create(type);
            if (prototype == null) continue;

            string subCategory = type.ToString();
            int sum = prototype.GetDefaultMaxLevel();

            sum += techModifiers.Where(m => Matches(m, subCategory)).Sum(m => (int)m.Value);
            sum += vertexModifiers.Where(m => Matches(m, subCategory)).Sum(m => (int)m.Value);
            sum += prestigeMap.Hexes.Sum(h =>
                h.PerVertexModifiers.Where(m => Matches(m, subCategory)).Sum(m => (int)m.Value) * h.AdjacentVertices.Count);
            sum += uniqueBuildingModifiers.Where(m => Matches(m, subCategory)).Sum(m => (int)m.Value);
            sum += GetAscensionBonus(subCategory);
            sum += raceBonusBySubCategory.GetValueOrDefault(subCategory);

            result[type] = sum;
        }
        return result;
    }

    /// <summary>Bonus accordes par d'autres batiments uniques une fois construits (Level 1 suffit :
    /// les modificateurs de guilde ne dependent pas du niveau exact, voir HarvestersGuild.cs etc.),
    /// lus directement sur les prototypes plutot que recopies ici.</summary>
    private static List<Modifier> GetUniqueBuildingGrantedModifiers()
    {
        var modifiers = new List<Modifier>();
        foreach (BuildingType type in Enum.GetValues<BuildingType>())
        {
            if (BuildingFactory.Create(type) is not IUniqueBuilding unique) continue;

            ((Building)unique).Level = 1;
            modifiers.AddRange(unique.GetUniqueBuildingModifiers());
        }
        return modifiers;
    }

    /// <summary>Foi (pouvoir divin d'Ascension) accorde Temple +3 — seule source d'Ascension
    /// touchant BUILDING_MAX_LEVEL aujourd'hui (voir AscensionController.GetModifiers()).</summary>
    private static int GetAscensionBonus(string subCategory) =>
        subCategory == nameof(BuildingType.Temple) ? 3 : 0;

    /// <summary>Meilleur bonus racial pour chaque type de batiment, jamais negatif : un seul choix
    /// de race est actif par partie, donc pour un type donne on retient la valeur de la race la
    /// plus favorable (en sommant d'abord les eventuels doublons d'une meme race sur le meme type),
    /// jamais la somme de plusieurs races differentes. Une race qui ne fait que penaliser un type
    /// (aucune autre race ne le touche, ou seulement en negatif aussi) est ignoree — le joueur
    /// choisirait simplement une autre race, jamais soumise a ce malus (voir le commentaire de
    /// classe pour les exemples Garuda et Gobelin/Geant).</summary>
    private static IReadOnlyDictionary<string, int> GetBestRaceBonusBySubCategory()
    {
        var best = new Dictionary<string, int>();
        foreach (var race in RaceDefinitions.All)
        {
            foreach (var group in race.Modifiers
                .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.Type == EType.ADDITIVE)
                .GroupBy(m => m.SubCategory))
            {
                int valueForThisRace = group.Sum(m => (int)m.Value);
                if (!best.TryGetValue(group.Key, out int current) || valueForThisRace > current)
                    best[group.Key] = valueForThisRace;
            }
        }

        foreach (var key in best.Keys.ToList())
            if (best[key] < 0) best[key] = 0;

        return best;
    }
}
