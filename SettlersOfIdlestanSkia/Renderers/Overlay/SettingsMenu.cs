using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Menu déroulant affichant les options de jeu (toggle debug, ajouter des ressources, etc.)
/// </summary>
public class SettingsMenu
{
    private const float MenuItemHeight = 35;
    public const float MenuItemWidth = 250;
    private const float IconSize = PlayerResourcesOverlayRenderer.IconSize;
    private const float Padding = 12;
    private const float SeparatorHeight = 10;

    private bool _isOpen = false;
    private int _hoveredItemIndex = -1;
    private float _lastScale = 0f;

    private SKPaint? _backgroundPaint;
    private SKPaint? _menuItemPaint;
    private SKPaint? _menuItemHoverPaint;
    private SKPaint? _textPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _separatorPaint;
    private SKFont? _textFont;
    private SKPaint? _itemBorderPaint;
    private SKPaint? _separatorTextPaint;

    private readonly MainGameController _gameController;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;
    private readonly AboutRenderer _aboutRenderer;
    private readonly SettingsPopupRenderer _settingsPopupRenderer;
    private readonly IFileSystemService _fileSystemService;
    private readonly CityBuildingService _cityBuildingService;
    private readonly DebugPanelRenderer? _debugPanelRenderer;
    private readonly Action? _onAfterNewGame;
    private readonly UILayoutService? _uiLayout;
    private List<MenuItem> _menuItems = new();

    private float _gearX;
    private float _barHeight;

    private class MenuItem
    {
        public string LabelKey { get; set; } = "";
        public Func<string>? DynamicLabel { get; set; }
        public Action? Action { get; set; }
        public bool IsSeparator { get; set; } = false;
        public bool IsClickable => !IsSeparator && Action != null;
    }

    public bool IsOpen => _isOpen;

