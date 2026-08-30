using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services.Localization;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrouille l'affichage des modificateurs de la carte de prestige par
/// <c>PrestigeMapRenderer.FormatModifier</c>.
///
/// <para>Rien ne liait <see cref="Modifier.ECategory"/> à cet affichage : une catégorie sans
/// <c>case</c> tombe sur le bras par défaut <c>_ =&gt; $"+{mod.Value}"</c> et s'affiche comme un
/// nombre nu, sans libellé, dans l'infobulle du vertex. Et une clé de localisation manquante ressort
/// telle quelle, <see cref="LocalizationService.Get"/> renvoyant la clé quand elle est absente. Les
/// deux pannes sont muettes en jeu.</para>
///
/// <para>Le balayage part des modificateurs <b>réellement portés par la carte de prestige</b>, et non
/// de l'enum entier : beaucoup de catégories ne proviennent que des recherches ou des races, affichées
/// ailleurs, et un balayage de l'enum les signalerait à tort — tout comme il inventerait des clés à
/// partir d'une SubCategory vide (cas MAGIC_FEATURE_COUNT, dont la clé dépend de la SubCategory). Ici,
/// chaque modificateur testé est exactement celui que le joueur verra, SubCategory comprise.</para>
/// </summary>
public class PrestigeMapRendererFormatModifierTests
{
    /// <summary>
    /// Catégories que l'appelant écarte avant d'atteindre FormatModifier, parce qu'il les rend
    /// lui-même sur une ligne dédiée — voir PrestigeMapRenderer (bâtiments de départ / de nouvelle
    /// ville, recherche débloquée, Potion de Soin).
    /// </summary>
    private static readonly HashSet<Modifier.ECategory> HandledByTheCallerInstead = new()
    {
        Modifier.ECategory.STARTING_CITY_BUILDING,
        Modifier.ECategory.NEW_CITY_BUILDING,
        Modifier.ECategory.UNLOCK_RESEARCH,
        Modifier.ECategory.UNLOCK_HEALING_POTION,
    };

    /// <summary>Tous les modificateurs affichés par les infobulles de la carte de prestige.</summary>
    private static List<Modifier> PrestigeMapModifiers()
    {
        var map = PrestigeMapController.DefaultMap;
        return map.Vertices.SelectMany(v => v.Modifiers)
            .Concat(map.Hexes.SelectMany(h => h.PerVertexModifiers))
            .Where(m => !HandledByTheCallerInstead.Contains(m.Category))
            .ToList();
    }

    public static TheoryData<Language> Languages()
    {
        var data = new TheoryData<Language>();
        foreach (var language in Enum.GetValues<Language>())
            data.Add(language);
        return data;
    }

    [Fact]
    public void TheSweepActuallyCoversTheMap()
    {
        // Garde-fou : sans lui, les deux tests ci-dessous passeraient à vide le jour où la carte
        // deviendrait illisible d'ici.
        Assert.True(PrestigeMapModifiers().Count > 50);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryPrestigeMapModifier_IsFormattedWithALabel(Language language)
    {
        var localization = new LocalizationService();
        localization.SetLanguage(language);

        var unlabelled = new SortedSet<string>();
        foreach (var modifier in PrestigeMapModifiers())
        {
            string text = PrestigeMapRenderer.FormatModifier(modifier, localization);
            // Sortie du bras par défaut du switch, pour la valeur de ce modificateur.
            if (text == $"+{modifier.Value}") unlabelled.Add(modifier.Category.ToString());
        }

        Assert.True(unlabelled.Count == 0,
            "Catégories portées par la carte de prestige mais sans cas dans FormatModifier "
            + "(affichées comme un nombre nu) : " + string.Join(", ", unlabelled));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryPrestigeMapModifier_ResolvesItsLocalizationKeys(Language language)
    {
        var localization = new LocalizationService();
        localization.SetLanguage(language);

        var missing = new SortedSet<string>();
        foreach (var modifier in PrestigeMapModifiers())
        {
            string text = PrestigeMapRenderer.FormatModifier(modifier, localization);

            // LocalizationService.Get renvoie la clé quand la traduction manque : une clé encore
            // visible dans la sortie est donc une traduction absente.
            if (text.Contains("prestige_tooltip_", StringComparison.Ordinal)
                || text.Contains("building_", StringComparison.Ordinal)
                || text.Contains("resource_", StringComparison.Ordinal))
                missing.Add($"{modifier.Category}/{modifier.SubCategory} → \"{text}\"");
        }

        Assert.True(missing.Count == 0,
            $"Clés de localisation absentes en {language} :\n  " + string.Join("\n  ", missing));
    }
}
