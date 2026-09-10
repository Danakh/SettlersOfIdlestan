using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class MonsterRenderer : HexBasedRenderer, IGameRenderer
{
    private const float AnimationDuration = 1f;
    private const float AttackAnimDuration = 0.8f;
    private const float ResourceFlyDuration = 0.6f;
    private const float ResourceIconSize = 18f;
    private const float AttackParticleDuration = 0.5f;
    private const float AttackParticleIconSize = 16f;

    /// <summary>Taille d'une boule de feu, rouge ou bleue — celle du volcan (voir VolcanoRenderer).</summary>
    private const float FireballParticleIconSize = 20f;

    /// <summary>Écartement (px) entre deux trajectoires d'une même salve — même valeur que les particules de récolte.</summary>
    private const float AttackParticleSpreadStep = 15f;

    private sealed class MonsterVisual
    {
        public HexCoord ModelPosition = new(0, 0, IslandMap.SurfaceLayer);
        // Movement
        public HexCoord FromHex = new(0, 0, IslandMap.SurfaceLayer);
        public SKPoint From;
        public SKPoint To;
        public float MoveProgress = 1f;
        // Attack
        public long KnownLastAttackTick = -1;
        public float AttackAnimProgress = 1f;
        public SKPoint HomePos;
        public SKPoint TargetPos;
        /// <summary>Figé au déclenchement, comme HomePos/TargetPos : l'attaque en cours partait-elle de loin (boules de feu, icône immobile) ou d'une ruée ?</summary>
        public bool AttackWasRanged;
        public Resource? FlyingResource;
        public float ResourceFlyProgress = 1f;
    }

    private sealed class AttackParticle
    {
        public SKPoint From;
        public SKPoint To;

        /// <summary>
        /// Point de contrôle de la courbe de Bézier quadratique — au milieu du segment pour une
        /// trajectoire droite, décalé perpendiculairement pour écarter les particules d'une même salve
        /// (voir <see cref="EmitAttackParticles"/>, même principe que HarvestParticleSystem).
        /// </summary>
        public SKPoint ControlPoint;
        public float Progress;

        /// <summary>Aspect de la particule — voir <see cref="AttackParticleKind"/>.</summary>
        public AttackParticleKind Kind;
    }

    /// <summary>
    /// Aspect d'une particule de salve. Les deux boules de feu partagent le même rendu et ne
    /// diffèrent que par le SVG : la rouge est celle du volcan, lancée par les monstres à distance ;
    /// la bleue est celle des Spires de Défense, pour qu'un tir de la ville se distingue d'un tir
    /// ennemi quand les deux se croisent à l'écran.
    /// </summary>
    private enum AttackParticleKind
    {
        Soldier,
        RedFireball,
        BlueFireball,
    }

    private readonly Dictionary<MonsterFeature, MonsterVisual> _monsterVisuals = new();
    private readonly List<AttackParticle> _attackParticles = new();
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private SKSvg? _attackSvg;
    private SKSvg? _fireballSvg;
    private SKSvg? _blueFireballSvg;
    private SKPaint? _resourceFlyPaint;
    private SKPaint? _attackParticlePaint;
    private SKPaint? _fireballPaint;
    private bool _disposed;

    public MonsterRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }

        _resourceFlyPaint = new SKPaint { Color = SKColors.White };
        _attackParticlePaint = new SKPaint { IsAntialias = true };
        _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg");
        _fireballPaint = new SKPaint { IsAntialias = true };
        _fireballSvg = _resourceManager.LoadImage("Resources.icons.features.fireball.svg");
        _blueFireballSvg = _resourceManager.LoadImage("Resources.icons.features.fireball_blue.svg");
    }

    public void Connect(
        MilitaryController militaryController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        militaryController.SoldierAttackedMonster += (_, args) => OnAttackedMonster(args, AttackParticleKind.Soldier);
        // La Spire de Défense part du même endroit (le vertex de la ville) mais lance une boule de feu
        // bleue : deux événements, un seul chemin de filtrage.
        militaryController.DefenseSpireAttackedMonster += (_, args) => OnAttackedMonster(args, AttackParticleKind.BlueFireball);

        void OnAttackedMonster(SoldierAttackEventArgs args, AttackParticleKind kind)
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            var worldState = gameControllerService.CurrentWorldState;
            if (worldState == null) return;
            if (args.CityVertex.Z != worldState.CurrentViewedLayer) return;
            if (!IsSourceOrDestinationVisible(worldState, args.CityVertex, args.MonsterPosition)) return;
            EmitAttackParticles(args.CityVertex, args.MonsterPosition, kind, args.SoldierCount);
        }
    }

    /// <summary>
    /// Une boule de feu par coup porté, cible par cible (voir MonsterFeature.LastAttackImpacts) :
    /// une salve de zone en lance une sur chacune de ses cibles, une salve concentrée en lance
    /// autant que de coups sur la même — les deux se distinguent donc à l'œil. Seuls les impacts
    /// marqués <c>Ranged</c> donnent un tir : le Dieu démon mène deux attaques de front, et sa ruée
    /// au corps-à-corps peut tomber sur le même tick que son déluge de boules de feu — elle
    /// s'affiche alors en élan de l'icône pendant que les autres cibles sont bombardées. Émise depuis la
    /// relecture de LastAttackTick et non sur un événement du contrôleur comme les tirs des villes :
    /// côté modèle une attaque de monstre ne s'observe pas autrement, exactement comme le reste de
    /// cette animation.
    ///
    /// <para>Un tir s'affiche dès que l'une de ses deux extrémités est découverte — le monstre ou la
    /// cible — même exception que celle qui laisse voir l'attaque d'un monstre posté sur un hex
    /// encore sous brouillard (voir <c>attackTargetVisible</c> dans Render).</para>
    /// </summary>
    private void EmitRangedAttack(MonsterFeature monster, SKPoint from, VisibleIslandMap? visibleMap)
    {
        bool sourceVisible = visibleMap == null || visibleMap.HasTile(monster.Position);

        var impacts = monster.LastAttackImpacts;
        for (int i = 0; i < impacts.Count; i++)
        {
            var impact = impacts[i];
            if (!impact.Ranged) continue;
            SKPoint to;
            bool targetVisible;
            if (impact.Vertex != null)
            {
                to = VertexToIsland(impact.Vertex);
                targetVisible = visibleMap == null || visibleMap.IsVertexVisible(impact.Vertex);
            }
            else if (impact.Hex != null)
            {
                to = HexToPoint(impact.Hex.Value);
                targetVisible = visibleMap == null || visibleMap.HasTile(impact.Hex.Value);
            }
            else continue;

            if (!sourceVisible && !targetVisible) continue;
            EmitAttackParticles(from, to, AttackParticleKind.RedFireball, count: impact.Strikes);
        }
    }

    private static bool IsSourceOrDestinationVisible(WorldState worldState, Vertex source, HexCoord target)
    {
        if (!worldState.Visibility.GetForZ(source.Z).TryGetValue(worldState.PlayerCivilization.Index, out var visibleMap))
            return true;
        if (visibleMap.HasTile(target)) return true;
        foreach (var hex in source.GetHexes())
            if (visibleMap.HasTile(hex)) return true;
        return false;
    }

    /// <summary>
    /// Une particule par soldat engagé dans l'attaque (Phalange — voir
    /// SoldierAttackEventArgs.SoldierCount) : sans écartement elles se superposeraient exactement et la
    /// salve se lirait comme une attaque unique. Les points de contrôle sont répartis en largeur par
    /// rapport à l'axe départ→cible, exactement comme les particules de récolte
    /// (HarvestParticleSystem.EmitParticles).
    /// </summary>
    private void EmitAttackParticles(Vertex cityVertex, HexCoord targetPosition, AttackParticleKind kind, int count)
    {
        var (bx, by) = AxialToIsland(targetPosition.Q, targetPosition.R);
        EmitAttackParticles(VertexToIsland(cityVertex), new SKPoint(bx, by), kind, count);
    }

    /// <summary>
    /// Même salve, exprimée en coordonnées écran : sert au tir d'un monstre à distance, qui part de la
    /// position animée du monstre (et non d'un vertex de ville) vers sa cible.
    /// </summary>
    private void EmitAttackParticles(SKPoint from, SKPoint to, AttackParticleKind kind, int count)
    {
        int n = Math.Max(1, count);
        var mid = new SKPoint((from.X + to.X) / 2f, (from.Y + to.Y) / 2f);

        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float perpX = 0f, perpY = 0f;
        if (len > 0f) { perpX = -dy / len; perpY = dx / len; }

        float halfSpan = (n - 1) * AttackParticleSpreadStep / 2f;
        for (int i = 0; i < n; i++)
        {
            float offset = n > 1 ? i * AttackParticleSpreadStep - halfSpan : 0f;
            _attackParticles.Add(new AttackParticle
            {
                From = from,
                To = to,
                ControlPoint = new SKPoint(mid.X + perpX * offset, mid.Y + perpY * offset),
                Progress = 0f,
                Kind = kind,
            });
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var worldState = mgs.CurrentWorldState;
        if (worldState == null) return;

        VisibleIslandMap? visibleMap = null;
        if (!DebugSettings.ShowFullMap && !mgs.GodState.AscensionState.IsEyeOfGodActive)
            worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out visibleMap);

        float dt = context.DeltaTime;
        float speedFactor = mgs.Clock.SpeedMultiplier > 0 ? mgs.Clock.SpeedMultiplier : 1;

        var monsters = worldState.Features.OfType<MonsterFeature>().ToList();
        SyncMonsterVisuals(monsters);

        foreach (var monster in monsters)
        {
            var v = _monsterVisuals[monster];

            // Movement animation
            var targetPoint = HexToPoint(monster.Position);
            if (!v.ModelPosition.Equals(monster.Position))
            {
                v.FromHex = v.ModelPosition;
                v.From = CurrentVisualPoint(v);
                v.To = targetPoint;
                v.MoveProgress = 0f;
                v.ModelPosition = monster.Position;
            }
            if (v.MoveProgress < 1f)
                v.MoveProgress = Math.Min(1f, v.MoveProgress + dt * speedFactor / AnimationDuration);

            var normalPos = Lerp(v.From, v.To, Smoothstep(v.MoveProgress));

            // Attack animation
            if (monster.LastAttackTick > 0
                && monster.LastAttackTick != v.KnownLastAttackTick
                && monster.LastAttackTick != monster.LastMovedTick
                && (monster.LastAttackTargetVertex != null || monster.LastAttackTargetHex != null))
            {
                v.KnownLastAttackTick = monster.LastAttackTick;
                v.AttackAnimProgress = 0f;
                v.HomePos = normalPos;
                v.TargetPos = monster.LastAttackTargetVertex != null
                    ? VertexToIsland(monster.LastAttackTargetVertex)
                    : HexToPoint(monster.LastAttackTargetHex!.Value);
                v.FlyingResource = null;
                if (monster.LastAttackResourcesString != null)
                {
                    var first = monster.LastAttackResourcesString.Split(',')[0];
                    if (Enum.TryParse<Resource>(first, out var res))
                        v.FlyingResource = res;
                }
                v.ResourceFlyProgress = 1f;

                // Attaque à distance : le monstre ne bouge pas (voir plus bas, sa position reste
                // normalPos), le tir est matérialisé par des boules de feu vers ses cibles.
                // AttackAnimProgress continue de courir : c'est lui qui cadence l'envol des
                // ressources volées. EmitRangedAttack est appelé quoi qu'il arrive et ne retient
                // que les impacts tirés de loin : un monstre à plusieurs attaques peut ruer sur une
                // ville et bombarder les autres dans la même volée.
                v.AttackWasRanged = monster.LastAttackWasRanged;
                if (monster.Found && monster.Position.Z == context.CurrentLayer)
                    EmitRangedAttack(monster, normalPos, visibleMap);
            }
            if (v.AttackAnimProgress < 1f)
            {
                v.AttackAnimProgress = Math.Min(1f, v.AttackAnimProgress + dt / AttackAnimDuration);
                if (v.AttackAnimProgress >= 0.45f && v.ResourceFlyProgress >= 1f && v.FlyingResource != null)
                    v.ResourceFlyProgress = 0f;
            }
            if (v.ResourceFlyProgress < 1f)
                v.ResourceFlyProgress = Math.Min(1f, v.ResourceFlyProgress + dt / ResourceFlyDuration);

            // Rendering — filtered by found / layer / visibility
            if (!monster.Found) continue;
            if (monster.Position.Z != context.CurrentLayer) continue;
            if (visibleMap != null)
            {
                bool destVisible = visibleMap.HasTile(monster.Position);
                bool fromVisible = v.MoveProgress < 1f && visibleMap.HasTile(v.FromHex);
                // Un monstre à portée étendue peut frapper une ville depuis un hex non
                // découvert (le brouillard de guerre ne s'étend en général qu'aux hexes de la
                // ville elle-même, sans anneau de rayon 1 sans Tour de Guet) : sans cette
                // exception, l'attaque entière (élan du monstre + particules de perte) restait
                // invisible bien que les dégâts soient réellement appliqués.
                bool attackTargetVisible = v.AttackAnimProgress < 1f && monster.LastAttackTargetVertex != null
                    && visibleMap.IsVertexVisible(monster.LastAttackTargetVertex);
                if (!destVisible && !fromVisible && !attackTargetVisible) continue;
            }

            var svgName = monster.SvgIconResourceName;
            if (svgName == null) continue;

            SKPoint pos;
            // Un tir laisse l'icône sur son hex : seul l'élan du corps-à-corps la déplace.
            if (v.AttackAnimProgress < 1f && !v.AttackWasRanged)
            {
                float t = v.AttackAnimProgress;
                pos = t < 0.5f
                    ? Lerp(v.HomePos, v.TargetPos, Smoothstep(t * 2f))
                    : Lerp(v.TargetPos, v.HomePos, Smoothstep((t - 0.5f) * 2f));
            }
            else
            {
                pos = normalPos;
            }

            SKSvg? svg = _resourceManager.LoadImage(svgName);
            DrawSvgMonsterIcon(canvas, pos, svg, monster.SvgIconSize * monster.IconSizeFactor);

            if (v.ResourceFlyProgress < 1f && v.FlyingResource != null)
            {
                var flyPos = Lerp(v.TargetPos, v.HomePos, Smoothstep(v.ResourceFlyProgress));
                DrawResourceIcon(canvas, flyPos, v.FlyingResource.Value, 1f - v.ResourceFlyProgress);
            }
        }

        // Attack particles (soldiers → monster, et boules de feu des Spires de Défense)
        for (int i = _attackParticles.Count - 1; i >= 0; i--)
        {
            var p = _attackParticles[i];
            p.Progress = Math.Min(1f, p.Progress + dt / AttackParticleDuration);
            float t = Smoothstep(p.Progress);
            var pos2 = QuadraticBezier(p.From, p.ControlPoint, p.To, t);
            float alpha = p.Progress < 0.7f ? 1f : (1f - p.Progress) / 0.3f;
            switch (p.Kind)
            {
                case AttackParticleKind.RedFireball: DrawFireballParticle(canvas, pos2, alpha, _fireballSvg); break;
                case AttackParticleKind.BlueFireball: DrawFireballParticle(canvas, pos2, alpha, _blueFireballSvg); break;
                default: DrawAttackParticle(canvas, pos2, alpha); break;
            }
            if (p.Progress >= 1f)
                _attackParticles.RemoveAt(i);
        }
    }

    private void SyncMonsterVisuals(IList<MonsterFeature> monsters)
    {
        // Add visuals for new monsters
        foreach (var monster in monsters)
        {
            if (_monsterVisuals.ContainsKey(monster)) continue;
            var hex = monster.Position;
            var pos = HexToPoint(hex);
            _monsterVisuals[monster] = new MonsterVisual
            {
                ModelPosition = hex,
                FromHex = hex,
                From = pos,
                To = pos,
                HomePos = pos,
            };
        }

        // Remove visuals for monsters that are gone
        var toRemove = _monsterVisuals.Keys.Except(monsters).ToList();
        foreach (var gone in toRemove)
            _monsterVisuals.Remove(gone);
    }

    private SKPoint CurrentVisualPoint(MonsterVisual v)
        => Lerp(v.From, v.To, Smoothstep(v.MoveProgress));

    /// <summary>Bézier quadratique B(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2 — droite quand P1 est au milieu.</summary>
    private static SKPoint QuadraticBezier(SKPoint from, SKPoint control, SKPoint to, float t)
    {
        float mt = 1f - t;
        return new SKPoint(
            mt * mt * from.X + 2f * mt * t * control.X + t * t * to.X,
            mt * mt * from.Y + 2f * mt * t * control.Y + t * t * to.Y);
    }

    private static void DrawSvgMonsterIcon(SKCanvas canvas, SKPoint center, SKSvg? svg, float size)
    {
        var picture = svg?.Picture;
        if (picture == null) return;

        float naturalSize = Math.Max(picture.CullRect.Width, picture.CullRect.Height);
        float scale = naturalSize > 0f ? size / naturalSize : 1f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private void DrawAttackParticle(SKCanvas canvas, SKPoint center, float alpha)
    {
        var picture = _attackSvg?.Picture;
        if (picture == null || _attackParticlePaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _attackParticlePaint.Color = new SKColor(255, 120, 80, alphaB);
        _attackParticlePaint.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(255, 120, 80, alphaB), SKBlendMode.SrcIn);

        const float size = AttackParticleIconSize;
        float scale = size / 64f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, 64, 64), _attackParticlePaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    /// <summary>
    /// Boule de feu — rouge pour un tir de monstre, bleue pour une Spire de Défense. Rendu repris tel
    /// quel de VolcanoRenderer.DrawFireball : teinte blanche sans filtre de couleur, pour que le SVG
    /// garde ses propres teintes de flamme — contrairement à l'icône d'attaque des soldats, recolorée
    /// en orange. C'est aussi pourquoi la couleur vient du SVG et non d'un filtre : la boule bleue est
    /// un dégradé de bleus, pas une boule rouge teintée.
    /// </summary>
    private void DrawFireballParticle(SKCanvas canvas, SKPoint center, float alpha, SKSvg? svg)
    {
        var picture = svg?.Picture;
        if (picture == null || _fireballPaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _fireballPaint.Color = SKColors.White.WithAlpha(alphaB);
        _fireballPaint.ColorFilter = null;

        float naturalSize = Math.Max(picture.CullRect.Width, picture.CullRect.Height);
        float scale = naturalSize > 0f ? FireballParticleIconSize / naturalSize : 1f;

        canvas.Save();
        canvas.Translate(center.X - FireballParticleIconSize / 2f, center.Y - FireballParticleIconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, naturalSize, naturalSize), _fireballPaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    private void DrawResourceIcon(SKCanvas canvas, SKPoint center, Resource resource, float alpha)
    {
        if (!_resourceIcons.TryGetValue(resource, out var svg) || svg?.Picture == null) return;
        if (_resourceFlyPaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _resourceFlyPaint.Color = SKColors.White.WithAlpha(alphaB);

        const float size = ResourceIconSize;
        float scale = size / 64f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, 64, 64), _resourceFlyPaint);
        canvas.DrawPicture(svg.Picture);
        canvas.Restore();
        canvas.Restore();
    }

    private SKPoint HexToPoint(HexCoord hex)
    {
        var (x, y) = AxialToIsland(hex.Q, hex.R);
        return new SKPoint(x, y);
    }

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static float Smoothstep(float t)
        => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _attackSvg = null;
        _fireballSvg = null;
        _blueFireballSvg = null;
        _resourceFlyPaint?.Dispose();
        _resourceFlyPaint = null;
        _attackParticlePaint?.Dispose();
        _attackParticlePaint = null;
        _fireballPaint?.Dispose();
        _fireballPaint = null;
        _disposed = true;
    }
}