    public SettingsMenu(MainGameController gameController, InputHandlingService inputService, LocalizationService localization, AboutRenderer aboutRenderer, SettingsPopupRenderer settingsPopupRenderer, IFileSystemService fileSystemService, CityBuildingService cityBuildingService, bool allowDebugMode = false, DebugPanelRenderer? debugPanelRenderer = null, Action? onAfterNewGame = null, UILayoutService? uiLayout = null)
    {
        _gameController = gameController;
        _inputService = inputService;
        _localization = localization;
        _aboutRenderer = aboutRenderer;
        _settingsPopupRenderer = settingsPopupRenderer;
        _fileSystemService = fileSystemService;
        _cityBuildingService = cityBuildingService;
        _debugPanelRenderer = debugPanelRenderer;
        _onAfterNewGame = onAfterNewGame;
        _uiLayout = uiLayout;
        _inputService.PointerPressed += HandlePointerPressed;

        Initialize();

        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_settings",
            Action = OpenSettingsPopup
        });

        _menuItems.Add(new MenuItem { IsSeparator = true });
        _menuItems.Add(new MenuItem
        {
            LabelKey = "new_game",
            Action = StartNewGame
        });
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

        _menuItems.Add(new MenuItem { IsSeparator = true });

        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_about",
            Action = ToggleAboutPopUp
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
                LabelKey = "menu_add_prestige",
                Action = AddPrestigePoints
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_goto_debug_map",
                Action = GoToDebugMap
            });
        }
    }

    public void Initialize()
    {
        _backgroundPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 220),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _menuItemPaint = new SKPaint
        {
            Color = new SKColor(40, 40, 40, 240),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _menuItemHoverPaint = new SKPaint
        {
            Color = new SKColor(60, 60, 60, 240),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        _borderPaint = new SKPaint
        {
            Color = SKColors.Gold,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _separatorPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 100, 150),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _textFont = new SKFont { Size = 12, Typeface = SkiaFonts.Bold };
        _itemBorderPaint = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        _separatorTextPaint = new SKPaint { Color = new SKColor(150, 150, 150, 180), IsAntialias = true };
    }

    public void ToggleMenu()
    {
        _isOpen = !_isOpen;
        _hoveredItemIndex = -1;
    }

    public void Close()
    {
        _isOpen = false;
        _hoveredItemIndex = -1;
    }

    public void SetGearPosition(float gearX, float barHeight)
    {
        _gearX = gearX;
        _barHeight = barHeight;
    }

    public void Draw(SKCanvas canvas, float gearX, float barHeight)
    {
        _gearX = gearX;
        _barHeight = barHeight;

        if (!_isOpen)
            return;

        float s = _uiLayout?.UiScale ?? 1f;
        if (s != _lastScale)
        {
            _lastScale = s;
            _textFont?.Dispose();
            _textFont = new SKFont { Size = 12 * s, Typeface = SkiaFonts.Bold };
        }

        float menuItemW  = MenuItemWidth * s;
        float menuItemH  = MenuItemHeight * s;
        float separatorH = SeparatorHeight * s;
        float cornerRadius = 4 * s;
        float menuX = gearX - menuItemW + 20 * s;
        float menuY = barHeight + 5 * s;

        // Calcule la hauteur totale du menu
        float totalHeight = 0;
        foreach (var item in _menuItems)
            totalHeight += item.IsSeparator ? separatorH : menuItemH;

        // Dessine le fond du menu
        var menuRect = new SKRect(menuX, menuY, menuX + menuItemW, menuY + totalHeight);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _borderPaint);

        // Dessine les items du menu
        float currentY = menuY;
        for (int i = 0; i < _menuItems.Count; i++)
        {
            var item = _menuItems[i];
            float itemHeight = item.IsSeparator ? separatorH : menuItemH;
            var itemRect = new SKRect(menuX, currentY, menuX + menuItemW, currentY + itemHeight);

            if (item.IsSeparator)
            {
                DrawSeparator(canvas, itemRect, item.LabelKey);
            }
            else
            {
                var bgPaint = i == _hoveredItemIndex ? _menuItemHoverPaint : _menuItemPaint;
                canvas.DrawRect(itemRect, bgPaint);
                canvas.DrawRect(itemRect, _itemBorderPaint);

                if (_textFont != null && _textPaint != null)
                {
                    float textX = menuX + 8 * s;
                    float textY = currentY + itemHeight / 2 + _textFont.Size / 2;
                    string label = item.DynamicLabel?.Invoke() ?? _localization.Get(item.LabelKey);
                    canvas.DrawText(label, textX, textY, _textFont, _textPaint);
                }
            }

            currentY += itemHeight;
        }
    }

    private void DrawSeparator(SKCanvas canvas, SKRect rect, string text)
    {
        // Dessine une ligne pointillée ou un texte centré
        if (_textFont != null && _textPaint != null)
        {
            float textY = rect.MidY + _textFont.Size / 2;
            float textX = rect.Left + (rect.Width - _textFont.MeasureText(text)) / 2;

            canvas.DrawText(text, textX, textY, _textFont, _separatorTextPaint);
        }
    }

    public void HandleGearClick()
    {
        ToggleMenu();
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left)
            return;

        if (!_isOpen)
            return;

        float s = _uiLayout?.UiScale ?? 1f;
        float menuItemW  = MenuItemWidth * s;
        float menuItemH  = MenuItemHeight * s;
        float separatorH = SeparatorHeight * s;

        // Vérifie si le clic est sur la roue crantée pour l'ignorer
        float iconSize = PlayerResourcesOverlayRenderer.IconSize * s;
        float gearY = (_barHeight - iconSize) / 2;
        var gearRect = new SKRect(_gearX, gearY, _gearX + iconSize, gearY + iconSize);
        if (gearRect.Contains(e.Position.X, e.Position.Y))
            return;

        // Calcule la hauteur totale et les Y positions
        float menuX = _gearX - menuItemW + 20 * s;
        float menuY = _barHeight + 5 * s;
        float totalHeight = 0;
        foreach (var item in _menuItems)
            totalHeight += item.IsSeparator ? separatorH : menuItemH;

        var menuRect = new SKRect(menuX, menuY, menuX + menuItemW, menuY + totalHeight);

        if (menuRect.Contains(e.Position.X, e.Position.Y))
        {
            // Trouve l'item cliqué
            float currentY = menuY;
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var item = _menuItems[i];
                float itemHeight = item.IsSeparator ? separatorH : menuItemH;

                if (e.Position.Y >= currentY && e.Position.Y < currentY + itemHeight)
                {
                    if (item.IsClickable)
                    {
                        item.Action?.Invoke();
                        _isOpen = false;
                        _hoveredItemIndex = -1;
                    }
                    break;
                }

                currentY += itemHeight;
            }
        }
        else
        {
            // Ferme le menu si on clique ailleurs
            _isOpen = false;
            _hoveredItemIndex = -1;
        }
    }

    private void OpenSettingsPopup()
    {
        _settingsPopupRenderer.Open();
    }

    private void OpenDebugPanel()
    {
        _debugPanelRenderer?.Open();
    }

    private void ToggleAboutPopUp()
    {
        _aboutRenderer.Show();
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

    private void AddPrestigePoints()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.PrestigeState != null)
        {
            mainState.PrestigeState.PrestigePoints += 1000;
            mainState.PrestigeState.TotalPrestigePointsEarned += 1000;
        }
    }

    private void GoToDebugMap()
    {
        _cityBuildingService.ClearSelectedCity();
        _gameController.GoToDebugMap();
        _onAfterNewGame?.Invoke();
    }

    private void StartNewGame()
    {
        _cityBuildingService.ClearSelectedCity();
        _gameController.CreateNewGame();
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
        if (!string.IsNullOrEmpty(json))
        {
            _gameController.ImportMainState(json);
        }
    }

    public void Dispose()
    {
        _inputService.PointerPressed -= HandlePointerPressed;
        _backgroundPaint?.Dispose();
        _menuItemPaint?.Dispose();
        _menuItemHoverPaint?.Dispose();
        _textPaint?.Dispose();
        _borderPaint?.Dispose();
        _separatorPaint?.Dispose();
        _textFont?.Dispose();
        _itemBorderPaint?.Dispose();
        _separatorTextPaint?.Dispose();
    }
}
