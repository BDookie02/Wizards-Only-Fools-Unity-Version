using System;
using UnityEngine;

namespace WOF
{
    internal enum WofSurvivalBiome
    {
        Plains = 0,
        Jungle = 1,
        Desert = 2,
        Swamp = 3,
        Mushroom = 4,
        Tallgrass = 5
    }

    internal readonly struct WofSurvivalRiverCarve
    {
        public WofSurvivalRiverCarve(double strength, double bed)
        {
            Strength = strength;
            Bed = bed;
        }

        public double Strength { get; }
        public double Bed { get; }
    }

    internal static class WofSurvivalTerrainMath
    {
        public const int BlockSize = 512;
        public const int RenderRadius = 3;
        public const int NearRadius = 1;
        public const int CollisionRadius = 2;
        public const double CenterHysteresis = BlockSize * 0.72d;
        public const double DetailUvWorldSize = BlockSize * 0.93d;
        public const double EdgeOverlap = 0.08d;
        public const double SkirtDepth = 44d;
        public const double SkirtTopInset = 0.04d;

        private const double BiomeHexRadius = BlockSize * 0.62d;
        private const double BiomeBlendInnerRadius = 0.18d;
        private const double BiomeBlendOuterRadius = 1.86d;
        private const double BiomeBlendPower = 2.15d;
        private const double StrictDesertWeight = 0.9d;
        private const double DesertExpansionMinX = 3d * BlockSize - BlockSize * 0.5d;
        private const double DesertExpansionMaxX = 5d * BlockSize + BlockSize * 0.5d;
        private const double DesertExpansionMinZ = -4d * BlockSize - BlockSize * 0.5d;
        private const double DesertExpansionMaxZ = -3d * BlockSize + BlockSize * 0.5d;
        private const double DesertExpansionBlendDistance = 192d;
        private const double DesertVillageCenterX = 4d * BlockSize;
        private const double DesertVillageCenterZ = -4d * BlockSize;
        private const double DesertVillageHalfSize = BlockSize * 0.5d;
        private const double DesertVillageBaseHeight = 17.885722662941443d;
        private const double BaseVillageHalfSize = 256d;
        private const double BaseVillageExitHeight = 2d;
        private const double BaseVillageExitBlendDistance = 220d;
        private const double BaseVillageApronDistance = 172d;
        private const double GraveyardPadFlatRadius = 246d;
        private const int GraveyardChunkX = 5;
        private const int GraveyardChunkZ = 2;
        private const int DarrelChunkX = 12;
        private const int DarrelChunkZ = -12;
        private const int LilyChunkX = 48;
        private const int LilyChunkZ = -48;
        private const double RouteCoreWidth = 4.5d;
        private const double RouteShoulderWidth = 10d;
        private const double RouteMaxMeander = 4.25d;
        private const int BiomeCount = 6;

        private static readonly WofSurvivalBiome[] Biomes =
        {
            WofSurvivalBiome.Plains,
            WofSurvivalBiome.Jungle,
            WofSurvivalBiome.Desert,
            WofSurvivalBiome.Swamp,
            WofSurvivalBiome.Mushroom,
            WofSurvivalBiome.Tallgrass
        };

        private static readonly Rgb[] GroundColors =
        {
            Hex("#477f2c"), Hex("#27652d"), Hex("#d3ad62"),
            Hex("#385333"), Hex("#5b477c"), Hex("#47892d")
        };

        private static readonly Rgb[] AccentColors =
        {
            Hex("#5fa43a"), Hex("#124822"), Hex("#aa7c31"),
            Hex("#667638"), Hex("#c865d6"), Hex("#64ad39")
        };

        private static readonly Rgb RockColor = Hex("#7a745f");
        private static readonly Rgb PeakColor = Hex("#d6d1bc");
        private static readonly Rgb WaterTintColor = Hex("#4f7042");
        private static readonly Rgb MeadowDarkColor = Hex("#3f7d28");
        private static readonly Rgb MeadowLightColor = Hex("#5ba335");
        private static readonly Rgb MeadowBaseColor = Hex("#4f9631");
        private static readonly Rgb MeadowLiftColor = Hex("#5fa836");
        private static readonly Rgb RestoredDarkColor = Hex("#43842b");
        private static readonly Rgb RestoredMidColor = Hex("#549b31");
        private static readonly Rgb RestoredLightColor = Hex("#63aa38");

        private static readonly Elevation[] Elevations =
        {
            new(3.2d, 24d, 19d, 38d, 15d, 2.1d, 1.55d),
            new(6.4d, 34d, 27d, 58d, 20d, 2.9d, 2.25d),
            new(2.3d, 29d, 26d, 48d, 13d, 3.15d, 0.95d),
            new(0.9d, 15d, 12d, 24d, 10d, 1.4d, 1.05d),
            new(4.8d, 31d, 25d, 52d, 18d, 2.65d, 1.85d),
            new(4.1d, 21d, 15d, 30d, 12d, 1.75d, 1.45d)
        };

        private static readonly MountainProfile[] MountainProfiles =
        {
            new(0.42d, 320d, 28d),
            new(0.6d, 355d, 44d),
            new(0.48d, 345d, 36d),
            new(0.26d, 270d, 18d),
            new(0.54d, 330d, 40d),
            new(0.34d, 310d, 24d)
        };

        private static readonly HexOffset[] BiomeOffsets = MakeBiomeOffsets();
        [ThreadStatic] private static double[] _rawHeightWeights;
        [ThreadStatic] private static double[] _waterWeights;
        [ThreadStatic] private static double[] _colorWeights;
        [ThreadStatic] private static double[] _strictDesertWeights;
        private static double[] RawHeightWeights => _rawHeightWeights ??= new double[BiomeCount];
        private static double[] WaterWeights => _waterWeights ??= new double[BiomeCount];
        private static double[] ColorWeights => _colorWeights ??= new double[BiomeCount];
        private static double[] StrictDesertWeights => _strictDesertWeights ??= new double[BiomeCount];
        private static readonly ChunkPoint[] RestoredMeadowCenters =
        {
            new(6, -3), new(6, -4)
        };

        private static readonly RouteSegment[] TownRoutes =
        {
            new(0, 0, 0, -3),
            new(0, -3, -3, -3),
            new(0, -3, 4, -4),
            new(0, 0, 3, 0),
            new(3, 0, 5, 2),
            new(4, -4, DarrelChunkX, DarrelChunkZ),
            new(DarrelChunkX, DarrelChunkZ, LilyChunkX, LilyChunkZ)
        };

        internal static int GetChunkCoordinate(double value)
        {
            return (int)Math.Floor((value + BlockSize * 0.5d) / BlockSize);
        }

        internal static int RecenterCoordinate(int currentChunk, double worldCoordinate)
        {
            var local = worldCoordinate - currentChunk * (double)BlockSize;
            while (local > CenterHysteresis)
            {
                currentChunk++;
                local -= BlockSize;
            }
            while (local < -CenterHysteresis)
            {
                currentChunk--;
                local += BlockSize;
            }
            return currentChunk;
        }

        internal static int GetRenderSegments(int distance)
        {
            if (distance == 0) return 32;
            return distance <= NearRadius ? 12 : 8;
        }

        internal static int GetCollisionSegments(int distance)
        {
            return distance <= CollisionRadius ? 32 : 0;
        }

        internal static bool IsInsideBakedAtlas(int cx, int cz)
        {
            return cx >= -4 && cx <= 6 && cz >= -4 && cz <= 3;
        }

