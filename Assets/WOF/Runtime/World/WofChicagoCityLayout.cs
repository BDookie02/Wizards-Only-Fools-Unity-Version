using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofChicagoCityDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofChicagoChunkRecord chunk;
        public float baseHeight;
        public WofChicagoCityCounts counts;
        public WofChicagoCityConstants constants;
        public WofChicagoLayoutRecord layout;
        public WofChicagoStreetRecord street;
        public WofChicagoOperatorRecord[] operators;
        public WofChicagoInitialTrafficRecord initialTraffic;
        public WofSerializedMeshRecord padGeometry;
    }

    [Serializable]
    public sealed class WofChicagoChunkRecord
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
    public sealed class WofChicagoCityCounts
    {
        public int buildings;
        public int operators;
        public int pedestrians;
        public int cars;
        public int trafficLightIntersections;
        public int lamps;
        public int streetTrees;
        public int sidewalkSegments;
        public int hydrants;
        public int trashCans;
        public int benches;
        public int grassPatches;
        public int crosswalks;
        public int sidewalkPlanes;
        public int parkingLines;
    }

    [Serializable]
    public sealed class WofChicagoCityConstants
    {
        public float cityHalfSize;
        public float[] roadPositions;
        public float beanParkX;
        public float beanParkZ;
        public float ledSignUpdateIntervalSeconds;
        public float trafficUpdateIntervalSeconds;
        public float pedestrianUpdateIntervalSeconds;
    }

    [Serializable]
    public sealed class WofChicagoLayoutRecord
    {
        public WofChicagoBuildingRecord[] buildings;
        public WofChicagoPedestrianRecord[] pedestrians;
        public WofChicagoCarRecord[] cars;
    }

    [Serializable]
    public sealed class WofChicagoBuildingRecord
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
        public int facadeStyle;
        public bool enterable;
        public WofVillagerCharacterRecord operatorCharacter;
        public string landmark;
    }

    [Serializable]
    public sealed class WofChicagoPedestrianRecord
    {
        public string key;
        public string route;
        public float lane;
        public float sideOffset;
        public float offset;
        public float speed;
        public int direction;
        public WofVillagerCharacterRecord character;
    }

    [Serializable]
    public sealed class WofChicagoCarRecord
    {
        public string key;
        public string route;
        public string vehicleType;
        public float lane;
        public float offset;
        public float speed;
        public int direction;
        public string color;
        public float scale;
    }

    [Serializable]
    public sealed class WofChicagoStreetRecord
    {
        public WofChicagoPointRecord[] trafficLightIntersections;
        public WofChicagoLampRecord[] lamps;
        public WofChicagoStreetTreeRecord[] streetTrees;
        public WofChicagoSidewalkSegmentRecord[] sidewalkSegments;
        public WofChicagoPointRecord[] hydrants;
        public WofChicagoPointRecord[] trashCans;
        public WofChicagoBenchRecord[] benches;
        public WofChicagoGrassPatchRecord[] grassPatches;
        public WofChicagoPlanePatchRecord[] crosswalks;
        public WofChicagoPlanePatchRecord[] sidewalkPlanes;
        public WofChicagoPlanePatchRecord[] parkingLines;
    }

    [Serializable]
    public class WofChicagoPointRecord
    {
        public string key;
        public float x;
        public float z;
    }

    [Serializable]
    public sealed class WofChicagoLampRecord : WofChicagoPointRecord
    {
        public float rotation;
    }

    [Serializable]
    public sealed class WofChicagoStreetTreeRecord : WofChicagoPointRecord
    {
        public float scale;
    }

    [Serializable]
    public sealed class WofChicagoBenchRecord : WofChicagoPointRecord
    {
        public float rotation;
    }

    [Serializable]
    public sealed class WofChicagoSidewalkSegmentRecord
    {
        public string key;
        public float center;
        public float length;
    }

    [Serializable]
    public class WofChicagoPlanePatchRecord
    {
        public string key;
        public float x;
        public float z;
        public float width;
        public float depth;
        public float opacity;
    }

    [Serializable]
    public sealed class WofChicagoGrassPatchRecord : WofChicagoPlanePatchRecord
    {
        public string color;
    }

    [Serializable]
    public sealed class WofChicagoOperatorRecord
    {
        public int index;
        public string buildingKey;
        public string spritePath;
        public WofVillagerCharacterRecord character;
    }

    [Serializable]
    public sealed class WofChicagoTransformRecord
    {
        public float x;
        public float z;
        public float yaw;
    }

    [Serializable]
    public sealed class WofChicagoInitialTrafficRecord
    {
        public WofChicagoTransformRecord[] cars;
        public WofChicagoTransformRecord[] pedestrians;
    }

    public static class WofChicagoCityLayout
    {
        public const int ChunkX = -3;
        public const int ChunkZ = -3;
        public const float SurvivalBlockSize = 512f;
        public const float ReactBaseHeight = 21.912045982731858f;
        public const float CityHalfSize = 236f;

        public static readonly Vector3 WorldOrigin = new(
            ChunkX * SurvivalBlockSize,
            0f,
            ChunkZ * SurvivalBlockSize);

        public static readonly Vector3 ViewProbeSpawn = WorldOrigin +
                                                        new Vector3(0f, ReactBaseHeight + 2.2f, -214f);

        public static bool HasExactCounts(WofChicagoCityCounts counts)
        {
            return counts != null &&
                   counts.buildings == 35 && counts.operators == 35 &&
                   counts.pedestrians == 220 && counts.cars == 46 &&
                   counts.trafficLightIntersections == 16 && counts.lamps == 48 &&
                   counts.streetTrees == 40 && counts.sidewalkSegments == 5 &&
                   counts.hydrants == 16 && counts.trashCans == 36 &&
                   counts.benches == 34 && counts.grassPatches == 40 &&
                   counts.crosswalks == 576 && counts.sidewalkPlanes == 80 &&
                   counts.parkingLines == 64;
        }
    }
}
