using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Audio;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace SettlersOfIdlestanAvalonia.Desktop.Services;

/// <summary>
/// Sortie audio du head de bureau, via miniaudio (SoundFlow) : WASAPI sous Windows, CoreAudio sous
/// macOS, ALSA/PulseAudio sous Linux, choisis par la bibliothèque. Les binaires natifs voyagent
/// dans le paquet NuGet et sont déposés par RID à la publication, comme ceux de SkiaSharp.
///
/// <para><b>Le mélange est fait ici</b> plutôt que par les lecteurs de SoundFlow. Un bruitage du
/// jeu dure moins d'une seconde et doit pouvoir se superposer à lui-même ; créer un lecteur par
/// passage, puis le retirer du graphe à la fin, reviendrait à construire et détruire des objets
/// du graphe audio depuis le thread de jeu, plusieurs fois par seconde. Une somme d'échantillons
/// dans un tableau pré-décodé coûte moins et ne peut pas désynchroniser le graphe.</para>
///
/// <para><b>Rien de ce qui suit ne peut faire échouer le jeu.</b> Une machine sans carte son, un
/// serveur audio absent (Linux en session distante), un pilote qui refuse le format : chaque
/// étape est gardée, l'échec est journalisé une fois et le jeu continue en silence. C'est aussi
/// ce que fait <see cref="IsAvailable"/>.</para>
/// </summary>
public sealed class DesktopAudioService : IAudioService
{
    /// <summary>
    /// Voix simultanées. Au-delà, la plus ancienne est remplacée : un pic de bruitages ne doit ni
    /// allouer ni saturer la sortie. Douze suffit largement — les sons durent moins d'une seconde
    /// et <c>GameAudioService</c> impose déjà un intervalle minimum entre deux passages.
    /// </summary>
    private const int MaxVoices = 12;

    /// <summary>Format demandé au pilote. Rien ne garantit qu'il soit accordé — voir <see cref="_sampleRate"/>.</summary>
    private const int PreferredSampleRate = 44100;
    private const int PreferredChannels = 2;

    private readonly MiniAudioEngine? _engine;
    private readonly AudioPlaybackDevice? _device;
    private readonly SfxMixer? _mixer;

    /// <summary>
    /// Fréquence réellement accordée par le pilote, et non celle demandée : miniaudio négocie, et
    /// un périphérique qui impose 48 kHz ferait jouer des échantillons préparés pour 44,1 kHz
    /// presque un demi-ton trop bas, sans aucune erreur.
    /// </summary>
    private readonly int _sampleRate = PreferredSampleRate;

    private bool _disposed;

    public DesktopAudioService()
    {
        try
        {
            _engine = new MiniAudioEngine();

            var preferred = new AudioFormat
            {
                SampleRate = PreferredSampleRate,
                Channels = PreferredChannels,
                Format = SampleFormat.F32,
            };

            _device = _engine.InitializePlaybackDevice(null, preferred, new MiniAudioDeviceConfig());

            // Le composant prend le format accordé, pas celui demandé.
            _sampleRate = _device.Format.SampleRate;
            _mixer = new SfxMixer(_engine, _device.Format);
            _device.MasterMixer.AddComponent(_mixer);
            _device.Start();
        }
        catch (Exception ex)
        {
            // Journalisé une seule fois, ici : les appels suivants voient _mixer nul et ne font
            // rien, sans quoi un poste sans son remplirait le journal à chaque récolte.
            GameLog.Error(nameof(DesktopAudioService), nameof(DesktopAudioService), ex);
            Dispose();
        }
    }

    /// <summary>Vrai si le périphérique a démarré. Faux = jeu silencieux, sans autre conséquence.</summary>
    public bool IsAvailable => _mixer != null && !_disposed;

    public void Load(SoundId id, byte[] wav)
    {
        if (_mixer == null || _disposed) return;

        var sample = WavSample.TryDecode(wav);
        if (sample == null)
        {
            GameLog.Error(nameof(DesktopAudioService), nameof(Load), $"WAV illisible : {SoundBank.FileName(id)}");
            return;
        }

        // Rééchantillonné une fois au chargement : le faire à la lecture coûterait le même travail
        // à chaque passage, sur le thread audio, où tout dépassement s'entend en craquement.
        _mixer.Store(id, sample.Resample(_sampleRate).Samples);
    }

