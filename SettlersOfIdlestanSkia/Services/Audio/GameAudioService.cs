using System.Diagnostics;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Renderers.Overlay;

namespace SettlersOfIdlestanSkia.Services.Audio;

/// <summary>
/// Le chef d'orchestre des bruitages : décide de ce qui se joue et quand, et laisse le
/// <see cref="IAudioService"/> du head faire sonner. Rien d'autre dans le jeu n'appelle
/// directement le service audio.
///
/// <para><b>Sans service audio, cette classe reste utilisable et muette</b> (iOS, tests, outils
/// hors-jeu, ou une carte son introuvable au démarrage). C'est ce qui permet de brancher les
/// événements sans condition partout ailleurs.</para>
///
/// <para><b>Ce qui protège l'oreille du joueur</b> — trois garde-fous, tous nécessaires :</para>
/// <list type="number">
/// <item>un intervalle minimum par son (voir <see cref="MinInterval"/>) : une fin de partie où
/// vingt villes frappent trente monstres émettrait, sans lui, des centaines d'attaques par
/// seconde ;</item>
/// <item>la suppression pendant un saut de temps, une transition de prestige ou l'intro (voir
/// <see cref="Connect"/>) : la simulation y avance de plusieurs heures en quelques secondes ;</item>
/// <item>le filtrage par propriétaire : les combats des civilisations PNJ entre elles sont
/// silencieux, seuls les coups portés et reçus par le joueur s'entendent.</item>
/// </list>
/// </summary>
public sealed class GameAudioService : IDisposable
{
    /// <summary>Volume par défaut d'une nouvelle partie — voir <see cref="GameSettings.SoundVolume"/>.</summary>
    public const float DefaultVolume = 0.6f;

    private readonly IAudioService? _audio;

    /// Horodatage (<see cref="Stopwatch.GetTimestamp"/>) du dernier passage de chaque son.
    private readonly long[] _lastPlayed;

    private Func<bool>? _suppressed;
    private Func<Civilization?>? _playerCivilization;

    private bool _enabled = true;
    private bool _combatEnabled = true;
    private bool _toastEnabled = true;
    private bool _achievementEnabled = true;
    private bool _cityEnabled = true;
    private bool _cityLostEnabled = true;
    private bool _harvestEnabled = true;
    private float _volume = DefaultVolume;
    private bool _disposed;

    /// <param name="audio">Sortie du head, ou null pour un jeu muet.</param>
    public GameAudioService(IAudioService? audio)
    {
        _audio = audio;
        _lastPlayed = new long[Enum.GetValues<SoundId>().Length];

        if (_audio != null) SoundBank.LoadAll(_audio);
    }

    /// <summary>Vrai si un head a fourni une sortie audio. Faux = jeu muet, sans erreur.</summary>
    public bool IsAvailable => _audio != null;

    /// <summary>
    /// Intervalle minimum entre deux passages d'un même son. Les bruitages d'ambiance de combat
    /// sont les plus serrés : ils doivent pouvoir se succéder assez vite pour qu'un assaut
    /// s'entende comme un assaut, sans devenir une mitraille. Les toasts, eux, n'ont besoin que
    /// d'éviter le doublon exact — deux découvertes au même tick.
    /// </summary>
    private static double MinInterval(SoundId id) => id switch
    {
        SoundId.AttackDealt       => 0.13,
        SoundId.AttackTaken       => 0.16,
        SoundId.HarvestManual     => 0.05,
        SoundId.BuildingDestroyed => 0.30,
        SoundId.CityFounded       => 0.30,
        // Le plus long des bruitages : deux chutes de ville dans le même tick se
        // superposeraient en bouillie, là où une seule dit déjà qu'on recule.
        SoundId.CityLost          => 0.90,
        _                         => 0.35,
    };

