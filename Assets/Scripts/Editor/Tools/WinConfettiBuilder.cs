using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor.Tools
{
    /// <summary>
    /// Generates <c>Assets/Prefabs/Effects/WinConfetti.prefab</c> used by
    /// <see cref="Component.Game.WinEffectView"/>. Each particle picks a random suit sprite
    /// (clubs / diamonds / hearts / spades) via Texture Sheet Animation in Sprites mode.
    ///
    /// Run once via <c>Solitaire/Effects/Build Win Confetti</c>. Re-running overwrites the prefab.
    /// After generation, drag the prefab as a child of the canvas (Screen Space - Camera recommended)
    /// containing the win panel and bind the inner ParticleSystem to <see cref="Component.Game.WinEffectView.confetti"/>.
    /// </summary>
    public static class WinConfettiBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Effects/WinConfetti.prefab";
        private const string MaterialPath = "Assets/Prefabs/Effects/WinConfetti.mat";

        private static readonly string[] SuitSpriteGuids =
        {
            "2b68502de9bbe3142b57a06d83b65e3c", // suit_clubs
            "a06f0c7e7d99ae849abf11ff9ee6ab5b", // suit_diamonds
            "dd76df8949c1ef64697dc2b2ecfb6a8d", // suit_hearts
            "9bc8bc7aa8344874c9970ed06d16d4a0", // suit_spades
        };

        [MenuItem("Solitaire/Effects/Build Win Confetti")]
        public static void Build()
        {
            EnsureDirectory(PrefabPath);

            var sprites = LoadSuitSprites();
            if (sprites == null) return;

            var material = EnsureMaterial();

            var go = new GameObject("WinConfetti");
            // Default Y is in Canvas RectTransform pixel-space, not world units — the Table
            // ancestor lives under a Canvas, so the prefab's local position is scaled by the
            // canvas. ~500 puts the emitter line above the visible play area at this project's
            // referenceResolution; world-units default of ~5 would land near screen center.
            go.transform.localPosition = new Vector3(0f, 500f, 0f);
            try
            {
                ConfigureParticleSystem(go, sprites, material);
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Debug.Log($"[WinConfettiBuilder] Generated {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Sprite[] LoadSuitSprites()
        {
            var sprites = new Sprite[SuitSpriteGuids.Length];
            for (int i = 0; i < SuitSpriteGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(SuitSpriteGuids[i]);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError($"[WinConfettiBuilder] Suit sprite missing for GUID {SuitSpriteGuids[i]} (path: {path})");
                    return null;
                }
                sprites[i] = sprite;
            }
            return sprites;
        }

        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            EnsureDirectory(MaterialPath);
            var shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "WinConfetti" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void ConfigureParticleSystem(GameObject go, Sprite[] sprites, Material material)
        {
            var ps = go.AddComponent<ParticleSystem>();

            // Unity defaults a freshly-added ParticleSystem to Play On Awake = true and starts simulating
            // immediately in edit mode. Stop + clear before configuring so the burst doesn't fire during build.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            // Randomized lifetime so particles don't all expire on the same frame —
            // avoids the visible "death line" sweeping the screen.
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            // C# rotation API is in radians; ±π = ±180°.
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 300;

            // Single burst at t=0. A staggered follow-up burst was tried but reads as a
            // second event — the second wave spawns at the same edge while the first is
            // mid-fall, leaving a visible fresh line in the air mid-celebration.
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 200),
            });

            // SingleSidedEdge defaults to local X axis (horizontal). No rotation needed —
            // gravity pulls particles down from the spawn line. We deliberately do NOT use
            // randomDirectionAmount: at non-zero values some particles spawn with an upward
            // velocity component, and combined with the asymmetric Y force below they read
            // as "rising helium" instead of falling confetti. Horizontal spread comes from
            // the velocityOverLifetime X drift instead.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 4.5f;

            // Per-particle horizontal drift gives the side-to-side flutter that distinguishes
            // confetti from straight-down rain. Constant per particle (no curve animation needed).
            // Unity requires X/Y/Z to share the same MinMaxCurve mode — explicit zero-range
            // TwoConstants on the unused axes keeps the mode consistent.
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Random per-particle Y force layered on top of gravity. Range is biased
            // negative so every particle still falls — symmetric ±0.4 made +Y particles
            // nearly cancel gravity (0.5) and rise on residual upward velocity. With
            // (-0.4, 0.1) the effective gravity stays in [0.4, 0.9]: varied fall speeds
            // (some slow flutter, some fast plunge) but never lifting off.
            // X/Z explicit zero-range to share the same TwoConstants mode (Unity requires
            // axes to match modes within a module).
            var forceOverLifetime = ps.forceOverLifetime;
            forceOverLifetime.enabled = true;
            forceOverLifetime.space = ParticleSystemSimulationSpace.World;
            forceOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            forceOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.1f);
            forceOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Hold full alpha for the first 70% of life, fade out across the last 30%.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));

            // Tumble while falling (radians/sec).
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            // Each particle picks one suit sprite at spawn and keeps it for its full life.
            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            for (int i = textureSheet.spriteCount - 1; i >= 0; i--)
            {
                textureSheet.RemoveSprite(i);
            }
            foreach (var sprite in sprites)
            {
                textureSheet.AddSprite(sprite);
            }
            // frameOverTime constant 0 + startFrame uniform [0, spriteCount) → fixed random sprite per particle.
            textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            textureSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, sprites.Length);
            textureSheet.cycleCount = 1;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = material;
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = 100;
        }

        private static void EnsureDirectory(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }
        }
    }
}
