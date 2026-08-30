using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Verrouille le sens des dépendances entre les deux couches : <c>Model/</c> ne doit rien connaître
/// de <c>Controller/</c>.
///
/// <para>Les deux vivent dans le même assembly (<c>SettlersOfIdlestanCore</c>), donc rien dans le
/// compilateur ne tient cette règle — elle n'existait que par convention, et trois fichiers du
/// modèle l'avaient déjà enfreinte : <c>Civilization</c> appelait <c>BuildingController</c> jusque
/// dans son constructeur, <c>BuildingMaxLevelCalculator</c> et <c>RaceDefinitions</c> passaient par
/// lui pour instancier un bâtiment. Ce test remplace la vigilance, en attendant une séparation en
/// deux projets qui rendrait la règle structurelle.</para>
///
/// <para>Ne regarde que le code : les <c>&lt;see cref&gt;</c> de documentation qui pointent vers un
/// contrôleur restent tolérés (ils n'entraînent aucune dépendance de compilation ici, et devront
/// être traités le jour de la séparation en assemblies).</para>
/// </summary>
public class ModelDoesNotDependOnControllerTests
{
    [Fact]
    public void NoFileUnderModel_ReferencesTheControllerLayerInCode()
    {
        var modelDirectory = FindModelDirectory();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(modelDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                // Documentation et commentaires exclus : voir le résumé de la classe.
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (!Regex.IsMatch(line, @"\bSettlersOfIdlestan\.Controller\b")) continue;

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {trimmed}");
            }
        }

        Assert.True(offenders.Count == 0,
            "La couche Model ne doit pas dépendre de la couche Controller :\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Remonte depuis le binaire de test jusqu'à la racine du dépôt (repérée par le fichier de
    /// solution). Échoue explicitement plutôt que de se désactiver en silence : un test
    /// d'architecture qui ne trouve plus les sources ne vaut rien s'il passe quand même.
    /// </summary>
    private static string FindModelDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (directory.GetFiles("SettlersOfIdlestan.slnx").Any())
            {
                string model = Path.Combine(directory.FullName, "SettlersOfIdlestan", "Model");
                Assert.True(Directory.Exists(model), $"Dossier Model introuvable sous {directory.FullName}.");
                return model;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Racine du dépôt (SettlersOfIdlestan.slnx) introuvable en remontant depuis {AppContext.BaseDirectory}.");
    }
}
