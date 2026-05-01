using SkiaSharp;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Controller;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Menu déroulant affichant les options de jeu (toggle debug, ajouter des ressources, etc.)
/// </summary>
public class SettingsMenu
{
    private const float MenuItemHeight = 35;
    public const float MenuItemWidth = 250;
    private const float IconSize = 20;
    private const float Padding = 12;

    private bool _isOpen = false;
    private int _hoveredItemIndex = -1;

    private SKPaint? _backgroundPaint;
    private SKPaint? _menuItemPaint;
    private SKPaint? _menuItemHoverPaint;
    private SKPaint? _textPaint;
    private SKPaint? _borderPaint;
    private SKFont? _textFont;

    private readonly MainGameController _gameController;
    private readonly InputHandlingService _inputService;
    private List<MenuItem> _menuItems = new();

    private float _gearX;
    private float _barHeight;

    private class MenuItem
    {
        public string Label { get; set; } = "";
        public Action? Action { get; set; }
    }

    public bool IsOpen => _isOpen;

    public SettingsMenu(MainGameController gameController, InputHandlingService inputService)
    {
        _gameController = gameController;
        _inputService = inputService;
        _inputService.PointerPressed += HandlePointerPressed;

        Initialize();

        // Initialise les items du menu
        _menuItems.Add(new MenuItem
        {
            Label = "Toggle Debug Mode",
            Action = ToggleDebugMode
        });
        _menuItems.Add(new MenuItem
        {
            Label = "Add 100 Resources",
            Action = AddResources
        });
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

        // Dessine le fond du menu
        var menuRect = new SKRect(menuX, menuY, menuX + MenuItemWidth, menuY + _menuItems.Count * MenuItemHeight);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(menuRect, cornerRadius, cornerRadius, _borderPaint);

        // Dessine les items du menu
        for (int i = 0; i < _menuItems.Count; i++)
        {
            float itemY = menuY + i * MenuItemHeight;
            var itemRect = new SKRect(menuX, itemY, menuX + MenuItemWidth, itemY + MenuItemHeight);

            // Fond de l'item (hover ou normal)
            var bgPaint = i == _hoveredItemIndex ? _menuItemHoverPaint : _menuItemPaint;
            canvas.DrawRect(itemRect, bgPaint);

            // Bordure
            using (var itemBorder = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true })
            {
                canvas.DrawRect(itemRect, itemBorder);
            }

            // Texte
            var label = _menuItems[i].Label;
            if (_textFont != null && _textPaint != null)
            {
                const float padding = 8;
                float textX = menuX + padding;
                float textY = itemY + MenuItemHeight / 2 + _textFont.Size / 2;
                canvas.DrawText(label, textX, textY, _textFont, _textPaint);
            }
        }
    }

    public void HandleGearClick()
    {
        ToggleMenu();
    }

    private void HandlePointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
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

        // Vérifie si le clic est sur un item du menu
        float menuX = _gearX - MenuItemWidth + 20;
        float menuY = _barHeight + 5;
        var menuRect = new SKRect(menuX, menuY, menuX + MenuItemWidth, menuY + _menuItems.Count * MenuItemHeight);

        if (menuRect.Contains(e.Position.X, e.Position.Y))
        {
            int itemIndex = (int)((e.Position.Y - menuY) / MenuItemHeight);
            if ((itemIndex >= 0) && (itemIndex < _menuItems.Count))
            {
                _menuItems[itemIndex].Action?.Invoke();
                _isOpen = false;
                _hoveredItemIndex = -1;
            }
        }
        else
        {
            // Ferme le menu si on clique ailleurs
            _isOpen = false;
            _hoveredItemIndex = -1;
        }
    }

    private void ToggleDebugMode()
    {
        DebugOverlayRenderer.DebugMode = !DebugOverlayRenderer.DebugMode;
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

    public void Dispose()
    {
        _inputService.PointerPressed -= HandlePointerPressed;
        _backgroundPaint?.Dispose();
        _menuItemPaint?.Dispose();
        _menuItemHoverPaint?.Dispose();
        _textPaint?.Dispose();
        _borderPaint?.Dispose();
        _textFont?.Dispose();
    }
}
