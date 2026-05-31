using SkiaSharp;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestanSkia.Renderers.Island;

namespace SettlersOfIdlestanSkia.Services;

public sealed class MilitaryInteractionService
{
    private const float CitySnapRadius = 20f;
    private const float DragThreshold = 8f;

    private readonly GameControllerService _gameControllerService;
    private readonly MilitaryController _militaryController;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private IslandMainRenderer? _renderer;

    private City? _potentialDragCity;
    private SKPoint _potentialDragStartScreen;
    private City? _activeDragSourceCity;
    private bool _wasDragging;

    public City? DragSourceCity => _activeDragSourceCity;
    public City? DragTargetCity { get; private set; }
    public bool DragTargetIsInRange { get; private set; }
    public SKPoint DragCurrentScreenPoint { get; private set; }

    /// <summary>True si un press vient d'être posé sur une cité alliée (pour suppression du pan).</summary>
    public bool IsPotentialDragFromCity => _potentialDragCity != null;

    /// <summary>True pendant ou juste après un drag (pour suppression de la sélection de cité).</summary>
    public bool ShouldSuppressConstruction => _activeDragSourceCity != null || _wasDragging;

    public MilitaryInteractionService(
        GameControllerService gameControllerService,
        MilitaryController militaryController,
        InputHandlingService inputService,
        CameraService cameraService)
    {
        _gameControllerService = gameControllerService;
        _militaryController = militaryController;
        _inputService = inputService;
        _cameraService = cameraService;

        _inputService.PointerPressed += OnPointerPressed;
        _inputService.PointerMoved += OnPointerMoved;
        _inputService.PointerReleased += OnPointerReleased;
    }

    public void AttachRenderer(IslandMainRenderer renderer) => _renderer = renderer;

    private SKPoint ScreenToIsland(SKPoint screen) => new(
        screen.X / _cameraService.ZoomLevel + _cameraService.Position.X,
        screen.Y / _cameraService.ZoomLevel + _cameraService.Position.Y);

    private City? FindPlayerCityNear(SKPoint islandPoint)
    {
        var islandState = _gameControllerService.CurrentIslandState;
        if (islandState == null || _renderer == null) return null;

        City? best = null;
        float bestDist = CitySnapRadius;
        foreach (var city in islandState.PlayerCivilization.Cities)
        {
            float dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(city.Position));
            if (dist < bestDist) { bestDist = dist; best = city; }
        }
        return best;
    }

    private City? FindAnyCityNear(SKPoint islandPoint)
    {
        var islandState = _gameControllerService.CurrentIslandState;
        if (islandState == null || _renderer == null) return null;

        City? best = null;
        float bestDist = CitySnapRadius;
        foreach (var civ in islandState.Civilizations)
            foreach (var city in civ.Cities)
            {
                float dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(city.Position));
                if (dist < bestDist) { bestDist = dist; best = city; }
            }
        return best;
    }

    private bool IsInRange(City source, City target)
    {
        var playerCiv = _gameControllerService.CurrentIslandState?.PlayerCivilization;
        if (playerCiv == null) return false;
        int dist = source.Position.EdgeDistanceTo(target.Position);
        bool isAlly = target.CivilizationIndex == playerCiv.Index;
        return isAlly
            ? dist <= _militaryController.ReinforcementRange(playerCiv)
            : dist <= _militaryController.CityAttackRange(playerCiv);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;
        _wasDragging = false;
        DragCurrentScreenPoint = e.Position;
        DragTargetCity = null;
        DragTargetIsInRange = false;

        var city = FindPlayerCityNear(ScreenToIsland(e.Position));
        if (city != null)
        {
            _potentialDragCity = city;
            _potentialDragStartScreen = e.Position;
        }
        else
        {
            _potentialDragCity = null;
            _activeDragSourceCity = null;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        DragCurrentScreenPoint = e.Position;

        if (_potentialDragCity != null && _activeDragSourceCity == null)
        {
            float dx = e.Position.X - _potentialDragStartScreen.X;
            float dy = e.Position.Y - _potentialDragStartScreen.Y;
            if (dx * dx + dy * dy >= DragThreshold * DragThreshold)
                _activeDragSourceCity = _potentialDragCity;
        }

        if (_activeDragSourceCity != null)
        {
            var target = FindAnyCityNear(ScreenToIsland(e.Position));
            DragTargetCity = target;
            DragTargetIsInRange = target != null && target != _activeDragSourceCity && IsInRange(_activeDragSourceCity, target);
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;

        _wasDragging = _activeDragSourceCity != null;

        if (_activeDragSourceCity != null)
        {
            var target = FindAnyCityNear(ScreenToIsland(e.Position));

            if (target == null || target == _activeDragSourceCity)
            {
                _militaryController.SetCityFlow(_activeDragSourceCity, null);
            }
            else if (IsInRange(_activeDragSourceCity, target))
            {
                _militaryController.SetCityFlow(_activeDragSourceCity, target.Position);
            }
            // hors portée → ne rien faire (conserver le flux existant)
        }

        _activeDragSourceCity = null;
        _potentialDragCity = null;
        DragTargetCity = null;
        DragTargetIsInRange = false;
    }

    public void Cleanup()
    {
        _inputService.PointerPressed -= OnPointerPressed;
        _inputService.PointerMoved -= OnPointerMoved;
        _inputService.PointerReleased -= OnPointerReleased;
    }
}
