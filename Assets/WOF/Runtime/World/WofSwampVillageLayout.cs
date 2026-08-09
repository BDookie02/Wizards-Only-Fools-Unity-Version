using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofSwampVillageDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofSwampChunkRecord chunk;
        public float baseHeight;
        public WofSwampVillageCounts counts;
        public WofSwampVillageConstants constants;
        public WofSwampVillageLayoutRecord layout;
        public WofSwampRopeSegmentRecord[] ropeSegments;
        public WofSwampRopeBulbRecord[] ropeBulbs;
        public WofVillagerLayoutRecord[] villagers;
        public WofSwampToadContract toad;
        public WofSerializedMeshRecord padGeometry;
        public WofSerializedMeshRecord lilyPadGeometry;
    }

    [Serializable]
    public sealed class WofSwampChunkRecord
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
    public sealed class WofSwampVillageCounts
    {
        public int huts;
        public int hutInfos;
        public int walkways;
        public int ramps;
        public int lilyPads;
        public int stumps;
        public int reeds;
        public int ropes;
        public int ropeSegments;
        public int ropeBulbs;
        public int pointLights;
        public int villagers;
    }

    [Serializable]
    public sealed class WofSwampVillageConstants
    {
        public float villageRadius;
        public float platformSize;
        public float toadUpdateIntervalSeconds;
    }

    [Serializable]
    public sealed class WofSwampVillageLayoutRecord
    {
        public WofSwampHutRecord[] huts;
        public WofSwampWalkwayRecord[] walkways;
        public WofSwampRampRecord[] ramps;
        public WofSwampLilyPadRecord[] lilyPads;
        public WofSwampStumpRecord[] stumps;
        public WofSwampReedRecord[] reeds;
        public WofSwampRopeRecord[] ropes;
        public float waterY;
        public float platformY;
    }

    [Serializable]
    public sealed class WofSwampHutRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float width;
        public float depth;
        public float height;
        public float rotation;
        public float ropeAngle;
        public float platformY;
        public string wallColor;
        public string roofColor;
        public float variant;
    }

    [Serializable]
    public sealed class WofSwampWalkwayRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float width;
        public float length;
        public float y;
    }

    [Serializable]
    public sealed class WofSwampRampRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float width;
        public float length;
        public float highY;
        public float lowY;
    }

    [Serializable]
    public sealed class WofSwampLilyPadRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float scale;
        public float rotation;
        public string color;
    }

    [Serializable]
    public sealed class WofSwampStumpRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float height;
        public float radius;
    }

    [Serializable]
    public sealed class WofSwampReedRecord
    {
        public string key;
        public float localX;
        public float localZ;
        public float rotation;
        public float scale;
    }

    [Serializable]
    public sealed class WofSwampRopeRecord
    {
        public string key;
        public float[] start;
        public float[] end;
        public float sag;
        public int lightCount;
        public float lightHue;
    }

    [Serializable]
    public sealed class WofSwampRopeSegmentRecord
    {
        public string key;
        public float[] position;
        public float[] quaternion;
        public float length;
    }

    [Serializable]
    public sealed class WofSwampRopeBulbRecord
    {
        public string key;
        public float[] position;
        public float[] cordPosition;
        public float cordLength;
        public string color;
        public bool hasPointLight;
    }

    [Serializable]
    public sealed class WofSwampToadContract
    {
        public string source;
        public int[] frameSize;
        public int idleFrameMs;
        public int yawnFrameMs;
        public string[] idle;
        public string[] yawn;
        public string sleep;
        public string sleepZ;
    }

    public static class WofSwampVillageLayout
    {
        public const int ChunkX = 0;
        public const int ChunkZ = -3;
        public const float SurvivalBlockSize = 512f;
        public const float ReactBaseHeight = 2.7529895363497836f;
        public const float ReactWaterY = 3.1729895363497835f;
        public const float ReactPlatformY = 9.072989536349784f;
        public const float VillageRadius = 214f;
        public const float PlatformSize = 76f;
        public static readonly Vector3 WorldOrigin = new(
            ChunkX * SurvivalBlockSize,
            0f,
            ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 ViewProbeSpawn = WorldOrigin + new Vector3(0f, ReactPlatformY + 2.2f, -68f);
        public static readonly Vector3 FirstVillagerWorldPosition = new(
            19.649948641613857f,
            10.122989536349785f + WofVillagerMath.AvatarGroundLift,
            -1442.0824339847065f);
        public static readonly Vector3 FirstVillagerControllerProbeSpawn = new(
            19.649948641613857f,
            ReactPlatformY + 2.2f,
            -1444.5824339847065f);

        public static bool HasExactCounts(WofSwampVillageCounts counts)
        {
            return counts != null &&
                   counts.huts == 13 && counts.hutInfos == 13 && counts.walkways == 17 &&
                   counts.ramps == 4 && counts.lilyPads == 28 && counts.stumps == 18 &&
                   counts.reeds == 36 && counts.ropes == 13 && counts.ropeSegments == 91 &&
                   counts.ropeBulbs == 39 && counts.pointLights == 3 && counts.villagers == 13;
        }
    }
}