    /// <summary>
    /// Famille d'un son. Sert à couper une famille entière depuis les réglages — voir
    /// <see cref="SoundCategory"/>. Chaque valeur est listée à dessein plutôt que couverte par un
    /// repli sur une famille fourre-tout : un son ajouté sans la sienne se serait rangé en
    /// silence dans celle qu'aucune case à cocher ne gouverne. Ici il lève, et
    /// <c>SoundCategoryTests</c> le rattrape avant le joueur.
    /// </summary>
    private static SoundCategory Category(SoundId id) => id switch
    {
        SoundId.ToastInfo    or SoundId.ToastWarning or
        SoundId.ToastVictory or SoundId.ToastLoss                                 => SoundCategory.Toast,

        SoundId.Achievement                                                       => SoundCategory.Achievement,

        SoundId.AttackDealt  or SoundId.AttackTaken  or SoundId.BuildingDestroyed => SoundCategory.Combat,

        SoundId.CityFounded                                                       => SoundCategory.City,

        SoundId.CityLost                                                          => SoundCategory.CityLost,

        SoundId.HarvestManual                                                     => SoundCategory.Harvest,

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Son sans famille déclarée."),
    };

    /// <summary>
    /// Volume relatif d'un son dans le mélange, avant le volume général du joueur. Les sons qui
    /// se répètent sans fin (récolte, coups) passent volontairement sous les annonces, qui doivent
    /// rester audibles par-dessus une bataille en cours.
    /// </summary>
    private static float Mix(SoundId id) => id switch
    {
        SoundId.HarvestManual     => 0.55f,
        SoundId.AttackDealt       => 0.5f,
        SoundId.AttackTaken       => 0.6f,
        SoundId.BuildingDestroyed => 0.8f,
        SoundId.CityFounded       => 0.8f,
        _                         => 1f,
    };

    /// <summary>
    /// Recopie les préférences du joueur. Appelée à chaque frame plutôt qu'au changement : les
    /// réglages se modifient depuis le popup en jeu comme depuis l'écran-titre, et une partie
    /// chargée apporte les siens — suivre tous ces chemins coûterait plus cher que deux
    /// affectations par frame.
    /// </summary>
    public void ApplySettings(GameSettings? settings)
    {
        if (settings == null) return;
        _enabled = settings.SoundEnabled;
        _combatEnabled = settings.SoundCombatEnabled;
        _toastEnabled = settings.SoundToastEnabled;
        _achievementEnabled = settings.SoundAchievementEnabled;
        _cityEnabled = settings.SoundCityEnabled;
        _cityLostEnabled = settings.SoundCityLostEnabled;
        _harvestEnabled = settings.SoundHarvestEnabled;
        _volume = Math.Clamp(settings.SoundVolume, 0f, 1f);
    }

    /// <summary>Vrai si la famille de ce son n'est pas coupée par les réglages.</summary>
    private bool IsCategoryEnabled(SoundId id) => Category(id) switch
    {
        SoundCategory.Toast       => _toastEnabled,
        // La fanfare est une notification : couper les notifications la coupe aussi, sa case ne
        // sert qu'à la retirer seule. C'est ce que dit la ligne grisée dans les réglages.
        SoundCategory.Achievement => _toastEnabled && _achievementEnabled,
        SoundCategory.Combat      => _combatEnabled,
        SoundCategory.City        => _cityEnabled,
        SoundCategory.CityLost    => _cityLostEnabled,
        SoundCategory.Harvest     => _harvestEnabled,
        _                         => true,
    };

    /// <summary>
    /// Vrai si ce son passerait maintenant. Ne consomme rien : sert aux appelants qui doivent
    /// écarter un son bon marché <b>avant</b> de payer la recherche du propriétaire d'un vertex
    /// (voir <see cref="PlayIfOurs"/>).
    /// </summary>
    /// <param name="ignoreCategory">Vrai pour un aperçu demandé depuis les réglages — voir
    /// <see cref="PlayPreview"/>.</param>
    private bool IsReady(SoundId id, bool ignoreCategory = false)
    {
        if (_disposed || _audio == null || !_enabled || _volume <= 0f) return false;
        if (!ignoreCategory && !IsCategoryEnabled(id)) return false;
        if (_suppressed?.Invoke() == true) return false;

        long last = _lastPlayed[(int)id];
        if (last == 0) return true;
        return Stopwatch.GetTimestamp() >= last + (long)(MinInterval(id) * Stopwatch.Frequency);
    }

    /// <summary>Joue un son, sous réserve des garde-fous décrits en tête de classe.</summary>
    public void Play(SoundId id)
    {
        if (!IsReady(id)) return;

        _lastPlayed[(int)id] = Stopwatch.GetTimestamp();
        _audio!.Play(id, Mix(id) * _volume);
    }