        internal static bool IsAuthoredChunk(int cx, int cz)
        {
            return (cx == 0 && cz == 0) ||
                   (cx == -3 && cz == -3) ||
                   (cx == 4 && cz == -4) ||
                   (cx == 0 && cz == -3) ||
                   (cx == 3 && cz == 0) ||
                   (cx == GraveyardChunkX && cz == GraveyardChunkZ) ||
                   (cx == DarrelChunkX && cz == DarrelChunkZ) ||
                   (cx == LilyChunkX && cz == LilyChunkZ);
        }

        private static bool IsSpecialVillageChunk(int cx, int cz)
        {
            return (cx == -3 && cz == -3) ||
                   (cx == 4 && cz == -4) ||
                   (cx == 0 && cz == -3) ||
                   (cx == 3 && cz == 0) ||
                   (cx == GraveyardChunkX && cz == GraveyardChunkZ) ||
                   (cx == DarrelChunkX && cz == DarrelChunkZ) ||
                   (cx == LilyChunkX && cz == LilyChunkZ);
        }

        internal static bool IsLilyRealmCenter(int cx, int cz)
        {
            return Math.Max(Math.Abs(cx - LilyChunkX), Math.Abs(cz - LilyChunkZ)) <= NearRadius;
        }

        internal static string GetBiomeName(WofSurvivalBiome biome)
        {
            return biome switch
            {
                WofSurvivalBiome.Plains => "plains",
                WofSurvivalBiome.Jungle => "jungle",
                WofSurvivalBiome.Desert => "desert",
                WofSurvivalBiome.Swamp => "swamp",
                WofSurvivalBiome.Mushroom => "mushroom",
                WofSurvivalBiome.Tallgrass => "tallgrass",
                _ => throw new ArgumentOutOfRangeException(nameof(biome), biome, null)
            };
        }

        internal static WofSurvivalBiome GetBiome(int cx, int cz)
        {
            if (cx == 0 && cz == 0) return WofSurvivalBiome.Plains;
            if (IsDesertVillageExpansionChunk(cx, cz)) return WofSurvivalBiome.Desert;
            if (cz == -3 && cx == 6) return WofSurvivalBiome.Tallgrass;
            if (cz == -4 && (cx == 4 || cx == 6)) return WofSurvivalBiome.Desert;
            if (cz == -2 && (cx == 4 || cx == 5)) return WofSurvivalBiome.Plains;
            return Biomes[(int)Math.Floor(Hash01(cx, cz, 1) * BiomeCount) % BiomeCount];
        }

        internal static bool HasRiver(int cx, int cz)
        {
            var biome = GetBiome(cx, cz);
            if (IsSpecialVillageChunk(cx, cz) && !(cx == 0 && cz == -3)) return false;
            if (biome == WofSurvivalBiome.Swamp) return true;
            var macroRibbon = Math.Abs(cz - JsRound(Math.Sin(cx * 0.52d) * 2.1d + Math.Sin(cx * 0.16d) * 1.4d)) <= 0;
            var crossingRibbon = Math.Abs(cx - JsRound(Math.Cos(cz * 0.47d) * 2.2d + Math.Sin(cz * 0.12d) * 1.2d)) <= 0;
            var ribbonKeep = Hash01(cx, cz, 407) > (biome == WofSurvivalBiome.Jungle ? 0.34d : 0.58d);
            return (macroRibbon || crossingRibbon) && ribbonKeep;
        }

        internal static bool IsRiverVertical(int cx, int cz)
        {
            return Hash01(cx, cz, 5) > 0.5d;
        }

        internal static double GetChunkRiverMask(int cx, int cz, double worldX, double worldZ)
        {
            if (!HasRiver(cx, cz)) return 0d;
            return GetRiverMask(
                cx * (double)BlockSize,
                cz * (double)BlockSize,
                GetBiome(cx, cz),
                IsRiverVertical(cx, cz),
                GetRiverOffset(cx, cz),
                worldX,
                worldZ);
        }

        internal static double GetRiverWidthForBiome(WofSurvivalBiome biome)
        {
            return GetRiverWidth(biome);
        }

        internal static double GetWaterLevelAtWorld(double worldX, double worldZ)
        {
            return GetWaterLevel(worldX, worldZ);
        }

        internal static bool IsWaterSuppressed(double worldX, double worldZ, double radius)
        {
            return IsRestoredMeadowWaterSuppressed(worldX, worldZ, radius);
        }

        internal static double GetTerrainHeight(int cx, int cz, double localX, double localZ)
        {
            var worldX = cx * (double)BlockSize + localX;
            var worldZ = cz * (double)BlockSize + localZ;
            var height = GetRawTerrainHeight(worldX, worldZ);

            var riverCarve = GetRiverCarve(worldX, worldZ);
            var restoredMeadowRiverSuppression = IsRestoredMeadowWaterSuppressed(worldX, worldZ, 96d)
                ? 1d
                : SmoothstepRange(0.02d, 0.18d, GetRestoredMeadowMask(worldX, worldZ));
            var riverStrength = riverCarve.Strength * (1d - restoredMeadowRiverSuppression);
            if (riverStrength > 0d)
                height = Lerp(height, Math.Min(height, riverCarve.Bed), riverStrength);

            var gateRoadDistance = Math.Min(Math.Abs(worldX), Math.Abs(worldZ));
            var gateRoadMask = 1d - SmoothstepRange(14d, 46d, gateRoadDistance);
            if (gateRoadMask > 0d)
            {
                var travelAxis = Math.Abs(worldX) < Math.Abs(worldZ) ? worldZ : worldX;
                var distanceFromVillageEdge = Math.Max(0d, Math.Abs(travelAxis) - BaseVillageHalfSize);
                var villageEdgeBlend = 1d - SmoothstepRange(0d, BaseVillageExitBlendDistance, distanceFromVillageEdge);
                var wildernessRoadHeight = 2.1d + Math.Sin(travelAxis * 0.009d) * 0.42d;
                var roadHeight = Lerp(wildernessRoadHeight, BaseVillageExitHeight, villageEdgeBlend);
                height = Lerp(height, roadHeight, gateRoadMask * 0.88d);
            }

            var baseTransitionMask = GetBaseVillageTransitionMask(worldX, worldZ);
            if (baseTransitionMask > 0d)
                height = Lerp(height, BaseVillageExitHeight, baseTransitionMask);

            var graveyardGateMask = GetGraveyardGateClearingMask(
                worldX - GraveyardChunkX * (double)BlockSize,
                worldZ - GraveyardChunkZ * (double)BlockSize);
            if (graveyardGateMask > 0d)
            {
                var graveyardBase = GetRawTerrainHeight(
                    GraveyardChunkX * (double)BlockSize,
                    GraveyardChunkZ * (double)BlockSize) - 0.46d;
                height = Lerp(height, graveyardBase, graveyardGateMask * 0.99d);
            }

            var apron = GetGraveyardExteriorApron(worldX, worldZ);
            if (apron.Mask > 0d) height = Lerp(height, apron.Height, apron.Mask);

            var townRouteMask = GetTownRouteMask(worldX, worldZ);
            if (townRouteMask > 0d && IsStrictDesert(worldX, worldZ))
            {
                var restoredSuppression = SmoothstepRange(0.001d, 0.08d, GetRestoredMeadowMask(worldX, worldZ));
                height -= SmoothstepRange(0.72d, 1d, townRouteMask) * 0.06d * (1d - restoredSuppression);
            }

            var desertFoundationMask = GetDesertVillageFoundationMaskAtWorld(worldX, worldZ);
            if (desertFoundationMask > 0d)
                height = Lerp(height, DesertVillageBaseHeight, desertFoundationMask);

            return height;
        }

