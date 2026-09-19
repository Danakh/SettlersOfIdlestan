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

    /// <summary>Ville ou avant-poste fondé.</summary>
    CityFounded,

    /// <summary>
    /// Une de nos villes vient de tomber — prise par une civilisation ennemie ou rasée par un
    /// monstre. Le seul son du jeu qui annonce une perte définitive de territoire : il est plus
    /// long et plus grave que <see cref="BuildingDestroyed"/>, qui ne coûte qu'un bâtiment.
    /// </summary>
    CityLost,
}

/// <summary>
/// Famille d'un bruitage. Le joueur coupe une famille entière depuis l'onglet Son des réglages,
/// sans toucher aux autres ni à l'interrupteur général — chaque famille a sa case dans
/// <c>GameSettings</c> (<c>SoundToastEnabled</c>, <c>SoundAchievementEnabled</c>,
/// <c>SoundCombatEnabled</c>, <c>SoundCityEnabled</c>, <c>SoundCityLostEnabled</c>,
/// <c>SoundHarvestEnabled</c>).
///
/// <para>Chaque <see cref="SoundId"/> appartient à exactement une famille : la table est dans
/// <c>GameAudioService.Category</c>, et <c>SoundCategoryTests</c> échoue si une valeur y manque.
/// Il n'y a volontairement pas de famille fourre-tout : un son ajouté sans la sienne lève, plutôt
/// que de se ranger en silence là où aucune case ne le gouverne.</para>
/// </summary>
public enum SoundCategory
{
    /// <summary>Notifications : les quatre toasts.</summary>
    Toast,

    /// <summary>
    /// La fanfare des succès. Sous-famille des notifications : sa case se grise avec la leur, et
    /// couper les notifications la coupe aussi — un succès <i>est</i> une notification, la case
    /// séparée ne sert qu'à garder les toasts sans la fanfare.
    /// </summary>
    Achievement,

    /// <summary>Combat : coups portés, coups reçus, bâtiment détruit au combat.</summary>
    Combat,

    /// <summary>Fondation de villes et d'avant-postes.</summary>
    City,

    /// <summary>
    /// Perte d'une de nos villes. Famille séparée du combat, dont elle est pourtant l'issue : les
    /// bruitages de combat sont une ambiance qu'un joueur peut vouloir taire en fin de partie,
    /// alors que la chute d'une ville est une annonce qu'il veut entendre même alors. Séparée
    /// aussi de la fondation, pour qu'un joueur qui coupe les rafales d'avant-postes automatiques
    /// ne perde pas du même coup l'alerte qui compte.
    /// </summary>
    CityLost,

    /// <summary>Récolte manuelle — le son le plus joué de la partie.</summary>
    Harvest,
}
