using UnityEngine;

namespace WOF
{
    public enum WofDarrelDragonMode
    {
        Sleep,
        Wake,
        Idle,
        Attack
    }

    public readonly struct WofDarrelDragonFrame
    {
        public WofDarrelDragonFrame(WofDarrelDragonMode mode, float modeStartedAt, int frameIndex)
        {
            Mode = mode;
            ModeStartedAt = modeStartedAt;
            FrameIndex = frameIndex;
        }

        public WofDarrelDragonMode Mode { get; }
        public float ModeStartedAt { get; }
        public int FrameIndex { get; }
    }

    public readonly struct WofDarrelDragonVisuals
    {
        public WofDarrelDragonVisuals(float localY, float width, float height)
        {
            LocalY = localY;
            Width = width;
            Height = height;
        }

        public float LocalY { get; }
        public float Width { get; }
        public float Height { get; }
    }

    public readonly struct WofDarrelWaterfallVisuals
    {
        public WofDarrelWaterfallVisuals(
            Vector2 fallTextureOffset,
            Vector2 poolTextureOffset,
            Vector2 runnelTextureOffset,
            float fallOpacity,
            float foamOpacity,
            float poolOpacity)
        {
            FallTextureOffset = fallTextureOffset;
            PoolTextureOffset = poolTextureOffset;
            RunnelTextureOffset = runnelTextureOffset;
            FallOpacity = fallOpacity;
            FoamOpacity = foamOpacity;
            PoolOpacity = poolOpacity;
        }

        public Vector2 FallTextureOffset { get; }
        public Vector2 PoolTextureOffset { get; }
        public Vector2 RunnelTextureOffset { get; }
        public float FallOpacity { get; }
        public float FoamOpacity { get; }
        public float PoolOpacity { get; }
    }

    public readonly struct WofDarrelWaterfallSprayVisuals
    {
        public WofDarrelWaterfallSprayVisuals(float localY, Vector3 localScale)
        {
            LocalY = localY;
            LocalScale = localScale;
        }

        public float LocalY { get; }
        public Vector3 LocalScale { get; }
    }

    public static class WofDarrelGroveLayout
    {
        public const int SurvivalBlockSize = 512;
        public const int ChunkX = 12;
        public const int ChunkZ = -12;
        public const float GroundY = 18f;
        public const float HalfSize = 252f;
        public const float HutHillHeight = 8.8f;
        public const float HutHillSurfaceOffset = 10.85f;
        public const float HutBaseLift = 3.25f;
        public const float HutFoundationHeight = 1.2f;
        public const float HutBaseY = 30.05f;
        public const float HutEntrySurfaceOffset = 14.15f;
        public const float DragonHouseHalfWidth = 39f;
        public const float DragonHouseHalfDepth = 31f;
        public const float DragonTalkRadius = 34f;
        public const float ReactSpawnYawRadians = Mathf.PI;
        public const float UnitySpawnYawDegrees = 0f;
        public const float SleepFrameSeconds = 0.240f;
        public const float WakeFrameSeconds = 0.115f;
        public const float IdleFrameSeconds = 0.155f;
        public const float AttackFrameSeconds = 0.095f;
        public const int SleepFrameCount = 8;
        public const int WakeFrameCount = 9;
        public const int IdleFrameCount = 11;
        public const int AttackFrameCount = 16;

        public static readonly Vector3 WorldOrigin = new(
            ChunkX * SurvivalBlockSize,
            0f,
            ChunkZ * SurvivalBlockSize);
        public static readonly Vector3 SpawnPosition = WorldOrigin + new Vector3(0f, 33.35f, -52f);
        public static readonly Vector3 DragonLocalPosition = new(10f, HutBaseY + 9.3f, 6f);
        public static readonly Vector3 DragonWorldPosition = WorldOrigin + DragonLocalPosition;
        public static readonly Vector3 DragonQuestMarkerWorldPosition = WorldOrigin + new Vector3(10f, 43.25f, 6f);
        public static readonly Vector3 ReturnGateLocalPosition = new(0f, GroundY, -224f);
        public static readonly Vector3 ReturnGateWorldPosition = WorldOrigin + ReturnGateLocalPosition;

        public static Vector3 ToLocal(Vector3 worldPosition)
        {
            return worldPosition - WorldOrigin;
        }

        public static bool IsInsideDragonHouse(Vector3 worldPosition)
        {
            var local = ToLocal(worldPosition);
            return Mathf.Abs(local.x) <= DragonHouseHalfWidth &&
                   Mathf.Abs(local.z) <= DragonHouseHalfDepth;
        }

        public static bool IsNearDragon(Vector3 worldPosition)
        {
            var local = ToLocal(worldPosition);
            var deltaX = local.x - DragonLocalPosition.x;
            var deltaZ = local.z - DragonLocalPosition.z;
            return deltaX * deltaX + deltaZ * deltaZ <= DragonTalkRadius * DragonTalkRadius;
        }

        public static bool CanInteractWithDragon(Vector3 worldPosition)
        {
            return IsInsideDragonHouse(worldPosition) && IsNearDragon(worldPosition);
        }

        public static bool IsInsideReturnGate(Vector3 worldPosition)
        {
            var local = ToLocal(worldPosition) - ReturnGateLocalPosition;
            return Mathf.Abs(local.x) <= 8f &&
                   Mathf.Abs(local.y - 8f) <= 8f &&
                   Mathf.Abs(local.z) <= 5f;
        }