        internal static double GetDesertVillageFoundationMaskAtWorld(double worldX, double worldZ)
        {
            var outsideX = Math.Max(0d, Math.Abs(worldX - DesertVillageCenterX) - DesertVillageHalfSize);
            var outsideZ = Math.Max(0d, Math.Abs(worldZ - DesertVillageCenterZ) - DesertVillageHalfSize);
            var outsideDistance = Math.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
            return 1d - SmoothstepRange(0d, DesertExpansionBlendDistance, outsideDistance);
        }

        internal static Color GetRenderedTerrainColor(double worldX, double worldZ, double height)
        {
            var color = GetSmoothedTerrainColor(worldX, worldZ, height);
            var restoredMeadowMask = GetRestoredMeadowMask(worldX, worldZ);
            if (restoredMeadowMask > 0.02d)
            {
                var fineShade = Math.Sin(worldX * 0.17d + worldZ * 0.29d) * 0.016d +
                                Math.Cos(worldZ * 0.23d - worldX * 0.11d) * 0.012d;
                var grassBlend = SmoothstepRange(0.02d, 0.18d, restoredMeadowMask);
                var terrainFiber = SmoothstepRange(-0.62d, 0.9d, Math.Sin(worldX * 0.063d + worldZ * 0.041d));
                var restoredGround = Rgb.Lerp(MeadowBaseColor, MeadowDarkColor,
                    SmoothstepRange(0.72d, 1d, 1d - terrainFiber) * 0.24d);
                restoredGround = Rgb.Lerp(restoredGround, MeadowLiftColor, terrainFiber * 0.22d);
                color = Rgb.Lerp(color, restoredGround, grassBlend * 0.96d) * (0.9d + fineShade);
                color = color.Clamped();
            }
            return new Color((float)color.R, (float)color.G, (float)color.B, 1f);
        }

        internal static double Hash01(double x, double z, double salt = 0d)
        {
            var value = Math.Sin(x * 127.1d + z * 311.7d + salt * 74.7d) * 43758.5453123d;
            return value - Math.Floor(value);
        }

        internal static double GetTownRouteMaskAtWorld(double worldX, double worldZ)
        {
            return GetTownRouteMask(worldX, worldZ);
        }

        private static Rgb GetSmoothedTerrainColor(double worldX, double worldZ, double height)
        {
            var target = new Rgb(0d, 0d, 0d);
            const double sampleRadius = 16d;
            target += GetTerrainColor(worldX, worldZ, height) * 0.38d;
            target += GetTerrainColor(worldX + sampleRadius, worldZ,
                GetRawTerrainHeight(worldX + sampleRadius, worldZ)) * 0.09d;
            target += GetTerrainColor(worldX - sampleRadius, worldZ,
                GetRawTerrainHeight(worldX - sampleRadius, worldZ)) * 0.09d;
            target += GetTerrainColor(worldX, worldZ + sampleRadius,
                GetRawTerrainHeight(worldX, worldZ + sampleRadius)) * 0.09d;
            target += GetTerrainColor(worldX, worldZ - sampleRadius,
                GetRawTerrainHeight(worldX, worldZ - sampleRadius)) * 0.09d;
            return target * (1d / 0.74d);
        }

        private static Rgb GetTerrainColor(double worldX, double worldZ, double height)
        {
            GetBiomeWeights(worldX, worldZ, ColorWeights);
            var color = new Rgb(0d, 0d, 0d);
            double meadowWeight = 0d;
            double lushMeadowWeight = 0d;
            double grasslandWeight = 0d;
            double desertWeight = 0d;

            for (var index = 0; index < BiomeCount; index++)
            {
                var weight = ColorWeights[index];
                if (weight <= 0d) continue;
                var biome = (WofSurvivalBiome)index;
                var biomeIndex = index + 1d;
                var patchNoise = (Math.Sin(worldX * (0.021d + biomeIndex * 0.002d) + worldZ * 0.013d + biomeIndex * 1.7d) +
                                  Math.Cos(worldZ * (0.018d + biomeIndex * 0.0017d) - worldX * 0.009d)) * 0.5d;
                var fineNoise = Math.Sin(worldX * 0.087d + worldZ * 0.061d + biomeIndex * 2.1d) * 0.5d + 0.5d;
                var accentMix = biome == WofSurvivalBiome.Desert
                    ? 0.09d + SmoothstepRange(-0.15d, 0.85d, patchNoise) * 0.05d
                    : biome == WofSurvivalBiome.Jungle || biome == WofSurvivalBiome.Swamp
                        ? 0.14d + SmoothstepRange(-0.35d, 0.9d, patchNoise) * 0.13d
                        : 0.1d + SmoothstepRange(-0.25d, 0.95d, patchNoise) * 0.1d;
                var speckle = (fineNoise - 0.5d) * (biome == WofSurvivalBiome.Desert ? 0.012d : 0.014d);
                var local = Rgb.Lerp(GroundColors[index], AccentColors[index], accentMix) + new Rgb(speckle, speckle, speckle);
                color += local * weight;
                if (biome == WofSurvivalBiome.Desert) desertWeight += weight;
                else
                {
                    meadowWeight += weight;
                    if (biome == WofSurvivalBiome.Tallgrass || biome == WofSurvivalBiome.Jungle || biome == WofSurvivalBiome.Mushroom)
                        lushMeadowWeight += weight;
                }
                if (biome == WofSurvivalBiome.Plains || biome == WofSurvivalBiome.Tallgrass || biome == WofSurvivalBiome.Jungle)
                    grasslandWeight += weight;
            }

            var rockMix = SmoothstepRange(58d, 142d, height);
            var peakMix = SmoothstepRange(148d, 230d, height);
            var lowMix = SmoothstepRange(1.2d, -1.5d, height);
            var contour = Math.Sin(height * 0.42d + worldX * 0.013d + worldZ * 0.009d) * 0.004d;
            var cliffStripe = Math.Pow(Math.Max(0d, Math.Sin(height * 0.58d + worldX * 0.007d)), 3d) * rockMix;
            var grassRows = Math.Sin(worldX * 0.028d + Math.Sin(worldZ * 0.012d) * 2.2d) * 0.002d;
            var altitudeShade = Lerp(0.92d, 1.08d, SmoothstepRange(-16d, 92d, height));
            var shade = (0.995d + Math.Sin(worldX * 0.041d + worldZ * 0.029d) * 0.008d + contour + grassRows) * altitudeShade;

            color = Rgb.Lerp(color, RockColor, rockMix * 0.14d);
            color = Rgb.Lerp(color, PeakColor, peakMix * 0.18d);
            color = Rgb.Lerp(color, new Rgb(0.64d, 0.58d, 0.47d), cliffStripe * 0.1d);
            color = Rgb.Lerp(color, WaterTintColor, lowMix * 0.06d);

            var meadowFiber = Math.Sin(worldX * 0.092d + worldZ * 0.038d) * 0.5d +
                              Math.Cos(worldZ * 0.084d - worldX * 0.027d) * 0.5d;
            var meadowSpeckle = Math.Sin(worldX * 0.47d + worldZ * 0.31d) * 0.5d + 0.5d;
            var meadowMask = meadowWeight * (1d - rockMix * 0.68d) * (1d - peakMix * 0.84d) * (1d - lowMix * 0.04d);
            var meadowTint = 0.46d + SmoothstepRange(-0.34d, 0.88d, meadowFiber) * 0.34d +
                             meadowSpeckle * 0.06d + lushMeadowWeight * 0.26d;
            var meadowColor = Rgb.Lerp(MeadowDarkColor, MeadowLightColor, SmoothstepRange(-0.18d, 0.95d, meadowFiber));
            color = Rgb.Lerp(color, meadowColor, meadowMask * meadowTint);

            var meadowUnifier = grasslandWeight * (1d - rockMix * 0.72d) * (1d - peakMix * 0.88d) * 0.9d;
            var meadowBaseFiber = Math.Sin(worldX * 0.049d + worldZ * 0.033d) * 0.5d +
                                  Math.Cos(worldZ * 0.044d - worldX * 0.021d) * 0.5d;
            var meadowBaseBlend = SmoothstepRange(-0.68d, 0.88d, meadowBaseFiber);
            var meadowUnified = Rgb.Lerp(MeadowDarkColor, MeadowBaseColor, 0.74d);
            meadowUnified = Rgb.Lerp(meadowUnified, MeadowLiftColor, meadowBaseBlend * 0.18d);
            color = Rgb.Lerp(color, meadowUnified, meadowUnifier);

            var restoredMeadowMask = GetRestoredMeadowMask(worldX, worldZ) *
                                     (1d - rockMix * 0.22d) * (1d - peakMix * 0.68d);
            if (restoredMeadowMask > 0.001d)
            {
                var restoredFiber = Math.Sin(worldX * 0.044d + worldZ * 0.022d) * 0.26d +
                                    Math.Cos(worldZ * 0.038d - worldX * 0.018d) * 0.24d;
                var restoredFine = Math.Sin(worldX * 0.68d + worldZ * 0.41d) * 0.5d + 0.5d;
                var restored = Rgb.Lerp(RestoredDarkColor, RestoredMidColor, 0.58d);
                restored = Rgb.Lerp(restored, RestoredLightColor, SmoothstepRange(-0.64d, 1.12d, restoredFiber) * 0.24d);
                color = Rgb.Lerp(color, restored, Clamp01(restoredMeadowMask * (0.52d + restoredFine * 0.04d)));
            }

            var strictDesert = desertWeight > StrictDesertWeight && restoredMeadowMask <= 0.015d &&
                               meadowWeight < 0.12d && grasslandWeight < 0.1d;
            if (!strictDesert)
            {
                var coverMask = Clamp01((1d - desertWeight) * 0.78d + meadowWeight * 0.58d +
                                        grasslandWeight * 0.92d + restoredMeadowMask * 1.35d);
                var surfaceCover = SmoothstepRange(0.08d, 0.52d, coverMask) *
                                   (1d - rockMix * 0.36d) * (1d - peakMix * 0.62d);
                if (surfaceCover > 0.001d)
                {
                    var coverFiber = Math.Sin(worldX * 0.053d + worldZ * 0.031d) * 0.5d +
                                     Math.Cos(worldZ * 0.047d - worldX * 0.019d) * 0.5d;
                    var restoredUndergrassMask = SmoothstepRange(0.02d, 0.18d, restoredMeadowMask);
                    var cover = Rgb.Lerp(restoredUndergrassMask > 0.001d ? MeadowDarkColor : RestoredDarkColor,
                        MeadowLiftColor, SmoothstepRange(-0.72d, 0.9d, coverFiber));
                    color = Rgb.Lerp(color, cover,
                        surfaceCover * Lerp(0.82d, 0.64d, restoredUndergrassMask));
                }
            }

            var restoredSurfaceSmooth = SmoothstepRange(0.001d, 0.08d, restoredMeadowMask);
            if (restoredSurfaceSmooth > 0.001d)
            {
                var fineVariation = Math.Sin(worldX * 0.31d + worldZ * 0.19d) * 0.012d +
                                    Math.Cos(worldZ * 0.53d - worldX * 0.17d) * 0.008d;
                var meadowGround = MeadowBaseColor + new Rgb(fineVariation * 0.48d, fineVariation * 0.42d, fineVariation * 0.34d);
                color = Rgb.Lerp(color, meadowGround, restoredSurfaceSmooth * 0.34d);
            }

            var finalShade = restoredSurfaceSmooth > 0.001d
                ? Lerp(shade, 0.92d, restoredSurfaceSmooth * 0.84d)
                : shade;
            return (color * finalShade).Clamped();
        }

