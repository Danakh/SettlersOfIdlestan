using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Magic;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace SOITests.ModelTests;

// Régression : une sauvegarde antérieure à la suppression d'une recherche, d'un rituel ou d'un
// pouvoir divin ne doit jamais faire échouer le chargement — voir TechnologyIdJsonConverter /
// RitualIdJsonConverter / AscensionPowerIdJsonConverter.
public class LegacyEnumRemapTests
{
    [Fact]
    public void TechnologyId_UnknownLegacyValue_DeserializesToRemoved()
    {
        var result = JsonSerializer.Deserialize<TechnologyId>("\"DeepLightRitual\"");

        Assert.Equal(TechnologyId.Removed, result);
        Assert.Null(TechnologyDefinitions.Get(result));
    }

    [Fact]
    public void RitualId_UnknownLegacyValue_DeserializesToRemoved()
    {
        var result = JsonSerializer.Deserialize<RitualId>("\"DeepLight\"");

        Assert.Equal(RitualId.Removed, result);
        Assert.Null(RitualDefinitions.Get(result));
    }

    [Theory]
    [InlineData("DivineLegacy")]
    [InlineData("EternalLegacy")]
    [InlineData("PrestigiousAscension")]
    public void AscensionPowerId_UnknownLegacyValue_DeserializesToRemoved(string legacyValue)
    {
        var result = JsonSerializer.Deserialize<AscensionPowerId>($"\"{legacyValue}\"");

        Assert.Equal(AscensionPowerId.Removed, result);
        Assert.Null(AscensionPowerDefinitions.Get(result));
    }

    [Fact]
    public void AscensionPowerId_UnlockedPowersSet_DeserializesRemovedPowersWithoutThrowing()
    {
        // Reproduit une sauvegarde antérieure à la suppression des 3 pouvoirs Héritage/Ascension
        // Prestigieuse : le HashSet entier ne doit pas faire échouer le chargement.
        var result = JsonSerializer.Deserialize<HashSet<AscensionPowerId>>(
            "[\"Faith\",\"DivineLegacy\",\"EternalLegacy\",\"PrestigiousAscension\",\"ArmOfGod\"]");

        Assert.NotNull(result);
        Assert.Contains(AscensionPowerId.Faith, result);
        Assert.Contains(AscensionPowerId.ArmOfGod, result);
        Assert.Contains(AscensionPowerId.Removed, result);
    }
}
