using System.Reflection;
using System.Text;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestanSkia.Services.Audio;

/// <summary>
/// Charge les bruitages embarqués dans cet assembly et les remet au head
/// (<see cref="IAudioService.Load"/>).
///
/// <para>Un fichier manquant ou illisible est journalisé et ignoré : le son concerné reste muet,
/// mais le jeu démarre. C'est ce qui permet de remplacer les WAV un par un sans jamais risquer
/// un écran noir sur une ressource mal exportée.</para>
/// </summary>
public static class SoundBank
{
    private const string ResourceFolder = "Resources.sounds.";

    /// <summary>
    /// Nom du fichier attendu pour un son : la valeur de l'enum en snake_case
    /// (<c>ToastWarning</c> → <c>toast_warning.wav</c>). C'est la seule convention qui relie
    /// <see cref="SoundId"/> aux fichiers produits par <c>assets/sounds/generate_sounds.py</c>.
    /// </summary>
    public static string FileName(SoundId id)
    {
        var name = id.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (i > 0) builder.Append('_');
                builder.Append(char.ToLowerInvariant(name[i]));
            }
            else builder.Append(name[i]);
        }
        builder.Append(".wav");
        return builder.ToString();
    }

    /// <summary>Lit le WAV embarqué d'un son, ou null s'il est absent.</summary>
    public static byte[]? Read(SoundId id)
    {
        var assembly = typeof(SoundBank).Assembly;
        string resource = $"{assembly.GetName().Name}.{ResourceFolder}{FileName(id)}";

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null) return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Charge tous les sons dans le service audio du head. Renvoie le nombre chargé.</summary>
    public static int LoadAll(IAudioService audio)
    {
        int loaded = 0;
        foreach (var id in Enum.GetValues<SoundId>())
        {
            var wav = Read(id);
            if (wav == null)
            {
                GameLog.Error(nameof(SoundBank), nameof(LoadAll), $"Bruitage absent : {FileName(id)}");
                continue;
            }

            audio.Load(id, wav);
            loaded++;
        }
        return loaded;
    }
}
