using System;
using UnityEngine;

namespace WOF
{
    [Serializable]
    public sealed class WofLilyCoilDocument
    {
        public int schemaVersion;
        public string source;
        public string sourceSignature;
        public WofLilyCoilChunkRecord chunk;
        public WofLilyCoilSpawnRecord spawn;
        public WofLilyCoilConstantsRecord constants;
        public WofLilyCoilCountsRecord counts;
        public WofLilyCoilTextureRecord textures;
        public WofLilyCoilEyeFrameRecord[] eyeFrames;
        public WofLilyCoilFloraRecord flora;
        public WofLilyCoilGeometryRecord geometries;
    }

    [Serializable]
    public sealed class WofLilyCoilChunkRecord
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
    public sealed class WofLilyCoilSpawnRecord
    {
        public float x;
        public float y;
        public float z;
        public float yawRadians;
    }

    [Serializable]
    public sealed class WofLilyCoilConstantsRecord
    {
        public float survivalBlockSize;
        public float groundY;
        public float realmRadius;
        public float wallHeight;
        public int wallSegmentCount;
        public float tubePathRadius;
        public float tubeStartY;
        public float tubeRise;
        public float tubeTurns;
        public float tubeStartAngle;
        public float tubeRadius;
        public int tubeCurvePointCount;
        public int tubeRenderSegments;
        public int tubeRenderRadialSegments;
        public int tubeColliderSegments;
        public int tubeColliderRadialSegments;
        public float eyeCapRadius;
        public int eyeFrameCount;
        public float eyeFrameFps;
        public int highlightCount;
        public float tubeJumpForce;
        public float tubeJumpGravity;
        public float tubeMaxJumpOffset;
        public float reactPlayerFootOffset;
        public float reactTubePlayerRadius;
        public float tubeMovementMultiplier;
    }

    [Serializable]
    public sealed class WofLilyCoilCountsRecord
    {
        public int tubeGrassGroups;
        public int tubeGrassTufts;
        public int tubeLilies;
        public int tubeFlowers;
        public int smallTubeFlowers;
        public int smallBloomParticles;
        public int fireflies;
        public int butterflies;
        public int groundGrassTufts;
        public int groundLilies;
        public int groundLilyLights;
        public int eyeFrames;
    }

    [Serializable]
    public sealed class WofLilyCoilTextureRecord
    {
        public string grass;
        public string stone;
        public string wall;
        public string ramp;
        public string callaBloom;
        public string meadowOverlay;
        public string groundBladeAlpha;
        public string tubeGrassAlpha;
    }

    [Serializable]
    public sealed class WofLilyCoilEyeFrameRecord
    {
        public int index;
        public string file;
        public int bytes;
        public string sha256;
    }

    [Serializable]
    public sealed class WofLilyCoilTubeGrassRecord
    {
        public int group;
        public float t;
        public float angle;
        public float yaw;
        public float radius;
        public float height;
        public float width;
        public float lean;
    }

    [Serializable]
    public sealed class WofLilyCoilDecorRecord
    {
        public float t;
        public float angle;
        public float yaw;
        public float scale;
    }

    [Serializable]
    public sealed class WofLilyCoilFlowerRecord
    {
        public float t;
        public float angle;
        public float yaw;
        public float scale;
        public float stemHeight;
        public float bloomHeight;
        public float bloomWidth;
        public float tilt;
    }

    [Serializable]
    public sealed class WofLilyCoilBloomParticleRecord
    {
        public int flowerIndex;
        public float phase;
        public float radius;
        public float speed;
        public float size;
        public float height;
    }

    [Serializable]
    public sealed class WofLilyCoilFlyingLightRecord
    {
        public int anchor;
        public int hop;
        public float phase;
        public float speed;
        public float arc;
        public float wander;
        public float size;
    }

    [Serializable]
    public sealed class WofLilyCoilGroundGrassRecord
    {
        public float x;
        public float z;
        public float yaw;
        public float height;
        public float width;
        public float lean;
    }

