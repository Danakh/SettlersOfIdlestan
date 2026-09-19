using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services.Audio;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Bruitages. Ce qui est verifie ici est ce qui casse en silence : un WAV absent, un fichier mal
/// exporte, un toast qui sonne comme une victoire alors qu'il annonce une perte.
/// </summary>
public class SoundBankTests
{
    /// <summary>
    /// Chaque valeur de <see cref="SoundId"/> doit avoir son WAV embarque. Sans ce test, ajouter
    /// une valeur sans relancer <c>assets/sounds/generate_sounds.py</c> passe la compilation et ne
    /// se remarque qu'a l'oreille, quand le son concerne ne sort pas.
    /// </summary>
    [Fact]
    public void Chaque_son_a_son_fichier_embarque()
    {
        foreach (var id in Enum.GetValues<SoundId>())
            Assert.True(SoundBank.Read(id) != null, $"Bruitage absent : {SoundBank.FileName(id)}");
    }

    /// <summary>La convention de nommage est le seul lien entre l'enum et les fichiers generes.</summary>
    [Fact]
    public void Le_nom_de_fichier_est_la_valeur_en_snake_case()
    {
        Assert.Equal("toast_warning.wav", SoundBank.FileName(SoundId.ToastWarning));
        Assert.Equal("achievement.wav", SoundBank.FileName(SoundId.Achievement));
        Assert.Equal("city_founded.wav", SoundBank.FileName(SoundId.CityFounded));
    }

    /// <summary>
    /// Les WAV embarques doivent etre lisibles par le decodeur du head bureau. Un fichier exporte
    /// dans un format que <see cref="WavSample"/> refuse se chargerait sans erreur visible et
    /// resterait muet toute la partie.
    /// </summary>
    [Fact]
    public void Chaque_son_se_decode_et_dure_moins_d_une_seconde()
    {
        foreach (var id in Enum.GetValues<SoundId>())
        {
            var wav = SoundBank.Read(id);
            Assert.NotNull(wav);

            var sample = WavSample.TryDecode(wav);
            Assert.True(sample != null, $"WAV illisible : {SoundBank.FileName(id)}");

            // Ce sont des retours d'interface : au-dela d'une seconde, un son se superpose au
            // suivant et le jeu devient bruyant.
            double seconds = (double)sample!.Samples.Length / sample.SampleRate;
            Assert.True(seconds is > 0 and < 1.0, $"{SoundBank.FileName(id)} dure {seconds:0.00} s");
        }
    }

    /// <summary>Le reechantillonnage doit conserver la duree, a un echantillon pres.</summary>
    [Fact]
    public void Le_reechantillonnage_conserve_la_duree()
    {
        var sample = WavSample.TryDecode(SoundBank.Read(SoundId.ToastInfo)!);
        Assert.NotNull(sample);

        var resampled = sample!.Resample(48000);
        Assert.Equal(48000, resampled.SampleRate);
        Assert.Equal((double)sample.Samples.Length / sample.SampleRate,
            (double)resampled.Samples.Length / resampled.SampleRate, precision: 3);
    }

    /// <summary>Un fichier qui n'est pas un WAV est refuse, pas lu de travers.</summary>
    [Fact]
    public void Un_fichier_invalide_est_refuse()
    {
        Assert.Null(WavSample.TryDecode("pas un wav du tout, vraiment pas"u8));
        Assert.Null(WavSample.TryDecode([]));
    }
}

/// <summary>
/// Classement des toasts. Le piege est <see cref="NotificationIcon.StoreFail"/>, que GameScreen
/// pose aussi bien sur « un dragon vient d'apparaitre » que sur « votre ville a ete rasee ».
/// </summary>
public class ToastSoundTests
{
    private static SoundId Sound(GameEventType type, NotificationIcon icon)
    {
        SoundId? played = null;
        var audio = new GameAudioService(new RecordingAudioService(id => played = id));
        audio.ApplySettings(new GameSettings { SoundEnabled = true, SoundVolume = 1f });
        audio.PlayForToast(type, icon);
        Assert.True(played.HasValue, $"{type} n'a produit aucun son");
        return played!.Value;
    }

