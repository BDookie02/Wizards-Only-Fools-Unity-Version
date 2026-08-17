using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofAmbientBirdSpecies
    {
        public WofAmbientBirdSpecies(
            string name,
            Color32 bodyColor,
            Color32 wingColor,
            Color32 accentColor,
            float wingLength,
            float bodyLength,
            float baseScale)
        {
            Name = name;
            BodyColor = bodyColor;
            WingColor = wingColor;
            AccentColor = accentColor;
            WingLength = wingLength;
            BodyLength = bodyLength;
            BaseScale = baseScale;
        }

        public string Name { get; }
        public Color32 BodyColor { get; }
        public Color32 WingColor { get; }
        public Color32 AccentColor { get; }
        public float WingLength { get; }
        public float BodyLength { get; }
        public float BaseScale { get; }
        public float WingHeight => Name == "moth" ? 0.9f : 0.48f;
        public float TailRadius => Name == "toucan" ? 0.28f : 0.16f;
        public float TailLength => Name == "toucan" ? 1.05f : 0.62f;
    }

    internal readonly struct WofAmbientBirdRecord
    {
        public WofAmbientBirdRecord(
            int index,
            WofAmbientBirdSpecies species,
            Vector3 localPosition,
            float scale,
            float tilt,
            float wingPhase)
        {
            Index = index;
            Species = species;
            LocalPosition = localPosition;
            Scale = scale;
            Tilt = tilt;
            WingPhase = wingPhase;
        }

        public int Index { get; }
        public WofAmbientBirdSpecies Species { get; }
        public Vector3 LocalPosition { get; }
        public float Scale { get; }
        public float Tilt { get; }
        public float WingPhase { get; }
    }

    internal readonly struct WofAmbientBirdFlock
    {
        public WofAmbientBirdFlock(
            int chunkX,
            int chunkZ,
            WofSurvivalBiome biome,
            double seed,
            float baseY,
            WofAmbientBirdRecord[] birds)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Biome = biome;
            Seed = seed;
            BaseY = baseY;
            Birds = birds;
        }

        public int ChunkX { get; }
        public int ChunkZ { get; }
        public WofSurvivalBiome Biome { get; }
        public double Seed { get; }
        public float BaseY { get; }
        public WofAmbientBirdRecord[] Birds { get; }
    }

    internal static class WofSurvivalAmbientBirdRules
    {
        internal const float MobileUpdateInterval = 1f / 24f;
        internal const float DesertOrbitSpeed = 0.08f;
        internal const float DefaultOrbitSpeed = 0.12f;
        internal const float VerticalDriftSpeed = 0.45f;
        internal const float VerticalDriftAmplitude = 8f;
        private const double DesktopStageOneDelayMilliseconds = 180d;
        private const double MobileStageOneDelayMilliseconds = 280d;
        private const double DesktopStageJitterMilliseconds = 520d;
        private const double MobileStageJitterMilliseconds = 760d;
        private const int TreeLoadStageSalt = 9011;

        private static readonly WofAmbientBirdSpecies[] DesertSpecies =
        {
            new("vulture", new Color32(214, 191, 138, 255), new Color32(234, 214, 163, 255),
                new Color32(255, 241, 184, 255), 3.4f, 2.3f, 0.94f),
            new("hawk", new Color32(231, 201, 142, 255), new Color32(240, 221, 168, 255),
                new Color32(255, 243, 191, 255), 2.7f, 1.8f, 0.84f)
        };

        private static readonly WofAmbientBirdSpecies[] JungleSpecies =
        {
            new("parrot", new Color32(134, 239, 172, 255), new Color32(187, 247, 208, 255),
                new Color32(252, 165, 165, 255), 2.2f, 1.55f, 0.78f),
            new("toucan", new Color32(254, 243, 199, 255), new Color32(186, 230, 253, 255),
                new Color32(253, 230, 138, 255), 2.5f, 1.75f, 0.84f),
            new("macaw", new Color32(147, 197, 253, 255), new Color32(252, 165, 165, 255),
                new Color32(254, 240, 138, 255), 2.4f, 1.7f, 0.8f)
        };

        private static readonly WofAmbientBirdSpecies[] SwampSpecies =
        {
            new("heron", new Color32(241, 245, 249, 255), new Color32(203, 213, 225, 255),
                new Color32(254, 243, 199, 255), 3.05f, 2f, 0.9f),
            new("marshbird", new Color32(221, 214, 254, 255), new Color32(191, 219, 254, 255),
                new Color32(254, 240, 138, 255), 2.35f, 1.65f, 0.78f)
        };

        private static readonly WofAmbientBirdSpecies[] MushroomSpecies =
        {
            new("moth", new Color32(245, 208, 254, 255), new Color32(233, 213, 255, 255),
                new Color32(254, 243, 199, 255), 3.15f, 1.35f, 0.74f),
            new("owl", new Color32(230, 200, 162, 255), new Color32(240, 217, 178, 255),
                new Color32(253, 230, 138, 255), 2.25f, 1.7f, 0.8f)
        };

        private static readonly WofAmbientBirdSpecies[] DefaultSpecies =
        {
            new("swallow", new Color32(239, 246, 255, 255), new Color32(191, 219, 254, 255),
                new Color32(255, 247, 214, 255), 2.2f, 1.45f, 0.72f),
            new("bluebird", new Color32(191, 219, 254, 255), new Color32(147, 197, 253, 255),
                new Color32(254, 240, 138, 255), 2.05f, 1.4f, 0.68f)
        };

        internal static bool ShouldShowBirds(
            bool survivalSession,
            bool ambientLifeReady,
            bool grassInspectionView,
            int chunkX,
            int chunkZ,
            int distance)
        {
            return survivalSession && ambientLifeReady && !grassInspectionView && distance == 0 &&
                   !WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ) &&
                   !WofSurvivalTerrainMath.IsLilyRealmCenter(chunkX, chunkZ);
        }

        internal static WofAmbientBirdFlock MakeFlock(int chunkX, int chunkZ, int distance = 0)
        {
            var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
            var seed = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 410);
            var speciesList = GetSpecies(biome);
            var birdCount = Math.Max(3, JsRound(GetBaseCount(biome) *
                (distance == 0 ? 0.72d : distance == 1 ? 0.38d : 0.22d)));
            var birds = new WofAmbientBirdRecord[birdCount];
            for (var index = 0; index < birdCount; index++)
            {
                var angle = Math.PI * 2d * index / birdCount + seed * Math.PI;
                var radius = 170d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 430 + index) * 190d;
                var speciesIndex = (int)Math.Floor(
                    WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 412 + index) * speciesList.Count) %
                                   speciesList.Count;
                var species = speciesList[speciesIndex];
                birds[index] = new WofAmbientBirdRecord(
                    index,
                    species,
                    new Vector3(
                        (float)(Math.Cos(angle) * radius),
                        (float)(28d + WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 450 + index) *
                            (biome == WofSurvivalBiome.Jungle ? 84d : 62d)),
                        (float)(Math.Sin(angle) * radius)),
                    (float)((species.BaseScale +
                             WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 470 + index) * 0.34d) * 0.88d),
                    (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 490 + index) - 0.5d),
                    (float)(WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, 492 + index) * Math.PI * 2d));
            }

            return new WofAmbientBirdFlock(chunkX, chunkZ, biome, seed, GetBaseY(biome), birds);
        }

        internal static float GetAmbientReadyDelaySeconds(int chunkX, int chunkZ, bool mobile)
        {
            var jitter = WofSurvivalTerrainMath.Hash01(chunkX, chunkZ, TreeLoadStageSalt) *
                         (mobile ? MobileStageJitterMilliseconds : DesktopStageJitterMilliseconds);
            var delay = (mobile ? MobileStageOneDelayMilliseconds : DesktopStageOneDelayMilliseconds) + jitter;
            return (float)(delay / 1000d);
        }

        internal static float GetFlockRotationRadians(WofAmbientBirdFlock flock, double elapsedSeconds)
        {
            var speed = flock.Biome == WofSurvivalBiome.Desert ? DesertOrbitSpeed : DefaultOrbitSpeed;
            return (float)(flock.Seed * Math.PI * 2d + elapsedSeconds * speed);
        }

        internal static float GetFlockWorldY(WofAmbientBirdFlock flock, double elapsedSeconds)
        {
            return flock.BaseY + (float)Math.Sin(elapsedSeconds * VerticalDriftSpeed + flock.Seed * 3d) *
                   VerticalDriftAmplitude;
        }

        internal static Vector3 GetBirdWorldPosition(
            WofAmbientBirdFlock flock,
            WofAmbientBirdRecord bird,
            double elapsedSeconds)
        {
            var groupRotation = Quaternion.AngleAxis(
                GetFlockRotationRadians(flock, elapsedSeconds) * Mathf.Rad2Deg, Vector3.up);
            return new Vector3(
                       flock.ChunkX * WofSurvivalTerrainMath.BlockSize,
                       GetFlockWorldY(flock, elapsedSeconds),
                       flock.ChunkZ * WofSurvivalTerrainMath.BlockSize) +
                   groupRotation * bird.LocalPosition;
        }

        private static IReadOnlyList<WofAmbientBirdSpecies> GetSpecies(WofSurvivalBiome biome)
        {
            return biome switch
            {
                WofSurvivalBiome.Desert => DesertSpecies,
                WofSurvivalBiome.Jungle => JungleSpecies,
                WofSurvivalBiome.Swamp => SwampSpecies,
                WofSurvivalBiome.Mushroom => MushroomSpecies,
                _ => DefaultSpecies
            };
        }

        private static int GetBaseCount(WofSurvivalBiome biome)
        {
            return biome switch
            {
                WofSurvivalBiome.Jungle => 14,
                WofSurvivalBiome.Desert => 12,
                WofSurvivalBiome.Mushroom => 12,
                WofSurvivalBiome.Swamp => 11,
                _ => 12
            };
        }

        private static float GetBaseY(WofSurvivalBiome biome)
        {
            return biome switch
            {
                WofSurvivalBiome.Jungle => 192f,
                WofSurvivalBiome.Swamp => 168f,
                WofSurvivalBiome.Desert => 176f,
                _ => 166f
            };
        }

        private static int JsRound(double value)
        {
            return (int)Math.Floor(value + 0.5d);
        }
    }
}
