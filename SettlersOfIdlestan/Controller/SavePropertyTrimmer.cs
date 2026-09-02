using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SettlersOfIdlestan.Controller
{
    /// <summary>
    /// Résolveur de métadonnées appliqué aux options d'écriture de <see cref="SaveController"/>.
    /// Il retire de la sauvegarde les propriétés qui ne peuvent de toute façon pas en revenir, pour
    /// deux raisons distinctes.
    ///
    /// <para>
    /// <b>1. Le <see cref="JsonIgnoreAttribute"/> perdu par redéfinition.</b> System.Text.Json lit
    /// ses attributs avec <c>inherit: false</c> : une propriété virtuelle marquée
    /// <c>[JsonIgnore]</c> sur <c>Building</c> ou <c>IslandFeature</c> redevient sérialisée dès
    /// qu'un sous-type la redéfinit. Toutes les constantes par sous-type y passaient
    /// (<c>DiscoveredEventType</c>, <c>BlocksHarvest</c>, <c>ShouldRenderIcon</c>, <c>MaxHp</c>,
    /// <c>SvgIconResourceName</c>...) : 348 Ko sur les 1,6 Mo d'une sauvegarde de fin de partie.
    /// Répéter l'attribut sur chaque redéfinition ne tiendrait pas — le prochain monstre ajouté
    /// réintroduirait la fuite en silence.
    /// </para>
    ///
    /// <para>
    /// <b>2. Les propriétés que la lecture ne peut pas restaurer.</b> Sans accesseur d'écriture ni
    /// paramètre de constructeur correspondant, System.Text.Json ignore la propriété à la lecture :
    /// elle est écrite à chaque sauvegarde et relue par personne. C'est le cas de
    /// <c>Building.RequiredUniqueBuildingType</c> (126 Ko à lui seul), des <c>Is...Active</c>
    /// d'<c>AutomationSettings</c>, de <c>City.Level</c>, <c>City.MaxDefense</c>...
    /// </para>
    ///
    /// <para>
    /// <b>Ce que ce résolveur ne touche pas.</b> Une propriété sans accesseur d'écriture reste
    /// sérialisée si son nom correspond à un paramètre d'un constructeur du type ou d'un de ses
    /// types de base : elle est alors bel et bien restaurée, par le constructeur
    /// (<see cref="Model.Buildings.Building.Type"/>, discriminant lu par
    /// <c>BuildingJsonConverter</c>, en est le cas critique). Les collections exposées en lecture
    /// seule ne sont pas concernées non plus : le modèle les sérialise déjà par un jumeau
    /// <c>...Serialized</c> à setter privé (voir <c>Civilization.CitiesSerialized</c>).
    /// </para>
    ///
    /// <para>
    /// Appliqué à l'écriture seulement. La lecture garde le résolveur par défaut : les anciennes
    /// sauvegardes contiennent encore ces propriétés et continuent d'être relues exactement comme
    /// avant — c'est-à-dire en les ignorant. Rien n'est donc à remapper, et une sauvegarde produite
    /// par cette version se recharge dans les précédentes.
    /// </para>
    /// </summary>
    public static class SavePropertyTrimmer
    {
        public static readonly IJsonTypeInfoResolver Resolver =
            new DefaultJsonTypeInfoResolver { Modifiers = { Trim } };

        private static void Trim(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                if (typeInfo.Properties[i].AttributeProvider is not PropertyInfo property) continue;
                if (IsIgnoredByBaseDeclaration(property) || !CanBeRestored(typeInfo.Type, property))
                    typeInfo.Properties.RemoveAt(i);
            }
        }

        /// <summary>
        /// True si la déclaration de base de cette propriété virtuelle porte
        /// <see cref="JsonIgnoreAttribute"/> — l'intention était bien de ne pas la sauvegarder.
        /// </summary>
        private static bool IsIgnoredByBaseDeclaration(PropertyInfo property)
        {
            var getter = property.GetGetMethod(nonPublic: true);
            if (getter is null || !getter.IsVirtual) return false;

            var baseGetter = getter.GetBaseDefinition();
            if (baseGetter == getter) return false;

            var baseProperty = baseGetter.DeclaringType?.GetProperty(
                property.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return baseProperty?.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false) is not null;
        }

        private static bool CanBeRestored(Type owner, PropertyInfo property)
            => property.SetMethod is not null || MatchesConstructorParameter(owner, property.Name);

        // Un type concret n'a pas forcément le constructeur qui porte le paramètre : Seaport() est
        // sans paramètre, mais c'est bien Building(BuildingType type, ...) qui restaure Type. On
        // remonte donc toute la hiérarchie.
        private static readonly ConcurrentDictionary<Type, HashSet<string>> _constructorParameterNames = new();

        private static bool MatchesConstructorParameter(Type owner, string propertyName)
            => _constructorParameterNames
                .GetOrAdd(owner, CollectConstructorParameterNames)
                .Contains(propertyName);

        private static HashSet<string> CollectConstructorParameterNames(Type owner)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (Type? type = owner; type is not null && type != typeof(object); type = type.BaseType)
                foreach (var constructor in type.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    foreach (var parameter in constructor.GetParameters())
                        if (parameter.Name is { } name) names.Add(name);
            return names;
        }
    }
}