    public void Play(SoundId id, float volume) => _mixer?.Trigger(id, volume);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Ordre imposé : arrêter le périphérique avant de défaire le graphe, sinon le thread de
        // mixage de miniaudio peut encore entrer dans un composant en cours de destruction.
        try { _device?.Stop(); } catch (Exception ex) { GameLog.Error(nameof(DesktopAudioService), nameof(Dispose), ex); }
        try { _device?.Dispose(); } catch (Exception ex) { GameLog.Error(nameof(DesktopAudioService), nameof(Dispose), ex); }
        try { _engine?.Dispose(); } catch (Exception ex) { GameLog.Error(nameof(DesktopAudioService), nameof(Dispose), ex); }
    }

    /// <summary>
    /// Le composant du graphe audio : additionne les voix actives dans le tampon de sortie.
    ///
    /// <para><b>Deux threads s'y croisent</b> — le thread de jeu, qui déclenche les voix, et le
    /// thread de mixage de miniaudio, qui les consomme. Le verrou est assumé : il ne protège
    /// qu'une écriture de quelques champs et n'est pris qu'une fois par tampon (soit toutes les
    /// ~10 ms) côté audio. Une file sans verrou n'apporterait rien à cette échelle et se
    /// relirait mal.</para>
    /// </summary>
    private sealed class SfxMixer : SoundComponent
    {
        private struct Voice
        {
            public float[]? Samples;
            public int Position;
            public float Gain;
        }

        private readonly Dictionary<SoundId, float[]> _samples = [];
        private readonly Voice[] _voices = new Voice[MaxVoices];
        private readonly Lock _gate = new();

        /// Prochaine voix à écraser quand toutes sont occupées : simple rotation, donc la plus
        /// anciennement déclenchée.
        private int _nextVoice;

        public SfxMixer(AudioEngine engine, AudioFormat format) : base(engine, format) { }

        public override string Name { get; set; } = "Bruitages";

        public void Store(SoundId id, float[] samples)
        {
            lock (_gate) _samples[id] = samples;
        }

        public void Trigger(SoundId id, float volume)
        {
            lock (_gate)
            {
                if (!_samples.TryGetValue(id, out var samples)) return;

                int slot = -1;
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i].Samples == null) { slot = i; break; }

                if (slot < 0)
                {
                    slot = _nextVoice;
                    _nextVoice = (_nextVoice + 1) % _voices.Length;
                }

                _voices[slot] = new Voice { Samples = samples, Position = 0, Gain = Math.Clamp(volume, 0f, 1f) };
            }
        }

        protected override void GenerateAudio(Span<float> buffer, int channels)
        {
            buffer.Clear();
            if (channels <= 0) return;

            int frames = buffer.Length / channels;

            lock (_gate)
            {
                for (int v = 0; v < _voices.Length; v++)
                {
                    var samples = _voices[v].Samples;
                    if (samples == null) continue;

                    int position = _voices[v].Position;
                    float gain = _voices[v].Gain;
                    int count = Math.Min(frames, samples.Length - position);

                    for (int f = 0; f < count; f++)
                    {
                        // Les bruitages sont mono et sans position dans l'espace : le même
                        // échantillon part sur tous les canaux.
                        float value = samples[position + f] * gain;
                        int at = f * channels;
                        for (int c = 0; c < channels; c++) buffer[at + c] += value;
                    }

                    position += count;
                    if (position >= samples.Length) _voices[v] = default;
                    else _voices[v].Position = position;
                }

                // Écrêtage doux inutile ici : douze voix à moins de 0,8 de pic chacune ne
                // saturent qu'en théorie, et miniaudio limite déjà la sortie du périphérique.
                // On borne malgré tout, pour qu'un volume mal réglé ne produise pas de
                // distorsion franche.
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = Math.Clamp(buffer[i], -1f, 1f);
            }
        }
    }
}
