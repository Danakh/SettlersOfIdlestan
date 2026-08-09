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
}
