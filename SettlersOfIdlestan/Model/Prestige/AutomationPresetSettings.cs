using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Model.Prestige;

/// <summary>
/// Plafonds de niveau par bâtiment pour l'automatisation de construction (voir
/// BuildingController.TickGuildAutomation), regroupés en 3 préréglages numérotés que le joueur
/// peut éditer et basculer depuis l'onglet Automatisation une fois TechnologyId.AutomationPreset
/// débloquée. Porté par GodState (et non GameSettings) : donnée cross-run comme AscensionState,
/// jamais recréée par PrestigeController.PerformPrestige ni AscensionController (seul
/// GodState.PrestigeState est remplacé par ces deux contrôleurs).
/// </summary>
public class AutomationPresetSettings
{
    public const int PresetCount = 3;
    public const int MinCap = 0;
    public const int MaxCap = 10;
    public const int DefaultCap = 10;

    public int ActivePreset { get; set; } = 1;

    public Dictionary<BuildingType, int> Preset1Caps { get; set; } = new();
    public Dictionary<BuildingType, int> Preset2Caps { get; set; } = new();
    public Dictionary<BuildingType, int> Preset3Caps { get; set; } = new();

    private Dictionary<BuildingType, int> CapsFor(int preset) => preset switch
    {
        2 => Preset2Caps,
        3 => Preset3Caps,
        _ => Preset1Caps,
    };

    public int GetCap(int preset, BuildingType type) =>
        CapsFor(preset).TryGetValue(type, out var v) ? v : DefaultCap;

    public void SetCap(int preset, BuildingType type, int value) =>
        CapsFor(preset)[type] = Math.Clamp(value, MinCap, MaxCap);

    public int GetActiveCap(BuildingType type) => GetCap(ActivePreset, type);

    public void SetActivePreset(int preset) => ActivePreset = Math.Clamp(preset, 1, PresetCount);

    /// <summary>
    /// Ramène chaque plafond stocké au niveau max théorique courant du bâtiment concerné, s'il le
    /// dépasse (ex. une sauvegarde plus ancienne où ce bâtiment plafonnait plus haut, avant qu'une
    /// recherche/un vertex/hexagone de prestige octroyant BUILDING_MAX_LEVEL n'ait été retiré ou
    /// réduit). Appelée une fois au chargement d'une partie (voir
    /// MainGameController.InitializeControllersForCurrentIsland), pas à chaque lecture : la valeur
    /// stockée doit rester cohérente avec le jeu courant, pas seulement bridée à l'affichage (voir
    /// BuildingMaxLevelCalculator).
    /// </summary>
    public void ClampToTheoreticalMax()
    {
        void ClampCaps(Dictionary<BuildingType, int> caps)
        {
            foreach (var type in caps.Keys.ToList())
            {
                int max = BuildingMaxLevelCalculator.GetTheoreticalMaxLevel(type);
                if (caps[type] > max) caps[type] = max;
            }
        }

        ClampCaps(Preset1Caps);
        ClampCaps(Preset2Caps);
        ClampCaps(Preset3Caps);
    }
}
