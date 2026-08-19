using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Menu déroulant affichant les options de jeu (toggle debug, ajouter des ressources, etc.)
/// </summary>
public class SettingsMenu
{
    private bool _isOpen = false;

    private readonly MainGameController _gameController;
    private readonly LocalizationService _localization;
    private readonly SettingsPopupRenderer _settingsPopupRenderer;
    private readonly IFileSystemService _fileSystemService;
    private readonly CityBuildingService _cityBuildingService;
    private readonly DebugPanelRenderer? _debugPanelRenderer;
    private readonly Action? _onAfterNewGame;
    private readonly Action? _onReturnToMenu;
    private readonly Action? _onRestartIsland;
    private readonly Action<string>? _onLoadGame;
    private List<MenuItem> _menuItems = new();

    private class MenuItem
    {
        public string LabelKey { get; set; } = "";
        public Func<string>? DynamicLabel { get; set; }
        public Action? Action { get; set; }
        public bool IsSeparator { get; set; } = false;
        public bool IsClickable => !IsSeparator && Action != null;
    }

    public bool IsOpen => _isOpen;

    public SettingsMenu(MainGameController gameController, LocalizationService localization, SettingsPopupRenderer settingsPopupRenderer, IFileSystemService fileSystemService, CityBuildingService cityBuildingService, bool allowDebugMode = false, DebugPanelRenderer? debugPanelRenderer = null, Action? onAfterNewGame = null, Action? onReturnToMenu = null, Action? onRestartIsland = null, Action<string>? onLoadGame = null)
    {
        _gameController = gameController;
        _localization = localization;
        _settingsPopupRenderer = settingsPopupRenderer;
        _fileSystemService = fileSystemService;
        _cityBuildingService = cityBuildingService;
        _debugPanelRenderer = debugPanelRenderer;
        _onAfterNewGame = onAfterNewGame;
        _onReturnToMenu = onReturnToMenu;
        _onRestartIsland = onRestartIsland;
        _onLoadGame = onLoadGame;

        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_settings",
            Action = OpenSettingsPopup
        });

        _menuItems.Add(new MenuItem { IsSeparator = true });
        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_save_game",
            Action = SaveGame
        });
        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_load_game",
            Action = LoadGame
        });
        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_restart_island",
            Action = RestartIsland
        });

        _menuItems.Add(new MenuItem { IsSeparator = true });

        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_return_to_menu",
            Action = ReturnToMenu
        });

        if (allowDebugMode)
        {
            _menuItems.Add(new MenuItem { IsSeparator = true });

            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_debug_panel",
                Action = OpenDebugPanel
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_resources",
                Action = AddResources
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_research",
                Action = AddResearchPoints
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_prestige",
                Action = AddPrestigePoints
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_divinity",
                Action = AddDivinityPoints
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_offline_time",
                Action = AddOfflineTime
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_goto_debug_map",
                Action = GoToDebugMap
            });
        }
    }

    /// <summary>Instantane du menu pour une vue portee par l'hote.</summary>
    public SettingsMenuSnapshot GetSnapshot()
    {
        if (!_isOpen) return SettingsMenuSnapshot.Closed;

        var items = _menuItems
            .Select(item => new SettingsMenuItemSnapshot(
                Key: item.LabelKey,
                Label: item.IsSeparator ? item.LabelKey : (item.DynamicLabel?.Invoke() ?? _localization.Get(item.LabelKey)),
                IsSeparator: item.IsSeparator))
            .ToList();

        return new SettingsMenuSnapshot(IsOpen: true, Items: items);
    }

    /// <summary>
    /// Declenche un item depuis une vue portee par l'hote, et referme le menu — comme le fait le
    /// hit-testing Skia. Un separateur n'est pas cliquable et ne referme rien.
    /// </summary>
    public void InvokeItemFromHost(string key)
    {
        var item = _menuItems.FirstOrDefault(i => i.IsClickable && i.LabelKey == key);
        if (item == null) return;

        item.Action?.Invoke();
        Close();
    }

    public void ToggleMenu() => _isOpen = !_isOpen;

    public void Close() => _isOpen = false;

    /// <summary>Ouvre/ferme le menu depuis l'icone d'engrenage portee par l'hote.</summary>
    public void HandleGearClick() => ToggleMenu();

    private void OpenSettingsPopup()
    {
        _settingsPopupRenderer.Open();
    }

    private void RestartIsland()
    {
        _onRestartIsland?.Invoke();
    }

    private void ReturnToMenu()
    {
        _onReturnToMenu?.Invoke();
    }

    private void OpenDebugPanel()
    {
        _debugPanelRenderer?.Open();
    }

    private void AddResources()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.CurrentWorldState?.Civilizations.Count > 0)
        {
            var civilization = mainState.CurrentWorldState.Civilizations[0];
            // Ajoute 100 de chaque ressource
            foreach (var resource in Enum.GetValues(typeof(SettlersOfIdlestan.Model.IslandMap.Resource)).Cast<SettlersOfIdlestan.Model.IslandMap.Resource>())
            {
                civilization.AddResource(resource, 100);
            }
        }
    }

    private void AddResearchPoints()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.PrestigeState != null)
        {
            mainState.PrestigeState.TechnologyTree.ResearchPoints = Math.Min(
                mainState.PrestigeState.TechnologyTree.ResearchPoints + 1000000,
                _gameController.ResearchController.MaxResearchPoints);
        }
    }

    private void AddPrestigePoints()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.PrestigeState != null)
        {
            int amount = Math.Max(100000, mainState.PrestigeState.TotalPrestigePointsEarned);
            mainState.PrestigeState.PrestigePoints += amount;
            mainState.PrestigeState.TotalPrestigePointsEarned += amount;
        }
    }

    private void AddOfflineTime()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.Clock != null)
        {
            mainState.Clock.OfflineBankTicks += 24L * 3600L * 100L;
        }
    }

    private void AddDivinityPoints()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.GodState != null)
        {
            mainState.GodState.GodPoints += 1000;
            mainState.GodState.TotalGodPointsEarned += 1000;
        }
    }

    private void GoToDebugMap()
    {
        _cityBuildingService.ClearSelectedCity();
        _gameController.GoToDebugMap();
        _onAfterNewGame?.Invoke();
    }

    private async void SaveGame()
    {
        var json = _gameController.ExportMainState();
        await _fileSystemService.SaveText("savegame.json", json);
    }

    private async void LoadGame()
    {
        var json = await _fileSystemService.LoadText("savegame.json");
        if (string.IsNullOrEmpty(json)) return;

        // Charger une sauvegarde remplace le WorldState et toutes les civilisations : c'est un
        // changement de monde au même titre qu'un prestige ou un redémarrage d'île, et la couche UI
        // doit faire le même ménage (sélections, abonnements, caches de constructibles décrivant
        // l'ancien monde). C'est le rôle du rappel — l'import direct qui se faisait ici ne
        // prévenait personne, et l'overlay gardait par exemple les routes constructibles de la
        // partie précédente tant qu'aucun compteur du cache de ConstructionInteractionService ne
        // bougeait.
        if (_onLoadGame != null)
            _onLoadGame(json);
        else
            _gameController.ImportMainState(json);
    }
}
