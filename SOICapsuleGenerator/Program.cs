using SkiaSharp;
using SettlersOfIdlestanSkia.Screens;

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
string illustrationPath = Path.Combine(repoRoot, "assets", "steam", "SettlersOfIdlestanMainIllustration.png");
string outputDir        = Path.Combine(repoRoot, "assets", "steam");

using var illustrationData = SKData.Create(illustrationPath);
using var illustration     = SKBitmap.Decode(illustrationData);

int srcW = illustration.Width;   // 1408
int srcH = illustration.Height;  // 768

Console.WriteLine($"Illustration chargée : {srcW}×{srcH}");

// Source rect de l'illustration 1408×768 pour chaque format :
//   - Portraits  : focus sur la ville (centre-gauche), calcul auto
//   - 1232×706   : illustration quasi-complète (crop symétrique mineur pour respecter le ratio)
//   - 920×430    : zoom ~50% sur la ville
//   - 462×174    : zoom ~35% sur la ville
//
// Focus ville : x≈510, y≈380 dans la source

var capsules = new (string File, int W, int H, SKRect SrcRect, bool TitleAtTop)[]
{
    ("capsule.png",         600,  900, PortraitCrop(srcW, srcH, 600,  900), true),
    ("capsule_748x896.png", 748,  896, PortraitCrop(srcW, srcH, 748,  896), true),
    // Illustration complète : crop symétrique horizontal pour honorer le ratio 1.745 (vs 1.833)
    ("capsule_1232x706.png", 1232, 706, new SKRect(34,  0,   1374, 768), false),
    // Zoom ville (~50% source) — aspect 2.14 maintenu
    ("capsule_920x430.png",  920,  430, new SKRect(99,  188,  921, 572), false),
    ("entete.png",           920,  430, new SKRect(99,  188,  921, 572), false),
    // Zoom ville (~35% source) — aspect 2.655 maintenu
    ("capsule_462x174.png",  462,  174, new SKRect(153, 246,  867, 515), false),
};

foreach (var (file, w, h, srcRect, titleAtTop) in capsules)
{
    using var surface = SKSurface.Create(new SKImageInfo(w, h));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Black);

    canvas.DrawBitmap(illustration, srcRect, new SKRect(0, 0, w, h));

    float scale = Math.Min(w / 600f, h / 200f);
    scale = Math.Max(0.35f, Math.Min(scale, 1.5f));

    if (titleAtTop)
    {
        // Passe une hauteur réduite (≈28 % du canvas) : TitleCardRenderer centre
        // le titre dans cette bande, ce qui le place dans le tiers supérieur de l'image.
        float bandH = h * 0.28f;
        TitleCardRenderer.Draw(canvas, new SKSize(w, bandH), scale);
    }
    else
    {
        TitleCardRenderer.Draw(canvas, new SKSize(w, h), scale);
    }

    using var image = surface.Snapshot();
    using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(Path.Combine(outputDir, file), data.ToArray());
    Console.WriteLine($"  Généré : {file} ({w}×{h})");
}

Console.WriteLine("Terminé.");

// Pour les formats portrait : crop vertical centré sur la ville (x≈510).
static SKRect PortraitCrop(int srcW, int srcH, int dstW, int dstH)
{
    float cropW = srcH * ((float)dstW / dstH);   // pleine hauteur source
    float x     = Math.Clamp(510f - cropW / 2f, 0f, srcW - cropW);
    return new SKRect(x, 0, x + cropW, srcH);
}