    [Theory]
    // Pertes : classees explicitement, parce que leur icone dit « menace ».
    [InlineData(GameEventType.CityLostToTerrain, NotificationIcon.StoreFail, SoundId.ToastLoss)]
    [InlineData(GameEventType.AbyssGateLost, NotificationIcon.StoreFail, SoundId.ToastLoss)]
    [InlineData(GameEventType.PandemoniumGateLost, NotificationIcon.StoreFail, SoundId.ToastLoss)]
    // Menaces : meme icone, son different.
    [InlineData(GameEventType.DragonDiscovered, NotificationIcon.StoreFail, SoundId.ToastWarning)]
    [InlineData(GameEventType.DemonGodDiscovered, NotificationIcon.StoreFail, SoundId.ToastWarning)]
    // Le reste suit l'icone choisie par GameScreen.
    [InlineData(GameEventType.CivilizationDestroyed, NotificationIcon.Achievement, SoundId.ToastVictory)]
    [InlineData(GameEventType.CivilizationDiscovered, NotificationIcon.Info, SoundId.ToastInfo)]
    // La premiere victoire sur le Dieu demon merite la fanfare des succes.
    [InlineData(GameEventType.DemonGodDefeatedFirst, NotificationIcon.Achievement, SoundId.Achievement)]
    [InlineData(GameEventType.DemonGodDefeated, NotificationIcon.Achievement, SoundId.ToastVictory)]
    public void Un_toast_sonne_selon_sa_nature(GameEventType type, NotificationIcon icon, SoundId expected) =>
        Assert.Equal(expected, Sound(type, icon));

    /// <summary>Son coupe dans les reglages : plus rien ne part vers le head.</summary>
    [Fact]
    public void Le_son_coupe_ne_joue_rien()
    {
        int played = 0;
        var audio = new GameAudioService(new RecordingAudioService(_ => played++));

        audio.ApplySettings(new GameSettings { SoundEnabled = false, SoundVolume = 1f });
        audio.PlayForToast(GameEventType.DragonDiscovered, NotificationIcon.StoreFail);
        Assert.Equal(0, played);

        // Volume a zero : meme resultat, par un autre chemin.
        audio.ApplySettings(new GameSettings { SoundEnabled = true, SoundVolume = 0f });
        audio.PlayForToast(GameEventType.DragonDiscovered, NotificationIcon.StoreFail);
        Assert.Equal(0, played);
    }

    /// <summary>
    /// L'intervalle minimum par son est le garde-fou qui empeche une fin de partie de mitrailler :
    /// deux declenchements consecutifs du meme son ne doivent en produire qu'un.
    /// </summary>
    [Fact]
    public void Le_meme_son_ne_part_pas_deux_fois_de_suite()
    {
        int played = 0;
        var audio = new GameAudioService(new RecordingAudioService(_ => played++));
        audio.ApplySettings(new GameSettings { SoundEnabled = true, SoundVolume = 1f });

        for (int i = 0; i < 50; i++) audio.Play(SoundId.AttackDealt);

        Assert.Equal(1, played);
    }

    /// <summary>Sans sortie audio (iOS, outils), tout l'appareillage reste utilisable et muet.</summary>
    [Fact]
    public void Sans_service_audio_le_jeu_reste_silencieux_sans_erreur()
    {
        var audio = new GameAudioService(null);
        Assert.False(audio.IsAvailable);

        audio.ApplySettings(new GameSettings { SoundEnabled = true, SoundVolume = 1f });
        audio.Play(SoundId.Achievement);
        audio.PlayForToast(GameEventType.DragonDiscovered, NotificationIcon.StoreFail);
        audio.Dispose();
    }

    /// <summary>Sortie audio de test : retient ce qui lui est demande, ne joue rien.</summary>
    private sealed class RecordingAudioService(Action<SoundId> onPlay) : IAudioService
    {
        public void Load(SoundId id, byte[] wav) { }
        public void Play(SoundId id, float volume) => onPlay(id);
        public void Dispose() { }
    }
}

/// <summary>
/// Familles de bruitages, coupables separement depuis l'onglet Son des reglages. Un joueur qui
/// coupe les combats doit garder ses annonces, et reciproquement : sans ce decoupage il n'avait
/// que l'interrupteur general.
/// </summary>
public class SoundCategoryTests
{
    /// <summary>Sortie audio de test : retient chaque son demande.</summary>
    private sealed class RecordingAudioService(List<SoundId> played) : IAudioService
    {
        public void Load(SoundId id, byte[] wav) { }
        public void Play(SoundId id, float volume) => played.Add(id);
        public void Dispose() { }
    }

