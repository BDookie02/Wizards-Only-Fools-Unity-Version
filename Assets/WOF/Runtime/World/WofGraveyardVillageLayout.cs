using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofGraveyardVillageDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofGraveyardChunkRecord chunk;
        public float baseHeight;
        public WofGraveyardCountsRecord counts;
        public WofGraveyardConstantsRecord constants;
        public WofGraveyardLayoutRecord layout;
        public WofGraveyardChapelRecord chapel;
        public WofGraveyardGeometryRecord geometries;
    }

    [Serializable]
    public sealed class WofGraveyardChunkRecord
    {
        public string key;
        public int cx;
        public int cz;
        public float x;
        public float z;
        public int distance;
        public string biome;
        public bool hasVillage;
        public string villageKind;
        public bool hasRiver;
        public bool riverVertical;
        public string lod;
    }

    [Serializable]
    public sealed class WofGraveyardCountsRecord
    {
        public int tombs;
        public int inscribedTombs;
        public int fenceSegments;
        public int pathStones;
        public int chapelCharacters;
        public int centerNpcs;
        public int sideWingNpcs;
    }

    [Serializable]
    public sealed class WofGraveyardConstantsRecord
    {
        public float villageRadius;
        public float ringPathRadius;
        public float fenceRadius;
        public float avatarScale;
        public float avatarGroundLift;
    }

    [Serializable]
    public sealed class WofGraveyardLayoutRecord
    {
        public float baseHeight;
        public WofGraveyardTombRecord[] tombs;
        public WofGraveyardFenceSegmentRecord[] fenceSegments;
        public WofGraveyardPathStoneRecord[] pathStones;
    }

    [Serializable]
    public sealed class WofGraveyardTombRecord
    {
        public string key;
        public float localX;
        public float localY;
        public float localZ;
        public float rotation;
        public string name;
        public string joke;
        public float variant;
        public int styleIndex;
        public float width;
        public float height;
        public float depth;
        public float baseWidth;
        public float baseDepth;
        public float labelY;
        public float labelHeight;
        public float labelWidth;
        public float frontZ;
        public WofGraveyardTombColorsRecord colors;
        public WofGraveyardTombTexturesRecord textures;
    }

    [Serializable]
    public sealed class WofGraveyardTombColorsRecord
    {
        public string stoneColor;
        public string darkStone;
        public string accentStone;
        public string foundationColor;
    }

    [Serializable]
    public sealed class WofGraveyardTombTexturesRecord
    {
        public string bodyTexture;
        public string darkTexture;
        public string accentTexture;
        public string foundationTexture;
        public string inscriptionTexture;
    }

    [Serializable]
    public sealed class WofGraveyardFenceSegmentRecord
    {
        public string key;
        public float localX;
        public float localY;
        public float localZ;
        public float rotation;
        public float length;
    }

    [Serializable]
    public sealed class WofGraveyardPathStoneRecord
    {
        public string key;
        public float localX;
        public float localY;
        public float localZ;
        public float rotation;
        public float width;
        public float depth;
        public string color;
    }

    [Serializable]
    public sealed class WofGraveyardGeometryRecord
    {
        public WofSerializedMeshRecord terrain;
        public WofSerializedMeshRecord terrainSkirt;
        public WofSerializedMeshRecord rampCollider;
    }

    [Serializable]
    public sealed class WofGraveyardChapelRecord
    {
        public WofGraveyardChapelViewSummaryRecord viewSummary;
        public WofGraveyardChapelViewSummaryRecord interiorSummary;
        public WofGraveyardChapelViewSummaryRecord exteriorSummary;
        public WofGraveyardChapelViewSummaryRecord seatingSummary;
        public WofGraveyardColliderSummaryRecord colliderSummary;
        public WofGraveyardChapelDimensionsRecord dimensions;
        public WofGraveyardWallSegmentRecord[] wallSegments;
        public WofGraveyardVectorRecord[] watchTowerPositions;
        public WofGraveyardGargoyleRecord[] gargoyles;
        public WofGraveyardExitRampRecord[] exitRamps;
        public WofGraveyardExitShadowRecord[] exitShadows;
        public WofGraveyardVectorRecord[] chandelierCandles;
        public WofGraveyardVectorRecord[] interiorCandles;
        public float[] centerPewRows;
        public WofGraveyardCenterPewRecord[] centerPewColliders;
        public WofGraveyardSidePewRecord[] sideWingPews;
        public WofGraveyardWingBeamRecord[] wingCeilingBeams;
        public WofGraveyardNpcPlacementRecord[] centerNpcPlacements;
        public WofGraveyardNpcPlacementRecord[] sideWingNpcPlacements;
        public WofGraveyardPopeRecord pope;
        public WofGraveyardCharacterArchiveRecord[] characters;
    }

    [Serializable]
    public sealed class WofGraveyardChapelViewSummaryRecord
    {
        public int towerCount;
        public int towerCrenelCount;
        public int towerArrowSlitPairCount;
        public int gargoyleCount;
        public int doorPanelCount;
        public int doorPlankCount;
        public int doorStrapCount;
        public int naveCeilingBeamCount;
        public int wingCeilingBeamCount;
        public int centerPewRows;
        public int centerPewCount;
        public int sideWingPewCount;
        public int centerNpcCount;
        public int sideWingNpcCount;
        public int altarGrainCount;
        public int pulpitGrainCount;
        public int interiorCandleCount;
        public int chandelierCount;
        public int chandelierCandleCount;
        public int foundationCount;
        public int doorCount;
        public int exitRampCount;
        public int exitShadowCount;
        public int windowCount;
        public int buttressCount;
        public int towerPierCount;
    }

    [Serializable]
    public sealed class WofGraveyardColliderSummaryRecord
    {
        public int rigidBodyCount;
        public int foundationColliderCount;
        public int centerPewColliderCount;
        public int sideWingPewColliderCount;
        public int altarColliderCount;
        public int wallColliderCount;
        public int towerColliderCount;
        public int fenceColliderCount;
        public int cuboidColliderCount;
    }

    [Serializable]
    public sealed class WofGraveyardChapelDimensionsRecord
    {
        public float centerHalfWidth;
        public float centerHalfDepth;
        public float sideWingHalfWidth;
        public float sideWingHalfDepth;
        public float sideWingCenterX;
        public float outerHalfWidth;
        public float wallThickness;
        public float wallHeight;
        public float wallHalfHeight;
        public float exitHalfWidth;
        public float sideExitHalfWidth;
        public float rearExitCenterX;
        public float rearExitHalfWidth;
        public float stairRampLength;
        public float stairRampThickness;
        public float stairRampLowTop;
        public float stairRampCenterTop;
        public float stairRampWingTop;
        public float watchTowerHeight;
        public float watchTowerRadius;
        public float watchTowerY;
    }

    [Serializable]
    public sealed class WofGraveyardVectorRecord
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class WofGraveyardWallSegmentRecord
    {
        public string key;
        public float[] position;
        public float[] size;
    }

    [Serializable]
    public sealed class WofGraveyardGargoyleRecord
    {
        public string key;
        public float[] position;
        public float yaw;
        public float scale = 1f;
    }

    [Serializable]
    public sealed class WofGraveyardExitRampRecord
    {
        public string key;
        public float[] position;
        public float rotation;
        public float distance;
        public float width;
        public float top;
        public float outset;
    }

    [Serializable]
    public sealed class WofGraveyardExitShadowRecord
    {
        public string key;
        public float[] position;
        public float[] rotation;
        public float[] size;
    }

    [Serializable]
    public sealed class WofGraveyardCenterPewRecord
    {
        public string key;
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class WofGraveyardSidePewRecord
    {
        public string key;
        public float x;
        public float z;
        public float width;
    }

    [Serializable]
    public sealed class WofGraveyardWingBeamRecord
    {
        public string key;
        public int side;
        public float z;
        public string color;
    }

    [Serializable]
    public sealed class WofGraveyardNpcPlacementRecord
    {
        public string key;
        public float[] position;
        public float yaw;
        public int characterIndex;
    }

    [Serializable]
    public sealed class WofGraveyardPopeRecord
    {
        public WofGraveyardPopeTargetRecord target;
        public float[] position;
        public float yaw;
        public int characterIndex;
        public string miterTexture;
    }

    [Serializable]
    public sealed class WofGraveyardPopeTargetRecord
    {
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class WofGraveyardCharacterArchiveRecord
    {
        public int index;
        public string role;
        public string archiveFile;
        public int archiveBytes;
        public string archiveSha256;
        public WofVillagerCharacterRecord character;
    }

    public static class WofGraveyardVillageLayout
    {
        public const int ChunkX = 5;
        public const int ChunkZ = 2;
        public const float SurvivalBlockSize = 512f;
        public const float ReactBaseHeight = 57.043982940225106f;
        public const float VillageRadius = 238f;
        public const float RingPathRadius = 88f;
        public const float FenceRadius = 246f;
        public const int TombCount = 21;
        public const int InscribedTombCount = 3;
        public const int FenceSegmentCount = 36;
        public const int PathStoneCount = 24;
        public const int ChapelCharacterCount = 7;
        public const int CenterNpcCount = 20;
        public const int SideWingNpcCount = 24;
        public const int CuboidColliderCount = 93;
        public static readonly Vector3 WorldOrigin = new(ChunkX * SurvivalBlockSize, 0f, ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 ViewProbeSpawn = WorldOrigin +
                                                        new Vector3(0f, ReactBaseHeight + 2.2f, 216f);
        public static readonly Vector3 ChapelInteriorViewProbeSpawn = WorldOrigin +
                                                                       new Vector3(0f, ReactBaseHeight + 3f, 62f);
        public static readonly Vector3 TombsViewProbeSpawn = WorldOrigin +
                                                             new Vector3(-51.6f, ReactBaseHeight + 2.2f, -226f);
        public static readonly Vector3 FenceViewProbeSpawn = WorldOrigin +
                                                             new Vector3(0f, ReactBaseHeight + 2.2f, 275f);

        public static bool HasExactCounts(WofGraveyardCountsRecord counts)
        {
            return counts != null && counts.tombs == TombCount &&
                   counts.inscribedTombs == InscribedTombCount &&
                   counts.fenceSegments == FenceSegmentCount &&
                   counts.pathStones == PathStoneCount &&
                   counts.chapelCharacters == ChapelCharacterCount &&
                   counts.centerNpcs == CenterNpcCount &&
                   counts.sideWingNpcs == SideWingNpcCount;
        }
    }
}
