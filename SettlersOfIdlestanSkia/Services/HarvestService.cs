using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service gérant les récoltes manuelles sur les hexagones adjacents aux villes.
/// Encapsule la logique de récolte et communique avec le HarvestController.
/// </summary>
public class HarvestService
{
    private readonly GameControllerService _gameControllerService;

    public HarvestService(GameControllerService gameControllerService)
    {
        _gameControllerService = gameControllerService ?? throw new ArgumentNullException(nameof(gameControllerService));
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

        var harvestController = GetHarvestController();
        if (harvestController == null)
            throw new InvalidOperationException("HarvestController n'est pas disponible.");

        try
        {
            return harvestController.ManualHarvest(playerCiv.Index, hexCoord);
        }
        catch (ArgumentException)
        {
            // L'hexagone n'est pas adjacent à une ville ou n'a pas de ressource
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erreur lors de la récolte: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Obtient le HarvestController du jeu via réflexion si nécessaire.
    /// </summary>
    private HarvestController? GetHarvestController()
    {
        // Essaie d'accéder au MainGameController qui expose le HarvestController
        var mainControllerField = _gameControllerService.GetType()
            .GetField("_controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (mainControllerField?.GetValue(_gameControllerService) is MainGameController mainController)
        {
            return mainController.HarvestController;
        }

        return null;
    }
}