    [Serializable]
    public sealed class WofLilyCoilGroundLilyRecord
    {
        public float x;
        public float z;
        public float yaw;
        public float scale;
    }

    [Serializable]
    public sealed class WofLilyCoilFloraRecord
    {
        public WofLilyCoilTubeGrassRecord[] tubeGrass;
        public WofLilyCoilDecorRecord[] tubeLilies;
        public WofLilyCoilFlowerRecord[] tubeFlowers;
        public WofLilyCoilFlowerRecord[] smallTubeFlowers;
        public WofLilyCoilBloomParticleRecord[] smallBloomParticles;
        public WofLilyCoilFlyingLightRecord[] fireflies;
        public WofLilyCoilFlyingLightRecord[] butterflies;
        public WofLilyCoilGroundGrassRecord[] groundGrass;
        public WofLilyCoilGroundLilyRecord[] groundLilies;
        public WofLilyCoilGroundLilyRecord[] groundLilyLights;
    }

    [Serializable]
    public sealed class WofLilyCoilGeometryRecord
    {
        public WofSerializedMeshRecord tunnel;
        public WofSerializedMeshRecord tunnelCollider;
    }

    public readonly struct WofLilyCoilFrame
    {
        public WofLilyCoilFrame(Vector3 center, Vector3 tangent, Vector3 up, Vector3 side)
        {
            Center = center;
            Tangent = tangent;
            Up = up;
            Side = side;
        }

        public Vector3 Center { get; }
        public Vector3 Tangent { get; }
        public Vector3 Up { get; }
        public Vector3 Side { get; }
    }

    public readonly struct WofLilyCoilNearestState
    {
        public WofLilyCoilNearestState(float t, float surfaceAngle)
        {
            T = t;
            SurfaceAngle = surfaceAngle;
        }

        public float T { get; }
        public float SurfaceAngle { get; }
    }

    public static class WofLilyCoilLayout
    {
        public const int ChunkX = 48;
        public const int ChunkZ = -48;
        public const float SurvivalBlockSize = 512f;
        public const float GroundY = 10f;
        public const float RealmRadius = 640f;
        public const float WallHeight = 650f;
        public const int WallSegmentCount = 36;
        public const float TubePathRadius = 238f;
        public const float TubeStartY = 108f;
        public const float TubeRise = 520f;
        public const float TubeTurns = 3.15f;
        public const float TubeStartAngle = -Mathf.PI / 2f;
        public const float TubeRadius = 76f;
        public const float EyeCapRadius = 106f;
        public const int EyeFrameCount = 36;
        public const float EyeFrameFps = 10f;
        public const float ReactPlayerFootOffset = 1.15f;
        public const float TubePlayerRadius = TubeRadius - ReactPlayerFootOffset;
        public const float TubeMovementMultiplier = 4.8f;
        public const float TubeJumpForce = 18f;
        public const float TubeJumpGravity = 38f;
        public const float TubeMaxJumpOffset = 18f;
        public const float TubeThrusterImpulsePerSecond = 35f;
        public const float TubeThrusterFuelDrainPerSecond = 0.8f;
        public const float TubeThrusterFuelRechargePerSecond = 0.4f;
        public const int TubeGrassTuftCount = 14276;
        public const int TubeLilyCount = 1318;
        public const int TubeFlowerCount = 174;
        public const int SmallTubeFlowerCount = 250;
        public const int SmallBloomParticleCount = 750;
        public const int FireflyCount = 160;
        public const int ButterflyCount = 10;
        public const int GroundGrassTuftCount = 5200;
        public const int GroundLilyCount = 560;
        public const int GroundLilyLightCount = 4;
        public const string SourceSignature = "50f48f75ddd68f65ae7cf651fad9615ac0c80d48141591af1c148bf3360d9266";

