using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using SettlersOfIdlestanUI;
using SettlersOfIdlestanUI.Controls;
using SettlersOfIdlestanUI.ViewModels;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

public class CityBuildingRowViewModelTests
{
    private static SkiaLayer.CityBuildingRowSnapshot Row(
        SkiaLayer.CityBuildingAction action = SkiaLayer.CityBuildingAction.Build,
        SkiaLayer.BuildingActivationState activation = SkiaLayer.BuildingActivationState.None) =>
        new("Sawmill", "Scierie", activation, [], action, "Construire", true, false);

    [Theory]
    [InlineData(SkiaLayer.CityBuildingAction.None, false)]
    [InlineData(SkiaLayer.CityBuildingAction.Build, true)]
    [InlineData(SkiaLayer.CityBuildingAction.Upgrade, true)]
    [InlineData(SkiaLayer.CityBuildingAction.MaxLevel, true)]
    [InlineData(SkiaLayer.CityBuildingAction.GoToOtherCity, true)]
    [InlineData(SkiaLayer.CityBuildingAction.PermanentlyGranted, true)]
    public void Le_bouton_n_est_affiche_que_si_une_action_existe(SkiaLayer.CityBuildingAction action, bool expected)
    {
        Assert.Equal(expected, new CityBuildingRowViewModel(Row(action)).HasAction);
    }

    [Theory]
    [InlineData(SkiaLayer.BuildingActivationState.None, false, false)]
    [InlineData(SkiaLayer.BuildingActivationState.Active, true, true)]
    [InlineData(SkiaLayer.BuildingActivationState.Inactive, true, false)]
    public void La_case_d_activation_reflete_l_etat(
        SkiaLayer.BuildingActivationState state, bool hasCheckbox, bool isChecked)
    {
        var row = new CityBuildingRowViewModel(Row(activation: state));
        Assert.Equal(hasCheckbox, row.HasActivation);
        Assert.Equal(isChecked, row.IsActivated);
    }

    [Theory]
    [InlineData(SkiaLayer.CityBuildingAction.None, false, false)]
    [InlineData(SkiaLayer.CityBuildingAction.Build, false, true)]
    [InlineData(SkiaLayer.CityBuildingAction.Upgrade, false, true)]
    [InlineData(SkiaLayer.CityBuildingAction.MaxLevel, true, false)]
    [InlineData(SkiaLayer.CityBuildingAction.GoToOtherCity, false, true)]
    [InlineData(SkiaLayer.CityBuildingAction.PermanentlyGranted, false, false)]
    public void Le_niveau_max_remplace_le_bouton_par_le_badge_dore(
        SkiaLayer.CityBuildingAction action, bool isMaxLevel, bool hasButton)
    {
        var row = new CityBuildingRowViewModel(Row(action));
        Assert.Equal(isMaxLevel, row.IsMaxLevel);
        Assert.Equal(hasButton, row.HasActionButton);
    }

    [Theory]
    [InlineData(SkiaLayer.CityBuildingAction.GoToOtherCity, false)]
    [InlineData(SkiaLayer.CityBuildingAction.PermanentlyGranted, true)]
    [InlineData(SkiaLayer.CityBuildingAction.Build, false)]
    public void Le_batiment_permanent_remplace_la_fleche_par_le_badge_dore(
        SkiaLayer.CityBuildingAction action, bool expected)
    {
        Assert.Equal(expected, new CityBuildingRowViewModel(Row(action)).IsPermanent);
    }

    [Fact]
    public void Changer_d_action_notifie_les_proprietes_derivees()
    {
        var row = new CityBuildingRowViewModel(Row(SkiaLayer.CityBuildingAction.Build));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Apply(Row(SkiaLayer.CityBuildingAction.GoToOtherCity));

        Assert.Contains(nameof(CityBuildingRowViewModel.HasAction), changed);
        Assert.Contains(nameof(CityBuildingRowViewModel.HasActionButton), changed);
        Assert.Contains(nameof(CityBuildingRowViewModel.IsMaxLevel), changed);
        Assert.Contains(nameof(CityBuildingRowViewModel.IsGoToOtherCity), changed);
        Assert.True(row.IsGoToOtherCity);
    }
}

public class CityPanelViewTests
{
    [Fact]
    public void Sans_ville_selectionnee_le_panneau_est_masque()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        var vm = new CityPanelViewModel(host);

        vm.Refresh();

        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Rows);
    }

    /// Le panneau ville etait la principale source de clics qui fuyaient vers la carte : il
    /// devait se declarer dans IsPointBlockedByUI, et ses lignes dans ShouldSuppressInput.
    [AvaloniaFact]
    public void Un_clic_sur_le_panneau_n_atteint_pas_la_carte()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        using var icons = new SvgIconCache();
        var map = new ProbeMapControl();

        var panel = new CityPanelView(new CityPanelViewModel(host), icons) { IsVisible = true };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { map, panel } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bounds = panel.Bounds;
        Assert.True(bounds.Width > 0, "Le panneau occupe zero pixel : le test ne prouverait rien.");
        var inside = new Point(bounds.X + bounds.Width / 2, bounds.Y + 20);

        window.MouseDown(inside, MouseButton.Left);
        window.MouseUp(inside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, map.PointerPressedCount);
    }
}
