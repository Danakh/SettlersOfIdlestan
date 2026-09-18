using System;
using System.Collections.Generic;
using System.Text.Json;
using SettlersOfIdlestan.Model.Obfuscation;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Le contrat d'<see cref="ObfInt"/> et <see cref="ObfLong"/> : se comporter exactement comme
/// l'entier qu'ils remplacent, tout en ne le laissant jamais apparaître tel quel dans les deux mots
/// stockés. Les deux moitiés étant privées, elles sont relues par réflexion — c'est précisément la
/// vue qu'aurait un outil de triche sur la mémoire du processus.
/// </summary>
public class ObfuscatedValueTests
{
    private static (int A, int B) Halves(ObfInt value)
    {
        var fields = typeof(ObfInt).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.Equal(2, fields.Length);
        object boxed = value;
        return ((int)fields[0].GetValue(boxed)!, (int)fields[1].GetValue(boxed)!);
    }

    private static (long A, long B) LongHalves(ObfLong value)
    {
        var fields = typeof(ObfLong).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.Equal(2, fields.Length);
        object boxed = value;
        return ((long)fields[0].GetValue(boxed)!, (long)fields[1].GetValue(boxed)!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(1500)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ObfInt_RoundTripsEveryValue(int value)
    {
        ObfInt obfuscated = value;
        Assert.Equal(value, obfuscated.Value);
        Assert.Equal(value, (int)obfuscated);
    }

    [Fact]
    public void ObfInt_RoundTripsOnLargeRandomSample()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 100_000; i++)
        {
            int value = rng.Next(int.MinValue, int.MaxValue);
            ObfInt obfuscated = value;
            Assert.Equal(value, obfuscated.Value);
        }
    }

    [Fact]
    public void ObfInt_DefaultIsZero()
    {
        ObfInt uninitialized = default;
        Assert.Equal(0, uninitialized.Value);

        var array = new ObfInt[3];
        Assert.Equal(0, array[1].Value);
    }

    /// <summary>Un balayage « cherche la valeur affichée » ne doit trouver aucune des deux moitiés.</summary>
    [Fact]
    public void ObfInt_NeverStoresThePlainValue()
    {
        for (int value = 1; value < 5000; value++)
        {
            var (a, b) = Halves(value);
            Assert.NotEqual(value, a);
            Assert.NotEqual(value, b);
        }
    }

    /// <summary>
    /// Ni la somme nue des deux moitiés : c'est ce que la clé multiplicative du processus achète
    /// face à un outil qui chercherait deux mots adjacents totalisant la quantité affichée.
    /// </summary>
    [Fact]
    public void ObfInt_HalvesDoNotSumToThePlainValue()
    {
        int matches = 0;
        for (int value = 1; value < 5000; value++)
        {
            var (a, b) = Halves(value);
            if (unchecked(a + b) == value) matches++;
        }
        Assert.Equal(0, matches);
    }

    /// <summary>
    /// Le point de la protection : réécrire la même quantité redécoupe tout, donc un balayage par
    /// différence ne peut pas suivre une adresse. On exige aussi que les deux moitiés changent de
    /// signe au fil des tirages, sans quoi le signe seul trahirait la paire.
    /// </summary>
    [Fact]
    public void ObfInt_ResplitsOnEveryWrite()
    {
        const int Value = 1500;
        var seenA = new HashSet<int>();
        bool sawPositiveA = false, sawNegativeA = false;
        bool sawPositiveB = false, sawNegativeB = false;

        for (int i = 0; i < 1000; i++)
        {
            var (a, b) = Halves(Value);
            seenA.Add(a);
            sawPositiveA |= a > 0;
            sawNegativeA |= a < 0;
            sawPositiveB |= b > 0;
            sawNegativeB |= b < 0;
        }

        Assert.True(seenA.Count > 990, $"Le découpage se répète : {seenA.Count} valeurs distinctes sur 1000.");
        Assert.True(sawPositiveA && sawNegativeA, "La première moitié ne change jamais de signe.");
        Assert.True(sawPositiveB && sawNegativeB, "La seconde moitié ne change jamais de signe.");
    }

