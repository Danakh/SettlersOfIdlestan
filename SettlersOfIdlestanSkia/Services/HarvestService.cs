using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller.Island;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service gérant les récoltes manuelles sur les hexagones adjacents aux villes.
/// Encapsule la logique de récolte et communique avec le HarvestController.
/// </summary>
public class HarvestService
{
    private readonly GameControllerService _gameControllerService;
    private HarvestController _harvestController;

    /// <summary>
    /// Événement déclenché quand une récolte (manuelle ou automatique) est complétée avec succès.
    /// </summary>
    public event EventHandler<HarvestCompletedEventArgs>? OnHarvestCompleted;

    /// <summary>
    /// Événement déclenché quand le Port de pêche génère une ressource aléatoire.
    /// </summary>
    public event EventHandler<MarketGenerationEventArgs>? OnRandomResourceGenerated;

    public HarvestService(GameControllerService gameControllerService)
    {
        _gameControllerService = gameControllerService ?? throw new ArgumentNullException(nameof(gameControllerService));
        _harvestController = _gameControllerService.MainGameController.HarvestController;
        _harvestController.OnHarvestCompleted += OnHarvestControllerCompleted;
        _harvestController.OnRandomResourceGenerated += (s, e) => OnRandomResourceGenerated?.Invoke(s, e);
    }

    /// <summary>
    /// Tente une récolte manuelle sur l'hexagone spécifié pour la civilisation du joueur.
    /// </summary>
    /// <param name="hexCoord">Coordonnées de l'hexagone à récolter</param>
    /// <returns>true si la récolte a réussi, false sinon</returns>
    public bool TryManualHarvest(HexCoord hexCoord)
    {
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null)
            throw new InvalidOperationException("La civilisation du joueur n'est pas disponible.");

        return _harvestController.ManualHarvest(playerCiv.Index, hexCoord);
    }

    /// <summary>
    /// Relaye les événements du HarvestController vers les abonnés du HarvestService.
    /// </summary>
    private void OnHarvestControllerCompleted(object? sender, HarvestCompletedEventArgs e)
    {
        OnHarvestCompleted?.Invoke(this, e);
    }
}
