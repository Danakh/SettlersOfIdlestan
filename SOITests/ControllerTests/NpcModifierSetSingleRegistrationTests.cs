using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Verrouille l'invariant « une civilisation PNJ porte exactement un jeu de modificateurs, et le
/// meme avant et apres un rechargement ».
///
/// <para>Il etait rompu des deux cotes. Le placeur installait son jeu pendant la generation, puis
/// <c>MainGameController.SetupModifierAggregators</c> en <i>ajoutait</i> un second a chaque
/// <c>SetGame</c> — <c>ModifierAggregator.Register</c> ne dedoublonne que par instance, et ce sont
/// deux instances distinctes. Le doublon s'appliquait qui plus est <i>apres</i> les malus de recolte,
/// donc sans etre reduit par eux. Et au rechargement, seul le second etait reconstruit : les malus
/// d'agressivite et de tier ainsi que les routes maritimes, poses par le placeur uniquement,
/// disparaissaient purement et simplement.</para>
///
/// <para>Mesure sur un PNJ Pacifiste de tier 1 : vitesse de recolte ×0,50 a la generation, ×1,20
/// apres un simple rechargement, pour ×0,30 voulus (1,2 × 0,5 × 0,5) — soit un PNJ quatre fois trop
/// rapide une fois la partie rouverte, et sans routes maritimes.</para>
/// </summary>
public class NpcModifierSetSingleRegistrationTests
{
    private static IslandParameters Parameters(NpcAggressivityLevel aggressivity) => new(
        worldId: 0,
        tileData: new[]
        {
            (TerrainType.Forest, 11), (TerrainType.Hill, 11),
            (TerrainType.Plain, 11), (TerrainType.Mountain, 11),
        },
        shapeType: IslandShapeType.Compact)
    {
        NpcCivilizations = new List<NpcParameters>
        {
            new() { EvolutionLevel = NpcEvolutionLevel.Low, AggressivityLevel = aggressivity },
        },
    };

    /// <summary>Providers propres a la civilisation, hors les deux que son constructeur enregistre toujours.</summary>
    private static List<IModifierProvider> CustomProviders(Civilization civ) =>
        civ.ModifierAggregator.RegisteredProviders
            .Where(p => p is not TechnologyTree && p is not UniqueBuildingsModifierProvider)
            .ToList();

    private static double HarvestSpeed(Civilization civ) =>
        civ.ModifierAggregator.ApplyModifiers(Modifier.ECategory.HARVEST_SPEED, "", 1.0);

    [Fact]
    public void GeneratedNpc_CarriesExactlyOneModifierSet()
    {
        var controller = new MainGameController();
        var mainState = controller.CreateNewGame(Parameters(NpcAggressivityLevel.Pacifist), prngSeed: 42);
        Assert.NotNull(mainState);

        var npc = mainState!.CurrentWorldState!.Civilizations.First(c => c.IsNpc);

        Assert.Single(CustomProviders(npc));
    }

    [Fact]
    public void NpcModifiers_AreIdentical_AfterGenerationAndAfterReload()
    {
        var controller = new MainGameController();
        var mainState = controller.CreateNewGame(Parameters(NpcAggressivityLevel.Pacifist), prngSeed: 42);
        Assert.NotNull(mainState);

        var generated = mainState!.CurrentWorldState!.Civilizations.First(c => c.IsNpc);
        double generatedHarvest = HarvestSpeed(generated);
        bool generatedMaritime = generated.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES);

        // ImportMainState enchaine deja sur SetGameFromSave : c'est le vrai chemin de rechargement.
        var reloadedController = new MainGameController();
        reloadedController.ImportMainState(controller.ExportMainState());
        var reloaded = reloadedController.CurrentMainState!.CurrentWorldState!.Civilizations.First(c => c.IsNpc);

        Assert.Single(CustomProviders(reloaded));
        Assert.Equal(generatedHarvest, HarvestSpeed(reloaded), precision: 10);
        Assert.Equal(generatedMaritime,
            reloaded.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES));
    }

    /// <summary>
    /// Les deux malus de recolte (agressivite, tier) et les routes maritimes ne vivaient que dans le
    /// provider pose par le placeur, jamais persiste : ce test echoue si le rechargement cesse de les
    /// reconstruire.
    /// </summary>
    [Fact]
    public void ReloadedNpc_KeepsItsHarvestMalusesAndMaritimeRoutes()
    {
        var controller = new MainGameController();
        Assert.NotNull(controller.CreateNewGame(Parameters(NpcAggressivityLevel.Pacifist), prngSeed: 42));

        var reloadedController = new MainGameController();
        reloadedController.ImportMainState(controller.ExportMainState());
        var reloaded = reloadedController.CurrentMainState!.CurrentWorldState!.Civilizations.First(c => c.IsNpc);

        // Sans malus, le jeu de tier est purement additif et laisse la vitesse au-dessus de 1.
        var withoutMaluses = new Civilization { Index = 99, IsNpc = true };
        withoutMaluses.AddCustomAggregator(NpcModifierSetMaker.Create(maxTechTier: 2, maxPrestigeDistance: 1));
        Assert.True(HarvestSpeed(withoutMaluses) > 1.0, "le jeu de tier seul doit accelerer la recolte");

        Assert.True(HarvestSpeed(reloaded) < HarvestSpeed(withoutMaluses),
            $"malus de recolte perdus au rechargement : {HarvestSpeed(reloaded)} au lieu d'etre sous "
            + $"{HarvestSpeed(withoutMaluses)}");
        Assert.True(reloaded.ModifierAggregator.HasModifier(Modifier.ECategory.UNLOCK_MARITIME_ROUTES),
            "routes maritimes perdues au rechargement");
    }

    [Fact]
    public void SetNpcModifiers_ReplacesThepreviousSetInsteadOfStacking()
    {
        var civ = new Civilization { Index = 99, IsNpc = true };

        civ.SetNpcModifiers(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.HARVEST_SPEED, Modifier.EType.ADDITIVE, 0.5),
        }));
        civ.SetNpcModifiers(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.HARVEST_SPEED, Modifier.EType.ADDITIVE, 0.5),
        }));

        Assert.Single(CustomProviders(civ));
        Assert.Equal(1.5, HarvestSpeed(civ), precision: 10);
    }
}
