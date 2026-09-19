namespace SettlersOfIdlestanSkia.Services.Audio;

/// <summary>
/// Les bruitages du jeu. Chaque valeur correspond à un fichier
/// <c>Resources/sounds/{nom_en_snake_case}.wav</c>, embarqué dans cet assembly et chargé par
/// <see cref="SoundBank"/> — <c>ToastWarning</c> ↔ <c>toast_warning.wav</c>.
///
/// <para>Cet enum n'est jamais sérialisé : il vit dans la couche d'affichage, pas dans le modèle.
/// Il n'a donc pas besoin du <c>JsonStringEnumConverter</c> qu'exige tout enum du projet
/// SettlersOfIdlestan, et une valeur peut être ajoutée, renommée ou retirée sans toucher aux
/// sauvegardes.</para>
///
/// <para>Ajouter un son : une valeur ici, une fonction du même nom dans
/// <c>assets/sounds/generate_sounds.py</c> (relancer le script), et l'appel qui le joue.
/// <c>SoundBankTests</c> échoue si un WAV manque.</para>
/// </summary>
public enum SoundId
{
    /// <summary>Toast neutre : une information, ni bonne ni mauvaise.</summary>
    ToastInfo,

    /// <summary>Toast de menace : un monstre, un volcan, une civilisation hostile vient d'apparaître.</summary>
    ToastWarning,

    /// <summary>Toast de victoire : un monstre est tombé, une civilisation s'est éteinte, un monument est achevé.</summary>
    ToastVictory,

    /// <summary>Toast de perte : ville rasée, portail perdu, essences divines envolées.</summary>
    ToastLoss,

    /// <summary>Succès débloqué — la seule fanfare du jeu.</summary>
    Achievement,

    /// <summary>Récolte manuelle. Le son le plus joué de la partie : volontairement le plus discret.</summary>
    HarvestManual,

    /// <summary>Coup porté par nos soldats (ou notre Spire de Défense).</summary>
    AttackDealt,

    /// <summary>Coup encaissé par une de nos villes : monstre, civilisation ennemie ou éruption.</summary>
    AttackTaken,

    /// <summary>Un bâtiment d'une de nos villes vient d'être détruit au combat.</summary>
    BuildingDestroyed,

    /// <summary>Bâtiment construit dans une de nos villes.</summary>
    BuildingBuilt,

    /// <summary>Ville ou avant-poste fondé.</summary>
    CityFounded,
}

/// <summary>
/// Famille d'un bruitage. Le joueur coupe une famille entière depuis l'onglet Son des réglages,
/// sans toucher aux autres ni à l'interrupteur général — voir <c>GameSettings.SoundCombatEnabled</c>
/// et <c>GameSettings.SoundToastEnabled</c>.
///
/// <para>Chaque <see cref="SoundId"/> appartient à exactement une famille : la table est dans
/// <c>GameAudioService.Category</c>, et <c>SoundCategoryTests</c> échoue si une valeur y manque.</para>
/// </summary>
public enum SoundCategory
{
    /// <summary>Notifications : les quatre toasts et la fanfare de succès.</summary>
    Toast,

    /// <summary>Combat : coups portés, coups reçus, bâtiment détruit au combat.</summary>
    Combat,

    /// <summary>Le reste (récolte manuelle, construction, fondation) : suit le seul interrupteur général.</summary>
    Other,
}
