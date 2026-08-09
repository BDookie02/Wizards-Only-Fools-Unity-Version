using System;
using System.Collections.Generic;
using UnityEngine;

namespace WOF
{
    public enum WofVillagerPhase
    {
        Idle = 0,
        Startled = 1,
        Angry = 2
    }

    [Serializable]
    public sealed class WofVillagerLayoutDocument
    {
        public int schemaVersion;
        public string source;
        public int count;
        public float renderDistanceDesktop;
        public float renderDistanceMobile;
        public float visibilityUpdateMs;
        public float runtimeTickMs;
        public float eyeLockRadius;
        public float avatarWorldHeight;
        public float avatarWorldWidth;
        public float avatarWorldCenterY;
        public float avatarScale;
        public float avatarGroundLift;
        public string darrelArchiveFile;
        public int darrelArchiveBytes;
        public string darrelArchiveSha256;
        public WofVillagerFrameContract frameContract;
        public WofVillagerLayoutRecord[] villagers;
    }

    [Serializable]
    public sealed class WofVillagerFrameContract
    {
        public int idleDirections;
        public int[] blinkDirections;
        public int startledDirections;
        public int startledUniqueFrames;
        public int angryDirections;
        public int[] reactionBlinkDirections;
        public int archiveEntriesPerVillager;
    }

    [Serializable]
    public sealed class WofVillagerLayoutRecord
    {
        public string id;
        public int index;
        public string displayName;
        public string townId;
        public string archiveFile;
        public int archiveBytes;
        public string archiveSha256;
        public float x;
        public float y;
        public float z;
        public float baseYaw;
        public float lookUpdateDesktopMs;
        public float lookUpdateMobileMs;
        public WofVillagerHutRecord hut;
        public WofVillagerCharacterRecord character;
    }

    [Serializable]
    public sealed class WofVillagerHutRecord
    {
        public float x;
        public float y;
        public float z;
        public int hutType;
        public bool isMushroom;
        public float rotation;
        public float interiorWidth;
        public float interiorDepth;
        public float interiorHeight;
    }

    [Serializable]
    public sealed class WofVillagerCharacterRecord
    {
        public string skinColor;
        public string topColor;
        public string pantsColor;
        public string shoesColor;
        public string hatColor;
        public string hairColor;
        public string facialHairColor;
        public string topStyle;
        public string pantsStyle;
        public string shoesStyle;
        public string hatStyle;
        public string hairStyle;
        public string facialHairStyle;
        public string eyeStyle;
        public string mouthStyle;
    }

    public static class WofVillagerMath
    {
        public const float DesktopRenderDistance = 90f;
        public const float MobileRenderDistance = 58f;
        public const float VisibilityUpdateSeconds = 0.35f;
        public const float RuntimeTickSeconds = 0.05f;
        public const float EyeLockRadius = 18f;
        public const float EyeLockRadiusSquared = EyeLockRadius * EyeLockRadius;
        public const float EyeLockVerticalDistance = 7f;
        public const float LookYawEpsilon = 0.045f;
        public const float StartledInsideSeconds = 0.68f;
        public const float AngryInsideSeconds = 3.2f;
        public const float StartledInteractSeconds = 0.52f;
        public const float AngryInteractSeconds = 2.4f;
        public const float JumpDurationSeconds = 0.65f;
        public const float JumpHeight = 0.55f;
        public const float AvatarScale = 2.25f;
        public const float AvatarWorldHeight = 2.95f;
        public const float AvatarWorldWidth = 2.95f;
        public const float AvatarWorldCenterY = 0.62f;
        public const float AvatarGroundLift = 1.06875f;

        public static bool ShouldRender(Vector3 playerPosition, Vector3 villagerPosition, bool reacting, bool mobile)
        {
            if (reacting)
            {
                return true;
            }

            var dx = playerPosition.x - villagerPosition.x;
            var dz = playerPosition.z - villagerPosition.z;
            var distance = mobile ? MobileRenderDistance : DesktopRenderDistance;
            return dx * dx + dz * dz < distance * distance;
        }

        public static bool TryResolveFacingYaw(
            Vector3 villagerPosition,
            Vector3 playerPosition,
            float baseYaw,
            out float yaw)
        {
            yaw = baseYaw;
            if (Mathf.Abs(playerPosition.y - villagerPosition.y) > EyeLockVerticalDistance)
            {
                return false;
            }

            var dx = playerPosition.x - villagerPosition.x;
            var dz = playerPosition.z - villagerPosition.z;
            if (dx * dx + dz * dz >= EyeLockRadiusSquared)
            {
                return false;
            }

            yaw = Mathf.Atan2(dx, -dz);
            return true;
        }

