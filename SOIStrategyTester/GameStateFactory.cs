using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;

namespace SOIStrategyTester;

/// <summary>
/// Builds a fresh MainGameController for a strategy run, either from a save file (same encrypted/JSON
/// format produced by MainGameController.ExportMainState, as found under the repo's saves/ folder) or
/// from a brand new game. Strategies are compared by re-running each one from an identical starting
/// state, so a new controller must be built for every run rather than reusing a mutated one.
/// </summary>
public static class GameStateFactory
{
    public static MainGameController FromSaveFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Save file not found: {path}", path);

        var controller = new MainGameController();
        controller.ImportMainState(File.ReadAllText(path));
        return controller;
    }

    public static MainGameController NewGame(int? worldId, int? prngSeed)
    {
        var controller = new MainGameController();
        var atlas = controller.AtlasController;
        var resolvedWorldId = worldId ?? atlas.GetFirstWorldId();
        var parameters = atlas.GetIslandParameters(resolvedWorldId);

        if (controller.CreateNewGame(parameters, prngSeed) == null)
            throw new InvalidOperationException($"Failed to generate a new game for world id {resolvedWorldId}.");

        return controller;
    }

    /// <summary>
    /// A brand-new game played by <paramref name="race"/>, on the state a player would actually be in
    /// the first time they can pick it: the divine powers its tier requires are unlocked, and the game
    /// starts on the first island of a fresh Ascension cycle.
    ///
    /// <para>Getting there through the real <c>MainGameController.PerformAscension</c> rather than by
    /// poking <c>AscensionState.SelectedRace</c> is deliberate — it is the only path that regenerates
    /// the island <i>for that race</i> (start terrain, Underworld start for the Dark Elves) and grants
    /// the free prestige vertices that come with Faith and with race selection being unlocked (central
    /// vertex + its 3 neighbours, plus RaceDefinition.FreePrestigeVertices). Skipping it would measure
    /// a race on a map and a prestige map it never actually starts from.</para>
    ///
    /// <para>Which island that is, is the Ascension's own answer and is deliberately not overridden:
    /// the milestones skip the opening of the archipelago (see
    /// AscensionController.AscensionSkippedIslandCount), so a race is measured from where a player
    /// actually restarts, and the gauntlet plays from there up to its <c>--last-island</c>. With the
    /// per-tier meta-progression below that means island 4 for a Base race and island 5 for an
    /// Advanced one. Read it back from <c>CurrentWorldState.WorldId</c> rather than assuming a
    /// number.</para>
    ///
    /// <para>The powers are granted rather than purchased: god points buy nothing else, so the
    /// bookkeeping would change no gameplay. The essence spent to trigger the Ascension is the
    /// controller's own minimum.</para>
    ///
    /// <para><b>Méta-progression par palier de race</b>, celle d'un joueur qui vient d'en débloquer
    /// une de ce palier :</para>
    /// <list type="bullet">
    /// <item>Race de base — tout le premier cercle de pouvoirs divins (voir <see cref="RequiredPowersFor"/>),
    /// les <b>2 premiers jalons</b> (une seule race déjà ascensionnée : les Humains du cycle qui
    /// vient de se terminer), et les 4 bâtiments uniques permanents de
    /// <see cref="BaseTierPermanentUniqueBuildings"/>.</item>
    /// <item>Race avancée — les deux premiers cercles de pouvoirs, les <b>4 jalons</b> (les 5 races
    /// de base déjà ascensionnées, donc 3 races minimum largement dépassées) et <b>tous</b> les
    /// bâtiments uniques permanents proposés : les communs, plus les bâtiments raciaux de ces 5
    /// races de base (voir AscensionController.PermanentUniqueBuildingChoices).</item>
    /// </list>
    /// <para>Les jalons décidant de l'île de départ, ce palier fixe aussi l'île sur laquelle la race
    /// commence — île 4 pour une race de base, île 5 pour une avancée (voir
    /// AscensionController.AscensionSkippedIslandCount). Le nombre d'Ascensions déjà accomplies n'est
    /// pas un réglage en soi : il est déduit du nombre de bâtiments permanents à ouvrir (2
    /// emplacements par Ascension, voir AscensionController.PermanentUniqueBuildingSlots), et c'est
    /// l'unique raison pour laquelle il ne vaut pas 1.</para>
    /// </summary>
    public static MainGameController NewGameForRace(RaceId race, int? prngSeed)
    {
        var controller = NewGame(worldId: null, prngSeed);
        var godState = controller.CurrentMainState!.GodState;
        var ascensionState = godState.AscensionState;
        bool advanced = RaceDefinitions.Get(race).Tier == RaceTier.Advanced;

        foreach (var power in RequiredPowersFor(RaceDefinitions.Get(race).Tier))
            ascensionState.UnlockedPowers.Add(power);

        // Races déjà ascensionnées : ce sont elles qui débloquent les jalons (voir
        // AscensionMilestoneDefinitions) et qui ajoutent leur bâtiment racial aux choix permanents.
        // Une race de base n'en a besoin d'aucune : l'Ascension déclenchée plus bas y verse les
        // Humains du cycle en cours (voir AscensionController.CreditAscensionPointsAndArchiveCycle),
        // ce qui suffit au 2ᵉ jalon. Une race avancée les a toutes les cinq.
        if (advanced)
            foreach (var baseRace in BaseRaces)
                ascensionState.AscendedRaces.Add(baseRace);

        // Choix permanents visés — pour une race avancée, tout ce qui est proposé une fois les races
        // de base ascensionnées (12 communs + leurs 5 bâtiments raciaux).
        var permanentBuildings = advanced
            ? controller.AscensionController.PermanentUniqueBuildingChoices.ToList()
            : BaseTierPermanentUniqueBuildings.ToList();

        // Assez d'Ascensions pour ouvrir un emplacement par bâtiment visé, l'Ascension déclenchée
        // juste après en comptant pour une. Au minimum 1 : c'est ce qui fait entrer les Humains dans
        // AscendedRaces (le tout premier cycle n'y compte pas) et donc le 2ᵉ jalon.
        ascensionState.AscensionsPerformed = Math.Max(1, (permanentBuildings.Count + 1) / 2 - 1);

        godState.DivineEssence = AscensionController.MinDivineEssenceForAscension;

        // Les pouvoirs sont accordés plutôt qu'achetés, mais un joueur les aurait payés : 13 points
        // divins pour le premier cercle, 43 pour les deux (voir AscensionPowerDefinitions). Le total
        // *gagné* compte quand même, et pas qu'en comptabilité — c'est lui que le jalon Ascension
        // Prestigieuse convertit 1 pour 1 en points de prestige au début du cycle (voir
        // AscensionController.GrantPrestigiousAscensionPoints), et les deux paliers ont ce jalon.
        // Sans lui la manche démarrait la bourse à 5 points, celle de l'essence dépensée ici.
        // L'Ascension ajoute son propre gain au total juste avant de verser la dotation, d'où la
        // soustraction : le cycle commence avec exactement GrantedGodPointsEarned points de prestige.
        // GodPoints (la caisse) reste à 0 : ces points-là sont réputés dépensés dans les pouvoirs.
        godState.TotalGodPointsEarned = Math.Max(0,
            GrantedGodPointsEarned - controller.AscensionController.GetGodPointsGain(godState));

        controller.PerformAscension(race);

        // Après l'Ascension : les emplacements se comptent sur AscensionsPerformed, qu'elle vient
        // d'incrémenter, et ApplyPermanentUniqueBuildingToCivilization doit être rappelée — celle de
        // l'initialisation d'île a tourné avant que quoi que ce soit ne soit choisi.
        foreach (var building in permanentBuildings)
            if (!controller.AscensionController.SelectPermanentUniqueBuilding(building))
                throw new InvalidOperationException(
                    $"Bâtiment unique permanent {building} refusé pour {race} : " +
                    $"{controller.AscensionController.PermanentUniqueBuildings.Count}/" +
                    $"{controller.AscensionController.PermanentUniqueBuildingSlots} emplacements utilisés.");
        controller.AscensionController.ApplyPermanentUniqueBuildingToCivilization();

        return controller;
    }

    /// <summary>
    /// Bâtiments uniques permanents d'une race de base : Guilde des bâtisseurs, Guilde des
    /// récolteurs, Académie et Salle de guerre — l'automatisation des routes, de la récolte, de la
    /// recherche et du militaire, soit ce qu'un joueur choisit en premier. Volontairement sans Port
    /// Impérial : c'est la condition de prestige, l'offrir retirerait à la manche ce qu'elle mesure
    /// en premier. Une race avancée, elle, prend tout (voir <see cref="NewGameForRace"/>).
    /// </summary>
    private static readonly IReadOnlyList<BuildingType> BaseTierPermanentUniqueBuildings = new[]
    {
        BuildingType.BuildersGuild,
        BuildingType.HarvestersGuild,
        BuildingType.Academy,
        BuildingType.WarRoom,
    };

    /// <summary>
    /// Points divins réputés gagnés avant le cycle, donc points de prestige en caisse à son début
    /// (jalon Ascension Prestigieuse, 1 pour 1). 50 : de quoi avoir payé les 43 points des deux
    /// premiers cercles de pouvoirs, avec la petite marge d'un joueur réel — voir
    /// <see cref="NewGameForRace"/>.
    /// </summary>
    private const int GrantedGodPointsEarned = 50;

    /// <summary>Les 5 races de base (Humains compris) — celles qu'une race avancée a déjà toutes
    /// ascensionnées dans <see cref="NewGameForRace"/>.</summary>
    private static readonly IReadOnlyList<RaceId> BaseRaces =
        RaceDefinitions.All.Where(r => r.Tier == RaceTier.Base).Select(r => r.Id).ToList();

    /// <summary>
    /// Variante « ascensionnée » de <see cref="NewGameForRace"/> : la partie démarre après
    /// <paramref name="ascensions"/> Ascensions, avec <b>tous</b> les pouvoirs divins achetés, toutes
    /// les races déjà ascensionnées, et tous les bâtiments uniques permanents que les emplacements
    /// d'Héritage autorisent.
    ///
    /// <para>C'est un autre joueur que celui de <see cref="NewGameForRace"/>, pas le même en mieux :
    /// Poing de Dieu frappe un monstre pour 100 à travers son armure, Bras de Dieu double les dégâts
    /// d'un soldat, Courroux de Dieu double la cadence d'attaque et Inventaire Divin décuple le
    /// stockage. Aucun de ces leviers n'existe sans Ascension — d'où deux manches Pandémonium
    /// distinctes plutôt qu'un réglage de difficulté (voir PandemoniumRunner).</para>
    ///
    /// <para>Tout passe par les vrais chemins : les pouvoirs sont achetés par
    /// <see cref="AscensionController.PurchasePower"/> dans l'ordre des colonnes, l'Ascension finale
    /// est la vraie (elle régénère l'île de départ du cycle pour la race — toutes les races étant déjà
    /// ascensionnées ici, les jalons la fixent à l'île 5, la première île de fin de partie, celle-là
    /// même que la manche vise par défaut —, incrémente le compteur d'Ascensions et verse
    /// la dotation d'Ascension Prestigieuse), et les bâtiments permanents passent par
    /// <see cref="AscensionController.SelectPermanentUniqueBuilding"/>, qui refuse au-delà des
    /// emplacements réellement ouverts.</para>
    /// </summary>
    /// <param name="ascensions">Nombre d'Ascensions accomplies au départ — l'Ascension finale comprise.</param>
    /// <param name="godPoints">Points divins laissés en caisse une fois tous les pouvoirs achetés.</param>
    /// <param name="prestigePoints">Points de prestige <b>versés artificiellement</b> en caisse après
    /// l'Ascension : c'est ce qui finance les pouvoirs divins ciblés (Poing de Dieu, Présence de
    /// Dieu), dont le coût double à chaque usage. Voir le corps de la méthode pour pourquoi ils sont
    /// posés à la main plutôt que gagnés par le jalon Ascension Prestigieuse.</param>
    public static MainGameController NewGameForAscendedRace(RaceId race, int? prngSeed,
        int ascensions, int godPoints, int prestigePoints)
    {
        if (ascensions < 1)
            throw new ArgumentOutOfRangeException(nameof(ascensions), "Au moins une Ascension est nécessaire.");

        var controller = NewGame(worldId: null, prngSeed);
        var godState = controller.CurrentMainState!.GodState;
        var ascensionState = godState.AscensionState;

        // L'Ascension finale en ajoutera une : le compteur demandé est celui d'après.
        ascensionState.AscensionsPerformed = ascensions - 1;

        // Toutes les races déjà ascensionnées — c'est ce qui rend leur bâtiment racial choisissable
        // en permanent, quelle que soit la race jouée ensuite (voir PermanentUniqueBuildingChoices).
        foreach (var definition in RaceDefinitions.All.Where(r => r.IsImplemented))
            ascensionState.AscendedRaces.Add(definition.Id);

        PurchaseEveryAscensionPower(controller, godState);

        godState.DivineEssence = AscensionController.MinDivineEssenceForAscension;
        controller.PerformAscension(race);

        // Caisse de prestige posée à la main, et c'est un écart assumé de plus (au même titre que les
        // niveaux de bâtiments, voir EndGameStateFactory). Le chemin réel serait le jalon Ascension
        // Prestigieuse, mais il verse 1 point par point divin *gagné* (GodState.TotalGodPointsEarned —
        // voir AscensionController.GrantPrestigiousAscensionPoints), et cette fabrique n'en gagne
        // aucun : elle gonfle GodPoints directement pour acheter les pouvoirs. Une partie réelle de
        // vingt Ascensions en aurait des milliers ; sans ce versement la manche ascensionnée démarrait
        // avec 90 points, de quoi frapper sept fois, ce qui ne mesurait plus le contenu mais la
        // fabrique. Ajouté, jamais écrasé : la dotation du jalon reste acquise.
        //
        // Volontairement absent de TotalPrestigePointsEarned : c'est lui qui fixe PrestigeState.Tier,
        // donc le niveau du dieu démon (voir EndGameStateFactory.PurchaseEveryPrestigeVertex). Le
        // gonfler ici durcirait le boss d'autant, et les manches ne seraient plus comparables entre
        // deux valeurs de --prestige-points.
        var prestigeState = godState.PrestigeState;
        if (prestigeState != null)
            prestigeState.PrestigePoints += prestigePoints;

        // Après l'Ascension : les emplacements d'Héritage se comptent sur AscensionsPerformed, qu'elle
        // vient d'incrémenter, et ApplyPermanentUniqueBuildingToCivilization doit être rappelé — celui
        // de l'initialisation d'île a tourné avant que quoi que ce soit ne soit choisi.
        SelectEveryPermanentUniqueBuilding(controller);

        godState.GodPoints = godPoints;
        return controller;
    }

    /// <summary>
    /// Achète tous les pouvoirs divins, par vagues : <see cref="AscensionController.ArePrerequisitesMet"/>
    /// impose Foi d'abord puis l'ordre interne de chaque colonne, donc une seule passe en manquerait.
    /// La caisse est gonflée le temps des achats, et refixée par l'appelant.
    /// </summary>
    private static void PurchaseEveryAscensionPower(MainGameController controller, GodState godState)
    {
        godState.GodPoints = AscensionPowerDefinitions.All.Sum(p => p.GodPointCost);

        bool purchasedSomething = true;
        while (purchasedSomething)
        {
            purchasedSomething = false;
            foreach (var power in AscensionPowerDefinitions.All)
                if (controller.AscensionController.PurchasePower(power.Id))
                    purchasedSomething = true;
        }
    }

    /// <summary>
    /// Choisit tous les bâtiments uniques permanents proposés, dans la limite des emplacements ouverts
    /// par la colonne Héritage (2 par Ascension une fois Héritage Divin et Héritage Éternel acquis, ce
    /// qui est le cas ici). Vingt Ascensions ouvrent quarante emplacements pour seize choix : tout rentre.
    /// </summary>
    private static void SelectEveryPermanentUniqueBuilding(MainGameController controller)
    {
        var ascension = controller.AscensionController;
        foreach (var building in ascension.PermanentUniqueBuildingChoices)
            ascension.SelectPermanentUniqueBuilding(building);

        ascension.ApplyPermanentUniqueBuildingToCivilization();
    }

    /// <summary>
    /// A superset of divine powers guaranteed to satisfy any race's own <see cref="RaceDefinition.RequiredPowers"/>
    /// combination for <paramref name="tier"/>: Faith, then the first power of every column (covers
    /// any Base race's combination, all drawn from first-rank powers), plus the second power of every
    /// column that has one (covers any Advanced race's combination — each drawn from the 6 second-rank
    /// powers, one per column 0-5). Derived rather than hard-coded so adding a column or a power keeps
    /// this honest; granting the superset rather than the specific race's 3 powers keeps this simple
    /// without needing race-specific plumbing here.
    /// </summary>
    public static IReadOnlyList<AscensionPowerId> RequiredPowersFor(RaceTier tier)
    {
        var powers = new List<AscensionPowerId> { AscensionPowerId.Faith };
        int rows = tier == RaceTier.Advanced ? 2 : 1;

        var columns = AscensionPowerDefinitions.All
            .Where(d => d.Column != AscensionPowerDefinition.FoundationColumn)
            .Select(d => d.Column)
            .Distinct()
            .OrderBy(c => c);

        foreach (var column in columns)
        {
            var powersInColumn = AscensionPowerDefinitions.GetColumn(column);
            for (int row = 0; row < rows && row < powersInColumn.Count; row++)
                powers.Add(powersInColumn[row].Id);
        }

        return powers;
    }
}