        private static double GetRawTerrainHeight(double worldX, double worldZ)
        {
            GetBiomeWeights(worldX, worldZ, RawHeightWeights);
            var height = 0d;
            for (var index = 0; index < BiomeCount; index++)
            {
                var weight = RawHeightWeights[index];
                if (weight > 0d) height += GetBiomeTerrainHeight((WofSurvivalBiome)index, worldX, worldZ) * weight;
            }
            return height;
        }

        private static double GetBiomeTerrainHeight(WofSurvivalBiome biome, double worldX, double worldZ)
        {
            var elevation = Elevations[(int)biome];
            var biomeSeed = (int)biome + 1d;
            var continental = Math.Sin(worldX * 0.0022d + 1.7d) * Math.Cos(worldZ * 0.0026d - 0.9d);
            var rolling = (Math.Sin(worldX * 0.0042d + worldZ * 0.0014d) +
                           Math.Cos(worldZ * 0.0037d - worldX * 0.0018d)) * 0.5d;
            var broadHills = (Math.Sin(worldX * 0.00155d + Math.Sin(worldZ * 0.00072d) * 1.6d) +
                              Math.Cos(worldZ * 0.00145d - Math.Sin(worldX * 0.00078d) * 1.35d) +
                              Math.Sin((worldX + worldZ) * 0.00095d + 4.2d)) / 3d;
            var plateauNoise = (Math.Sin(worldX * 0.00074d + worldZ * 0.00026d + 2.3d) +
                                Math.Cos(worldZ * 0.0008d - worldX * 0.00032d - 0.4d)) * 0.5d;
            var hillLift = SmoothstepRange(-0.8d, 0.72d, broadHills) * elevation.Hills * 0.78d;
            var shoulderHills = Math.Pow(SmoothstepRange(-0.52d, 0.9d, rolling + broadHills * 0.58d), 1.02d) * elevation.Hills * 0.38d;
            var ridgeWave = Math.Sin(worldX * 0.0068d + worldZ * 0.0047d + Math.Sin(worldZ * 0.0016d) * 2.1d);
            var ridges = Math.Pow(1d - Math.Abs(ridgeWave), 1.85d);
            var cliffBands = Math.Pow(1d - Math.Abs(Math.Sin(worldX * 0.0032d - worldZ * 0.0041d)), 4.4d);
            var mountainNoise = (Math.Sin(worldX * 0.00105d + 3.1d) * 0.56d +
                                 Math.Cos(worldZ * 0.00118d - 1.4d) * 0.5d +
                                 Math.Sin((worldX - worldZ) * 0.00062d + 0.7d) * 0.38d) / 1.44d;
            var mountainMask = SmoothstepRange(-0.02d, 0.74d, mountainNoise);
            var mountainField = GetBiomeMountainField(biome, worldX, worldZ);
            var valleyNoise = (Math.Cos(worldX * 0.0037d - 0.7d) * 0.52d +
                               Math.Sin(worldZ * 0.0032d + 2.2d) * 0.48d) * 0.5d + 0.5d;
            var valleyMask = SmoothstepRange(0.58d, 0.96d, valleyNoise);
            var basinCut = Math.Pow(SmoothstepRange(0.18d, 0.95d, 1d - mountainNoise), 1.45d) * elevation.Valleys * 0.42d;
            var mountainLift = Math.Pow(mountainMask, 1.38d) * elevation.Mountains;
            var ridgeLift = ridges * elevation.Ridges * (0.38d + mountainMask * 0.58d);
            var cliffLift = cliffBands * elevation.Ridges * SmoothstepRange(0.02d, 0.78d, mountainMask + broadHills * 0.36d) * 0.36d;
            var plateauLift = SmoothstepRange(0.08d, 0.78d, plateauNoise) * elevation.Hills *
                              (biome == WofSurvivalBiome.Desert ? 0.28d : 0.18d);
            var detail = Math.Sin(worldX * 0.024d + worldZ * 0.013d) * 0.75d +
                         Math.Cos(worldZ * 0.019d - worldX * 0.012d) * 0.62d;
            var duneRipples = biome == WofSurvivalBiome.Desert
                ? Math.Sin(worldX * 0.035d + worldZ * 0.011d) * 1.35d + Math.Sin(worldZ * 0.029d) * 0.75d
                : 0d;
            var swampSink = biome == WofSurvivalBiome.Swamp ? SmoothstepRange(0.42d, 0.88d, valleyNoise) * 1.7d : 0d;
            var macroSwell = (Math.Sin(worldX * 0.00128d + Math.Cos(worldZ * 0.00062d) * 2.2d) +
                              Math.Cos(worldZ * 0.00116d - Math.Sin(worldX * 0.00057d) * 2.4d) +
                              Math.Sin((worldX - worldZ) * 0.00082d + biomeSeed * 1.9d)) / 3d;
            var highlandSwell = Math.Pow(SmoothstepRange(-0.52d, 0.82d, macroSwell), 1.08d) * elevation.Mountains *
                                (biome == WofSurvivalBiome.Swamp ? 0.34d : 0.52d);
            var ravineCut = Math.Pow(SmoothstepRange(0.38d, 0.94d,
                -macroSwell + Math.Sin(worldX * 0.0022d + worldZ * 0.0018d) * 0.28d), 1.22d) *
                            elevation.Valleys * (biome == WofSurvivalBiome.Desert ? 0.46d : 0.58d);
            var foldRidgeWave = Math.Sin(worldX * 0.0034d + worldZ * 0.0058d + Math.Sin(worldX * 0.0009d) * 2.5d);
            var foldRidges = Math.Pow(1d - Math.Abs(foldRidgeWave), 1.65d) * elevation.Ridges *
                             (biome == WofSurvivalBiome.Swamp ? 0.3d : 0.52d);

            return elevation.Base + continental * elevation.Hills * 0.42d + rolling * elevation.Hills * 0.32d +
                   hillLift + shoulderHills + ridgeLift + cliffLift + plateauLift + mountainLift -
                   valleyMask * elevation.Valleys * 1.28d - basinCut + highlandSwell + foldRidges - ravineCut +
                   detail * elevation.Detail + mountainField + duneRipples - swampSink;
        }