    private static (GameAudioService Audio, List<SoundId> Played) Build(GameSettings settings)
    {
        var played = new List<SoundId>();
        var audio = new GameAudioService(new RecordingAudioService(played));
        audio.ApplySettings(settings);
        return (audio, played);
    }

    private static GameSettings Settings(
        bool combat = true, bool toast = true, bool achievement = true, bool city = true,
        bool cityLost = true, bool harvest = true) =>
        new()
        {
            SoundEnabled = true, SoundVolume = 1f,
            SoundCombatEnabled = combat, SoundToastEnabled = toast, SoundAchievementEnabled = achievement,
            SoundCityEnabled = city, SoundCityLostEnabled = cityLost, SoundHarvestEnabled = harvest,
        };

    /// <summary>
    /// La table des familles liste chaque <see cref="SoundId"/> une a une et leve sur une valeur
    /// oubliee : un son ajoute sans sa famille doit tomber ici, pas en pleine partie.
    /// </summary>
    [Fact]
    public void Chaque_son_a_sa_famille_declaree()
    {
        var (audio, played) = Build(Settings());

        foreach (var id in Enum.GetValues<SoundId>()) audio.Play(id);

        Assert.Equal(Enum.GetValues<SoundId>().Length, played.Count);
    }

    [Fact]
    public void Les_bruitages_de_combat_coupes_laissent_passer_le_reste()
    {
        var (audio, played) = Build(Settings(combat: false));

        audio.Play(SoundId.AttackDealt);
        audio.Play(SoundId.AttackTaken);
        audio.Play(SoundId.ToastWarning);
        audio.Play(SoundId.HarvestManual);
        audio.Play(SoundId.CityFounded);

        Assert.Equal([SoundId.ToastWarning, SoundId.HarvestManual, SoundId.CityFounded], played);
    }

    /// <summary>
    /// Couper les notifications coupe aussi la fanfare de succes, qui en est une sous-famille :
    /// c'est ce que dit la ligne grisee dans les reglages.
    /// </summary>
    [Fact]
    public void Les_bruitages_de_notification_coupes_laissent_passer_le_reste()
    {
        var (audio, played) = Build(Settings(toast: false));

        audio.Play(SoundId.ToastInfo);
        audio.Play(SoundId.ToastLoss);
        audio.Play(SoundId.Achievement);
        audio.PlayForToast(GameEventType.DragonDiscovered, NotificationIcon.StoreFail);
        audio.Play(SoundId.AttackDealt);
        audio.Play(SoundId.HarvestManual);

        Assert.Equal([SoundId.AttackDealt, SoundId.HarvestManual], played);
    }

    /// <summary>
    /// La reciproque : la fanfare seule se coupe, les toasts restent. C'est tout l'objet de sa
    /// case separee — elle est le seul son long du jeu.
    /// </summary>
    [Fact]
    public void La_fanfare_coupee_laisse_passer_les_toasts()
    {
        var (audio, played) = Build(Settings(achievement: false));

        audio.Play(SoundId.Achievement);
        audio.Play(SoundId.ToastInfo);
        audio.Play(SoundId.ToastVictory);
        audio.PlayForToast(GameEventType.DemonGodDefeatedFirst, NotificationIcon.Achievement);

        Assert.Equal([SoundId.ToastInfo, SoundId.ToastVictory], played);
    }

    /// <summary>
    /// La fondation de villes a sa propre case : l'automatisation des avant-postes en enchaine
    /// des rafales, et le joueur doit pouvoir les taire sans perdre le reste.
    /// </summary>
    [Fact]
    public void Les_bruitages_de_fondation_coupes_laissent_passer_le_reste()
    {
        var (audio, played) = Build(Settings(city: false));

        audio.Play(SoundId.CityFounded);
        audio.Play(SoundId.HarvestManual);
        audio.Play(SoundId.ToastInfo);
        audio.Play(SoundId.AttackDealt);

        Assert.Equal([SoundId.HarvestManual, SoundId.ToastInfo, SoundId.AttackDealt], played);
    }

