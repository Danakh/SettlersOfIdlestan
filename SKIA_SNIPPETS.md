// SNIPPETS UTILES POUR LA MIGRATION

// ============================================================
// 1. ENREGISTRER UN NOUVEAU RENDERER
// ============================================================

// Dans MainPage.xaml.cs, dans InitializeGameServices():

_renderService.RegisterRenderer(new GameBoardRenderer());
_renderService.RegisterRenderer(new UIRenderer());       // À créer
_renderService.RegisterRenderer(new CityPanelRenderer()); // À créer

// L'ordre détermine le z-order (premier = back, dernier = front)


// ============================================================
// 2. ACCÉDER AUX DONNÉES DU JEU DEPUIS UN RENDERER
// ============================================================

public void Render(SKCanvas canvas, GameRenderContext context)
{
    // Unsafe cast - assurez-vous que le type est correct
    var gameState = context.GameState as MyGameStateClass;
    if (gameState == null)
        return;

    // Accès aux données
    var map = gameState.Map;
    var player = gameState.PlayerCivilization;
    var cities = player.Cities;
}


// ============================================================
// 3. GÉRER LES ÉVÉNEMENTS D'ENTRÉE
// ============================================================

// Dans MainPage.xaml.cs, dans InitializeGameServices():

_inputService.PointerPressed += (sender, args) =>
{
    var position = args.Position; // SKPoint (x, y)
    var pointerId = args.PointerId; // Pour multi-touch

    // TODO: Implémenter la logique de clic
    // Par exemple: déterminer quel hexagone est cliqué
};

_inputService.PointerMoved += (sender, args) =>
{
    // Utilisé pour le drag, hover, etc.
};

_inputService.PointerReleased += (sender, args) =>
{
    // End de drag
};

_inputService.ZoomChanged += (sender, args) =>
{
    // args.ZoomDelta : facteur de zoom
    // args.Center : point central du zoom
};


// ============================================================
// 4. DESSINER UN HIT TEST (DÉTERMINER CLIC SUR HEXAGONE)
// ============================================================

private HexCoord? GetHexUnderPoint(SKPoint point)
{
    // Pour chaque hexagone:
    foreach (var tile in _gameState.Map.Tiles.Values)
    {
        var hexPos = AxialToPixel(tile.Coord);
        var distance = Distance(point, hexPos);

        // Rayon de l'hexagone
        const float HexRadius = 40f;

        if (distance < HexRadius)
            return tile.Coord;
    }

    return null;
}

private float Distance(SKPoint a, SKPoint b)
{
    var dx = a.X - b.X;
    var dy = a.Y - b.Y;
    return (float)Math.Sqrt(dx * dx + dy * dy);
}


// ============================================================
// 5. UTILISER LE RESOURCE MANAGER
// ============================================================

// Le ResourceManager est injecté dans MainPage et accessible
// via les renderers via context (à ajouter si nécessaire)

// Charger une police:
var typeface = _resourceManager.GetOrCreateTypeface("Arial");

// Charger une image:
var image = _resourceManager.LoadImage("path/to/image.png");

// Créer un paint réutilisable:
var paint = _resourceManager.GetOrCreatePaint("redBrush", SKColors.Red);


// ============================================================
// 6. ANIMATION BASIQUE AVEC DELTATIME
// ============================================================

public void Render(SKCanvas canvas, GameRenderContext context)
{
    // Animer une propriété
    float angle = context.TotalTime * 90; // Rotation de 90° par seconde

    canvas.Save();
    canvas.RotateDegrees(angle);
    // Dessiner...
    canvas.Restore();
}


// ============================================================
// 7. CAMÉRA / ZOOM ET PAN
// ============================================================

// Le zoom/pan est stocké dans context.CameraPosition et context.ZoomLevel
// À utiliser dans les renderers pour transformer les coordonnées:

public void Render(SKCanvas canvas, GameRenderContext context)
{
    // Appliquer la transformation caméra
    canvas.Translate(context.CameraPosition.X, context.CameraPosition.Y);
    canvas.Scale(context.ZoomLevel);

    // Tous les dessins seront alors transformés
}


// ============================================================
// 8. DÉBOGUER - AFFICHER LES COORDONNÉES
// ============================================================

public void Render(SKCanvas canvas, GameRenderContext context)
{
    var debugPaint = new SKPaint
    {
        Color = SKColors.Red,
        TextSize = 12
    };

    canvas.DrawText($"Pos: {context.CameraPosition}", 10, 20, debugPaint);
    canvas.DrawText($"Zoom: {context.ZoomLevel:F2}", 10, 40, debugPaint);
    canvas.DrawText($"FPS: {1f / context.DeltaTime:F1}", 10, 60, debugPaint);

    debugPaint.Dispose();
}


// ============================================================
// 9. CONVERTIR LES ENUMS POUR LES COULEURS
// ============================================================

private SKColor TerrainTypeToColor(TerrainType terrain)
{
    return terrain switch
    {
        TerrainType.Grass => new SKColor(144, 238, 144),      // Light Green
        TerrainType.Water => new SKColor(135, 206, 235),      // Light Blue
        TerrainType.Mountain => new SKColor(169, 169, 169),   // Dark Gray
        TerrainType.Forest => new SKColor(34, 139, 34),       // Forest Green
        TerrainType.Desert => new SKColor(210, 180, 140),     // Tan
        _ => SKColors.White
    };
}


// ============================================================
// 10. CRÉER UN BOUTON CLICKABLE
// ============================================================

private class ButtonArea
{
    public SKRect Bounds { get; set; }
    public Action OnClick { get; set; }
    public string Label { get; set; }
}

private List<ButtonArea> _buttons = new();

public void Render(SKCanvas canvas, GameRenderContext context)
{
    foreach (var button in _buttons)
    {
        // Dessiner le bouton
        var paint = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(button.Bounds, paint);

        // Afficher le label
        var textPaint = new SKPaint { Color = SKColors.White, TextSize = 14 };
        canvas.DrawText(button.Label, button.Bounds.MidX, button.Bounds.MidY, textPaint);

        paint.Dispose();
        textPaint.Dispose();
    }
}

// Dans InputHandlingService:
_inputService.PointerPressed += (sender, args) =>
{
    foreach (var button in _buttons)
    {
        if (button.Bounds.Contains(args.Position.X, args.Position.Y))
        {
            button.OnClick?.Invoke();
        }
    }
};
