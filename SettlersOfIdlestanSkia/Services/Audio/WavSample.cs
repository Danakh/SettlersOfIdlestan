using System.Buffers.Binary;

namespace SettlersOfIdlestanSkia.Services.Audio;

/// <summary>
/// Un bruitage décodé : des échantillons mono en flottants, prêts à être sommés dans le tampon de
/// sortie. Sert aux heads qui mixent eux-mêmes (le bureau) ; le navigateur, lui, reçoit le WAV
/// brut et laisse <c>decodeAudioData</c> faire ce travail.
///
/// <para>Le décodage est volontairement minimal — assez pour lire ce que produit
/// <c>assets/sounds/generate_sounds.py</c> (PCM 16 bits mono) et ce qu'exporte n'importe quel
/// éditeur audio si l'un des fichiers est un jour remplacé par un vrai enregistrement : PCM
/// entier 8/16/24/32 bits ou flottant 32 bits, mono ou multicanal, n'importe quelle fréquence.
/// Tout le reste (ADPCM, MP3 dans un conteneur WAV) est refusé plutôt que lu de travers.</para>
///
/// <para>Mono par choix : les bruitages du jeu n'ont pas de position dans l'espace, et un
/// échantillon mono se mixe vers autant de canaux que la carte son en réclame sans conversion.
/// Un fichier stéréo est donc replié à la lecture.</para>
/// </summary>
public sealed class WavSample
{
    private WavSample(float[] samples, int sampleRate)
    {
        Samples = samples;
        SampleRate = sampleRate;
    }

    /// <summary>Échantillons mono, dans [-1, 1].</summary>
    public float[] Samples { get; }

    public int SampleRate { get; }

    /// <summary>
    /// Décode un WAV. Renvoie null si le fichier n'est pas un PCM lisible — l'appelant se contente
    /// alors de ne pas jouer ce son (voir <see cref="IAudioService.Load"/>).
    /// </summary>
    public static WavSample? TryDecode(ReadOnlySpan<byte> wav)
    {
        // En-tête RIFF : "RIFF" <taille> "WAVE", puis une suite de chunks <id><taille><données>.
        if (wav.Length < 44) return null;
        if (!wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8)) return null;

        int format = 0, channels = 0, sampleRate = 0, bits = 0;
        ReadOnlySpan<byte> data = default;

        int offset = 12;
        while (offset + 8 <= wav.Length)
        {
            var id = wav.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(offset + 4, 4));
            int body = offset + 8;

            // Une taille annoncée plus grande que le fichier arrive sur un enregistrement
            // interrompu : on lit ce qui existe réellement plutôt que de refuser le fichier.
            int available = Math.Min((int)Math.Min(size, int.MaxValue), wav.Length - body);
            if (available < 0) return null;

            if (id.SequenceEqual("fmt "u8) && available >= 16)
            {
                format     = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body, 2));
                channels   = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 2, 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(wav.Slice(body + 4, 4));
                bits       = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 14, 2));

                // WAVE_FORMAT_EXTENSIBLE (0xFFFE) range le vrai format dans son sous-GUID, dont
                // les deux premiers octets reprennent le code classique.
                if (format == 0xFFFE && available >= 26)
                    format = BinaryPrimitives.ReadUInt16LittleEndian(wav.Slice(body + 24, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = wav.Slice(body, available);
            }

            // Les chunks sont alignés sur 2 octets : une taille impaire est suivie d'un octet de
            // bourrage qui n'appartient à personne.
            offset = body + available + (available & 1);
        }

        if (channels <= 0 || sampleRate <= 0 || data.IsEmpty) return null;

        const int PcmInteger = 1;
        const int PcmFloat = 3;
        if (format != PcmInteger && format != PcmFloat) return null;
        if (format == PcmFloat && bits != 32) return null;

        int bytesPerSample = bits / 8;
        if (bytesPerSample is < 1 or > 4 || bits % 8 != 0) return null;

        int frameBytes = bytesPerSample * channels;
        int frames = data.Length / frameBytes;
        if (frames == 0) return null;

        var samples = new float[frames];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0f;
            int at = f * frameBytes;
            for (int c = 0; c < channels; c++)
                sum += ReadSample(data.Slice(at + c * bytesPerSample, bytesPerSample), format, bits);
            samples[f] = sum / channels;
        }

        return new WavSample(samples, sampleRate);
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, int format, int bits)
    {
        if (format == 3) return BinaryPrimitives.ReadSingleLittleEndian(bytes);

        return bits switch
        {
            // Seul le 8 bits est non signé dans un WAV : 128 y vaut le silence.
            8  => (bytes[0] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
            24 => ((bytes[0] | (bytes[1] << 8) | ((sbyte)bytes[2] << 16))) / 8388608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
            _  => 0f,
        };
    }

    /// <summary>
    /// Rééchantillonne vers la fréquence du périphérique, par interpolation linéaire. Suffisant
    /// ici : les fichiers du jeu sont déjà en 44100 Hz, donc le cas courant est l'identité, et un
    /// remplacement en 48000 Hz ne demande qu'un ratio proche de 1 sur des sons de moins d'une
    /// seconde — l'aliasing y est inaudible.
    /// </summary>
    public WavSample Resample(int targetSampleRate)
    {
        if (targetSampleRate <= 0 || targetSampleRate == SampleRate) return this;

        double ratio = (double)targetSampleRate / SampleRate;
        int count = Math.Max(1, (int)(Samples.Length * ratio));
        var output = new float[count];

        for (int i = 0; i < count; i++)
        {
            double source = i / ratio;
            int index = (int)source;
            float a = Samples[Math.Min(index, Samples.Length - 1)];
            float b = Samples[Math.Min(index + 1, Samples.Length - 1)];
            output[i] = a + (b - a) * (float)(source - index);
        }

        return new WavSample(output, targetSampleRate);
    }
}
