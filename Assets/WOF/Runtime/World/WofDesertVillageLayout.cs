using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofDesertVillageDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofDesertChunkRecord chunk;
        public float baseHeight;
        public WofDesertVillageCounts counts;
        public WofDesertVillageLayoutRecord layout;
        public WofVillagerLayoutRecord[] villagers;
        public WofSerializedMeshRecord padGeometry;
        public WofDesertSurfaceGeometryRecord surfaceGeometries;
    }

    [Serializable]
    public sealed class WofDesertChunkRecord
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
    public sealed class WofDesertVillageCounts
    {
        public int buildings;
        public int huts;
        public int wallSegments;
        public int marketStalls;
        public int palms;
        public int ladders;
        public int fences;
        public int clothesLines;
        public int streetProps;
        public int villagers;
    }

    [Serializable]
    public sealed class WofDesertVillageLayoutRecord
    {
        public WofDesertBuildingRecord[] buildings;
        public WofDesertWallSegmentRecord[] wallSegments;
        public WofDesertMarketStallRecord[] marketStalls;
        public WofDesertPalmRecord[] palms;
        public WofDesertLadderRecord[] ladders;
        public WofDesertFenceRecord[] fences;
        public WofDesertClothesLineRecord[] clothesLines;
        public WofDesertStreetPropRecord[] streetProps;
    }

    [Serializable]
    public sealed class WofDesertBuildingRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float width;
        public float depth;
        public float height;
        public float rotation;
        public string color;
        public string roofColor;
        public float variant;
    }

    [Serializable]
    public sealed class WofDesertWallSegmentRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float width;
        public float height;
        public float depth;
    }

    [Serializable]
    public sealed class WofDesertMarketStallRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public string color;
    }

    [Serializable]
    public sealed class WofDesertPalmRecord
    {
        public string key;
        public float localX;
        public float localY;
        public float localZ;
        public float scale;
        public float rotation;
    }

    [Serializable]
    public sealed class WofDesertLadderRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float height;
        public float width;
    }

    [Serializable]
    public sealed class WofDesertFenceRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float length;
    }

    [Serializable]
    public sealed class WofDesertClothesLineRecord
    {
        public string key;
        public float startX;
        public float startZ;
        public float endX;
        public float endZ;
        public float y;
        public string[] colors;
    }

    [Serializable]
    public sealed class WofDesertStreetPropRecord
    {
        public string key;
        public string kind;
        public float localX;
        public float localZ;
        public float rotation;
        public float scale;
    }

    [Serializable]
    public sealed class WofSerializedMeshRecord
    {
        public int vertexCount;
        public int indexCount;
        public float[] positions;
        public float[] normals;
        public float[] colors;
        public float[] uvs;
        public int[] indices;
    }

    [Serializable]
    public sealed class WofDesertSurfaceGeometryRecord
    {
        public WofSerializedMeshRecord northSouthRoad;
        public WofSerializedMeshRecord eastWestRoad;
        public WofSerializedMeshRecord diagonalRoadA;
        public WofSerializedMeshRecord diagonalRoadB;
        public WofSerializedMeshRecord northSouthLeft;
        public WofSerializedMeshRecord northSouthRight;
        public WofSerializedMeshRecord eastWestLeft;
        public WofSerializedMeshRecord eastWestRight;
        public WofSerializedMeshRecord diagonalALeft;
        public WofSerializedMeshRecord diagonalARight;
        public WofSerializedMeshRecord diagonalBLeft;
        public WofSerializedMeshRecord diagonalBRight;
    }

    public static class WofDesertVillageLayout
    {
        public const int ChunkX = 4;
        public const int ChunkZ = -4;
        public const float SurvivalBlockSize = 512f;
        public const float ReactBaseHeight = 17.885722662941443f;
        public const float VillageRadius = 250f;
        public static readonly Vector3 WorldOrigin = new(
            ChunkX * SurvivalBlockSize,
            0f,
            ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 ViewProbeSpawn = WorldOrigin + new Vector3(0f, ReactBaseHeight + 2.2f, -214f);
        public static readonly Vector3 FirstVillagerWorldPosition = new(
            2101.443264570222f,
            18.835722662941443f + WofVillagerMath.AvatarGroundLift,
            -1988.2660909595888f);
        public static readonly Vector3 FirstVillagerControllerProbeSpawn = new(
            2101.443264570222f,
            ReactBaseHeight + 2.2f,
            -1990.7660909595888f);

        public static bool HasExactCounts(WofDesertVillageCounts counts)
        {
            return counts != null &&
                   counts.buildings == 55 && counts.huts == 55 && counts.wallSegments == 52 &&
                   counts.marketStalls == 10 && counts.palms == 22 && counts.ladders == 37 &&
                   counts.fences == 41 && counts.clothesLines == 15 && counts.streetProps == 94 &&
                   counts.villagers == 55;
        }
    }
}