    /// <summary>
    /// Fait entendre un son en réponse à un geste du joueur dans les réglages. Ignore la famille
    /// du son — sans quoi l'aperçu du volume (un toast d'information) deviendrait muet dès que le
    /// joueur coupe les bruitages de notification, et le curseur se réglerait à l'aveugle. Les
    /// autres garde-fous, eux, tiennent : coupé, à volume nul ou pendant un saut de temps, rien
    /// ne sort.
    /// </summary>
    public void PlayPreview(SoundId id)
    {
        if (!IsReady(id, ignoreCategory: true)) return;

        _lastPlayed[(int)id] = Stopwatch.GetTimestamp();
        _audio!.Play(id, Mix(id) * _volume);
    }

    // ── Toasts ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Son d'un toast de notification. Le type de l'événement prime, l'icône sert de repli : les
    /// deux familles que l'icône confond sont les menaces et les pertes, toutes deux en
    /// <see cref="NotificationIcon.StoreFail"/> alors qu'elles n'appellent pas le même son — « un
    /// dragon vient d'apparaître » n'est pas « votre ville a été rasée ». Seules les pertes sont
    /// donc listées ici, le reste suit l'icône que GameScreen a déjà choisie.
    ///
    /// <para>Un événement ajouté plus tard sonne correctement sans être listé, tant que son icône
    /// dit juste. Il n'y a rien à tenir à jour ici hormis une nouvelle perte.</para>
    /// </summary>
    public void PlayForToast(GameEventType type, NotificationIcon icon)
    {
        var sound = type switch
        {
            GameEventType.CityLostToTerrain                    => SoundId.ToastLoss,
            GameEventType.MilitaryVertexLostToTerrain          => SoundId.ToastLoss,
            GameEventType.AbyssGateLost                        => SoundId.ToastLoss,
            GameEventType.AbyssLostDivineEssence               => SoundId.ToastLoss,
            GameEventType.PandemoniumGateLost                  => SoundId.ToastLoss,
            GameEventType.UnderworldLost                       => SoundId.ToastLoss,
            GameEventType.SurfaceLost                          => SoundId.ToastLoss,
            GameEventType.MonumentInvestmentBlockedByCityLoss  => SoundId.ToastLoss,
            GameEventType.RitualCollapsed                      => SoundId.ToastLoss,

            // Première victoire sur le Dieu démon : le joueur peut considérer qu'il a gagné la
            // partie. Seul événement du jeu à mériter la fanfare des succès.
            GameEventType.DemonGodDefeatedFirst                => SoundId.Achievement,

            _ => icon switch
            {
                NotificationIcon.Achievement => SoundId.ToastVictory,
                NotificationIcon.StoreFail   => SoundId.ToastWarning,
                _                            => SoundId.ToastInfo,
            },
        };

        Play(sound);
    }

    // ── Branchement sur la partie ────────────────────────────────────────────

