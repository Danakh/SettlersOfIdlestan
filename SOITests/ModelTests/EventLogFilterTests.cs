using System;
using System.Linq;
using System.Text.Json;
using SettlersOfIdlestan.Model.Game;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Filtre d'affichage du journal (onglet Réglages du Journal). Il est appliqué à la source, dans
/// <see cref="GameEventLog.Add"/> : c'est ce qui éteint d'un seul geste les trois manifestations
/// d'un événement — la ligne du journal, la surbrillance de l'onglet (qui compte les entrées) et
/// le toast.
/// </summary>
public class EventLogFilterTests
{
    [Fact]
    public void Sans_filtre_cable_tout_est_journalise()
    {
        var log = new GameEventLog();

        log.Add(GameEventType.DragonDiscovered);

        Assert.Single(log.Entries);
    }

    [Fact]
    public void Une_famille_masquee_ne_produit_ni_entree_ni_toast()
    {
        var filter = new EventLogFilter();
        filter.SetCategoryVisible(EventLogCategory.Dragon, false);
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.DragonDiscovered, toast: true);
        log.Add(GameEventType.DragonDefeated);

        Assert.Empty(log.Entries);
        Assert.False(log.HasEntries, "L'onglet Journal se met en surbrillance sur le nombre d'entrées.");
        Assert.False(log.TryDequeueToast(out _));
    }

    /// Masquer une famille ne doit pas en emporter d'autres, ni les événements non filtrables.
    [Fact]
    public void Les_autres_familles_et_la_progression_restent_journalisees()
    {
        var filter = new EventLogFilter();
        filter.SetCategoryVisible(EventLogCategory.Dragon, false);
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.DragonDiscovered);
        log.Add(GameEventType.TrollDiscovered);
        log.Add(GameEventType.WonderPlaced);

        Assert.Equal(
            new[] { GameEventType.WonderPlaced, GameEventType.TrollDiscovered },
            log.Entries.Select(e => e.Type));
    }

    [Fact]
    public void Recocher_une_famille_la_reaffiche()
    {
        var filter = new EventLogFilter();
        var log = new GameEventLog();
        log.Bind(filter);

        filter.ToggleCategory(EventLogCategory.TreasureTrove);
        log.Add(GameEventType.TreasureTroveDiscovered);
        filter.ToggleCategory(EventLogCategory.TreasureTrove);
        log.Add(GameEventType.TreasureTroveClaimed);

        Assert.Equal(new[] { GameEventType.TreasureTroveClaimed }, log.Entries.Select(e => e.Type));
    }

    /// <summary>
    /// Apparition ET disparition tombent sous la même case : c'est ce que demande le réglage.
    /// Un oubli ici laisserait passer la moitié des lignes d'une famille pourtant décochée.
    /// </summary>
    [Theory]
    [InlineData(EventLogCategory.Bandit, GameEventType.BanditDiscovered, GameEventType.BanditDefeated)]
    [InlineData(EventLogCategory.BanditHideout, GameEventType.BanditHideoutDiscovered, GameEventType.BanditHideoutDestroyed)]
    [InlineData(EventLogCategory.Rats, GameEventType.RatsDiscovered, GameEventType.RatsDefeated)]
    [InlineData(EventLogCategory.Troll, GameEventType.TrollDiscovered, GameEventType.TrollDefeated)]
    [InlineData(EventLogCategory.Ogre, GameEventType.OgreDiscovered, GameEventType.OgreDefeated)]
    [InlineData(EventLogCategory.Dragon, GameEventType.DragonDiscovered, GameEventType.DragonDefeated)]
    [InlineData(EventLogCategory.MinorDemon, GameEventType.MinorDemonDiscovered, GameEventType.MinorDemonDefeated)]
    [InlineData(EventLogCategory.MajorDemon, GameEventType.MajorDemonDiscovered, GameEventType.MajorDemonDefeated)]
    [InlineData(EventLogCategory.Tentacle, GameEventType.TentacleDiscovered, GameEventType.TentacleDefeated)]
    [InlineData(EventLogCategory.DemonGod, GameEventType.DemonGodDiscovered, GameEventType.DemonGodDefeated)]
    [InlineData(EventLogCategory.Adventurer, GameEventType.AdventurerDiscovered, GameEventType.AdventurerDefeated)]
    [InlineData(EventLogCategory.TreasureTrove, GameEventType.TreasureTroveDiscovered, GameEventType.TreasureTroveClaimed)]
    public void Chaque_famille_couvre_son_apparition_et_sa_disparition(
        EventLogCategory category, GameEventType appearance, GameEventType disappearance)
    {
        Assert.Equal(category, EventLogFilter.GetCategory(appearance));
        Assert.Equal(category, EventLogFilter.GetCategory(disappearance));
    }

    /// Chaque famille doit apparaître dans l'onglet Réglages, sans quoi elle serait masquable
    /// par une sauvegarde mais introuvable dans l'interface.
    [Fact]
    public void Toutes_les_familles_sont_affichees_avec_un_libelle()
    {
        Assert.Equal(
            Enum.GetValues<EventLogCategory>().OrderBy(c => c),
            EventLogFilter.DisplayOrder.OrderBy(c => c));

        Assert.All(EventLogFilter.DisplayOrder,
            c => Assert.NotEmpty(EventLogFilter.GetLabelKey(c)));
    }

    /// <summary>
    /// Le filtre vit dans GameSettings, donc dans les sauvegardes : encodé par nom, une catégorie
    /// insérée ailleurs qu'en fin d'énumération ne décale pas les préférences existantes.
    /// </summary>
    [Fact]
    public void Le_filtre_se_serialise_par_nom()
    {
        var settings = new GameSettings();
        settings.EventLogFilter.SetCategoryVisible(EventLogCategory.Rats, false);
        settings.EventLogFilter.MarkKnown(GameEventType.DragonDiscovered);

        string json = JsonSerializer.Serialize(settings);
        Assert.Contains("\"Rats\"", json);
        Assert.Contains("\"Dragon\"", json);

        var restored = JsonSerializer.Deserialize<GameSettings>(json)!;
        Assert.False(restored.EventLogFilter.IsCategoryVisible(EventLogCategory.Rats));
        Assert.True(restored.EventLogFilter.IsCategoryVisible(EventLogCategory.Dragon));
        Assert.True(restored.EventLogFilter.IsCategoryKnown(EventLogCategory.Dragon));
    }

    /// <summary>
    /// L'onglet Réglages ne liste que les familles déjà croisées : les lister toutes dévoilerait
    /// le bestiaire complet — dieu démon compris — dès le premier bandit.
    /// </summary>
    [Fact]
    public void Une_famille_jamais_croisee_reste_inconnue()
    {
        var filter = new EventLogFilter();
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.BanditDiscovered);

        Assert.True(filter.IsCategoryKnown(EventLogCategory.Bandit));
        Assert.All(EventLogFilter.DisplayOrder.Where(c => c != EventLogCategory.Bandit),
            c => Assert.False(filter.IsCategoryKnown(c)));
    }

    /// Une disparition suffit : c'est bien qu'on a croisé la créature.
    [Fact]
    public void Une_disparition_fait_connaitre_la_famille()
    {
        var filter = new EventLogFilter();
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.TreasureTroveClaimed);

        Assert.True(filter.IsCategoryKnown(EventLogCategory.TreasureTrove));
    }

    /// <summary>
    /// Régression : marquer la rencontre après le filtrage retirerait une famille décochée de ses
    /// propres réglages — plus aucune case pour la recocher.
    /// </summary>
    [Fact]
    public void Une_famille_masquee_reste_connue()
    {
        var filter = new EventLogFilter();
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.OgreDiscovered);
        filter.SetCategoryVisible(EventLogCategory.Ogre, false);
        log.Add(GameEventType.OgreDefeated);

        Assert.True(filter.IsCategoryKnown(EventLogCategory.Ogre));
        Assert.Empty(log.Entries.Where(e => e.Type == GameEventType.OgreDefeated));
    }

    /// Un événement de progression n'appartient à aucune famille : il n'en fait connaître aucune.
    [Fact]
    public void Un_evenement_non_filtrable_ne_fait_connaitre_aucune_famille()
    {
        var filter = new EventLogFilter();
        var log = new GameEventLog();
        log.Bind(filter);

        log.Add(GameEventType.WonderPlaced);

        Assert.Empty(filter.KnownCategories);
    }
}
