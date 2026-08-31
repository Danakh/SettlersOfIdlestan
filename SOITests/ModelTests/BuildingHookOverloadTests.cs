using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SettlersOfIdlestan.Model.Buildings;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Verrouille l'unicite de signature des cinq questions que le jeu pose a un batiment.
///
/// <para>Chacune existait en deux surcharges, la riche (avec la Civilization ou le WorldState)
/// retombant sur la pauvre. Rien n'obligeait un appelant a prendre la riche : celui qui prenait la
/// pauvre sautait silencieusement toute redefinition portee par l'autre, et le batiment devenait
/// constructible ou recoltable la ou sa propre regle l'interdit. C'est exactement ce que faisait
/// AutoExtendController et ce que faisait GetUniqueBuildingsAndBuildables face a
/// GetBuildableUniqueBuildings.</para>
///
/// <para>Une seule signature virtuelle par question rend la panne impossible : la redefinition est
/// toujours consultee, et l'absence d'une donnee devient un <c>null</c> ecrit noir sur blanc a
/// l'appel. Ce test echoue si une seconde surcharge reapparait, sur <see cref="Building"/> comme sur
/// n'importe quel type concret.</para>
/// </summary>
public class BuildingHookOverloadTests
{
    /// <summary>Methodes virtuelles dont la surcharge unique est le contrat.</summary>
    public static TheoryData<string> HookNames() => new()
    {
        nameof(Building.IsBuildingAvailableForCity),
        nameof(Building.HasBuildPrerequisites),
        nameof(Building.GetMissingPrerequisiteKey),
        nameof(Building.GetBuildWarningKey),
        nameof(Building.AutomaticHarvestCapability),
    };

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Theory]
    [MemberData(nameof(HookNames))]
    public void Building_DeclaresExactlyOneSignature(string hook)
    {
        var declared = typeof(Building).GetMethods(Declared).Where(m => m.Name == hook).ToList();

        Assert.True(declared.Count == 1,
            $"Building.{hook} doit avoir une seule signature ; trouve {declared.Count} :\n  "
            + string.Join("\n  ", declared.Select(Describe)));
    }

    [Theory]
    [MemberData(nameof(HookNames))]
    public void NoConcreteBuilding_AddsASecondSignature(string hook)
    {
        var offenders = new SortedSet<string>();

        foreach (var type in BuildingFactory.RegisteredTypes
                     .Select(BuildingFactory.GetClrType)
                     .Where(t => t != null)
                     .Distinct())
        {
            // Toute la hierarchie sous Building, pas seulement la feuille : une surcharge ajoutee sur
            // une classe intermediaire serait tout aussi invisible depuis l'appelant.
            for (var t = type; t != null && t != typeof(Building); t = t.BaseType)
                foreach (var m in t.GetMethods(Declared).Where(m => m.Name == hook))
                    if (m.GetBaseDefinition().DeclaringType != typeof(Building))
                        offenders.Add($"{t.Name}.{Describe(m)}");
        }

        Assert.True(offenders.Count == 0,
            $"Surcharges de {hook} qui ne redefinissent pas celle de Building — un appelant peut les "
            + "manquer sans erreur :\n  " + string.Join("\n  ", offenders));
    }

    private static string Describe(MethodInfo m) =>
        $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})";
}
