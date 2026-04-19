# Guide de Démarrage - Prochaines Étapes

## 🎯 Objectif de la Phase 2 : Migration du Plateau de Jeu

### Tâche 1 : Examiner la structure de données existante

```csharp
// Explorer ces fichiers dans SettlersOfIdlestan:
SettlersOfIdlestan\src\Model\IslandMap\IslandMap.cs
SettlersOfIdlestan\src\Model\HexGrid\*.cs
SettlersOfIdlestan\src\Model\Civilization\*.cs
```

### Tâche 2 : Créer une instance GameState dans Desktop

```csharp
// Dans MainPage.xaml.cs InitializeGameServices()

// Importer les namespaces du modèle
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

// Créer une vraie GameState à la place de new object()
// Vous pouvez:
// 1. Créer une nouvelle partie (GenerateNewGame)
// 2. Charger une partie sauvegardée
// 3. Utiliser une instance statique/singleton

_gameState = InitializeGame();
```

### Tâche 3 : Implémenter GameBoardRenderer complet

Le renderer doit:

1. **Accéder à l'IslandMap** depuis GameState
```csharp
public override void Render(SKCanvas canvas, GameRenderContext context)
{
    var gameState = context.GameState as MyGameStateClass;
    if (gameState?.Map == null)
        return;

    foreach (var tile in gameState.Map.Tiles.Values)
    {
        // Calculer position pixel
        var pixelPos = AxialToPixel(tile.Coord);

        // Déterminer couleur selon TerrainType
        var color = TerrainTypeToColor(tile.TerrainType);

        // Dessiner hexagone
        DrawHexagon(canvas, pixelPos, color);

        // Afficher ressource si présente
        if (tile.Resource.HasValue)
            DrawResourceIcon(canvas, pixelPos, tile.Resource.Value);
    }
}
```

2. **Convertir les hexagones Blazor en SkiaSharp**
   - Prendre le code de `IslandMapView.razor` (lignes 30-50)
   - Adapter la logique de rendu SVG → SkiaSharp

3. **Implémenter la sélection**
   - Dans InputHandlingService, ajouter un événement HexClicked
   - Dans MainPage.xaml.cs, écouter et mettre en surbrillance

### Tâche 4 : Créer UIRenderer pour les ressources

```csharp
// SettlersOfIdlestanSkia\Renderers\UIRenderer.cs

public class UIRenderer : IGameRenderer
{
    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        // Afficher les ressources du joueur (Wood, Wheat, Ore, etc.)
        // En haut à gauche, ou en bande horizontale

        // Ressources = context.GameState.PlayerCivilization.Resources
        // Afficher chaque ressource comme une capsule (voir CityPanel.razor)
    }
}
```

### Tâche 5 : Tester

```powershell
# Lancer et vérifier:
# - Plateau visible
# - Hexagones s'affichent
# - Ressources s'affichent
# - Pas de crash au clic

dotnet run --project SettlersOfIdlestanDesktop

# Sur Windows: devrait lancer l'app et afficher la grille
```

---

## 📁 Fichiers à Examiner

| Fichier | Raison |
|---------|--------|
| `IslandMapView.razor` | Logique de rendu Blazor à migrer |
| `CityPanel.razor` | Logique d'UI à migrer |
| `MainPage.razor` | Logique de buttons/menus |
| `HexGrid.cs` | Mathématiques des hexagones |
| `IslandMap.cs` | Structure de données principale |
| `Civilization.cs` | État du joueur |

---

## 🎨 Couleurs et Styles

Récupérer depuis `MainLayout.razor.css` et `app.css`:

```css
/* Adapter ces couleurs en SKColor */
--terrain-grass: #90EE90
--terrain-water: #87CEEB
--terrain-mountain: #A9A9A9
/* etc. */
```

---

## ⚙️ Service Utilisé pour Interactivité

L'InputHandlingService a déjà:
- `PointerPressed` event
- `PointerMoved` event
- `PointerReleased` event

**À implémenter:**
- Déterminer quel hexagone est cliqué (hit test)
- Déclencher action métier (sélectionner, récolter, etc.)

---

## 📝 Pseudocode pour HitTest

```csharp
// Dans GameBoardRenderer ou InputService

private HexCoord? GetHexUnderPoint(SKPoint point)
{
    // Parcourir tous les hexagones de la map
    // Calculer leur bounding box
    // Si le point est dedans → retourner les coordonnées

    // Pour un hexagone régulier, on peut utiliser une distance euclidienne
    // ou vérifier les 6 triangles
}

// Dans MainPage.xaml.cs
_inputService.PointerPressed += (sender, args) =>
{
    var hexCoord = _gameState.GetHexUnderPoint(args.Position);
    if (hexCoord != null)
    {
        // Appeler logique de jeu (sélectionner, etc.)
    }
};
```

---

## 🚀 Résumé de la Phase 2

| # | Tâche | Durée est. | Complexité |
|---|-------|-----------|-----------|
| 2.1 | Intégrer GameState | 30 min | ⭐ Faible |
| 2.2 | Renderer Plateau | 2-3h | ⭐⭐⭐ Moyen |
| 2.3 | Renderer UI | 1h | ⭐⭐ Faible |
| 2.4 | Wiring Interactivité | 1-2h | ⭐⭐ Faible |
| 2.5 | Test & Debug | 1h | ⭐ Faible |
| **Total** | **~6-8h** | | |

Bonne chance! 🎮