        private static double GetBiomeMountainField(WofSurvivalBiome biome, double worldX, double worldZ)
        {
            var profile = MountainProfiles[(int)biome];
            var biomeSeed = (int)biome + 1;
            var cellX = (int)Math.Floor(worldX / BlockSize);
            var cellZ = (int)Math.Floor(worldZ / BlockSize);
            var lift = 0d;
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var cx = cellX + dx;
                var cz = cellZ + dz;
                if (Hash01(cx, cz, 820 + biomeSeed * 11) > profile.Chance) continue;
                var centerX = cx * (double)BlockSize + (Hash01(cx, cz, 821 + biomeSeed * 13) - 0.5d) * BlockSize * 0.48d;
                var centerZ = cz * (double)BlockSize + (Hash01(cx, cz, 822 + biomeSeed * 17) - 0.5d) * BlockSize * 0.48d;
                var radius = profile.Radius * (0.9d + Hash01(cx, cz, 823 + biomeSeed * 19) * 0.28d);
                var mountainHeight = profile.Height * (0.72d + Hash01(cx, cz, 824 + biomeSeed * 23) * 0.34d);
                var deltaX = worldX - centerX;
                var deltaZ = worldZ - centerZ;
                var distanceSq = deltaX * deltaX + deltaZ * deltaZ;
                if (distanceSq >= radius * radius) continue;
                var distance = Math.Sqrt(distanceSq);
                var raw = 1d - distance / radius;
                if (raw <= 0d) continue;
                var shoulder = Smoothstep01(raw);
                var summit = SmoothstepRange(0.5d, 1d, shoulder);
                var spire = Math.Pow(SmoothstepRange(0.54d, 0.96d, raw),
                    biome == WofSurvivalBiome.Jungle ? 3d : 3.5d);
                var cliffRidges = Math.Max(0d, Math.Sin(distance * 0.082d + Hash01(cx, cz, 829 + biomeSeed) * Math.PI * 2d));
                var terrace = Math.Floor(shoulder * 7d) / 7d;
                var terracedShoulder = Lerp(shoulder, terrace, biome == WofSurvivalBiome.Desert ? 0.3d : 0.17d);
                var skirt = SmoothstepRange(0.08d, 0.46d, raw) * Math.Max(0d, 1d - raw) * mountainHeight * 0.16d;
                lift += terracedShoulder * terracedShoulder * mountainHeight * 0.92d +
                        summit * mountainHeight * 0.26d +
                        spire * mountainHeight * (biome == WofSurvivalBiome.Jungle ? 0.38d : 0.3d) +
                        cliffRidges * shoulder * mountainHeight * 0.075d + skirt;
            }
            return lift;
        }

        private static void GetBiomeWeights(double worldX, double worldZ, double[] target)
        {
            Array.Clear(target, 0, target.Length);
            var center = WorldToBiomeHex(worldX, worldZ);
            var outerDistance = BiomeHexRadius * BiomeBlendOuterRadius;
            var outerDistanceSq = outerDistance * outerDistance;
            var totalWeight = 0d;
            foreach (var offset in BiomeOffsets)
            {
                var q = center.Q + offset.Q;
                var r = center.R + offset.R;
                var hexCenterX = BiomeHexRadius * Math.Sqrt(3d) * (q + r / 2d);
                var hexCenterZ = BiomeHexRadius * 1.5d * r;
                var dx = worldX - hexCenterX;
                var dz = worldZ - hexCenterZ;
                var distanceSq = dx * dx + dz * dz;
                if (distanceSq >= outerDistanceSq) continue;
                var normalizedDistance = Math.Sqrt(distanceSq) / BiomeHexRadius;
                var falloff = 1d - SmoothstepRange(BiomeBlendInnerRadius, BiomeBlendOuterRadius, normalizedDistance);
                var ringDistance = Math.Max(Math.Abs(offset.Q), Math.Max(Math.Abs(offset.R), Math.Abs(offset.Q + offset.R)));
                var ringDamping = 1d / (1d + ringDistance * 0.08d);
                var weight = Math.Pow(Math.Max(0d, falloff), BiomeBlendPower) * ringDamping;
                totalWeight += weight;
                if (weight > 0.0001d) target[(int)GetBiome(q, r)] += weight;
            }
            if (totalWeight <= 0.0001d)
            {
                target[(int)GetBiome(center.Q, center.R)] = 1d;
            }
            else
            {
                var inverse = 1d / totalWeight;
                for (var index = 0; index < target.Length; index++) target[index] *= inverse;
            }

            // Chunk identity and the visual biome field use different coordinate
            // systems. Apply the requested six-chunk desert footprint in world
            // space, then feather only beyond its perimeter so every point inside
            // the village chunk and its five neighbors remains true desert.
            var desertExpansion = GetDesertVillageExpansionMaskAtWorld(worldX, worldZ);
            if (desertExpansion <= 0d) return;
            var retained = 1d - desertExpansion;
            for (var index = 0; index < target.Length; index++) target[index] *= retained;
            target[(int)WofSurvivalBiome.Desert] += desertExpansion;
        }

