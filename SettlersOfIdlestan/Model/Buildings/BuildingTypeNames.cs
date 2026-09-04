namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Noms des <see cref="BuildingType"/> pré-calculés. Les modificateurs identifient leur cible par
/// <c>SubCategory</c>, une chaîne valant le nom de l'enum : le convertir avec <c>ToString()</c> à
/// chaque appel passe par la table de réflexion des enums et alloue à chaque fois, alors que les
/// chemins concernés (vitesse de récolte par bâtiment, niveau max par bâtiment) sont interrogés pour
/// chaque bâtiment de chaque ville à chaque tick.
/// </summary>
public static class BuildingTypeNames
{
    private static readonly string[] _names = BuildNames();

    private static string[] BuildNames()
    {
        var values = Enum.GetValues<BuildingType>();
        int max = 0;
        foreach (var v in values)
            if ((int)v > max) max = (int)v;

        var names = new string[max + 1];
        foreach (var v in values)
            names[(int)v] = v.ToString();
        return names;
    }

    /// <summary>Nom de l'enum, identique à <c>type.ToString()</c>, sans allocation ni réflexion.</summary>
    public static string Of(BuildingType type)
    {
        int index = (int)type;
        return (uint)index < (uint)_names.Length && _names[index] != null
            ? _names[index]
            : type.ToString();
    }

    private static readonly string[] _nameKeys = BuildKeys("_name");
    private static readonly string[] _descriptionKeys = BuildKeys("_desc");

    private static string[] BuildKeys(string suffix)
    {
        var keys = new string[_names.Length];
        for (int i = 0; i < _names.Length; i++)
            if (_names[i] != null)
                keys[i] = $"building_{_names[i].ToLowerInvariant()}{suffix}";
        return keys;
    }

    /// <summary>
    /// Clé de localisation du nom d'un bâtiment (<c>building_{type}_name</c>), pré-calculée.
    ///
    /// <para>Elle était construite dans le constructeur de <see cref="Building"/>, qui allouait donc
    /// quatre chaînes intermédiaires plus deux clés à chaque instanciation — y compris pour les
    /// prototypes que <c>BuildingController</c> crée à la volée pour répondre à « ce type est-il
    /// constructible ici ? ». Avec l'automatisation des guildes active, cette question est posée des
    /// milliers de fois par tranche de saut de temps, et ces chaînes étaient le premier poste
    /// d'allocation de la simulation.</para>
    /// </summary>
    public static string NameKeyOf(BuildingType type)
    {
        int index = (int)type;
        return (uint)index < (uint)_nameKeys.Length && _nameKeys[index] != null
            ? _nameKeys[index]
            : $"building_{Of(type).ToLowerInvariant()}_name";
    }

    /// <summary>Clé de localisation de la description (<c>building_{type}_desc</c>) — voir <see cref="NameKeyOf"/>.</summary>
    public static string DescriptionKeyOf(BuildingType type)
    {
        int index = (int)type;
        return (uint)index < (uint)_descriptionKeys.Length && _descriptionKeys[index] != null
            ? _descriptionKeys[index]
            : $"building_{Of(type).ToLowerInvariant()}_desc";
    }
}
