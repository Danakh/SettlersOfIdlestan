using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Races;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// État des pouvoirs divins débloqués par le joueur. Persiste cross-prestige (porté par GodState).
/// </summary>
[Serializable]
public class AscensionState
{
    public HashSet<AscensionPowerId> UnlockedPowers { get; set; } = new();

    public bool IsEyeOfGodActive => UnlockedPowers.Contains(AscensionPowerId.EyeOfGod);

    /// <summary>Magie Divine : les sorts (Model.Magic) peuvent accumuler des charges de lancement — voir
    /// MagicController.MaxSpellCharges et MagicState.SpellCharges.</summary>
    public bool IsDivineMagicActive => UnlockedPowers.Contains(AscensionPowerId.DivineMagic);

    /// <summary>Purification Supérieure : la Nécropole récolte les Os Divins sur lesquels elle est
    /// bâtie au lieu de les détruire (voir NecropolisController.PlaceNecropolis).</summary>
    public bool IsGreaterPurificationActive => UnlockedPowers.Contains(AscensionPowerId.GreaterPurification);

    /// <summary>
    /// Purification Supérieure double aussi la capacité du Reliquaire Divin
    /// (Civilization.DivineEssenceKeptOnPrestige) : 2 essences avec le seul Reliquaire Sacré, 4 avec
    /// le Reliquaire Renforcé — voir PrestigeController.GetDivineEssenceLoss et PerformPrestige.
    /// </summary>
    public int ApplyReliquaryCapacityBonus(int baseCapacity) => IsGreaterPurificationActive ? baseCapacity * 2 : baseCapacity;

    /// <summary>
    /// Nombre total d'Ascensions effectuées (cross-prestige, ne diminue jamais). Pilote le nombre
    /// d'emplacements de bâtiments uniques permanents (voir AscensionController.
    /// PermanentUniqueBuildingSlots) : 1 emplacement par Ascension une fois Héritage Divin acquis,
    /// 2 avec Héritage Éternel — aucun sans ces pouvoirs.
    /// </summary>
    public int AscensionsPerformed { get; set; }

    /// <summary>
    /// Bâtiments uniques permanents choisis (jusqu'au nombre d'emplacements ouvert par la colonne
    /// Héritage, voir AscensionController.PermanentUniqueBuildingSlots)
    /// — voir AscensionController.PermanentUniqueBuildingChoices. Appliqués à chaque début d'île
    /// (AscensionController.ApplyPermanentUniqueBuildingToCivilization), sans jamais occuper
    /// d'emplacement dans une ville.
    /// </summary>
    public HashSet<BuildingType> PermanentUniqueBuildings { get; set; } = new();

    /// <summary>
    /// Race jouée pendant le cycle d'Ascension en cours (toutes les îles jusqu'à la prochaine
    /// Ascension). Choisie au moment de l'Ascension une fois la première rangée de pouvoirs divins
    /// complète (voir AscensionController.IsRaceSelectionUnlocked) ; Humains par défaut, y compris
    /// pour les anciennes sauvegardes.
    /// </summary>
    public RaceId SelectedRace { get; set; } = RaceId.Human;

    /// <summary>
    /// Races avec lesquelles au moins une Ascension a été effectuée. Chacune ajoute définitivement
    /// son bâtiment racial aux choix de bâtiment permanent (voir
    /// AscensionController.PermanentUniqueBuildingChoices), quelle que soit la race jouée ensuite.
    /// </summary>
    public HashSet<RaceId> AscendedRaces { get; set; } = new();

    /// <summary>
    /// Meilleur nombre de complétions jamais atteint par chaque recherche répétable, tous cycles
    /// d'Ascension confondus. Alimenté à chaque Ascension depuis le TechnologyTree du cycle qui se
    /// termine (voir AscensionController.RecordBestRepeatCounts) — donc renseigné même pour les
    /// cycles joués avant l'achat de Mémoire de Dieu. C'est le palier auquel ce pouvoir ramène les
    /// recherches répétables, à son achat comme au début de chaque cycle suivant (voir
    /// AscensionController.RestoreRepeatableResearchToBest).
    /// </summary>
    public Dictionary<TechnologyId, int> BestRepeatCounts { get; set; } = new();

    /// <summary>Tick (GameClock) auquel le cycle d'Ascension en cours a débuté — sert à calculer le temps de jeu du cycle.</summary>
    public long CycleStartTick { get; set; }

    /// <summary>Valeur de GameRecord.TotalResearchCompleted au début du cycle en cours — sert à isoler les recherches faites pendant ce cycle.</summary>
    public int CycleStartResearchCompleted { get; set; }

    /// <summary>Historique des 5 derniers cycles d'Ascension terminés (le plus récent en dernier), voir AscensionController.PerformAscension.</summary>
    public List<AscensionRunStats> RunHistory { get; set; } = new();

    /// <summary>
    /// Vrai entre AscensionController.RequestAscension (essence déjà convertie en points, cycle
    /// archivé) et AscensionController.ConfirmAscensionRace (île régénérée avec la race choisie) :
    /// le joueur a demandé l'Ascension mais n'a pas encore choisi la race du prochain cycle. Le jeu
    /// reste en pause tout ce temps (voir MainGameController.RequestAscension/ConfirmAscensionRace),
    /// et la liste des races proposées (AscensionController.GetSelectableRaces) reste réévaluée en
    /// direct : acheter un pouvoir divin pendant l'attente peut donc faire apparaître une nouvelle race.
    /// </summary>
    public bool IsAscensionPending { get; set; }

    /// <summary>Tier d'île maximum jamais atteint dans un cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public int MaxIslandTierReached { get; set; }

    /// <summary>Niveau de corruption maximum jamais atteint dans un cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public int MaxCorruptionReached { get; set; }

    /// <summary>Temps de jeu maximum (ticks) d'un seul cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public long MaxPlaytimeInSingleAscension { get; set; }

    /// <summary>Nombre maximum de recherches complétées en un seul cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public int MaxResearchInSingleAscension { get; set; }

    /// <summary>Points de prestige finaux maximum obtenus en un seul cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public int MaxPrestigePointsInSingleAscension { get; set; }

    /// <summary>Points divins maximum gagnés en un seul cycle d'Ascension (cross-ascension, ne diminue jamais).</summary>
    public int MaxDivinePointsInSingleAscension { get; set; }
}
