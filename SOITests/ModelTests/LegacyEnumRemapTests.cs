using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Magic;
using System.Text.Json;
using Xunit;

namespace SOITests.ModelTests;

// Régression : une sauvegarde antérieure à la suppression d'une recherche ou d'un rituel ne doit
// jamais faire échouer le chargement — voir TechnologyIdJsonConverter / RitualIdJsonConverter.
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
}
