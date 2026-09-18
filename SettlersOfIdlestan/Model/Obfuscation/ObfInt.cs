using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Obfuscation;

/// <summary>
/// Entier 32 bits stocké sous forme brouillée, pour les quantités que le joueur lit à l'écran
/// (ressources, points de recherche, de prestige, divins, soldats, défense). Aucune de ces quantités
/// n'existe en clair en mémoire : la valeur est répartie entre deux moitiés dont la somme, une fois
/// démultipliée par la clé du processus, redonne le nombre affiché (voir <see cref="ValueScrambler"/>).
///
/// <para>Les deux moitiés sont retirées au hasard à <b>chaque</b> écriture : un même stock qui monte
/// de 1 fait varier les deux mots de façon indépendante et sans rapport avec l'incrément, signe
/// compris. Un balayage de type « cherche 1500 » ne trouve rien, et un balayage par différence
/// (« la valeur a augmenté ») ne peut pas suivre une paire dont les deux moitiés sautent dans tout
/// l'intervalle des entiers signés à chaque tick.</para>
///
/// <para><b>Transparent pour le reste du code</b> : les conversions implicites dans les deux sens
/// font que ce type se lit, s'écrit, se compare et se calcule exactement comme un <c>int</c>. Seule
/// la déclaration du champ change. Volontairement, ni <c>==</c> ni <c>&lt;</c> ne sont redéfinis :
/// les définir rendrait <c>quantité == 5</c> ambigu (l'opérateur du type contre celui de <c>int</c>,
/// chacun à une conversion implicite de distance), alors qu'en leur absence les deux opérandes
/// passent en <c>int</c> et retombent sur les opérateurs habituels.</para>
///
/// <para><b>Sérialisation</b> : le convertisseur n'écrit que la valeur réelle, en nombre JSON. La
/// sauvegarde est chiffrée par ailleurs, et cela garde les sauvegardes d'avant le brouillage
/// lisibles telles quelles.</para>
/// </summary>
[JsonConverter(typeof(ObfIntJsonConverter))]
public readonly struct ObfInt : IEquatable<ObfInt>, IComparable<ObfInt>, IComparable, IFormattable
{
    private readonly int _a;
    private readonly int _b;

    private ObfInt(int a, int b)
    {
        _a = a;
        _b = b;
    }

    /// <summary>
    /// La quantité en clair. <c>default(ObfInt)</c> vaut bien 0 : les deux moitiés valent alors zéro,
    /// et zéro multiplié par l'inverse de la clé reste zéro.
    /// </summary>
    public int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((int)((uint)(_a + _b) * ValueScrambler.KeyInverse32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObfInt From(int value)
    {
        int scrambled = unchecked((int)((uint)value * ValueScrambler.Key32));
        int a = ValueScrambler.NextNoise32();
        return new ObfInt(a, unchecked(scrambled - a));
    }

    public static implicit operator int(ObfInt value) => value.Value;

    public static implicit operator ObfInt(int value) => From(value);

    public bool Equals(ObfInt other) => Value == other.Value;

    public override bool Equals(object? obj) => obj switch
    {
        ObfInt other => Value == other.Value,
        int other => Value == other,
        _ => false,
    };

    /// <summary>
    /// Haché sur la valeur en clair, et non sur les deux moitiés : deux instances de même quantité
    /// mais de découpage différent doivent se ranger au même endroit dans un dictionnaire.
    /// </summary>
    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(ObfInt other) => Value.CompareTo(other.Value);

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ObfInt other => Value.CompareTo(other.Value),
        int other => Value.CompareTo(other),
        _ => throw new ArgumentException($"Cannot compare {nameof(ObfInt)} with {obj.GetType()}.", nameof(obj)),
    };

    public override string ToString() => Value.ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);
}
