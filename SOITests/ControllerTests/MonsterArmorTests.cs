using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests de l'armure des monstres : Dragon/MinorDemon/MajorDemon ont Armor = 1 fixe,
    /// tous les autres monstres n'ont pas d'armure. La réduction de dégâts qui en découle
    /// (MonsterFeature.ApplyArmorReduction) vaut Armor/2, arrondi de façon probabiliste sur
    /// le reste fractionnaire pour que la moyenne sur la durée reste exacte.
    /// </summary>
    public class MonsterArmorTests
    {
        private static readonly HexCoord Origin = new(0, 0, 0);

        // ── Valeur de l'armure par type de monstre ──────────────────────────────

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void Dragon_MinorDemon_MajorDemon_ArmorIsFixedAtOne(int level)
        {
            Assert.Equal(1, new Dragon(Origin, level).Armor);
            Assert.Equal(1, new MinorDemon(Origin, level).Armor);
            Assert.Equal(1, new MajorDemon(Origin, level).Armor);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void Troll_Ogre_HaveNoArmor(int level)
        {
            Assert.Equal(0, new Troll(Origin, level).Armor);
            Assert.Equal(0, new Ogre(Origin, level).Armor);
        }

        [Fact]
        public void Bandit_Rats_HaveNoArmor()
        {
            Assert.Equal(0, new Bandit(Origin).Armor);
            Assert.Equal(0, new Rats(Origin).Armor);
        }

        // ── ApplyArmorReduction : cas déterministes ─────────────────────────────

        [Fact]
        public void ApplyArmorReduction_ZeroArmor_NoReduction()
        {
            var prng = new GamePRNG(1);
            Assert.Equal(3, MonsterFeature.ApplyArmorReduction(3, 0, prng));
        }

        [Fact]
        public void ApplyArmorReduction_ZeroDamage_StaysZero()
        {
            var prng = new GamePRNG(1);
            Assert.Equal(0, MonsterFeature.ApplyArmorReduction(0, 4, prng));
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(4, 2)]
        [InlineData(6, 3)]
        public void ApplyArmorReduction_EvenArmor_AlwaysReducesByExactHalf(int armor, int expectedReduction)
        {
            // Armure paire → moitié exacte, pas de composante aléatoire : même résultat quel que soit le seed.
            for (int seed = 0; seed < 20; seed++)
            {
                var prng = new GamePRNG(seed);
                Assert.Equal(10 - expectedReduction, MonsterFeature.ApplyArmorReduction(10, armor, prng));
            }
        }

        [Fact]
        public void ApplyArmorReduction_NeverGoesBelowZero()
        {
            var prng = new GamePRNG(1);
            Assert.Equal(0, MonsterFeature.ApplyArmorReduction(1, 100, prng));
        }

        // ── ApplyArmorReduction : cas probabiliste (armure impaire / à mi-palier) ──

        /// <summary>
        /// Armure 1 (Dragon niveau 1) : réduction = 0 ou 1 selon un tirage 50/50. Un seul PRNG est
        /// réutilisé sur de nombreux tirages successifs (comme en jeu, où la même instance persiste
        /// sur toute une partie) — semer un nouveau PRNG à chaque essai avec un seed croissant
        /// biaiserait fortement l'échantillon : ce générateur Lehmer ne "mélange" bien qu'après
        /// plusieurs appels, pas dès le premier tirage d'un seed proche de zéro.
        /// </summary>
        [Fact]
        public void ApplyArmorReduction_OddArmor_IsRoughlyHalfOnAverage()
        {
            var prng = new GamePRNG(12345);
            int reducedCount = 0;
            const int trials = 2000;
            for (int i = 0; i < trials; i++)
            {
                int result = MonsterFeature.ApplyArmorReduction(1, 1, prng);
                Assert.True(result == 0 || result == 1);
                if (result == 0) reducedCount++;
            }

            double ratio = reducedCount / (double)trials;
            Assert.InRange(ratio, 0.4, 0.6);
        }

        /// <summary>Armure 0.5 (Troll/Ogre niveau 1) : réduction = 0 ou 1 avec 25% de chance de 1 (reste fractionnaire 0.25).</summary>
        [Fact]
        public void ApplyArmorReduction_HalfArmor_ReducesAboutQuarterOfTheTime()
        {
            var prng = new GamePRNG(12345);
            int reducedCount = 0;
            const int trials = 2000;
            for (int i = 0; i < trials; i++)
            {
                int result = MonsterFeature.ApplyArmorReduction(1, 0.5, prng);
                Assert.True(result == 0 || result == 1);
                if (result == 0) reducedCount++; // rawDamage=1, donc result==0 signifie qu'une réduction de 1 a eu lieu
            }

            double ratio = reducedCount / (double)trials;
            Assert.InRange(ratio, 0.15, 0.35);
        }
    }
}
