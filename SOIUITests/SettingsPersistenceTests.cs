using System.Text.Json;
using Avalonia.Headless.XUnit;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services;
using Xunit;

namespace SOIUITests;

/// <summary>
/// settings.json doit refleter a tout instant les reglages du joueur — c'est lui que relit le
/// prochain lancement, et lui qui alimente l'ecran-titre. Il n'etait ecrit qu'au basculement plein
/// ecran : tout le reste (tutoriel masque, echelle d'interface, format des nombres) ne survivait
/// qu'a l'interieur de la session, ou dans la sauvegarde de partie.
/// </summary>
public class SettingsPersistenceTests
{
    private static GameSettings Read(FakeFileSystemService fs) =>
        JsonSerializer.Deserialize<GameSettings>(fs.SavedSettings[^1])!;

    [AvaloniaFact]
    public void Un_reglage_change_sur_l_ecran_titre_est_ecrit_aussitot()
    {
        var fs = new FakeFileSystemService();
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(fs);

        runtime.SetTitleSettingToggle(SettingsPanelSnapshot.KeyShowTutorial);

        Assert.NotEmpty(fs.SavedSettings);
        Assert.False(Read(fs).ShowTutorial);
    }

    [AvaloniaFact]
    public void Un_reglage_change_en_partie_est_ecrit_aussitot()
    {
        var fs = new FakeFileSystemService();
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(fs);
        runtime.InvokeTitleAction(TitleScreenSnapshot.ActionPrimary);

        runtime.ToggleSetting(SettingsPanelSnapshot.KeyPauseAfterPrestige);

        Assert.NotEmpty(fs.SavedSettings);
        Assert.True(Read(fs).PauseAfterPrestige);
    }

    [AvaloniaFact]
    public void Un_fichier_deja_a_jour_n_est_pas_reecrit()
    {
        // Sans la comparaison au dernier contenu ecrit, la passe periodique de Tick reecrirait le
        // fichier en boucle — sur le web, c'est une ecriture localStorage par seconde pour rien.
        var fs = new FakeFileSystemService
        {
            SettingsToLoad = JsonSerializer.Serialize(new GameSettings()),
        };
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(fs);

        // Sans store connecte, la sauvegarde cloud est grisee : le geste ne change rien.
        runtime.SetTitleSettingToggle(SettingsPanelSnapshot.KeyCloudSave);
        runtime.Tick();

        Assert.Empty(fs.SavedSettings);
    }

    [AvaloniaFact]
    public void Le_fichier_absent_est_cree_des_le_premier_reglage()
    {
        var fs = new FakeFileSystemService();
        var runtime = new SkiaGameRuntime();
        runtime.Initialize(fs);

        runtime.SetTitleSettingChoice(SettingsPanelSnapshot.KeyNumberFormat, "scientific");

        Assert.Single(fs.SavedSettings);
        Assert.Equal(NumberFormatMode.Scientific, Read(fs).NumberFormat);
    }
}
