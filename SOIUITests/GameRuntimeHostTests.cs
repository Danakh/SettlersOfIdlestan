using SettlersOfIdlestanUI;
using Xunit;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SOIUITests;

/// <summary>
/// Regression : sous Avalonia le rendu s'execute sur le thread de rendu pendant que la boucle
/// de jeu et l'input tournent sur le thread UI. Le runtime et le modele etant mono-thread,
/// une recolte emettant des particules pendant un Tick corrompait l'enumeration en cours cote
/// rendu ("Collection was modified" dans HarvestRenderer).
///
/// Ces tests ne demarrent pas de partie : ils verifient le contrat d'exclusion mutuelle de
/// GameRuntimeHost, qui est ce qui retablit la serialisation d'origine.
/// </summary>
public class GameRuntimeHostTests
{
    [Fact]
    public void Read_et_Invoke_ne_s_executent_jamais_en_parallele()
    {
        using var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());

        // Sert de detecteur, calque sur HarvestParticleSystem : une liste enumeree d'un cote
        // et mutee de l'autre. L'enumeration est volontairement lente (SpinWait par element)
        // pour elargir la fenetre de course — sans cela le test passerait meme sans verrou.
        var shared = new List<int>(Enumerable.Range(0, 200));
        var errors = new List<Exception>();
        var stop = false;
        const int readIterations = 2000;

        var writer = new Thread(() =>
        {
            try
            {
                int i = 0;
                while (!Volatile.Read(ref stop))
                    host.Invoke(_ => { shared.Add(i++); if (shared.Count > 400) shared.RemoveAt(0); });
            }
            catch (Exception ex) { lock (errors) errors.Add(ex); }
        });

        var reader = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < readIterations; i++)
                    host.Read(_ =>
                    {
                        int sum = 0;
                        foreach (var v in shared) { sum += v; Thread.SpinWait(4); }
                        return sum;
                    });
            }
            catch (Exception ex) { lock (errors) errors.Add(ex); }
            finally { Volatile.Write(ref stop, true); }
        });

        writer.Start();
        reader.Start();
        reader.Join();
        writer.Join();

        Assert.Empty(errors);
    }

    [Fact]
    public void Apres_Dispose_les_acces_ne_touchent_plus_le_runtime()
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        host.Dispose();

        bool invoked = false;
        host.Invoke(_ => invoked = true);

        // Un frame peut encore etre en vol sur le thread de rendu au moment de la fermeture :
        // les acces doivent devenir inoffensifs plutot que de lever ObjectDisposedException.
        Assert.False(invoked);
        Assert.False(host.Read(_ => true));
    }

    [Fact]
    public void Dispose_est_idempotent()
    {
        var host = new GameRuntimeHost(new SkiaLayer.SkiaGameRuntime());
        host.Dispose();
        host.Dispose();
    }
}