        private static HexCoord WorldToBiomeHex(double worldX, double worldZ)
        {
            var q = ((Math.Sqrt(3d) / 3d) * worldX - worldZ / 3d) / BiomeHexRadius;
            var r = ((2d / 3d) * worldZ) / BiomeHexRadius;
            var cubeX = q;
            var cubeZ = r;
            var cubeY = -cubeX - cubeZ;
            var roundedX = JsRound(cubeX);
            var roundedY = JsRound(cubeY);
            var roundedZ = JsRound(cubeZ);
            var xDiff = Math.Abs(roundedX - cubeX);
            var yDiff = Math.Abs(roundedY - cubeY);
            var zDiff = Math.Abs(roundedZ - cubeZ);
            if (xDiff > yDiff && xDiff > zDiff) roundedX = -roundedY - roundedZ;
            else if (yDiff > zDiff) roundedY = -roundedX - roundedZ;
            else roundedZ = -roundedX - roundedY;
            return new HexCoord(roundedX, roundedZ);
        }

        private static WofSurvivalRiverCarve GetRiverCarve(double worldX, double worldZ)
        {
            var centerCx = GetChunkCoordinate(worldX);
            var centerCz = GetChunkCoordinate(worldZ);
            double strength = 0d;
            double bed = 0d;
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            {
                var cx = centerCx + dx;
                var cz = centerCz + dz;
                if (!HasRiver(cx, cz)) continue;
                var biome = GetBiome(cx, cz);
                var carve = GetRiverMask(cx * (double)BlockSize, cz * (double)BlockSize, biome,
                    IsRiverVertical(cx, cz), GetRiverOffset(cx, cz), worldX, worldZ);
                if (carve <= strength) continue;
                strength = carve;
                bed = GetWaterLevel(worldX, worldZ) - (biome == WofSurvivalBiome.Swamp ? 2.6d : 3.4d);
            }
            return new WofSurvivalRiverCarve(strength, bed);
        }

        private static double GetRiverMask(double chunkX, double chunkZ, WofSurvivalBiome biome,
            bool vertical, double offset, double worldX, double worldZ)
        {
            var localX = worldX - chunkX;
            var localZ = worldZ - chunkZ;
            var halfWidth = GetRiverWidth(biome) * 0.5d + (biome == WofSurvivalBiome.Swamp ? 8d : 12d);
            var distance = vertical ? Math.Abs(localX - offset) : Math.Abs(localZ - offset);
            var travelAxis = vertical ? Math.Abs(localZ) : Math.Abs(localX);
            var endFade = 1d - SmoothstepRange(BlockSize * 0.46d, BlockSize * 0.56d, travelAxis);
            var rawMask = Math.Max(0d, 1d - distance / halfWidth) * endFade;
            return Math.Pow(Smoothstep01(rawMask), biome == WofSurvivalBiome.Swamp ? 1.35d : 1.18d);
        }

        private static double GetRiverWidth(WofSurvivalBiome biome)
        {
            return biome == WofSurvivalBiome.Swamp ? 86d :
                biome == WofSurvivalBiome.Jungle ? 58d :
                biome == WofSurvivalBiome.Desert ? 48d : 44d;
        }

        private static double GetRiverOffset(int cx, int cz)
        {
            return (Hash01(cx, cz, 15) - 0.5d) * BlockSize * 0.32d;
        }

        private static double GetWaterLevel(double worldX, double worldZ)
        {
            GetBiomeWeights(worldX, worldZ, WaterWeights);
            var level = 0d;
            for (var index = 0; index < BiomeCount; index++) level += Elevations[index].WaterLevel * WaterWeights[index];
            return level;
        }

        internal static double GetBiomeGrassCoverageAtWorld(double worldX, double worldZ)
        {
            var restored = GetRestoredMeadowMask(worldX, worldZ);
            GetBiomeWeights(worldX, worldZ, StrictDesertWeights);
            var nonDesert = 1d - StrictDesertWeights[(int)WofSurvivalBiome.Desert];
            return Clamp01(SmoothstepRange(0.035d, 0.34d, nonDesert + restored * 0.92d));
        }

        internal static double GetDesertVillageExpansionMaskAtWorld(double worldX, double worldZ)
        {
            var outsideX = worldX < DesertExpansionMinX
                ? DesertExpansionMinX - worldX
                : worldX > DesertExpansionMaxX
                    ? worldX - DesertExpansionMaxX
                    : 0d;
            var outsideZ = worldZ < DesertExpansionMinZ
                ? DesertExpansionMinZ - worldZ
                : worldZ > DesertExpansionMaxZ
                    ? worldZ - DesertExpansionMaxZ
                    : 0d;
            if (outsideX <= 0d && outsideZ <= 0d) return 1d;
            var outsideDistance = Math.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
            return 1d - SmoothstepRange(0d, DesertExpansionBlendDistance, outsideDistance);
        }

        private static bool IsStrictDesert(double worldX, double worldZ)
        {
            if (GetRestoredMeadowMask(worldX, worldZ) > 0.015d) return false;
            GetBiomeWeights(worldX, worldZ, StrictDesertWeights);
            double desert = 0d;
            double meadow = 0d;
            double grassland = 0d;
            for (var index = 0; index < BiomeCount; index++)
            {
                var weight = StrictDesertWeights[index];
                var biome = (WofSurvivalBiome)index;
                if (biome == WofSurvivalBiome.Desert) desert += weight;
                else
                {
                    meadow += weight;
                    if (biome == WofSurvivalBiome.Plains || biome == WofSurvivalBiome.Tallgrass || biome == WofSurvivalBiome.Jungle)
                        grassland += weight;
                }
            }
            return desert > StrictDesertWeight && meadow < 0.12d && grassland < 0.1d;
        }

        private static double GetRestoredMeadowMask(double worldX, double worldZ)
        {
            if (IsDesertVillageExpansionChunk(
                    GetChunkCoordinate(worldX),
                    GetChunkCoordinate(worldZ))) return 0d;
            var mask = 0d;
            var radialInner = BlockSize * 0.82d;
            var radialOuter = BlockSize * 1.42d;
            var radialInnerSq = radialInner * radialInner;
            var radialOuterSq = radialOuter * radialOuter;
            foreach (var center in RestoredMeadowCenters)
            {
                var localX = worldX - center.X * (double)BlockSize;
                var localZ = worldZ - center.Z * (double)BlockSize;
                var squareDistance = Math.Max(Math.Abs(localX), Math.Abs(localZ));
                var radialDistanceSq = localX * localX + localZ * localZ;
                var radialDistance = radialDistanceSq <= radialInnerSq ? radialInner :
                    radialDistanceSq >= radialOuterSq ? radialOuter : Math.Sqrt(radialDistanceSq);
                var squareMask = 1d - SmoothstepRange(BlockSize * 0.64d, BlockSize * 1.24d, squareDistance);
                var radialMask = 1d - SmoothstepRange(BlockSize * 0.82d, BlockSize * 1.42d, radialDistance);
                mask = Math.Max(mask, Clamp01(squareMask * 0.72d + radialMask * 0.38d));
            }
            return Clamp01(mask);
        }

        internal static bool IsDesertVillageExpansionChunk(int cx, int cz)
        {
            return cx >= 3 && cx <= 5 && cz >= -4 && cz <= -3;
        }

