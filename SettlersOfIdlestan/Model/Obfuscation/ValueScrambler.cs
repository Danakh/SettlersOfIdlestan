using System;
using System.Runtime.CompilerServices;

namespace SettlersOfIdlestan.Model.Obfuscation;

/// <summary>
/// Fournit les deux ingrédients du brouillage des quantités lisibles par le joueur
/// (<see cref="ObfInt"/>, <see cref="ObfLong"/>) : une clé multiplicative tirée au démarrage du
/// processus et une source de bruit rapide pour découper chaque valeur en deux moitiés.
///
/// <para><b>Clé multiplicative.</b> Une quantité <c>v</c> n'est jamais stockée telle quelle : c'est
/// <c>v * Key</c> (multiplication non contrôlée, donc modulo 2^32 ou 2^64) qui est réparti entre les
/// deux moitiés, et la lecture remultiplie par l'inverse modulaire. Sans elle, deux entiers adjacents
/// dont la somme vaut la quantité affichée forment un motif qu'un outil de triche sait chercher ;
/// avec elle, retrouver la paire suppose de connaître une clé qui change à chaque lancement du jeu.
/// <c>Key</c> est impair, donc inversible modulo une puissance de deux, et la valeur 0 reste stockée
/// comme une paire de somme nulle — c'est ce qui rend <c>default(ObfInt)</c> correctement égal à 0.</para>
///
/// <para><b>Bruit.</b> Volontairement séparé de <see cref="Model.Game.GamePRNG"/> : le découpage ne
/// doit tirer aucun nombre de la génération de partie, sous peine de désynchroniser le jeu de son
/// seed. Rien de ce qu'il produit n'est observable depuis le modèle — seule la somme l'est — donc un
/// générateur non déterministe et propre au thread convient et évite tout partage d'état.</para>
/// </summary>
internal static class ValueScrambler
{
    /// <summary>Clé impaire du processus, 32 bits.</summary>
    internal static readonly uint Key32;

    /// <summary>Inverse modulaire de <see cref="Key32"/> modulo 2^32.</summary>
    internal static readonly uint KeyInverse32;

    /// <summary>Clé impaire du processus, 64 bits.</summary>
    internal static readonly ulong Key64;

    /// <summary>Inverse modulaire de <see cref="Key64"/> modulo 2^64.</summary>
    internal static readonly ulong KeyInverse64;

    static ValueScrambler()
    {
        // Guid.NewGuid : entropie disponible partout, y compris sur la tête WebAssembly, sans tirer
        // de dépendance sur System.Security.Cryptography. Le « | 1 » force l'imparité exigée par
        // l'inversion modulaire.
        var g1 = Guid.NewGuid().ToByteArray();
        var g2 = Guid.NewGuid().ToByteArray();
        Key32 = BitConverter.ToUInt32(g1, 0) | 1u;
        Key64 = BitConverter.ToUInt64(g2, 0) | 1ul;
        KeyInverse32 = Inverse32(Key32);
        KeyInverse64 = Inverse64(Key64);
    }

    /// <summary>
    /// Inverse de <paramref name="k"/> (impair) modulo 2^32, par itération de Newton : <c>inv = k</c>
    /// est déjà juste sur 3 bits pour tout impair, et chaque passe double le nombre de bits corrects
    /// (3 → 6 → 12 → 24 → 48), d'où les quatre passes.
    /// </summary>
    private static uint Inverse32(uint k)
    {
        uint inv = k;
        unchecked
        {
            for (int i = 0; i < 4; i++) inv *= 2u - k * inv;
        }
        return inv;
    }

    /// <summary>Même itération que <see cref="Inverse32"/>, une passe de plus pour couvrir 64 bits.</summary>
    private static ulong Inverse64(ulong k)
    {
        ulong inv = k;
        unchecked
        {
            for (int i = 0; i < 5; i++) inv *= 2ul - k * inv;
        }
        return inv;
    }

    /// <summary>
    /// État du xorshift propre au thread. Un état nul est absorbant pour un xorshift : il est
    /// réensemencé à la première utilisation sur chaque thread, et le « | 1 » garantit qu'il ne
    /// puisse pas l'être avec zéro.
    /// </summary>
    [ThreadStatic] private static uint _noiseState;

    /// <summary>
    /// Bruit 32 bits, xorshift. Appelé une fois par écriture de quantité — y compris sur les chemins
    /// chauds du tick — d'où les trois décalages plutôt qu'un appel à <see cref="Random"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int NextNoise32()
    {
        uint x = _noiseState;
        if (x == 0)
        {
            x = unchecked((uint)(Environment.TickCount64 ^ (Environment.CurrentManagedThreadId * 0x9E3779B9L))) | 1u;
        }
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _noiseState = x;
        return unchecked((int)x);
    }

    /// <summary>Bruit 64 bits, deux tours du même xorshift.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long NextNoise64()
    {
        ulong high = unchecked((uint)NextNoise32());
        ulong low = unchecked((uint)NextNoise32());
        return unchecked((long)((high << 32) | low));
    }
}
