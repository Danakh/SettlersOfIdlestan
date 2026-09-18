using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.Obfuscation;

/// <summary>
/// Équivalent 64 bits d'<see cref="ObfInt"/>, pour les quantités lisibles par le joueur qui
/// dépassent l'entier 32 bits (points de recherche). Même principe et mêmes garanties : deux moitiés
/// tirées au hasard à chaque écriture, dont la somme démultipliée par la clé du processus redonne la
/// valeur affichée.
///
/// <para>Une seule conversion implicite sortante, vers <c>long</c> : ouvrir aussi vers <c>int</c>
/// perdrait des bits en silence sur les grandes valeurs, exactement ce que <c>long</c> était là pour
/// éviter. La conversion entrante depuis <c>int</c> existe, elle, puisqu'elle est sans perte.</para>
/// </summary>
[JsonConverter(typeof(ObfLongJsonConverter))]
public readonly struct ObfLong : IEquatable<ObfLong>, IComparable<ObfLong>, IComparable, IFormattable
{
    private readonly long _a;
    private readonly long _b;

    private ObfLong(long a, long b)
    {
        _a = a;
        _b = b;
    }

    /// <summary>La quantité en clair ; <c>default(ObfLong)</c> vaut 0, comme pour <see cref="ObfInt"/>.</summary>
    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((long)((ulong)(_a + _b) * ValueScrambler.KeyInverse64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObfLong From(long value)
    {
        long scrambled = unchecked((long)((ulong)value * ValueScrambler.Key64));
        long a = ValueScrambler.NextNoise64();
        return new ObfLong(a, unchecked(scrambled - a));
    }

    public static implicit operator long(ObfLong value) => value.Value;

    public static implicit operator ObfLong(long value) => From(value);

    public static implicit operator ObfLong(int value) => From(value);

    public bool Equals(ObfLong other) => Value == other.Value;

    public override bool Equals(object? obj) => obj switch
    {
        ObfLong other => Value == other.Value,
        long other => Value == other,
        int other => Value == other,
        _ => false,
    };

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(ObfLong other) => Value.CompareTo(other.Value);

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ObfLong other => Value.CompareTo(other.Value),
        long other => Value.CompareTo(other),
        int other => Value.CompareTo((long)other),
        _ => throw new ArgumentException($"Cannot compare {nameof(ObfLong)} with {obj.GetType()}.", nameof(obj)),
    };

    public override string ToString() => Value.ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);
}
