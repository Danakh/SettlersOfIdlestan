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

    /// <summary>
    /// [Legacy remap v0.21] Une sauvegarde antérieure à la file de recherche multi-places n'a qu'un
    /// champ QueuedResearch : sa valeur doit atterrir dans TechnologyTree.ResearchQueue, et le champ
    /// ne doit plus être réécrit dans les nouvelles sauvegardes.
    /// </summary>
    [Fact]
    public void TechnologyTree_LegacyQueuedResearch_IsLoadedIntoResearchQueue()
    {
        var tree = JsonSerializer.Deserialize<TechnologyTree>(
            "{\"ActiveResearch\":\"Architecture\",\"QueuedResearch\":\"Artisanat\"}");

        Assert.NotNull(tree);
        Assert.Equal(TechnologyId.Architecture, tree.ActiveResearch);
        Assert.Equal(new[] { TechnologyId.Artisanat }, tree.ResearchQueue);

        Assert.DoesNotContain("QueuedResearch", JsonSerializer.Serialize(tree));
    }
}