        public static bool TryResolveNearestFacingYaw(
            Vector3 villagerPosition,
            IReadOnlyList<Vector3> playerPositions,
            float baseYaw,
            out float yaw)
        {
            yaw = baseYaw;
            if (playerPositions == null)
            {
                return false;
            }

            var bestDistanceSquared = EyeLockRadiusSquared;
            var found = false;
            for (var index = 0; index < playerPositions.Count; index++)
            {
                var playerPosition = playerPositions[index];
                if (Mathf.Abs(playerPosition.y - villagerPosition.y) > EyeLockVerticalDistance)
                {
                    continue;
                }

                var dx = playerPosition.x - villagerPosition.x;
                var dz = playerPosition.z - villagerPosition.z;
                var distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                yaw = Mathf.Atan2(dx, -dz);
                found = true;
            }
            return found;
        }

        public static int ResolveDirection(float yaw, Vector3 villagerPosition, Vector3 cameraPosition)
        {
            var toCamera = Mathf.Atan2(
                cameraPosition.x - villagerPosition.x,
                -(cameraPosition.z - villagerPosition.z));
            var relative = Mathf.Repeat(toCamera - yaw + Mathf.PI * 2f, Mathf.PI * 2f);
            return (int)Math.Floor(relative / (Mathf.PI * 0.25f) + 0.5d) % 8;
        }

        public static float AngleDistance(float left, float right)
        {
            return Mathf.Abs(Mathf.DeltaAngle(left * Mathf.Rad2Deg, right * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
        }

        public static bool IsPlayerInsideHut(Vector3 playerPosition, WofVillagerHutRecord hut)
        {
            if (hut == null)
            {
                return false;
            }

            var dx = playerPosition.x - hut.x;
            var dz = playerPosition.z - hut.z;
            var interiorHeight = hut.interiorHeight > 0f ? hut.interiorHeight : 9.5f;
            if (playerPosition.y <= hut.y - 1.2f || playerPosition.y >= hut.y + interiorHeight)
            {
                return false;
            }

            if (hut.isMushroom)
            {
                return dx * dx + dz * dz < 5.35f * 5.35f;
            }

            var cos = Mathf.Cos(-hut.rotation);
            var sin = Mathf.Sin(-hut.rotation);
            var localX = dx * cos - dz * sin;
            var localZ = dx * sin + dz * cos;
            var halfWidth = hut.interiorWidth > 0f ? hut.interiorWidth * 0.5f : 7.35f;
            var halfDepth = hut.interiorDepth > 0f ? hut.interiorDepth * 0.5f : 7.35f;
            return Mathf.Abs(localX) < halfWidth && Mathf.Abs(localZ) < halfDepth;
        }

        public static WofVillagerPhase ResolvePhase(
            float now,
            float startledUntil,
            float angryUntil,
            bool playerInside)
        {
            if (now < startledUntil)
            {
                return WofVillagerPhase.Startled;
            }
            if (playerInside || now < angryUntil)
            {
                return WofVillagerPhase.Angry;
            }
            return WofVillagerPhase.Idle;
        }

        public static float ResolveJumpOffset(float now, float startedAt, WofVillagerPhase phase)
        {
            if (phase != WofVillagerPhase.Startled)
            {
                return 0f;
            }

            var progress = Mathf.Clamp01((now - startedAt) / JumpDurationSeconds);
            return Mathf.Sin(progress * Mathf.PI) * JumpHeight;
        }

        public static string ResolveFrameKey(
            WofVillagerPhase phase,
            int direction,
            int frame,
            bool blinking)
        {
            direction = ((direction % 8) + 8) % 8;
            var visibleFace = direction == 0 || direction == 1 || direction == 2 || direction == 6 || direction == 7;
            var blinkSuffix = blinking && visibleFace ? "-blink" : string.Empty;
            return phase switch
            {
                WofVillagerPhase.Startled => $"startled{blinkSuffix}/d{direction}/f{Math.Abs(frame) % 2}",
                WofVillagerPhase.Angry => $"angry{blinkSuffix}/d{direction}",
                _ => $"idle{blinkSuffix}/d{direction}"
            };
        }
    }

}