        private const float AngleRate = Mathf.PI * 2f * TubeTurns;
        private const float HorizontalPath = TubePathRadius * AngleRate;
        public static readonly float TubePathLength = Mathf.Sqrt(HorizontalPath * HorizontalPath + TubeRise * TubeRise);
        public static readonly Vector3 WorldOrigin = new(ChunkX * SurvivalBlockSize, 0f, ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 SpawnPosition = WorldOrigin + new Vector3(237.11f, 72.15f, -20.54f);
        public static readonly float SpawnYawDegrees = Mathf.Repeat(3.055f * Mathf.Rad2Deg + 180f, 360f);

        public static bool HasExactCounts(WofLilyCoilCountsRecord counts)
        {
            return counts != null && counts.tubeGrassGroups == 3 &&
                   counts.tubeGrassTufts == TubeGrassTuftCount && counts.tubeLilies == TubeLilyCount &&
                   counts.tubeFlowers == TubeFlowerCount && counts.smallTubeFlowers == SmallTubeFlowerCount &&
                   counts.smallBloomParticles == SmallBloomParticleCount && counts.fireflies == FireflyCount &&
                   counts.butterflies == ButterflyCount && counts.groundGrassTufts == GroundGrassTuftCount &&
                   counts.groundLilies == GroundLilyCount && counts.groundLilyLights == GroundLilyLightCount &&
                   counts.eyeFrames == EyeFrameCount;
        }

        public static WofLilyCoilFrame GetFrame(float t)
        {
            var clampedT = Mathf.Clamp01(t);
            var angle = TubeStartAngle + AngleRate * clampedT;
            var center = WorldOrigin + new Vector3(
                Mathf.Cos(angle) * TubePathRadius,
                TubeStartY + TubeRise * clampedT,
                Mathf.Sin(angle) * TubePathRadius);
            var tangent = new Vector3(
                -Mathf.Sin(angle) * TubePathRadius * AngleRate,
                TubeRise,
                Mathf.Cos(angle) * TubePathRadius * AngleRate).normalized;
            var up = Vector3.up - tangent * Vector3.Dot(Vector3.up, tangent);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.right;
            up.Normalize();
            var side = Vector3.Cross(tangent, up).normalized;
            return new WofLilyCoilFrame(center, tangent, up, side);
        }

        public static WofLilyCoilNearestState GetNearestState(Vector3 position)
        {
            var bestT = 0f;
            var bestDistanceSquared = float.PositiveInfinity;
            const int samples = 180;
            for (var index = 0; index <= samples; index++)
            {
                var t = index / (float)samples;
                var center = GetFrame(t).Center;
                var distanceSquared = (center - position).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                bestT = t;
            }

            var frame = GetFrame(bestT);
            var offset = position - frame.Center;
            var upAmount = Vector3.Dot(offset, frame.Up);
            var sideAmount = Vector3.Dot(offset, frame.Side);
            var surfaceRadiusSquared = upAmount * upAmount + sideAmount * sideAmount;
            var surfaceAngle = surfaceRadiusSquared > 0.0001f ? Mathf.Atan2(sideAmount, upAmount) : Mathf.PI;
            return new WofLilyCoilNearestState(bestT, surfaceAngle);
        }

        public static bool IsInsideTubeRealm(Vector3 position)
        {
            var local = position - WorldOrigin;
            var horizontalLimit = TubePathRadius + TubeRadius + 145f;
            return local.x * local.x + local.z * local.z < horizontalLimit * horizontalLimit &&
                   position.y > -80f &&
                   position.y < TubeStartY + TubeRise + TubeRadius + 120f;
        }

        public static Vector3 GetRadial(WofLilyCoilFrame frame, float surfaceAngle)
        {
            return (frame.Up * Mathf.Cos(surfaceAngle) + frame.Side * Mathf.Sin(surfaceAngle)).normalized;
        }

        public static Vector3 GetAroundSurface(WofLilyCoilFrame frame, float surfaceAngle)
        {
            return (frame.Up * -Mathf.Sin(surfaceAngle) + frame.Side * Mathf.Cos(surfaceAngle)).normalized;
        }

        public static float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
        }
    }
}
