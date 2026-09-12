using System.Reflection;
using SettlersOfIdlestanSkia.Renderers.Overlay.Panels;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrouille l'accord entre la page Automatisation, qui propose d'epingler un automatisme au
/// panneau civilisation, et ce panneau, qui doit savoir le nommer et le basculer.
///
/// Ces deux moities avaient deja diverge : cinq automatismes (hotel de ville, grand temple, mine
/// de mithril, tour des arcanes, investissement monument) etaient epinglables mais s'affichaient
/// sous leur cle brute, et leur bascule ne faisait rien.
/// </summary>
public class AutomationPinKeyTests
{
    /// Cles d'epinglage declarees par la page Automatisation, lues par reflexion pour qu'une
    /// nouvelle constante soit prise en compte sans toucher a ce test.
    public static TheoryData<string> AllPinKeys()
    {
        var data = new TheoryData<string>();
        foreach (var field in typeof(AutomationRenderer).GetFields(
                     BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
        {
            if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
            if (!field.Name.StartsWith("PinKey")) continue;
            if (field.GetRawConstantValue() is string value) data.Add(value);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPinKeys))]
    public void Chaque_automatisme_epinglable_est_nomme_par_le_panneau_civilisation(string pinKey)
    {
        // Les batiments (Caserne, Arsenal...) sont nommes par leur propre cle building_*_name,
        // hors de la table des automatismes : ils sont traites par l'autre branche du resolveur.
        if (IsBuildingPinKey(pinKey)) return;

        Assert.True(
            PlayerCivilizationPanelRenderer.AutomationPinLocalizationRoots.ContainsKey(pinKey),
            $"L'automatisme « {pinKey} » peut etre epingle mais le panneau civilisation ne sait pas "
            + "le nommer : il afficherait la cle brute. Ajouter sa racine de localisation dans "
            + "PlayerCivilizationPanelRenderer.AutomationPinLocalizationRoots.");
    }

    private static bool IsBuildingPinKey(string pinKey) => pinKey is
        "Barracks" or "Arsenal" or "Laboratory" or "Smelter" or
        "WeaponSmith" or "ArmorSmith" or "AlchimistHut" or "DefenseSpire" or "MithrilGreatForge";

    /// Contrairement a AutomationPinLocalizationRoots, PinKeyCategories doit couvrir aussi les
    /// cles de batiment (Barracks...) : le panneau civilisation en a besoin pour styler leur
    /// bascule, meme si leur libelle vient d'ailleurs (building_*_name).
    [Theory]
    [MemberData(nameof(AllPinKeys))]
    public void Chaque_automatisme_epinglable_a_une_famille_pour_le_panneau_civilisation(string pinKey)
    {
        Assert.True(
            AutomationRenderer.PinKeyCategories.ContainsKey(pinKey),
            $"L'automatisme « {pinKey} » peut etre epingle mais n'a pas de famille declaree dans "
            + "AutomationRenderer.PinKeyDisplayOrder : le panneau civilisation ne saurait ni le "
            + "styler ni le classer comme l'ecran d'automatisation.");
    }

    /// <summary>
    /// L'ordre d'affichage etant partage entre l'ecran d'automatisation et les bascules epinglees
    /// du panneau civilisation, il doit rester groupe par famille : la vue du panneau s'appuie sur
    /// ce classement pour styler chaque bascule, et un rang qui entrelacerait deux familles
    /// disperserait les styles au milieu de la liste.
    /// </summary>
    [Fact]
    public void L_ordre_d_affichage_est_groupe_par_famille()
    {
        var seen = new List<AutomationCategory>();
        foreach (var (_, category) in AutomationRenderer.PinKeyDisplayOrder)
        {
            if (seen.Count > 0 && seen[^1] == category) continue;
            Assert.DoesNotContain(category, seen);
            seen.Add(category);
        }
    }

    /// Les huit reglages de routes et d'avant-postes se suivent, chaque groupe classe par palier
    /// de profondeur (surface, Inframonde, Abysse, Pandemonium).
    [Fact]
    public void Les_routes_puis_les_avant_postes_se_suivent_par_palier()
    {
        var keys = AutomationRenderer.PinKeyDisplayOrder.Select(e => e.Key).ToList();
        string[] expected =
        [
            "Road", "RoadUnderworld", "RoadAbyss", "RoadPandemonium",
            "Outpost", "OutpostUnderworld", "OutpostAbyss", "OutpostPandemonium",
        ];

        int start = keys.IndexOf(expected[0]);
        Assert.InRange(start, 0, keys.Count - expected.Length);
        Assert.Equal(expected, keys.Skip(start).Take(expected.Length));
    }
}
