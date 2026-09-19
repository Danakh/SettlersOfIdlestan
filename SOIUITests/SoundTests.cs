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
        Assert.Equal("building_destroyed.wav", SoundBank.FileName(SoundId.BuildingDestroyed));
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

    private static GameSettings Settings(bool combat = true, bool toast = true) =>
        new() { SoundEnabled = true, SoundVolume = 1f, SoundCombatEnabled = combat, SoundToastEnabled = toast };

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
        audio.Play(SoundId.BuildingDestroyed);
        audio.Play(SoundId.ToastWarning);
        audio.Play(SoundId.HarvestManual);
        audio.Play(SoundId.CityFounded);

        Assert.Equal([SoundId.ToastWarning, SoundId.HarvestManual, SoundId.CityFounded], played);
    }

    /// <summary>La fanfare de succes est un toast : elle suit la meme case que les autres.</summary>
    [Fact]
    public void Les_bruitages_de_notification_coupes_laissent_passer_le_reste()
    {
        var (audio, played) = Build(Settings(toast: false));

        audio.Play(SoundId.ToastInfo);
        audio.Play(SoundId.ToastLoss);
        audio.Play(SoundId.Achievement);
        audio.PlayForToast(GameEventType.DragonDiscovered, NotificationIcon.StoreFail);
        audio.Play(SoundId.AttackDealt);
        audio.Play(SoundId.BuildingBuilt);

        Assert.Equal([SoundId.AttackDealt, SoundId.BuildingBuilt], played);
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

        audio.Play(SoundId.BuildingDestroyed);
        Assert.Empty(played);

        settings.SoundCombatEnabled = true;
        audio.ApplySettings(settings);
        audio.Play(SoundId.BuildingDestroyed);

        Assert.Equal([SoundId.BuildingDestroyed], played);
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
