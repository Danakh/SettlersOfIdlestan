using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Renderers; // Ajout pour AboutRenderer
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

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

    private SKPaint? _backgroundPaint;
    private SKPaint? _menuItemPaint;
    private SKPaint? _menuItemHoverPaint;
    private SKPaint? _textPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _separatorPaint;
    private SKFont? _textFont;

    private readonly MainGameController _gameController;
    private readonly InputHandlingService _inputService;
    private readonly ILocalizationService _localization;
    private readonly AboutRenderer _aboutRenderer;
    private readonly IFileSystemService _fileSystemService;
    private readonly CityBuildingService _cityBuildingService;
    private List<MenuItem> _menuItems = new();

    private float _gearX;
    private float _barHeight;

    private class MenuItem
    {
        public string LabelKey { get; set; } = "";
        public Action? Action { get; set; }
        public bool IsSeparator { get; set; } = false;
        public bool IsClickable => !IsSeparator && Action != null;
    }

    public bool IsOpen => _isOpen;

    public SettingsMenu(MainGameController gameController, InputHandlingService inputService, ILocalizationService localization, AboutRenderer aboutRenderer, IFileSystemService fileSystemService, CityBuildingService cityBuildingService, bool allowDebugMode = false)
    {
        _gameController = gameController;
        _inputService = inputService;
        _localization = localization;
        _aboutRenderer = aboutRenderer;
        _fileSystemService = fileSystemService;
        _cityBuildingService = cityBuildingService;
        _inputService.PointerPressed += HandlePointerPressed;

        Initialize();

        // Initialise les items du menu
        // Section Langue (avant le séparateur)
        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_language_french",
            Action = () => SetLanguage(Language.French)
        });
        _menuItems.Add(new MenuItem
        {
            LabelKey = "menu_language_english",
            Action = () => SetLanguage(Language.English)
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
                LabelKey = "menu_toggle_debug_mode",
                Action = ToggleDebugMode
            });
            _menuItems.Add(new MenuItem
            {
                LabelKey = "menu_add_resources",
                Action = AddResources
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

        _textFont = new SKFont { Size = 12, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
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

        const float cornerRadius = 4;
        float menuX = gearX - MenuItemWidth + 20;
        float menuY = barHeight + 5;

        // Calcule la hauteur totale du menu
        float totalHeight = 0;
        foreach (var item in _menuItems)
        {
            totalHeight += item.IsSeparator ? SeparatorHeight : MenuItemHeight;
        }

        // Dessine le fond du menu
        var menuRect = new SKRect(menuX, menuY, menuX + MenuItemWidth, menuY + totalHeight);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _borderPaint);

        // Dessine les items du menu
        float currentY = menuY;
        for (int i = 0; i < _menuItems.Count; i++)
        {
            var item = _menuItems[i];
            float itemHeight = item.IsSeparator ? SeparatorHeight : MenuItemHeight;
            var itemRect = new SKRect(menuX, currentY, menuX + MenuItemWidth, currentY + itemHeight);

            if (item.IsSeparator)
            {
                // Dessine le séparateur
                DrawSeparator(canvas, itemRect, item.LabelKey);
            }
            else
            {
                // Fond de l'item (hover ou normal)
                var bgPaint = i == _hoveredItemIndex ? _menuItemHoverPaint : _menuItemPaint;
                canvas.DrawRect(itemRect, bgPaint);

                // Bordure
                using (var itemBorder = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true })
                {
                    canvas.DrawRect(itemRect, itemBorder);
                }

                // Texte
                if (_textFont != null && _textPaint != null)
                {
                    const float padding = 8;
                    float textX = menuX + padding;
                    float textY = currentY + itemHeight / 2 + _textFont.Size / 2;
                    canvas.DrawText(_localization.Get(item.LabelKey), textX, textY, _textFont, _textPaint);
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

            using (var separatorTextPaint = new SKPaint { Color = new SKColor(150, 150, 150, 180), IsAntialias = true })
            {
                canvas.DrawText(text, textX, textY, _textFont, separatorTextPaint);
            }
        }
    }

    public void HandleGearClick()
    {
        ToggleMenu();
    }

    private void HandlePointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left)
            return;

        if (!_isOpen)
            return;

        // Vérifie si le clic est sur la roue crantée pour l'ignorer
        float gearY = (_barHeight - IconSize) / 2;
        var gearRect = new SKRect(_gearX, gearY, _gearX + IconSize, gearY + IconSize);
        if (gearRect.Contains(e.Position.X, e.Position.Y))
        {
            // Ignore le clic sur la roue crantée
            return;
        }

        // Calcule la hauteur totale et les Y positions
        float menuX = _gearX - MenuItemWidth + 20;
        float menuY = _barHeight + 5;
        float totalHeight = 0;
        foreach (var item in _menuItems)
        {
            totalHeight += item.IsSeparator ? SeparatorHeight : MenuItemHeight;
        }

        var menuRect = new SKRect(menuX, menuY, menuX + MenuItemWidth, menuY + totalHeight);

        if (menuRect.Contains(e.Position.X, e.Position.Y))
        {
            // Trouve l'item cliqué
            float currentY = menuY;
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var item = _menuItems[i];
                float itemHeight = item.IsSeparator ? SeparatorHeight : MenuItemHeight;

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

    private void SetLanguage(Language language)
    {
        _localization.SetLanguage(language);
    }

    private void ToggleDebugMode()
    {
        DebugOverlayRenderer.DebugMode = !DebugOverlayRenderer.DebugMode;
    }

    private void ToggleAboutPopUp()
    {
        _aboutRenderer.Show();
    }

    private void AddResources()
    {
        var mainState = _gameController.CurrentMainState;
        if (mainState?.CurrentIslandState?.Civilizations.Count > 0)
        {
            var civilization = mainState.CurrentIslandState.Civilizations[0];
            // Ajoute 100 de chaque ressource
            foreach (var resource in Enum.GetValues(typeof(SettlersOfIdlestan.Model.IslandMap.Resource)).Cast<SettlersOfIdlestan.Model.IslandMap.Resource>())
            {
                civilization.AddResource(resource, 100);
            }
        }
    }

    private void StartNewGame()
    {
        _cityBuildingService.ClearSelectedCity();
        _gameController.CreateNewGame();
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
    }
}