    /// <summary>
    /// Abonne le son aux événements de la partie. Appelée une fois par <c>GameScreen</c>, qui
    /// construit ses propres contrôleurs : ceux-ci vivent aussi longtemps que lui et traversent
    /// les nouvelles îles, les prestiges et les ascensions (seul leur <c>Initialize</c> est
    /// rejoué), il n'y a donc rien à réabonner en cours de partie.
    ///
    /// <para>Ce service, lui, survit au <c>GameScreen</c> : un retour au menu suivi d'une nouvelle
    /// partie rappelle cette méthode avec de nouveaux contrôleurs. Les abonnements de l'ancienne
    /// partie restent accrochés à des contrôleurs qui ne tournent plus et disparaissent avec
    /// eux.</para>
    /// </summary>
    /// <param name="suppressed">
    /// Vrai quand rien ne doit sonner : saut de temps, transition de prestige, animation d'intro.
    /// Évalué à chaque son — l'état change en cours de partie.
    /// </param>
    public void Connect(GameControllerService controllers, HarvestService harvest, Func<bool> suppressed)
    {
        if (_disposed || _audio == null) return;

        _suppressed = suppressed;
        _playerCivilization = () => controllers.PlayerCivilization;

        var main = controllers.MainGameController;

        // Récolte manuelle seule : l'automatique tombe toutes les cinq secondes par bâtiment et
        // couvrirait la partie entière d'un cliquetis continu.
        harvest.OnHarvestCompleted += (_, e) =>
        {
            if (e.IsAutomatic) return;
            if (e.CivilizationIndex != _playerCivilization?.Invoke()?.Index) return;
            Play(SoundId.HarvestManual);
        };

        // ── Coups portés ──
        main.MilitaryController.SoldierAttackedMonster += (_, e) => PlayIfOurs(e.CityVertex, SoundId.AttackDealt);
        main.MilitaryController.DefenseSpireAttackedMonster += (_, e) => PlayIfOurs(e.CityVertex, SoundId.AttackDealt);

        // Une attaque entre villes se juge des deux côtés : nos soldats partent à l'assaut, ou
        // c'est notre ville qui est prise pour cible. Une civilisation PNJ qui en attaque une
        // autre ne produit rien.
        main.MilitaryController.SoldierAttackedCity += (_, e) =>
        {
            if (IsReady(SoundId.AttackDealt) && IsOurs(e.SourceCity)) Play(SoundId.AttackDealt);
            else if (IsReady(SoundId.AttackTaken) && IsOurs(e.TargetCity)) Play(SoundId.AttackTaken);
        };

        // ── Coups reçus ──
        main.MonsterFeatureController.MonsterAttackedVertex += (_, e) => PlayIfOurs(e.TargetVertex, SoundId.AttackTaken);
        main.VolcanoController.VolcanoHitCity += (_, e) => PlayIfOurs(e.TargetCityVertex, SoundId.AttackTaken);
        main.MilitaryController.CityBuildingDestroyed += (_, e) => PlayIfOurs(e.CityVertex, SoundId.BuildingDestroyed);

        // ── Fondation ──
        main.CityBuilderController.OnCityBuilt += (_, e) => PlayIfOurCiv(e.CivilizationIndex, SoundId.CityFounded);
        main.CityBuilderController.OnAutoOutpostBuilt += (_, e) => PlayIfOurCiv(e.CivilizationIndex, SoundId.CityFounded);

        // ── Perte ──
        // Seules les deux causes qui nous arrachent la ville. `Terrain` est écartée parce qu'elle
        // porte déjà son propre toast (CityLostToTerrain, donc ToastLoss) : les deux sons se
        // chevaucheraient. `PlayerChoice` l'est parce que le joueur vient de cliquer « Détruire »
        // — ce n'est pas une perte, c'est une décision.
        //
        // Cet abonnement vient après ceux de MainGameController, dont RemoveEliminatedCivilization
        // qui doit rester le dernier à pouvoir retrouver la civilisation détruite. Sans effet ici :
        // on ne compare que des index, et on ne cherche que la nôtre — jamais celle qui tombe.
        main.CityBuilderController.OnCityDestroyed += (_, e) =>
        {
            if (e.Cause is not (CityDestructionCause.Combat or CityDestructionCause.Monster)) return;
            PlayIfOurCiv(e.CivilizationIndex, SoundId.CityLost);
        };
    }

    private void PlayIfOurCiv(int civilizationIndex, SoundId sound)
    {
        if (civilizationIndex != _playerCivilization?.Invoke()?.Index) return;
        Play(sound);
    }

    private void PlayIfOurs(Vertex vertex, SoundId sound)
    {
        // Ordre voulu : le garde-fou de cadence est une comparaison d'entiers, la recherche du
        // propriétaire un parcours de toutes nos villes. Inversé, chaque coup porté par chaque
        // soldat balaierait la liste.
        if (!IsReady(sound)) return;
        if (!IsOurs(vertex)) return;
        Play(sound);
    }

    /// <summary>Vrai si ce vertex porte une de nos villes, Flottes de Guerre ou Camps Mobiles.</summary>
    private bool IsOurs(Vertex vertex)
    {
        var civ = _playerCivilization?.Invoke();
        if (civ == null) return false;

        var vertices = civ.MilitaryVertices;
        for (int i = 0; i < vertices.Count; i++)
            if (vertices[i].Position.Equals(vertex)) return true;

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _audio?.Dispose();
    }
}