    /// <summary>Deux incréments successifs ne doivent pas faire varier une moitié de l'incrément.</summary>
    [Fact]
    public void ObfInt_IncrementDoesNotShowUpAsAnIncrementInEitherHalf()
    {
        int reveals = 0;
        for (int value = 1; value < 5000; value++)
        {
            var (a0, b0) = Halves(value);
            var (a1, b1) = Halves(value + 1);
            if (unchecked(a1 - a0) == 1 || unchecked(b1 - b0) == 1) reveals++;
        }
        Assert.True(reveals < 10, $"L'incrément transparaît dans une moitié {reveals} fois sur 5000.");
    }

    [Fact]
    public void ObfInt_BehavesLikeIntInArithmeticAndComparisons()
    {
        ObfInt stock = 100;
        stock += 50;
        Assert.Equal<int>(150, stock);
        stock -= 30;
        Assert.Equal<int>(120, stock);
        stock++;
        Assert.Equal<int>(121, stock);

        Assert.True(stock > 100);
        Assert.True(stock < 200);
        Assert.False(stock == 100);
        Assert.True(stock != 100);
        Assert.Equal(121, Math.Min(stock, 500));
        Assert.Equal(500, Math.Max(stock, 500));

        ObfInt other = 121;
        Assert.True(stock == other);
        Assert.Equal<int>(stock, other);
        Assert.Equal(stock.GetHashCode(), other.GetHashCode());
    }

    [Fact]
    public void ObfInt_FormatsLikeInt()
    {
        ObfInt stock = 1234;
        Assert.Equal("1234", stock.ToString());
        Assert.Equal("1234", $"{stock}");
        Assert.Equal(1234.ToString("N0"), stock.ToString("N0", null));
        Assert.Equal(string.Format("{0:N0}", 1234), string.Format("{0:N0}", stock));
    }

    [Fact]
    public void ObfInt_SerializesAsAPlainNumber()
    {
        ObfInt stock = 4321;
        string json = JsonSerializer.Serialize(stock);
        Assert.Equal("4321", json);
        Assert.Equal(4321, JsonSerializer.Deserialize<ObfInt>(json).Value);
    }

    /// <summary>Une sauvegarde d'avant le brouillage porte un nombre nu et doit se relire telle quelle.</summary>
    [Fact]
    public void ObfInt_ReadsBackLegacyDictionariesOfPlainNumbers()
    {
        const string LegacyJson = "{\"Wood\":120,\"Brick\":7}";
        var read = JsonSerializer.Deserialize<Dictionary<string, ObfInt>>(LegacyJson)!;
        Assert.Equal<int>(120, read["Wood"]);
        Assert.Equal<int>(7, read["Brick"]);
        Assert.Equal(LegacyJson, JsonSerializer.Serialize(read));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(1_000_000_007L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void ObfLong_RoundTripsEveryValue(long value)
    {
        ObfLong obfuscated = value;
        Assert.Equal(value, obfuscated.Value);
        Assert.Equal(value, (long)obfuscated);
    }

    [Fact]
    public void ObfLong_RoundTripsOnLargeRandomSample()
    {
        var rng = new Random(5678);
        var buffer = new byte[8];
        for (int i = 0; i < 100_000; i++)
        {
            rng.NextBytes(buffer);
            long value = BitConverter.ToInt64(buffer, 0);
            ObfLong obfuscated = value;
            Assert.Equal(value, obfuscated.Value);
        }
    }

    [Fact]
    public void ObfLong_DefaultIsZero()
    {
        ObfLong uninitialized = default;
        Assert.Equal(0L, uninitialized.Value);
    }

    [Fact]
    public void ObfLong_NeverStoresThePlainValue()
    {
        for (long value = 1; value < 5000; value++)
        {
            var (a, b) = LongHalves(value);
            Assert.NotEqual(value, a);
            Assert.NotEqual(value, b);
            Assert.NotEqual(value, unchecked(a + b));
        }
    }

    [Fact]
    public void ObfLong_BehavesLikeLongInArithmeticAndComparisons()
    {
        ObfLong points = 4_000_000_000L;
        points += 1;
        Assert.Equal<long>(4_000_000_001L, points);
        Assert.True(points > int.MaxValue);
        Assert.Equal("4000000001", points.ToString());
        Assert.Equal("4000000001", JsonSerializer.Serialize(points));
    }
}
