using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofMountainVillageDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofMountainChunkRecord chunk;
        public float baseHeight;
        public float summitY;
        public WofMountainVillageCounts counts;
        public WofMountainVillageConstants constants;
        public WofMountainVillageSceneRecord layout;
        public WofMountainOpeningRecord opening;
        public WofMountainWallDecorRecord wallDecor;
        public WofMountainBanquetRecord banquet;
        public WofMountainBanquetColliderRecord banquetColliders;
        public WofMountainCatwalkRecord catwalk;
        public WofMountainCatwalkColliderRecord catwalkColliders;
        public WofMountainInteriorPlatformRecord[] interiorPlatforms;
        public WofMountainLadderDetailRecord[] ladderDetails;
        public WofMountainExitBridgeRecord exitBridge;
        public WofMountainWaterfallVisualRecord waterfallVisuals;
        public WofVillagerLayoutRecord[] villagers;
        public WofMountainGeometryRecord geometries;
    }

    [Serializable]
    public sealed class WofMountainChunkRecord
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
    public sealed class WofMountainVillageCounts
    {
        public int trailPoints;
        public int trailSegments;
        public int cliffPatches;
        public int cabins;
        public int interiorHuts;
        public int interiorLadders;
        public int hutInfos;
        public int slopeGrassTufts;
        public int summitSnowDrifts;
        public int rimBeams;
        public int supportFrames;
        public int supportPosts;
        public int supportSnowCaps;
        public int bottomRocks;
        public int wallLanterns;
        public int wallPaintings;
        public int wallRopeLights;
        public int banquetBottomLights;
        public int banquetChairs;
        public int banquetTablePlanks;
        public int banquetTableLegs;
        public int banquetBreads;
        public int banquetFruitBowls;
        public int banquetFruits;
        public int banquetPlates;
        public int banquetCandles;
        public int villagers;
    }

    [Serializable]
    public sealed class WofMountainVillageConstants
    {
        public float radius;
        public float height;
        public float plateauRadius;
        public float trailTurns;
        public float trailStartRadius;
        public float trailEndRadius;
        public float trailHeightOffset;
        public float summitColliderRadius;
        public float mineshaftHoleRadius;
        public float mineshaftTerrainCutRadius;
        public float mineshaftRimMidRadius;
        public float mineshaftRimOuterRadius;
        public float mineshaftBottomBaseOffset;
        public float mineshaftBottomRadius;
        public float mineshaftCatwalkInnerRadius;
        public float mineshaftCatwalkOuterRadius;
        public int mineshaftCatwalkSegments;
        public float mineshaftLadderRingRadius;
        public float mineshaftLadderWidth;
        public float mineshaftLadderSensorDepth;
        public float mineshaftLadderPlatformGap;
        public float mineshaftExitBridgeWidth;
        public float mineshaftExitBridgeYOffset;
        public int slopeGrassNearCount;
        public WofMountainUnityPerimeterReshape unityPerimeterReshape;
    }

    [Serializable]
    public sealed class WofMountainUnityPerimeterReshape
    {
        public float protectedRadius;
        public float rimPeakRadius;
        public float rimOuterRadius;
        public float shoulderOuterRadius;
        public float centerX;
        public float centerZ;
    }

    [Serializable]
    public sealed class WofMountainVillageSceneRecord
    {
        public float baseHeight;
        public float summitY;
        public WofMountainTrailPointRecord[] trailPoints;
        public WofMountainTrailSegmentRecord[] trailSegments;
        public WofMountainCliffPatchRecord[] cliffPatches;
        public WofMountainCabinRecord[] cabins;
        public WofMountainInteriorHutRecord[] interiorHuts;
        public WofMountainLadderRecord[] interiorLadders;
        public WofMountainWaterfallRecord waterfall;
    }

    [Serializable]
    public sealed class WofMountainTrailPointRecord
    {
        public float localX;
        public float localZ;
        public float y;
        public float width;
        public float t;
    }

    [Serializable]
    public sealed class WofMountainTrailSegmentRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float y;
        public float yaw;
        public float slope;
        public float width;
        public float length;
        public int index;
        public WofMountainTrailSupportRecord[] supports;
    }

    [Serializable]
    public sealed class WofMountainTrailSupportRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float topY;
        public float height;
        public float yaw;
        public int side;
    }

    [Serializable]
    public sealed class WofMountainCliffPatchRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float y;
        public float yaw;
        public float roll;
        public float width;
        public float depth;
        public float thickness;
        public string color;
        public float opacity;
    }

    [Serializable]
    public class WofMountainCabinMetricsRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float width;
        public float depth;
        public float height;
        public string bodyColor;
        public string roofColor;
        public string accentColor;
    }

    [Serializable]
    public sealed class WofMountainCabinRecord : WofMountainCabinMetricsRecord
    {
    }

    [Serializable]
    public sealed class WofMountainInteriorHutRecord : WofMountainCabinMetricsRecord
    {
        public float angle;
        public float y;
        public float platformWidth;
        public float platformDepth;
    }

    [Serializable]
    public sealed class WofMountainLadderRecord
    {
        public string key;
        public float angle;
        public float localX;
        public float localZ;
        public float startY;
        public float endY;
        public float rotation;
        public float width;
    }

    [Serializable]
    public sealed class WofMountainWaterfallRecord
    {
        public float angle;
        public float topX;
        public float topZ;
        public float topY;
        public float bottomX;
        public float bottomZ;
        public float bottomY;
        public float width;
    }

    [Serializable]
    public sealed class WofMountainGeometryRecord
    {
        public WofSerializedMeshRecord terrain;
        public WofSerializedMeshRecord terrainCollider;
        public WofSerializedMeshRecord slopeGrass;
        public WofSerializedMeshRecord trailDeck;
        public WofSerializedMeshRecord trailTop;
        public WofSerializedMeshRecord trailCollider;
        public WofSerializedMeshRecord summitCollider;
    }

    [Serializable]
    public sealed class WofMountainOpeningRecord
    {
        public WofMountainSnowDriftRecord[] summitSnowDrifts;
        public WofMountainRimBeamRecord[] rimBeams;
        public WofMountainSupportFrameRecord[] supportFrames;
        public WofMountainBottomRockRecord[] bottomRocks;
    }

    [Serializable]
    public sealed class WofMountainSnowDriftRecord
    {
        public int index;
        public float[] positionXZ;
        public float yOffset;
        public float[] rotation;
        public float[] scale;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainRimBeamRecord
    {
        public int index;
        public float angle;
        public float x;
        public float z;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainSupportFrameRecord
    {
        public int index;
        public float angle;
        public float[] rotation;
        public WofMountainSupportPostRecord[] posts;
        public float[] topBeamPositionOffset;
        public WofMountainSupportSnowCapRecord[] snowCaps;
    }

    [Serializable]
    public sealed class WofMountainSupportPostRecord
    {
        public int side;
        public float[] positionOffset;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainSupportSnowCapRecord
    {
        public int side;
        public float[] position;
    }

    [Serializable]
    public sealed class WofMountainBottomRockRecord
    {
        public int index;
        public float angle;
        public float x;
        public float z;
        public float[] rotation;
        public float[] scale;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainWallDecorRecord
    {
        public WofMountainWallLanternRecord[] lanterns;
        public WofMountainWallPaintingRecord[] paintings;
        public WofMountainWallRopeLightRecord[] ropeLights;
    }

    [Serializable]
    public sealed class WofMountainWallLanternRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
        public bool withLight;
    }

    [Serializable]
    public sealed class WofMountainWallPaintingRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
        public int variant;
    }

    [Serializable]
    public sealed class WofMountainWallRopeLightRecord
    {
        public string key;
        public int tierIndex;
        public int lightIndex;
        public float[] position;
        public float[] rotation;
        public float bulbScale;
        public string glowColor;
        public bool hasLight;
    }

    [Serializable]
    public sealed class WofMountainBanquetRecord
    {
        public WofMountainBottomLightRecord[] bottomLights;
        public WofMountainBanquetChairRecord[] chairs;
        public WofMountainBanquetTableRecord table;
    }

    [Serializable]
    public sealed class WofMountainBottomLightRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
        public string bodyColor;
        public bool withLight;
    }

    [Serializable]
    public sealed class WofMountainBanquetChairRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
        public string seatColor;
    }

    [Serializable]
    public sealed class WofMountainBanquetTableRecord
    {
        public float radius;
        public WofMountainTablePlankRecord[] planks;
        public WofMountainIndexedTransformRecord[] legs;
        public WofMountainColoredTransformRecord[] breads;
        public WofMountainFruitBowlRecord[] fruitBowls;
        public WofMountainColoredTransformRecord[] plates;
        public WofMountainIndexedTransformRecord[] candles;
    }

    [Serializable]
    public class WofMountainIndexedTransformRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainColoredTransformRecord : WofMountainIndexedTransformRecord
    {
        public string color;
        public string foodColor;
    }

    [Serializable]
    public sealed class WofMountainTablePlankRecord
    {
        public int index;
        public float z;
        public float width;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainFruitBowlRecord : WofMountainIndexedTransformRecord
    {
        public WofMountainFruitRecord[] fruits;
    }

    [Serializable]
    public sealed class WofMountainFruitRecord
    {
        public int index;
        public float[] position;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainBanquetColliderRecord
    {
        public WofMountainColliderRecord table;
        public WofMountainColliderRecord throne;
        public WofMountainChairColliderRecord[] chairs;
    }

    [Serializable]
    public class WofMountainColliderRecord
    {
        public float[] args;
        public float[] positionOffset;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainChairColliderRecord : WofMountainColliderRecord
    {
        public int index;
    }

    [Serializable]
    public sealed class WofMountainCatwalkRecord
    {
        public int centerGuardPostCount;
        public float centerGuardRailRadius;
        public float centerGuardRailSegmentLength;
        public float lightPoleRadius;
        public WofMountainCatwalkEntryRecord[] planks;
        public WofMountainCatwalkEntryRecord[] darkGaps;
        public WofMountainCatwalkEntryRecord[] edgeBlocks;
        public WofMountainCatwalkEntryRecord[] guardPosts;
        public WofMountainCatwalkEntryRecord[] railSegments;
    }

    [Serializable]
    public sealed class WofMountainCatwalkEntryRecord : WofMountainIndexedTransformRecord
    {
        public float angle;
    }

    [Serializable]
    public sealed class WofMountainCatwalkColliderRecord
    {
        public float[] args;
        public WofMountainCatwalkColliderSegmentRecord[] segments;
    }

    [Serializable]
    public sealed class WofMountainCatwalkColliderSegmentRecord
    {
        public int index;
        public float[] positionOffset;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainInteriorPlatformRecord
    {
        public string hutKey;
        public float platformZ;
        public float landingLocalX;
        public int poleSide;
        public WofMountainPlatformPieceRecord[] pieces;
        public WofMountainPlatformPieceRecord[] topPieces;
        public WofMountainPlatformDetailsRecord details;
        public WofMountainCatwalkEntryRecord[] catwalkLightPoles;
        public WofMountainGuardRailOpeningRecord guardRailOpenings;
    }

    [Serializable]
    public sealed class WofMountainPlatformPieceRecord
    {
        public string key;
        public float centerX;
        public float width;
    }

    [Serializable]
    public sealed class WofMountainPlatformDetailsRecord
    {
        public WofMountainPlatformPieceDetailsRecord[] pieces;
        public WofMountainPlatformSupportRecord[] supports;
        public WofMountainPlatformLightPoleRecord lightPole;
    }

    [Serializable]
    public sealed class WofMountainPlatformPieceDetailsRecord
    {
        public string key;
        public WofMountainSidePositionRecord[] sideShadows;
        public WofMountainPlatformGrooveRecord[] plankGrooves;
        public WofMountainNamedPositionRecord frontRail;
        public WofMountainNamedPositionRecord backRail;
        public WofMountainSidePositionRecord[] bolts;
    }

    [Serializable]
    public sealed class WofMountainSidePositionRecord
    {
        public int side;
        public float[] position;
    }

    [Serializable]
    public sealed class WofMountainPlatformGrooveRecord
    {
        public int index;
        public float[] position;
        public float width;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainNamedPositionRecord
    {
        public string key;
        public float[] position;
        public float width;
    }

    [Serializable]
    public sealed class WofMountainPlatformSupportRecord
    {
        public int side;
        public float[] position;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainPlatformLightPoleRecord
    {
        public int direction;
        public float[] position;
    }

    [Serializable]
    public sealed class WofMountainGuardRailOpeningRecord
    {
        public float hutAngle;
        public float ladderAngle;
        public float nextLadderAngle;
        public float balconyGapHalfAngle;
        public float ladderGapHalfAngle;
        public float nextLadderGapHalfAngle;
        public int[] visibleEdgeBlockIndices;
        public int[] visibleGuardPostIndices;
        public int[] visibleRailSegmentIndices;
    }

    [Serializable]
    public sealed class WofMountainLadderDetailRecord
    {
        public string ladderKey;
        public float height;
        public WofMountainLadderDetailsRecord details;
    }

    [Serializable]
    public sealed class WofMountainLadderDetailsRecord
    {
        public int rungCount;
        public int wrapCount;
        public WofMountainColoredTransformRecord[] rungs;
        public WofMountainLadderWrapRecord[] wraps;
        public WofMountainIndexedTransformRecord[] brightEdges;
        public WofMountainIndexedTransformRecord[] darkEdges;
    }

    [Serializable]
    public sealed class WofMountainLadderWrapRecord
    {
        public int index;
        public float[] leftPosition;
        public float[] rightPosition;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainExitBridgeRecord
    {
        public WofMountainExitBridgeFrameRecord frame;
        public float y;
        public WofMountainExitBridgeDetailsRecord details;
    }

    [Serializable]
    public sealed class WofMountainExitBridgeFrameRecord
    {
        public float angle;
        public float length;
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class WofMountainExitBridgeDetailsRecord
    {
        public float supportLength;
        public WofMountainSidePositionRecord[] edgeShadows;
        public WofMountainBridgeGapRecord[] darkGaps;
        public WofMountainBridgePlankRecord[] planks;
        public WofMountainBridgeSideRailRecord[] sideRails;
        public WofMountainNamedTransformRecord[] supports;
        public float[] lanternPosition;
    }

    [Serializable]
    public sealed class WofMountainBridgeGapRecord
    {
        public int index;
        public float z;
    }

    [Serializable]
    public sealed class WofMountainBridgePlankRecord
    {
        public int index;
        public float z;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainBridgeSideRailRecord
    {
        public int side;
        public float[] position;
        public WofMountainBridgePostRecord[] posts;
    }

    [Serializable]
    public sealed class WofMountainBridgePostRecord
    {
        public int index;
        public int side;
        public float[] position;
        public string color;
    }

    [Serializable]
    public sealed class WofMountainNamedTransformRecord
    {
        public string key;
        public float[] position;
        public float[] rotation;
    }

    [Serializable]
    public sealed class WofMountainWaterfallVisualRecord
    {
        public WofMountainWaterfallPlaneRecord mainFall;
        public WofMountainWaterfallPlaneRecord brightFall;
        public WofMountainWaterfallPlaneRecord[] darkEdges;
        public WofMountainWaterfallFoamRecord topFoam;
        public WofMountainWaterfallFoamRecord bottomFoam;
        public WofMountainWaterfallFoamRecord[] sprayPuffs;
    }

    [Serializable]
    public class WofMountainWaterfallPlaneRecord
    {
        public int side;
        public float[] position;
        public float[] rotation;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class WofMountainWaterfallFoamRecord
    {
        public int index;
        public float[] position;
        public float[] rotation;
        public float[] scale;
    }

    public static class WofMountainVillageLayout
    {
        public const int ChunkX = 3;
        public const int ChunkZ = 0;
        public const float SurvivalBlockSize = 512f;
        public const float ReactBaseHeight = 3.364967894227928f;
        public const float ReactSummitY = 217.54496789422794f;
        public const float MountainRadius = 250.88f;
        public const float PerimeterShoulderRadius = 720f;
        public const int ExactSlopeGrassCount = 1793;
        public const float MineshaftBottomY = ReactBaseHeight + 3.2f;
        public static readonly Vector3 WorldOrigin = new(ChunkX * SurvivalBlockSize, 0f, ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 ViewProbeSpawn = WorldOrigin + new Vector3(0f, 110f, 900f);
        public static readonly Vector3 ProfileViewProbeSpawn = WorldOrigin + new Vector3(0f, 245f, 760f);
        public static readonly Vector3 AerialViewProbeSpawn = WorldOrigin + new Vector3(0f, 520f, 0f);
        public static readonly Vector3 SummitViewProbeSpawn = WorldOrigin + new Vector3(0f, ReactSummitY + 8f, 92f);
        public static readonly Vector3 BanquetViewProbeSpawn = WorldOrigin + new Vector3(-12f, MineshaftBottomY + 4f, 20f);
        public static readonly Vector3 CatwalkViewProbeSpawn = WorldOrigin + new Vector3(-12f, 109.43536789422794f, 12f);
        public static readonly Vector3 FirstVillagerWorldPosition = new(
            1559.0938176301752f,
            218.49496789422793f + WofVillagerMath.AvatarGroundLift,
            55.893706884455895f);
        public static readonly Vector3 FirstVillagerControllerProbeSpawn = new(
            1559.0938176301752f,
            ReactSummitY + 2.2f,
            53.393706884455895f);
        public static readonly Vector3 FirstLadderControllerProbeSpawn = WorldOrigin + new Vector3(
            2.598990542748065f,
            8.344967894227929f,
            11.663950795451175f);

        public static bool HasExactCounts(WofMountainVillageCounts counts)
        {
            return counts != null && counts.trailPoints == 25 && counts.trailSegments == 24 &&
                   counts.cliffPatches == 48 && counts.cabins == 8 && counts.interiorHuts == 3 &&
                   counts.interiorLadders == 4 && counts.hutInfos == 11 &&
                   counts.slopeGrassTufts == ExactSlopeGrassCount && counts.summitSnowDrifts == 28 &&
                   counts.rimBeams == 12 && counts.supportFrames == 4 && counts.supportPosts == 8 &&
                   counts.supportSnowCaps == 8 && counts.bottomRocks == 14 && counts.wallLanterns == 9 &&
                   counts.wallPaintings == 6 && counts.wallRopeLights == 20 &&
                   counts.banquetBottomLights == 8 && counts.banquetChairs == 7 &&
                   counts.banquetTablePlanks == 9 && counts.banquetTableLegs == 6 &&
                   counts.banquetBreads == 4 && counts.banquetFruitBowls == 4 &&
                   counts.banquetFruits == 20 && counts.banquetPlates == 8 &&
                   counts.banquetCandles == 2 && counts.villagers == 11;
        }
    }
}