    /// <summary>
    /// La perte d'une ville a sa propre case, distincte du combat comme de la fondation : couper
    /// l'ambiance des batailles ou les rafales d'avant-postes ne doit pas emporter l'annonce que
    /// le joueur vient de reculer.
    /// </summary>
    [Fact]
    public void La_perte_de_ville_se_coupe_sans_toucher_au_combat_ni_a_la_fondation()
    {
        var (audio, played) = Build(Settings(cityLost: false));

        audio.Play(SoundId.CityLost);
        audio.Play(SoundId.CityFounded);
        audio.Play(SoundId.AttackTaken);

        Assert.Equal([SoundId.CityFounded, SoundId.AttackTaken], played);
    }

    /// <summary>La reciproque : couper le combat et la fondation laisse passer la perte.</summary>
    [Fact]
    public void La_perte_de_ville_passe_malgre_le_combat_et_la_fondation_coupes()
    {
        var (audio, played) = Build(Settings(combat: false, city: false));

        audio.Play(SoundId.AttackTaken);
        audio.Play(SoundId.CityFounded);
        audio.Play(SoundId.CityLost);

        Assert.Equal([SoundId.CityLost], played);
    }

    /// <summary>
    /// La recolte manuelle est le son le plus joue de la partie — un par clic : le joueur qui
    /// recolte en continu doit pouvoir la taire sans perdre ses annonces.
    /// </summary>
    [Fact]
    public void Les_bruitages_de_recolte_coupes_laissent_passer_le_reste()
    {
        var (audio, played) = Build(Settings(harvest: false));

        audio.Play(SoundId.HarvestManual);
        audio.Play(SoundId.CityFounded);
        audio.Play(SoundId.ToastInfo);
        audio.Play(SoundId.AttackDealt);

        Assert.Equal([SoundId.CityFounded, SoundId.ToastInfo, SoundId.AttackDealt], played);
    }

    /// <summary>
    /// Couper une famille ne touche pas les autres reglages sonores : retablir la case rend la
    /// famille, sans passer par l'interrupteur general ni le volume.
    /// </summary>
    [Fact]
    public void Retablir_la_famille_rend_ses_sons()
    {
        var settings = Settings(combat: false);
        var (audio, played) = Build(settings);

        audio.Play(SoundId.AttackTaken);
        Assert.Empty(played);

        settings.SoundCombatEnabled = true;
        audio.ApplySettings(settings);
        audio.Play(SoundId.AttackTaken);

        Assert.Equal([SoundId.AttackTaken], played);
    }

    /// <summary>
    /// L'apercu joue depuis les reglages ignore la famille : regler le volume est impossible si
    /// le son temoin — un toast — se tait des que le joueur coupe les notifications.
    /// </summary>
    [Fact]
    public void L_apercu_des_reglages_passe_malgre_la_famille_coupee()
    {
        var (audio, played) = Build(Settings(toast: false));

        audio.PlayPreview(SoundId.ToastInfo);

        Assert.Equal([SoundId.ToastInfo], played);
    }

    /// <summary>Mais l'apercu reste soumis a l'interrupteur general et au volume.</summary>
    [Fact]
    public void L_apercu_se_tait_quand_le_son_est_coupe()
    {
        var (audio, played) = Build(new GameSettings { SoundEnabled = false, SoundVolume = 1f });
        audio.PlayPreview(SoundId.ToastInfo);
        Assert.Empty(played);

        var (silent, none) = Build(new GameSettings { SoundEnabled = true, SoundVolume = 0f });
        silent.PlayPreview(SoundId.ToastInfo);
        Assert.Empty(none);
    }
}

/// <summary>
/// Les coups sonnent a l'impact, pas a l'evenement : le controleur resout une attaque d'un bloc,
/// le rendu met une demi-seconde a plus d'une seconde a l'amener sur sa cible. Sans ce delai le
/// joueur entend le coup au depart de la particule, bien avant de le voir porter.
/// </summary>
public class CombatImpactSoundTests
{
    private sealed class RecordingAudioService(List<SoundId> played) : IAudioService
    {
        public void Load(SoundId id, byte[] wav) { }
        public void Play(SoundId id, float volume) => played.Add(id);
        public void Dispose() { }
    }

    /// <summary>Assez court pour ne pas ralentir la suite, assez long pour tenir une frame.</summary>
    private const float Travel = 0.05f;

    private static GameSettings Settings(bool combat = true) =>
        new() { SoundEnabled = true, SoundVolume = 1f, SoundCombatEnabled = combat };

