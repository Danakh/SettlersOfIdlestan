namespace SettlersOfIdlestanSkia.Services.Audio;

/// <summary>
/// Sortie audio du head. Pendant de <see cref="IFileSystemService"/> : la couche Skia dit
/// <i>quoi</i> jouer et <i>quand</i> (voir <see cref="GameAudioService"/>), chaque head dit
/// <i>comment</i> — miniaudio sur le bureau, Web Audio dans le navigateur.
///
/// <para>Le contrat passe volontairement le <b>WAV brut</b> plutôt que du PCM décodé : le
/// navigateur veut nourrir <c>decodeAudioData</c>, qui prend le fichier tel quel, alors que le
/// bureau mixe lui-même des échantillons flottants (voir <see cref="WavSample"/>). Décoder en
/// amont pour tout le monde obligerait le head navigateur à ré-encoder ce qu'il vient de recevoir.</para>
///
/// <para>Un head sans son n'implémente rien : <see cref="GameAudioService"/> accepte un service
/// nul et devient muet, ce qui est le cas d'iOS, des tests et des outils hors-jeu.</para>
/// </summary>
public interface IAudioService : IDisposable
{
    /// <summary>
    /// Charge (ou remplace) l'échantillon d'un son. Appelé une fois par son au démarrage, avant
    /// tout <see cref="Play"/>. Un WAV illisible doit être ignoré sans lever : un bruitage absent
    /// rend le jeu silencieux, il ne doit pas l'empêcher de démarrer.
    /// </summary>
    void Load(SoundId id, byte[] wav);

    /// <summary>
    /// Joue un son déjà chargé, sans attendre la fin du précédent : deux appels rapprochés se
    /// superposent. <paramref name="volume"/> vaut 0 à 1 et intègre déjà le volume général du
    /// joueur — l'implémentation ne relit pas les réglages.
    ///
    /// <para>Appelé depuis le thread de jeu, jamais depuis le thread audio.</para>
    /// </summary>
    void Play(SoundId id, float volume);
}
