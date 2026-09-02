using System;
using System.Linq;
using SkiaSharp;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Renderers.Island;

namespace SettlersOfIdlestanSkia.Services;

public sealed class MilitaryInteractionService
{
    private const float CitySnapRadius = 20f;
    private const float MonsterSnapRadius = 20f;
    private const float DragThreshold = 8f;

    /// <summary>Clé de localisation du tooltip affiché en survol quand l'attaque à distance 2 manque de Tour de guet.</summary>
    public const string RequiresWatchtowerMessageKey = "tooltip_monster_attack_requires_watchtower";
    /// <summary>Clé de localisation du tooltip affiché en survol quand la cible (ville ou monstre) est hors de portée.</summary>
    public const string TooFarMessageKey = "tooltip_attack_too_far";
    /// <summary>Clé de localisation du tooltip affiché en survol quand la ville alliée ciblée n'a aucune capacité de soldats.</summary>
    public const string NoSoldierCapacityMessageKey = "tooltip_reinforcement_no_capacity";

    private readonly GameControllerService _gameControllerService;
    private readonly MilitaryController _militaryController;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private IslandMainRenderer? _renderer;

    private IMilitaryVertex? _potentialDragCity;
    private SKPoint _potentialDragStartScreen;
    private IMilitaryVertex? _activeDragSourceCity;
    private bool _wasDragging;

    public IMilitaryVertex? DragSourceCity => _activeDragSourceCity;
    public IMilitaryVertex? DragTargetCity { get; private set; }
    public bool DragTargetIsInRange { get; private set; }
    public bool DragTargetHasCapacity { get; private set; } = true;
    public MonsterFeature? DragTargetMonster { get; private set; }
    public MonsterAttackAvailability DragTargetMonsterAvailability { get; private set; }
    public SKPoint DragCurrentScreenPoint { get; private set; }

    /// <summary>True si un press vient d'être posé sur une cité alliée (pour suppression du pan).</summary>
    public bool IsPotentialDragFromCity => _potentialDragCity != null;

    /// <summary>True pendant ou juste après un drag (pour suppression de la sélection de cité).</summary>
    public bool ShouldSuppressConstruction => _activeDragSourceCity != null || _wasDragging;

    /// <summary>Prédicat externe (ex: clic sous un panneau UI) empêchant le déclenchement d'une interaction militaire.</summary>
    public Func<SKPoint, bool>? ShouldSuppressInteraction { get; set; }

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

