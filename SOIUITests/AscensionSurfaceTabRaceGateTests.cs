using System.Reflection;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Screens;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Clic sur l'onglet Surface pendant qu'une Ascension attend son choix de race (voir
/// AscensionController.IsAscensionPending) : l'île du cycle précédent n'existe plus, TabBarRenderer
/// renvoyait donc aussitôt sur l'onglet Ascension et le clic restait sans effet visible. Il ouvre
/// désormais la modale d'AscensionRaceGatePopupRenderer — confirmation du départ sur une nouvelle
/// île, ou avertissement quand aucune race valide n'est sélectionnée.
/// </summary>
public class AscensionSurfaceTabRaceGateTests
{
    private static GameControllerService GetGameControllerService(GameScreen screen)
    {
        var field = typeof(GameScreen).GetField("_gameControllerService", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("champ _gameControllerService introuvable");
        return (GameControllerService)field.GetValue(screen)!;
    }

    private static (GameScreen screen, GameControllerService gcs, SKSurface surface) StartPendingAscension()
    {
        var screen = new GameScreen(new FakeFileSystemService(), new LocalizationService(), new UILayoutService(),
            new ResourceManager(), saveJson: null, allowDebugMode: true);

        var gcs = GetGameControllerService(screen);
        var godState = gcs.CurrentGameState!.GodState;
        var ascension = gcs.MainGameController.AscensionController;

        // Debloque la premiere rangee de pouvoirs divins (choix de race) + de quoi ascensionner.
        godState.GodPoints = 100;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        godState.DivineEssence = 10;

        screen.EnsureCanvasInitialized(new SKSize(1280, 800));
        var surface = SKSurface.Create(new SKImageInfo(1280, 800, SKColorType.Rgba8888, SKAlphaType.Premul));
        screen.Render(surface.Canvas);

        gcs.RequestAscension();
        Assert.True(ascension.IsAscensionPending);

        return (screen, gcs, surface);
    }

    [Fact]
    public void ClickingSurfaceTab_WithSelectedRace_ConfirmsBeforeStartingTheNewGame()
    {
        var (screen, gcs, surface) = StartPendingAscension();
        using (surface)
        {
            // La page Ascension preselectionne la race courante des sa premiere image (voir
            // AscensionRenderer.RenderAscensionPage) : c'est l'etat dans lequel le joueur clique.
            screen.Render(surface.Canvas);

            screen.SetActiveTabFromHost(TabBarRenderer.TabIsland);

            var popup = screen.GetModalPopupSnapshot();
            Assert.True(popup.IsOpen);
            Assert.Equal(ModalPopupSnapshot.IdAscensionRaceConfirm, popup.Id);

            // Rien n'est lance tant que la confirmation n'est pas validee.
            Assert.True(gcs.MainGameController.AscensionController.IsAscensionPending);

            var confirm = Assert.Single(popup.Buttons, b => b.Tone == ModalPopupButtonTone.Confirm);
            screen.InvokeModalPopupButtonFromHost(popup.Id, confirm.Key);

            Assert.False(gcs.MainGameController.AscensionController.IsAscensionPending);
            Assert.NotNull(gcs.CurrentGameState!.GodState.PrestigeState);
            Assert.False(screen.GetModalPopupSnapshot().IsOpen);
        }
    }

    [Fact]
    public void ClickingSurfaceTab_CancelledConfirmation_LeavesTheAscensionPending()
    {
        var (screen, gcs, surface) = StartPendingAscension();
        using (surface)
        {
            screen.Render(surface.Canvas);
            screen.SetActiveTabFromHost(TabBarRenderer.TabIsland);

            var popup = screen.GetModalPopupSnapshot();
            Assert.Equal(ModalPopupSnapshot.IdAscensionRaceConfirm, popup.Id);

            var cancel = Assert.Single(popup.Buttons, b => b.Tone == ModalPopupButtonTone.Neutral);
            screen.InvokeModalPopupButtonFromHost(popup.Id, cancel.Key);

            Assert.False(screen.GetModalPopupSnapshot().IsOpen);
            Assert.True(gcs.MainGameController.AscensionController.IsAscensionPending);

            // L'onglet Ascension reste actif : le clic n'a jamais bascule sur la Surface.
            var tabs = screen.GetTabBarSnapshot();
            Assert.Contains(tabs.Tabs, t => t.TabId == TabBarRenderer.TabAscension && t.IsActive);
        }
    }

    [Fact]
    public void ClickingSurfaceTab_WithoutSelectedRace_WarnsInsteadOfStartingTheNewGame()
    {
        var (screen, gcs, surface) = StartPendingAscension();
        using (surface)
        {
            // Aucune image de la page Ascension depuis la demande : rien n'est encore preselectionne
            // (voir AscensionRenderer._pendingSelectedRace).
            screen.SetActiveTabFromHost(TabBarRenderer.TabIsland);

            var popup = screen.GetModalPopupSnapshot();
            Assert.True(popup.IsOpen);
            Assert.Equal(ModalPopupSnapshot.IdAscensionRaceRequired, popup.Id);

            var ok = Assert.Single(popup.Buttons);
            screen.InvokeModalPopupButtonFromHost(popup.Id, ok.Key);

            Assert.False(screen.GetModalPopupSnapshot().IsOpen);
            Assert.True(gcs.MainGameController.AscensionController.IsAscensionPending);
        }
    }
}