    private static (GameAudioService Audio, List<SoundId> Played) Build(GameSettings settings)
    {
        var played = new List<SoundId>();
        var audio = new GameAudioService(new RecordingAudioService(played));
        audio.ApplySettings(settings);
        return (audio, played);
    }

    /// <summary>Attendre l'echeance sans dependre de la precision du sommeil.</summary>
    private static void AttendreLImpact() => Thread.Sleep((int)(Travel * 1000) + 40);

    [Fact]
    public void Le_coup_ne_sonne_pas_au_depart_de_la_particule()
    {
        var (audio, played) = Build(Settings());

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        Assert.Empty(played);

        // Frame suivante, la particule est encore en vol.
        audio.Update();
        Assert.Empty(played);
    }

    [Fact]
    public void Le_coup_sonne_quand_la_particule_arrive()
    {
        var (audio, played) = Build(Settings());

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        AttendreLImpact();
        audio.Update();

        Assert.Equal([SoundId.AttackDealt], played);
    }

    /// <summary>Une fois joue, le coup quitte la file : la frame d'apres ne le rejoue pas.</summary>
    [Fact]
    public void Un_coup_arrive_ne_sonne_qu_une_fois()
    {
        var (audio, played) = Build(Settings());

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        AttendreLImpact();
        audio.Update();
        audio.Update();
        audio.Update();

        Assert.Single(played);
    }

    /// <summary>
    /// Sans animation a attendre, l'appelant n'a rien de special a faire : le son part tout de
    /// suite, sans passer par la file.
    /// </summary>
    [Fact]
    public void Un_temps_de_vol_nul_sonne_immediatement()
    {
        var (audio, played) = Build(Settings());

        audio.PlayOnImpact(SoundId.AttackDealt, 0f);

        Assert.Equal([SoundId.AttackDealt], played);
    }

    /// <summary>
    /// Le garde-fou de cadence compte les departs : une fin de partie qui resout cinquante coups
    /// dans le meme tick ne doit pas remplir la file de cinquante impacts a venir.
    /// </summary>
    [Fact]
    public void La_cadence_est_consommee_au_depart()
    {
        var (audio, played) = Build(Settings());

        for (int i = 0; i < 50; i++) audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        AttendreLImpact();
        audio.Update();

        Assert.Single(played);
    }

    /// <summary>
    /// Le joueur peut couper le son pendant qu'un coup est en vol : ce sont les reglages de
    /// l'arrivee qui decident, pas ceux du depart.
    /// </summary>
    [Fact]
    public void Couper_la_famille_pendant_le_vol_tait_le_coup()
    {
        var settings = Settings();
        var (audio, played) = Build(settings);

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        settings.SoundCombatEnabled = false;
        audio.ApplySettings(settings);

        AttendreLImpact();
        audio.Update();

        Assert.Empty(played);
    }

    /// <summary>La reciproque : une famille coupee au depart ne met rien en file.</summary>
    [Fact]
    public void La_famille_coupee_au_depart_ne_met_rien_en_file()
    {
        var settings = Settings(combat: false);
        var (audio, played) = Build(settings);

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        settings.SoundCombatEnabled = true;
        audio.ApplySettings(settings);

        AttendreLImpact();
        audio.Update();

        Assert.Empty(played);
    }

    /// <summary>
    /// Saut de temps, transition de prestige, intro : les coups restes en vol sont jetes, pas
    /// deverses d'un bloc au retour — c'est ce que font aussi les renderers de leurs particules.
    /// </summary>
    [Fact]
    public void Une_suppression_jette_les_coups_en_vol()
    {
        var played = new List<SoundId>();
        bool suppressed = false;
        var audio = new GameAudioService(new RecordingAudioService(played));
        audio.ApplySettings(Settings());
        audio.SetSuppression(() => suppressed);

        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        suppressed = true;
        audio.Update();

        suppressed = false;
        AttendreLImpact();
        audio.Update();

        Assert.Empty(played);
    }

    /// <summary>Sans sortie audio, la file ne se remplit pas et Update ne leve pas.</summary>
    [Fact]
    public void Sans_service_audio_l_impact_reste_silencieux_sans_erreur()
    {
        var audio = new GameAudioService(null);

        audio.ApplySettings(Settings());
        audio.PlayOnImpact(SoundId.AttackDealt, Travel);
        AttendreLImpact();
        audio.Update();
        audio.Dispose();
    }
}