    private IMilitaryVertex? FindPlayerCityNear(SKPoint islandPoint, int layer)
    {
        var WorldState = _gameControllerService.CurrentWorldState;
        if (WorldState == null || _renderer == null) return null;

        IMilitaryVertex? best = null;
        float bestDist = CitySnapRadius;
        foreach (var vertex in WorldState.PlayerCivilization.MilitaryVertices)
        {
            if (vertex.Position.Z != layer) continue;
            float dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(vertex.Position));
            if (dist < bestDist) { bestDist = dist; best = vertex; }
        }
        return best;
    }

    private IMilitaryVertex? FindAnyCityNear(SKPoint islandPoint, int layer)
    {
        var WorldState = _gameControllerService.CurrentWorldState;
        if (WorldState == null || _renderer == null) return null;

        IMilitaryVertex? best = null;
        float bestDist = CitySnapRadius;
        foreach (var civ in WorldState.Civilizations)
            foreach (var vertex in civ.MilitaryVertices)
            {
                if (vertex.Position.Z != layer) continue;
                float dist = SKPoint.Distance(islandPoint, _renderer.VertexToIslandPoint(vertex.Position));
                if (dist < bestDist) { bestDist = dist; best = vertex; }
            }
        return best;
    }

    private MonsterFeature? FindMonsterNear(SKPoint islandPoint, int layer)
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null || _renderer == null) return null;

        MonsterFeature? best = null;
        float bestDist = MonsterSnapRadius;
        foreach (var monster in worldState.Features.OfType<MonsterFeature>())
        {
            if (!monster.Found) continue;
            if (monster.Position.Z != layer) continue;
            float dist = SKPoint.Distance(islandPoint, _renderer.HexCoordToIslandPoint(monster.Position));
            if (dist < bestDist) { bestDist = dist; best = monster; }
        }
        return best;
    }

    /// <summary>
    /// True si la cible est une ville alliée dont la capacité de soldats effective est nulle. Reflète
    /// la même condition que <see cref="Controller.Military.MilitaryController.SetCityFlow"/> — capacité
    /// des bâtiments <b>plus</b> les bonus civ-wide (CITY_MAX_SOLDIERS_BONUS) : inutile de proposer un
    /// flux que le moteur rejettera silencieusement, mais une ville sans bâtiment militaire reste une
    /// cible valable dès que la civilisation lui donne de la place.
    /// </summary>
    private bool IsAllyTargetWithoutCapacity(IMilitaryVertex target)
    {
        var playerIndex = _gameControllerService.CurrentWorldState?.PlayerCivilization?.Index ?? -1;
        return target.CivilizationIndex == playerIndex && _militaryController.GetMaximumSoldierCapacity(target) == 0;
    }

    private bool IsInRange(IMilitaryVertex source, IMilitaryVertex target)
    {
        var playerCiv = _gameControllerService.CurrentWorldState?.PlayerCivilization;
        if (playerCiv == null) return false;
        int dist = source.Position.EdgeDistanceTo(target.Position);
        bool isAlly = target.CivilizationIndex == playerCiv.Index;
        return isAlly
            ? dist <= _militaryController.ReinforcementRange(playerCiv)
                || _militaryController.HasUnlimitedRangeReinforcementLink(playerCiv, source, target)
            : dist <= _militaryController.CityAttackRange(playerCiv);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;
        if (ShouldSuppressInteraction?.Invoke(e.Position) == true) return;
        _wasDragging = false;
        DragCurrentScreenPoint = e.Position;
        DragTargetCity = null;
        DragTargetIsInRange = false;
        DragTargetHasCapacity = true;
        DragTargetMonster = null;
        DragTargetMonsterAvailability = MonsterAttackAvailability.TooFar;

        var city = FindPlayerCityNear(ScreenToIsland(e.Position), _gameControllerService.CurrentWorldState!.CurrentViewedLayer);
        if ((city != null) && (_militaryController.GetMaximumSoldierCapacity(city) > 0))
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
            var islandPoint = ScreenToIsland(e.Position);
            var target = FindAnyCityNear(islandPoint, _activeDragSourceCity.Position.Z);
            DragTargetCity = target;
            DragTargetIsInRange = target != null && target != _activeDragSourceCity && IsInRange(_activeDragSourceCity, target);
            DragTargetHasCapacity = target == null || !IsAllyTargetWithoutCapacity(target);

            if (target != null && target != _activeDragSourceCity)
            {
                DragTargetMonster = null;
            }
            else
            {
                var monster = FindMonsterNear(islandPoint, _activeDragSourceCity.Position.Z);
                DragTargetMonster = monster;
                DragTargetMonsterAvailability = monster != null
                    ? _militaryController.GetMonsterAttackAvailability(_activeDragSourceCity, monster)
                    : MonsterAttackAvailability.TooFar;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;

        _wasDragging = _activeDragSourceCity != null;

        if (_activeDragSourceCity != null)
        {
            var islandPoint = ScreenToIsland(e.Position);
            var target = FindAnyCityNear(islandPoint, _activeDragSourceCity.Position.Z);

            if (target != null && target != _activeDragSourceCity)
            {
                if (IsInRange(_activeDragSourceCity, target) && !IsAllyTargetWithoutCapacity(target))
                    _militaryController.SetCityFlow(_activeDragSourceCity, target.Position);
                // hors portée ou ville alliée sans capacité de soldats → ne rien faire (conserver le flux existant) ;
                // la raison est déjà visible en tooltip pendant le survol (cf. MilitaryRenderer.DrawDragInteraction).
            }
            else
            {
                var monster = FindMonsterNear(islandPoint, _activeDragSourceCity.Position.Z);
                if (monster == null)
                {
                    _militaryController.SetCityFlow(_activeDragSourceCity, null);
                    _militaryController.SetMonsterFlow(_activeDragSourceCity, null);
                }
                else if (_militaryController.GetMonsterAttackAvailability(_activeDragSourceCity, monster) == MonsterAttackAvailability.Available)
                {
                    _militaryController.SetMonsterFlow(_activeDragSourceCity, monster.Position);
                }
                // bloqué (trop loin / nécessite une tour de guet) → ne rien faire (conserver le flux existant) ;
                // la raison est déjà visible en tooltip pendant le survol (cf. MilitaryRenderer.DrawDragInteraction).
            }
        }

        _activeDragSourceCity = null;
        _potentialDragCity = null;
        DragTargetCity = null;
        DragTargetIsInRange = false;
        DragTargetHasCapacity = true;
        DragTargetMonster = null;
    }

    public void Cleanup()
    {
        _inputService.PointerPressed -= OnPointerPressed;
        _inputService.PointerMoved -= OnPointerMoved;
        _inputService.PointerReleased -= OnPointerReleased;
    }
}
