using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    internal readonly struct WofWorldWillowRecord
    {
        public WofWorldWillowRecord(
            int index,
            Vector3 position,
            int chunkX,
            int chunkZ,
            float yaw,
            float scale,
            WofSurvivalBiome biome,
            double variant)
        {
            Index = index;
            Position = position;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Yaw = yaw;
            Scale = scale;
            Biome = biome;
            Variant = variant;
        }

        public int Index { get; }
        public string Key => $"world-willow-{Index}";
        public Vector3 Position { get; }
        public int ChunkX { get; }
        public int ChunkZ { get; }
        public float Yaw { get; }
        public float Scale { get; }
        public WofSurvivalBiome Biome { get; }
        public double Variant { get; }
        public float TrunkHeight => 86f * Scale;
        public float TrunkRadius => 4.6f * Scale;
        public float CanopyRadius => 24f * Scale;
        public float CanopyHeight => 56f * Scale;
    }

    internal readonly struct WofWorldWillowBranch
    {
        public WofWorldWillowBranch(Vector3 start, Vector3 end, float radius)
        {
            Start = start;
            End = end;
            Radius = radius;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
        public float Radius { get; }
    }

    internal readonly struct WofWorldWillowLobe
    {
        public WofWorldWillowLobe(Vector3 position, float radius, Vector3 scale, Color32 color)
        {
            Position = position;
            Radius = radius;
            Scale = scale;
            Color = color;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public Vector3 Scale { get; }
        public Color32 Color { get; }
    }

    internal readonly struct WofWorldWillowVine
    {
        public WofWorldWillowVine(float x, float y, float z, float length, float sway)
        {
            X = x;
            Y = y;
            Z = z;
            Length = length;
            Sway = sway;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Length { get; }
        public float Sway { get; }
        public Vector3 Start => new(X, Y, Z);
        public Vector3 End => new(
            X + Mathf.Sin(Sway) * 1.25f,
            Y - Length,
            Z + Mathf.Cos(Sway) * 1.25f);
        public Vector3 LeafPosition => new(
            X + Mathf.Sin(Sway) * 0.65f,
            Y - Length * 0.52f,
            Z + Mathf.Cos(Sway) * 0.65f);
    }

    internal readonly struct WofWorldWillowParticle
    {
        public WofWorldWillowParticle(float angle, float radius, float height, float speed, float size, float phase)
        {
            Angle = angle;
            Radius = radius;
            Height = height;
            Speed = speed;
            Size = size;
            Phase = phase;
        }

        public float Angle { get; }
        public float Radius { get; }
        public float Height { get; }
        public float Speed { get; }
        public float Size { get; }
        public float Phase { get; }
    }

    internal static class WofSurvivalWorldWillowRules
    {
        internal const int WillowCount = 6;
        internal const int BranchCount = 8;
        internal const int LobeCount = 11;
        internal const int VineCount = 14;
        internal const int DesktopParticleCount = 72;
        internal const int MobileParticleCount = 36;
        internal const float MobileParticleUpdateInterval = 1f / 24f;

        private static readonly Color32[] PlainsCanopy = Colors("#4f8730", "#76aa48", "#9ac45a");
        private static readonly Color32[] DesertCanopy = Colors("#7f974f", "#a9a85f", "#c2b76e");
        private static readonly Color32[] SwampCanopy = Colors("#51672e", "#6f7e38", "#89a04f");
        private static readonly Color32[] MushroomCanopy = Colors("#7851a2", "#9f65c7", "#c47ddb");
        private static readonly Color32[] JungleCanopy = Colors("#145a2c", "#1f793c", "#4f9a45");

        internal static WofWorldWillowRecord[] MakeWillows()
        {
            var records = new WofWorldWillowRecord[WillowCount];
            for (var index = 0; index < WillowCount; index++)
            {
                var angle = index / (double)WillowCount * Math.PI * 2d +
                            (WofSurvivalTerrainMath.Hash01(index, 31, 9100) - 0.5d) * 0.54d;
                var radius = WofSurvivalTerrainMath.BlockSize *
                             (2.35d + WofSurvivalTerrainMath.Hash01(index, 32, 9101) * 5.15d);
                var x = Math.Cos(angle) * radius;
                var z = Math.Sin(angle) * radius;
                var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(x);
                var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(z);
                for (var attempt = 0; attempt < 8 && HasReactVillage(chunkX, chunkZ); attempt++)
                {
                    angle += 0.34d + attempt * 0.08d;
                    radius = Math.Min(WofSurvivalTerrainMath.BlockSize * 7.8d,
                        radius + WofSurvivalTerrainMath.BlockSize * 0.22d);
                    x = Math.Cos(angle) * radius;
                    z = Math.Sin(angle) * radius;
                    chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(x);
                    chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(z);
                }

                var biome = WofSurvivalTerrainMath.GetBiome(chunkX, chunkZ);
                var y = Math.Max(
                            WofSurvivalTerrainMath.GetRawTerrainHeightAtWorld(x, z),
                            WofSurvivalTerrainMath.GetWaterLevelAtWorld(x, z) + 1.4d) + 0.1d;
                records[index] = new WofWorldWillowRecord(
                    index,
                    new Vector3((float)x, (float)y, (float)z),
                    chunkX,
                    chunkZ,
                    (float)(angle + WofSurvivalTerrainMath.Hash01(index, 33, 9102) * Math.PI),
                    (float)(1.18d + WofSurvivalTerrainMath.Hash01(index, 34, 9103) * 0.58d),
                    biome,
                    WofSurvivalTerrainMath.Hash01(index, 35, 9104));
            }
            return records;
        }

        internal static WofWorldWillowBranch[] MakeBranches(WofWorldWillowRecord willow)
        {
            var branches = new WofWorldWillowBranch[BranchCount];
            for (var index = 0; index < BranchCount; index++)
            {
                var side = index % 2 == 0 ? 1d : -1d;
                var angle = index * 0.78d + willow.Variant * 2.2d;
                var startY = willow.TrunkHeight * (0.38d + index * 0.052d);
                var length = willow.CanopyRadius *
                             (0.62d + Hash(index, willow.Variant, 9200) * 0.54d);
                branches[index] = new WofWorldWillowBranch(
                    new Vector3(0f, (float)startY, 0f),
                    new Vector3(
                        (float)(Math.Sin(angle) * length * side),
                        (float)(startY + willow.CanopyHeight *
                            (0.16d + Hash(index, willow.Variant, 9210) * 0.42d)),
                        (float)(Math.Cos(angle) * length)),
                    Mathf.Max(0.42f, willow.TrunkRadius * (0.34f - index * 0.018f)));
            }
            return branches;
        }

        internal static WofWorldWillowLobe[] MakeLobes(WofWorldWillowRecord willow)
        {
            var colors = GetCanopyColors(willow.Biome);
            var lobes = new WofWorldWillowLobe[LobeCount];
            lobes[0] = new WofWorldWillowLobe(
                new Vector3(0f, willow.TrunkHeight + willow.CanopyHeight * 0.18f, 0f),
                willow.CanopyRadius * 0.72f,
                new Vector3(1.12f, 0.86f, 1.04f),
                colors[0]);
            for (var index = 0; index < 10; index++)
            {
                var angle = index * 0.64d + willow.Variant * 4.1d;
                var radius = willow.CanopyRadius * (0.32d + Hash(index, willow.Variant, 9220) * 0.58d);
                var lobeSize = willow.CanopyRadius * (0.38d + Hash(index, willow.Variant, 9230) * 0.32d);
                lobes[index + 1] = new WofWorldWillowLobe(
                    new Vector3(
                        (float)(Math.Sin(angle) * radius),
                        (float)(willow.TrunkHeight + willow.CanopyHeight *
                            (0.02d + Hash(index, willow.Variant, 9240) * 0.46d)),
                        (float)(Math.Cos(angle) * radius)),
                    (float)lobeSize,
                    new Vector3(
                        (float)(0.78d + Hash(index, willow.Variant, 9250) * 0.36d),
                        (float)(0.62d + Hash(index, willow.Variant, 9260) * 0.3d),
                        (float)(0.74d + Hash(index, willow.Variant, 9270) * 0.38d)),
                    colors[index % colors.Length]);
            }
            return lobes;
        }

        internal static WofWorldWillowVine[] MakeVines(WofWorldWillowRecord willow)
        {
            var vines = new WofWorldWillowVine[VineCount];
            for (var index = 0; index < VineCount; index++)
            {
                var angle = index * 0.45d + willow.Variant * 5.2d;
                var radius = willow.CanopyRadius * (0.45d + Hash(index, willow.Variant, 9360) * 0.62d);
                vines[index] = new WofWorldWillowVine(
                    (float)(Math.Sin(angle) * radius),
                    (float)(willow.TrunkHeight + willow.CanopyHeight *
                        (0.06d + Hash(index, willow.Variant, 9370) * 0.54d)),
                    (float)(Math.Cos(angle) * radius),
                    (float)(willow.Scale * (18d + Hash(index, willow.Variant, 9380) * 32d)),
                    (float)(angle + Hash(index, willow.Variant, 9390) * 1.8d));
            }
            return vines;
        }

        internal static WofWorldWillowParticle[] MakeParticles(WofWorldWillowRecord willow, bool mobile)
        {
            var count = mobile ? MobileParticleCount : DesktopParticleCount;
            var particles = new WofWorldWillowParticle[count];
            for (var index = 0; index < count; index++)
            {
                particles[index] = new WofWorldWillowParticle(
                    (float)(Hash(index, willow.Variant, 9300) * Math.PI * 2d),
                    (float)(willow.CanopyRadius * (0.16d + Hash(index, willow.Variant, 9310) * 0.9d)),
                    (float)(willow.CanopyHeight * (0.08d + Hash(index, willow.Variant, 9320) * 0.98d)),
                    (float)(0.34d + Hash(index, willow.Variant, 9330) * 0.42d),
                    (float)(willow.Scale * (0.42d + Hash(index, willow.Variant, 9340) * 0.72d)),
                    (float)(Hash(index, willow.Variant, 9350) * Math.PI * 2d));
            }
            return particles;
        }

        internal static Vector3 GetParticleLocalPosition(
            WofWorldWillowRecord willow,
            WofWorldWillowParticle particle,
            double elapsedSeconds)
        {
            var drift = elapsedSeconds * particle.Speed + particle.Phase;
            var fall = Repeat(particle.Height + drift * 12d, willow.CanopyHeight);
            var angle = particle.Angle + Math.Sin(drift * 0.7d) * 0.18d;
            var radius = particle.Radius + Math.Sin(drift * 1.3d) * willow.Scale * 2.6d;
            return new Vector3(
                (float)(Math.Sin(angle) * radius),
                (float)(willow.TrunkHeight + willow.CanopyHeight * 0.26d + willow.CanopyHeight - fall),
                (float)(Math.Cos(angle) * radius));
        }

        internal static float GetParticleScale(WofWorldWillowParticle particle, double elapsedSeconds)
        {
            var drift = elapsedSeconds * particle.Speed + particle.Phase;
            return particle.Size * (float)(0.72d + Math.Sin(drift * 2.2d) * 0.18d);
        }

        internal static bool IsVisible(WofWorldWillowRecord willow, int centerX, int centerZ, int renderRadius)
        {
            return Math.Max(Math.Abs(willow.ChunkX - centerX), Math.Abs(willow.ChunkZ - centerZ)) <= renderRadius + 1;
        }

        internal static bool ShouldShowWillows(bool survivalSession, bool grassInspectionView)
        {
            return survivalSession && !grassInspectionView;
        }

        internal static bool ShouldShowParticles(
            WofWorldWillowRecord willow,
            int centerX,
            int centerZ,
            int renderRadius)
        {
            return Math.Max(Math.Abs(willow.ChunkX - centerX), Math.Abs(willow.ChunkZ - centerZ)) <= renderRadius;
        }

        internal static Color32 GetTrunkColor(WofSurvivalBiome biome)
        {
            return biome == WofSurvivalBiome.Mushroom ? Hex("#51315c") :
                biome == WofSurvivalBiome.Desert ? Hex("#6f5428") : Hex("#332315");
        }

        internal static Color32 GetBranchColor(WofSurvivalBiome biome)
        {
            return biome == WofSurvivalBiome.Mushroom ? Hex("#68406f") :
                biome == WofSurvivalBiome.Desert ? Hex("#7a5f30") : Hex("#2c2214");
        }

        internal static Color32 GetEdgeColor(WofSurvivalBiome biome)
        {
            return biome == WofSurvivalBiome.Mushroom ? Hex("#311839") : Hex("#1a2d12");
        }

        internal static Color32 GetCanopyColor(WofSurvivalBiome biome, int index)
        {
            var colors = GetCanopyColors(biome);
            return colors[Math.Abs(index) % colors.Length];
        }

        internal static Color32 VineColor => Hex("#1f4f20");
        internal static Color32 VineLeafColor => Hex("#2f7b35");
        internal static Color32 ParticleColor => new(217, 249, 157, 148);

        private static bool HasReactVillage(int chunkX, int chunkZ)
        {
            return WofSurvivalTerrainMath.IsAuthoredChunk(chunkX, chunkZ) && !(chunkX == 0 && chunkZ == 0);
        }

        private static double Hash(int index, double variant, int salt)
        {
            return WofSurvivalTerrainMath.Hash01(index, variant * 1000d, salt);
        }

        private static double Repeat(double value, double length)
        {
            return value - Math.Floor(value / length) * length;
        }

        private static Color32[] GetCanopyColors(WofSurvivalBiome biome)
        {
            return biome switch
            {
                WofSurvivalBiome.Desert => DesertCanopy,
                WofSurvivalBiome.Swamp => SwampCanopy,
                WofSurvivalBiome.Mushroom => MushroomCanopy,
                WofSurvivalBiome.Jungle => JungleCanopy,
                _ => PlainsCanopy
            };
        }

        private static Color32[] Colors(string first, string second, string third)
        {
            return new[] { Hex(first), Hex(second), Hex(third) };
        }

        private static Color32 Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out var color) ? (Color32)color : default;
        }
    }
}
