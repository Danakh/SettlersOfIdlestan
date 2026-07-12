using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Palette de couleurs partagée pour distinguer les civilisations sur la carte (villes, routes,
/// campements, balises maritimes...). L'index 0 est toujours le joueur (<see cref="Model.IslandMap.WorldState.PlayerCivilization"/>) ;
/// les NPC ne doivent jamais recevoir cette couleur, même quand leur nombre dépasse la taille de la palette.
/// </summary>
public static class CivilizationColorPalette
{
    private static readonly SKColor[] Colors =
    {
        new SKColor(220, 50,  50),  // Rouge   - réservé au joueur (Civ 0)
        new SKColor(60,  100, 220), // Bleu
        new SKColor(50,  180, 50),  // Vert
        new SKColor(230, 180, 0),   // Jaune
        new SKColor(180, 60,  220), // Violet
        new SKColor(220, 130, 40),  // Orange
        new SKColor(0,   190, 190), // Cyan
        new SKColor(220, 100, 160), // Rose
        new SKColor(180, 220, 20),  // Citron vert
        new SKColor(0,   130, 130), // Sarcelle
        new SKColor(150, 90,  40),  // Marron
        new SKColor(130, 10,  10),  // Bordeaux
        new SKColor(120, 120, 0),   // Olive
        new SKColor(20,  20,  120), // Bleu marine
        new SKColor(130, 130, 130), // Gris
    };

    /// <summary>
    /// Retourne la couleur associée à un index de civilisation. L'index 0 (joueur) obtient toujours
    /// la même couleur ; les autres index (NPC) tournent sur le reste de la palette et ne reçoivent
    /// donc jamais la couleur du joueur, même en cas de dépassement.
    /// </summary>
    public static SKColor GetColor(int civilizationIndex)
    {
        if (civilizationIndex <= 0) return Colors[0];
        return Colors[1 + (civilizationIndex - 1) % (Colors.Length - 1)];
    }
}
