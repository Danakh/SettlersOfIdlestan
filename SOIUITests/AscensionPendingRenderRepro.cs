using System.Reflection;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Régression pour l'écran de choix de race différé (voir AscensionController.IsAscensionPending) :
/// pousse une vraie GameScreen dans cet état (essence suffisante, RequestAscension appelé) puis
/// force un Render() réel sur les deux onglets concernés — Ascension, où le joueur reste après
/// confirmation, et Île, où RenderPendingRaceChoicePage prend le relais de la carte. Couvre le bug
/// trouvé lors de la mise en place de cet écran : 9 races sur une grille à hauteur de carte fixe
/// débordaient les unes sur les autres (descriptions trop longues), rendant l'écran illisible sans
/// qu'aucune exception ne soit levée — d'où la vérification pixel en plus du simple Record.Exception.
/// </summary>
public class AscensionPendingRenderRepro
{
    private static GameControllerService GetGameControllerService(GameScreen screen)
    {
        var field = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _gameControllerService introuvable");
        return (GameControllerService)field.GetValue(screen)!;
    }

    [Fact]
    public void Render_WithAscensionPendingRaceChoice_DoesNotThrow_OnAscensionOrIslandTab()
    {
        var fs = new FakeFileSystemService();
        var loc = new LocalizationService();
        var uiLayout = new UILayoutService();
        var resources = new ResourceManager();

        var screen = new GameScreen(fs, loc, uiLayout, resources, saveJson: null, allowDebugMode: true);

        var gcs = GetGameControllerService(screen);
        var mgs = gcs.CurrentGameState!;
        var godState = mgs.GodState;
        var ascension = gcs.MainGameController.AscensionController;

        // Debloque la premiere rangee de pouvoirs divins (choix de race) + de quoi ascensionner.
        godState.GodPoints = 100;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.EyeOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.IsRaceSelectionUnlocked);

        godState.DivineEssence = 10;
        Assert.True(ascension.CanAscend(godState));

        var canvasSize = new SKSize(1280, 800);
        screen.EnsureCanvasInitialized(canvasSize);

        using var surface = SKSurface.Create(new SKImageInfo(1280, 800, SKColorType.Rgba8888, SKAlphaType.Premul));

        // Rendu normal avant Ascension, pour verifier que le harnais lui-meme est sain.
        var ex0 = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(ex0);

        gcs.RequestAscension();
        Assert.True(ascension.IsAscensionPending);
        Assert.Null(godState.PrestigeState);

        // Reste sur l'onglet Ascension (comportement voulu) : rendu repete plusieurs frames.
        Exception? exAscension = null;
        for (int i = 0; i < 5 && exAscension == null; i++)
            exAscension = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(exAscension);

        // Bascule sur l'onglet Ile (Surface) pour choisir la race.
        var exSetTab = Record.Exception(() => screen.SetActiveTabFromHost(SettlersOfIdlestanSkia.Renderers.Overlay.TabBarRenderer.TabIsland));
        Assert.Null(exSetTab);

        Exception? exIsland = null;
        for (int i = 0; i < 5 && exIsland == null; i++)
            exIsland = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(exIsland);

        AssertNotAllOneColor(surface, "onglet Ile (choix de race)");

        // Confirme la race pour s'assurer que la reprise fonctionne aussi.
        gcs.ConfirmAscensionRace(RaceId.Human);
        Assert.False(ascension.IsAscensionPending);
        Assert.NotNull(godState.PrestigeState);

        var exAfterConfirm = Record.Exception(() => screen.Render(surface.Canvas));
        Assert.Null(exAfterConfirm);
    }

    /// <summary>Echoue si le canevas ne contient qu'une seule couleur unie (signe qu'aucun
    /// contenu, texte ou carte, n'a ete dessine par-dessus le fond).</summary>
    private static void AssertNotAllOneColor(SKSurface surface, string label)
    {
        using var bitmap = new SKBitmap(surface.PeekPixels().Info);
        surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);

        var distinctColors = new HashSet<SKColor>();
        for (int y = 0; y < bitmap.Height; y += 4)
            for (int x = 0; x < bitmap.Width; x += 4)
            {
                distinctColors.Add(bitmap.GetPixel(x, y));
                if (distinctColors.Count > 1) break;
            }

        Assert.True(distinctColors.Count > 1, $"{label} : le canevas est entierement uniforme ({distinctColors.FirstOrDefault()}) - rien n'a ete dessine par-dessus le fond.");
    }
}
