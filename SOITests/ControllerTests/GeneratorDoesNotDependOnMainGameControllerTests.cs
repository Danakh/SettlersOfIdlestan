using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Verrouille l'absence de cycle entre la generation d'ile et le controleur principal.
///
/// <para><c>MainGameController</c> declenche la generation ; le generateur ne doit donc pas en
/// dependre en retour. Il le faisait : <c>NpcCivilizationPlacer</c> construisait un
/// <c>MainGameController</c> jetable rien que pour emprunter huit de ses sous-controleurs a
/// <c>NpcCivilizationAutoplayer</c>.</para>
///
/// <para>Le prix n'etait pas seulement conceptuel. <c>SetGame</c> cable un jeu complet sur le
/// WorldState passe — qui est le monde reel, partage : il enregistrait les providers de modificateurs
/// de ce controleur jetable (Ascension a la race Humaine par defaut, prestige) sur la civilisation
/// REELLE du joueur, et un second jeu de modificateurs PNJ sur chaque civilisation PNJ, par-dessus
/// celui que le placeur venait de poser. Le premier effet etait rattrape a la main par un
/// <c>DetachModifierProvidersFrom</c> ecrit pour l'occasion ; le second ne l'etait pas, et rendait les
/// PNJ plus forts pendant leur expansion initiale que partout ailleurs dans le jeu.</para>
///
/// <para>Le placeur monte desormais lui-meme les controleurs dont l'autoplay a besoin et les prete
/// via <c>AutoplayControllers</c>. Ce test echoue si la dependance revient — y compris sous la forme
/// d'un simple champ ou parametre, qui rouvrirait le meme chemin.</para>
/// </summary>
public class GeneratorDoesNotDependOnMainGameControllerTests
{
    [Fact]
    public void NoFileUnderControllerGenerator_ReferencesMainGameControllerInCode()
    {
        var generatorDirectory = FindGeneratorDirectory();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(generatorDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();

                // Documentation et commentaires exclus : ils peuvent legitimement expliquer d'ou vient
                // l'appel, comme le fait NpcCivilizationPlacer.CreateAutoplayControllers.
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;
                if (!Regex.IsMatch(lines[i], @"\bMainGameController\b")) continue;

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {trimmed}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Le generateur d'ile ne doit pas dependre de MainGameController — voir AutoplayControllers "
            + "pour lui preter des controleurs sans lui preter le jeu entier :\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Remonte depuis le binaire de test jusqu'a la racine du depot (reperee par le fichier de
    /// solution). Echoue explicitement plutot que de se desactiver en silence : un test
    /// d'architecture qui ne trouve plus les sources ne vaut rien s'il passe quand meme.
    /// </summary>
    private static string FindGeneratorDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (directory.GetFiles("SettlersOfIdlestan.slnx").Any())
            {
                string generator = Path.Combine(directory.FullName, "SettlersOfIdlestan", "Controller", "Generator");
                Assert.True(Directory.Exists(generator), $"Dossier Controller/Generator introuvable sous {directory.FullName}.");
                return generator;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Racine du depot (SettlersOfIdlestan.slnx) introuvable en remontant depuis {AppContext.BaseDirectory}.");
    }
}
