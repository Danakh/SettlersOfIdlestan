using System;
using System.Linq;
using System.Text.Json;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Verrouille la table unique <see cref="BuildingFactory"/>, qui a remplacé les deux switch de 48
/// entrées auparavant maintenus en parallèle (<c>BuildingController.CreateBuilding</c> et
/// <see cref="BuildingJsonConverter"/>).
///
/// <para>L'oubli du second rendait illisible toute sauvegarde contenant le nouveau bâtiment, sans
/// qu'aucun test ne le signale. Le seul oubli encore possible — ne pas enregistrer un type dans la
/// table — est couvert ici.</para>
/// </summary>
public class BuildingFactoryTests
{
    public static TheoryData<BuildingType> AllBuildingTypes()
    {
        var data = new TheoryData<BuildingType>();
        foreach (var type in Enum.GetValues<BuildingType>())
            data.Add(type);
        return data;
    }

    [Fact]
    public void EveryBuildingTypeIsRegistered()
    {
        var missing = Enum.GetValues<BuildingType>()
            .Except(BuildingFactory.RegisteredTypes)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Types absents de BuildingFactory : {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(AllBuildingTypes))]
    public void Create_ReturnsAnInstanceCarryingThatVeryType(BuildingType type)
    {
        var building = BuildingFactory.Create(type);

        // Attrape l'erreur de copier-coller classique ([BuildingType.Mill] = () => new Mine()), que
        // ni l'ancien switch d'instanciation ni celui du converter n'auraient jamais révélée.
        Assert.NotNull(building);
        Assert.Equal(type, building!.Type);
    }

    [Theory]
    [MemberData(nameof(AllBuildingTypes))]
    public void GetClrType_MatchesTheInstanceCreatedForThatType(BuildingType type)
    {
        Assert.Equal(BuildingFactory.Create(type)!.GetType(), BuildingFactory.GetClrType(type));
    }

    [Theory]
    [MemberData(nameof(AllBuildingTypes))]
    public void EveryBuildingType_SurvivesAJsonRoundTripAsItsConcreteType(BuildingType type)
    {
        var options = SaveController.SerializationOptions();
        var original = BuildingFactory.Create(type)!;
        original.Level = 2;

        var json = JsonSerializer.Serialize(original, original.GetType(), options);
        var restored = JsonSerializer.Deserialize<Building>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(original.GetType(), restored!.GetType());
        Assert.Equal(type, restored.Type);
        Assert.Equal(2, restored.Level);
    }

    [Fact]
    public void Read_UnknownBuildingTypeName_ThrowsJsonExceptionRatherThanReturningNull()
    {
        var options = SaveController.SerializationOptions();

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Building>("""{"Type":"UnBatimentQuiNExistePas","Level":1}""", options));
    }
}