        private static bool IsRestoredMeadowWaterSuppressed(double worldX, double worldZ, double radius)
        {
            const double threshold = 0.025d;
            if (GetRestoredMeadowMask(worldX, worldZ) > threshold) return true;
            var sampleRadius = Math.Max(0d, radius);
            if (sampleRadius <= 0d) return false;
            var diagonal = sampleRadius * 0.72d;
            return GetRestoredMeadowMask(worldX + sampleRadius, worldZ) > threshold ||
                   GetRestoredMeadowMask(worldX - sampleRadius, worldZ) > threshold ||
                   GetRestoredMeadowMask(worldX, worldZ + sampleRadius) > threshold ||
                   GetRestoredMeadowMask(worldX, worldZ - sampleRadius) > threshold ||
                   GetRestoredMeadowMask(worldX + diagonal, worldZ + diagonal) > threshold ||
                   GetRestoredMeadowMask(worldX - diagonal, worldZ + diagonal) > threshold ||
                   GetRestoredMeadowMask(worldX + diagonal, worldZ - diagonal) > threshold ||
                   GetRestoredMeadowMask(worldX - diagonal, worldZ - diagonal) > threshold;
        }

        private static double GetBaseVillageTransitionMask(double worldX, double worldZ)
        {
            var absX = Math.Abs(worldX);
            var absZ = Math.Abs(worldZ);
            var maxAbs = Math.Max(absX, absZ);
            var minAbs = Math.Min(absX, absZ);
            if (maxAbs < BaseVillageHalfSize - 0.5d) return 0d;
            var edgeApron = 1d - SmoothstepRange(BaseVillageHalfSize + 4d,
                BaseVillageHalfSize + BaseVillageApronDistance, maxAbs);
            var gateRoadMask = 1d - SmoothstepRange(18d, 58d, minAbs);
            var wallApronMask = 1d - SmoothstepRange(BaseVillageHalfSize + 18d,
                BaseVillageHalfSize + 108d, maxAbs);
            return Clamp01(edgeApron * Math.Max(gateRoadMask, wallApronMask));
        }

        private static HeightMask GetGraveyardExteriorApron(double worldX, double worldZ)
        {
            var centerX = GraveyardChunkX * (double)BlockSize;
            var centerZ = GraveyardChunkZ * (double)BlockSize;
            var localX = worldX - centerX;
            var localZ = worldZ - centerZ;
            var radiusSq = localX * localX + localZ * localZ;
            const double apronDistance = 220d;
            var minRadius = GraveyardPadFlatRadius - 4d;
            var maxRadius = GraveyardPadFlatRadius + apronDistance;
            if (radiusSq < minRadius * minRadius || radiusSq > maxRadius * maxRadius) return default;
            var radius = Math.Sqrt(radiusSq);
            var outside = radius - GraveyardPadFlatRadius;
            if (outside < -4d || outside > apronDistance) return default;
            var mask = 1d - SmoothstepRange(0d, apronDistance, Math.Max(0d, outside));
            var baseHeight = GetRawTerrainHeight(centerX, centerZ);
            var edgeScale = radius > GraveyardPadFlatRadius ? GraveyardPadFlatRadius / radius : 1d;
            var edgeHeight = GetGraveyardLocalSurfaceHeight(localX * edgeScale, localZ * edgeScale, baseHeight);
            return new HeightMask(mask, edgeHeight);
        }

        private static double GetGraveyardLocalSurfaceHeight(double localX, double localZ, double baseHeight)
        {
            var radius = Math.Sqrt(localX * localX + localZ * localZ);
            var gateEntry = GetGraveyardGateEntryMask(localX, localZ);
            var gateClearing = GetGraveyardGateClearingMask(localX, localZ);
            var hillA = Math.Sin(localX * 0.035d + GraveyardChunkX * 1.7d) *
                        Math.Cos(localZ * 0.028d - GraveyardChunkZ * 1.3d);
            var hillB = Math.Sin((localX + localZ) * 0.023d + 2.4d) * 0.58d;
            var moundRing = Math.Pow(SmoothstepRange(42d, 238d, radius) *
                                     (1d - SmoothstepRange(204d, 238d, radius)), 0.9d);
            var pathMask = GetGraveyardEffectivePathMask(localX, localZ);
            var chapelMask = GetGraveyardChapelMask(localX, localZ);
            var hills = (hillA * 4.6d + hillB * 2.8d + moundRing * 5.8d) *
                        (1d - pathMask * 0.78d) * (1d - chapelMask * 0.98d) * (1d - gateClearing);
            var flatten = Math.Max(gateEntry * 0.96d, gateClearing * 0.9d);
            return Lerp(baseHeight + hills - pathMask * 0.38d, baseHeight - 0.46d, flatten);
        }

        private static double GetGraveyardGateClearingMask(double localX, double localZ)
        {
            var absX = Math.Abs(localX);
            var absZ = Math.Abs(localZ);
            var northSouth = (1d - SmoothstepRange(214d, 306d, absX)) *
                             SmoothstepRange(48d, 104d, absZ) *
                             (1d - SmoothstepRange(362d, 516d, absZ));
            var eastWest = (1d - SmoothstepRange(214d, 306d, absZ)) *
                           SmoothstepRange(48d, 104d, absX) *
                           (1d - SmoothstepRange(362d, 516d, absX));
            return Clamp01(Math.Max(GetGraveyardGateEntryMask(localX, localZ), Math.Max(northSouth, eastWest)));
        }

        private static double GetGraveyardGateEntryMask(double localX, double localZ)
        {
            var absX = Math.Abs(localX);
            var absZ = Math.Abs(localZ);
            var northSouth = (1d - SmoothstepRange(82d, 154d, absX)) *
                             SmoothstepRange(88d, 146d, absZ) *
                             (1d - SmoothstepRange(274d, 430d, absZ));
            var eastWest = (1d - SmoothstepRange(82d, 154d, absZ)) *
                           SmoothstepRange(88d, 146d, absX) *
                           (1d - SmoothstepRange(274d, 430d, absX));
            return Clamp01(Math.Max(northSouth, eastWest));
        }

        private static double GetGraveyardEffectivePathMask(double localX, double localZ)
        {
            var radius = Math.Sqrt(localX * localX + localZ * localZ);
            var cross = Math.Max(1d - SmoothstepRange(17.5d, 27.3d, Math.Abs(localX)),
                1d - SmoothstepRange(17.5d, 27.3d, Math.Abs(localZ)));
            var ring = 1d - SmoothstepRange(8.4d, 15.6d, Math.Abs(radius - 88d));
            var path = Math.Max(Math.Max(cross, ring),
                Math.Max(GetGraveyardChapelWalkMask(localX, localZ), GetGraveyardGateEntryMask(localX, localZ) * 0.92d));
            return path * (1d - GetGraveyardChapelFootprintMask(localX, localZ) * 0.98d);
        }

        private static double GetGraveyardChapelMask(double localX, double localZ)
        {
            return Math.Max(GetGraveyardChapelFootprintMask(localX, localZ), GetGraveyardChapelWalkMask(localX, localZ));
        }

        private static double GetGraveyardChapelFootprintMask(double localX, double localZ)
        {
            var center = GetSoftRectMask(localX, localZ, 0d, 0d, 54d, 82d);
            var west = GetSoftRectMask(localX, localZ, -88d, 0d, 34d, 57d);
            var east = GetSoftRectMask(localX, localZ, 88d, 0d, 34d, 57d);
            return Math.Max(center, Math.Max(west, east));
        }

        private static double GetGraveyardChapelWalkMask(double localX, double localZ)
        {
            var absX = Math.Abs(localX);
            var absZ = Math.Abs(localZ);
            var south = (1d - SmoothstepRange(28d, 44d, absX)) *
                        SmoothstepRange(64d, 80d, localZ) * (1d - SmoothstepRange(132d, 162d, localZ));
            var rearX = Math.Max(1d - SmoothstepRange(13.5d, 27.5d, Math.Abs(localX - 33d)),
                1d - SmoothstepRange(13.5d, 27.5d, Math.Abs(localX + 33d)));
            var north = rearX * SmoothstepRange(64d, 80d, -localZ) *
                        (1d - SmoothstepRange(132d, 162d, -localZ));
            var eastWest = (1d - SmoothstepRange(28d, 44d, absZ)) *
                           SmoothstepRange(104d, 120d, absX) * (1d - SmoothstepRange(174d, 206d, absX));
            return Math.Max(south, Math.Max(north, eastWest));
        }