        public static WofDarrelDragonMode ResolveNextDragonMode(
            WofDarrelDragonMode current,
            bool hasFoughtDragon,
            bool hasWoken)
        {
            if (hasFoughtDragon)
            {
                return WofDarrelDragonMode.Attack;
            }
            if (hasWoken && current == WofDarrelDragonMode.Sleep)
            {
                return WofDarrelDragonMode.Wake;
            }
            if (hasWoken && current == WofDarrelDragonMode.Attack)
            {
                return WofDarrelDragonMode.Idle;
            }
            return hasWoken ? current : WofDarrelDragonMode.Sleep;
        }

        public static WofDarrelDragonFrame ResolveDragonFrame(
            WofDarrelDragonMode mode,
            float modeStartedAt,
            float now)
        {
            var elapsed = Mathf.Max(0f, now - modeStartedAt);
            var frameSeconds = GetFrameSeconds(mode);
            if (mode == WofDarrelDragonMode.Wake && elapsed + 0.000001f >= WakeFrameCount * frameSeconds)
            {
                return new WofDarrelDragonFrame(WofDarrelDragonMode.Idle, now, 0);
            }

            var frameCount = GetFrameCount(mode);
            var rawFrame = Mathf.FloorToInt(elapsed / frameSeconds);
            var frame = mode == WofDarrelDragonMode.Wake
                ? Mathf.Min(frameCount - 1, rawFrame)
                : rawFrame % frameCount;
            return new WofDarrelDragonFrame(mode, modeStartedAt, frame);
        }

        public static WofDarrelDragonVisuals ResolveDragonVisuals(
            WofDarrelDragonMode mode,
            float modeStartedAt,
            float now)
        {
            var wakeDuration = WakeFrameCount * WakeFrameSeconds;
            var wakeProgress = mode switch
            {
                WofDarrelDragonMode.Sleep => 0f,
                WofDarrelDragonMode.Wake => Mathf.Clamp01((now - modeStartedAt) / wakeDuration),
                _ => 1f
            };
            var breathAmount = mode == WofDarrelDragonMode.Sleep
                ? 0.018f
                : mode == WofDarrelDragonMode.Attack
                    ? 0.055f
                    : 0.035f;
            var breath = 1f + Mathf.Sin(now / 0.620f) * breathAmount;
            var width = mode == WofDarrelDragonMode.Attack ? 49f : Mathf.Lerp(43f, 38f, wakeProgress);
            var height = mode == WofDarrelDragonMode.Attack ? 34f : Mathf.Lerp(27f, 31f, wakeProgress);
            var attentionLift = mode == WofDarrelDragonMode.Attack ? 4.2f : Mathf.Lerp(0f, 2.7f, wakeProgress);
            var floatAmplitude = mode == WofDarrelDragonMode.Attack ? 0.34f : 0.18f;
            return new WofDarrelDragonVisuals(
                attentionLift + Mathf.Sin(now / 0.700f) * floatAmplitude * wakeProgress,
                width * breath,
                height * breath);
        }

        public static WofDarrelWaterfallVisuals ResolveWaterfallVisuals(float elapsed)
        {
            elapsed = Mathf.Max(0f, elapsed);
            return new WofDarrelWaterfallVisuals(
                new Vector2(
                    Mathf.Sin(elapsed * 1.35f) * 0.035f,
                    Mathf.Repeat(elapsed * 0.82f, 1f)),
                new Vector2(
                    Mathf.Repeat(elapsed * 0.08f, 1f),
                    Mathf.Sin(elapsed * 0.62f) * 0.035f),
                new Vector2(
                    Mathf.Sin(elapsed * 0.9f) * 0.04f,
                    Mathf.Repeat(elapsed * 0.36f, 1f)),
                0.55f + Mathf.Sin(elapsed * 4.2f) * 0.08f,
                0.28f + Mathf.Sin(elapsed * 5.1f + 0.8f) * 0.08f,
                0.82f + Mathf.Sin(elapsed * 1.7f) * 0.06f);
        }

        public static float ResolveWaterfallRunnelLocalX(int runtimeIndex, float elapsed)
        {
            return Mathf.Sin(runtimeIndex) * 7f +
                   Mathf.Sin(Mathf.Max(0f, elapsed) * 1.7f + runtimeIndex * 1.8f) * 0.42f;
        }

        public static WofDarrelWaterfallSprayVisuals ResolveWaterfallSprayVisuals(
            int index,
            float baseY,
            float baseScale,
            float elapsed)
        {
            elapsed = Mathf.Max(0f, elapsed);
            var pulse = 0.86f + Mathf.Sin(elapsed * 3.8f + index * 1.4f) * 0.18f;
            return new WofDarrelWaterfallSprayVisuals(
                baseY + Mathf.Sin(elapsed * 4.7f + index) * 0.42f,
                new Vector3(baseScale * pulse, baseScale * 0.45f * pulse, baseScale * pulse));
        }

        public static int GetFrameCount(WofDarrelDragonMode mode)
        {
            return mode switch
            {
                WofDarrelDragonMode.Sleep => SleepFrameCount,
                WofDarrelDragonMode.Wake => WakeFrameCount,
                WofDarrelDragonMode.Attack => AttackFrameCount,
                _ => IdleFrameCount
            };
        }

        public static float GetFrameSeconds(WofDarrelDragonMode mode)
        {
            return mode switch
            {
                WofDarrelDragonMode.Sleep => SleepFrameSeconds,
                WofDarrelDragonMode.Wake => WakeFrameSeconds,
                WofDarrelDragonMode.Attack => AttackFrameSeconds,
                _ => IdleFrameSeconds
            };
        }
    }
}