        private static double GetSoftRectMask(double x, double z, double centerX, double centerZ,
            double halfWidth, double halfDepth)
        {
            return Math.Min(1d - SmoothstepRange(halfWidth, halfWidth + 10d, Math.Abs(x - centerX)),
                1d - SmoothstepRange(halfDepth, halfDepth + 10d, Math.Abs(z - centerZ)));
        }

        private static double GetTownRouteMask(double worldX, double worldZ)
        {
            var mask = 0d;
            for (var routeIndex = 0; routeIndex < TownRoutes.Length; routeIndex++)
            {
                var route = TownRoutes[routeIndex];
                var startX = route.StartX * (double)BlockSize;
                var startZ = route.StartZ * (double)BlockSize;
                var endX = route.EndX * (double)BlockSize;
                var endZ = route.EndZ * (double)BlockSize;
                var dx = endX - startX;
                var dz = endZ - startZ;
                var padding = RouteShoulderWidth + RouteMaxMeander + 8d;
                if (worldX < Math.Min(startX, endX) - padding || worldX > Math.Max(startX, endX) + padding ||
                    worldZ < Math.Min(startZ, endZ) - padding || worldZ > Math.Max(startZ, endZ) + padding) continue;
                var length = Math.Max(1d, Math.Sqrt(dx * dx + dz * dz));
                var seed = Hash01(route.StartX + route.EndX * 7, route.StartZ + route.EndZ * 11, 15011 + routeIndex);
                var count = Math.Max(2, Math.Min(42, (int)Math.Ceiling(length / (BlockSize * 0.85d))));
                var previous = GetRoutePoint(startX, startZ, endX, endZ, dx, dz, length, seed, 0d);
                var closest = double.PositiveInfinity;
                for (var segmentIndex = 1; segmentIndex <= count; segmentIndex++)
                {
                    var next = GetRoutePoint(startX, startZ, endX, endZ, dx, dz, length, seed,
                        segmentIndex / (double)count);
                    closest = Math.Min(closest, DistanceToSegmentSq(worldX, worldZ, previous.X, previous.Z, next.X, next.Z));
                    previous = next;
                }
                var routeMask = 1d - SmoothstepRange(RouteCoreWidth, RouteShoulderWidth, Math.Sqrt(closest));
                mask = Math.Max(mask, routeMask);
            }
            return Clamp01(mask);
        }

        private static WorldPoint GetRoutePoint(double startX, double startZ, double endX, double endZ,
            double dx, double dz, double length, double seed, double t)
        {
            var fade = Math.Sin(t * Math.PI);
            var meander = fade * Math.Sin(t * Math.PI + seed * Math.PI * 2d) * RouteMaxMeander;
            return new WorldPoint(Lerp(startX, endX, t) + -dz / length * meander,
                Lerp(startZ, endZ, t) + dx / length * meander);
        }

        private static double DistanceToSegmentSq(double px, double pz, double ax, double az, double bx, double bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var lengthSq = dx * dx + dz * dz;
            if (lengthSq < 0.0001d)
            {
                var pointX = px - ax;
                var pointZ = pz - az;
                return pointX * pointX + pointZ * pointZ;
            }
            var t = Clamp01(((px - ax) * dx + (pz - az) * dz) / lengthSq);
            var closestX = ax + dx * t;
            var closestZ = az + dz * t;
            var resultX = px - closestX;
            var resultZ = pz - closestZ;
            return resultX * resultX + resultZ * resultZ;
        }

        private static HexOffset[] MakeBiomeOffsets()
        {
            var result = new HexOffset[19];
            var index = 0;
            for (var q = -2; q <= 2; q++)
            for (var r = -2; r <= 2; r++)
            {
                var distance = Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(q + r)));
                if (distance <= 2) result[index++] = new HexOffset(q, r);
            }
            if (index != result.Length) throw new InvalidOperationException($"Unexpected biome offset count {index}.");
            return result;
        }

        private static int JsRound(double value) => (int)Math.Floor(value + 0.5d);
        private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));
        private static double Smoothstep01(double value)
        {
            var t = Clamp01(value);
            return t * t * (3d - 2d * t);
        }
        private static double SmoothstepRange(double edge0, double edge1, double value)
        {
            var span = edge1 - edge0;
            if (Math.Abs(span) < 0.0001d) return value >= edge1 ? 1d : 0d;
            return Smoothstep01((value - edge0) / span);
        }
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static Rgb Hex(string value)
        {
            var hex = value.TrimStart('#');
            return new Rgb(Convert.ToInt32(hex.Substring(0, 2), 16) / 255d,
                Convert.ToInt32(hex.Substring(2, 2), 16) / 255d,
                Convert.ToInt32(hex.Substring(4, 2), 16) / 255d);
        }

        private readonly struct Elevation
        {
            public Elevation(double @base, double hills, double ridges, double mountains, double valleys,
                double detail, double waterLevel)
            {
                Base = @base; Hills = hills; Ridges = ridges; Mountains = mountains;
                Valleys = valleys; Detail = detail; WaterLevel = waterLevel;
            }
            public double Base { get; }
            public double Hills { get; }
            public double Ridges { get; }
            public double Mountains { get; }
            public double Valleys { get; }
            public double Detail { get; }
            public double WaterLevel { get; }
        }

        private readonly struct MountainProfile
        {
            public MountainProfile(double chance, double radius, double height)
            { Chance = chance; Radius = radius; Height = height; }
            public double Chance { get; }
            public double Radius { get; }
            public double Height { get; }
        }

        private readonly struct Rgb
        {
            public Rgb(double r, double g, double b) { R = r; G = g; B = b; }
            public double R { get; }
            public double G { get; }
            public double B { get; }
            public Rgb Clamped() => new(Clamp01(R), Clamp01(G), Clamp01(B));
            public static Rgb Lerp(Rgb a, Rgb b, double t) => new(
                WofSurvivalTerrainMath.Lerp(a.R, b.R, t),
                WofSurvivalTerrainMath.Lerp(a.G, b.G, t),
                WofSurvivalTerrainMath.Lerp(a.B, b.B, t));
            public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
            public static Rgb operator *(Rgb a, double b) => new(a.R * b, a.G * b, a.B * b);
        }

        private readonly struct HexCoord
        {
            public HexCoord(int q, int r) { Q = q; R = r; }
            public int Q { get; }
            public int R { get; }
        }
        private readonly struct HexOffset
        {
            public HexOffset(int q, int r) { Q = q; R = r; }
            public int Q { get; }
            public int R { get; }
        }
        private readonly struct ChunkPoint
        {
            public ChunkPoint(int x, int z) { X = x; Z = z; }
            public int X { get; }
            public int Z { get; }
        }
        private readonly struct RouteSegment
        {
            public RouteSegment(int startX, int startZ, int endX, int endZ)
            { StartX = startX; StartZ = startZ; EndX = endX; EndZ = endZ; }
            public int StartX { get; }
            public int StartZ { get; }
            public int EndX { get; }
            public int EndZ { get; }
        }
        private readonly struct WorldPoint
        {
            public WorldPoint(double x, double z) { X = x; Z = z; }
            public double X { get; }
            public double Z { get; }
        }
        private readonly struct HeightMask
        {
            public HeightMask(double mask, double height) { Mask = mask; Height = height; }
            public double Mask { get; }
            public double Height { get; }
        }
    }
}
