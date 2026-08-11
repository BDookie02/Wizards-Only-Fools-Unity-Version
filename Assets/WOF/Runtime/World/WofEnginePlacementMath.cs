using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace WOF
{
    public readonly struct WofEnginePlacementPlan
    {
        public WofEnginePlacementPlan(
            bool ok,
            string reason,
            Vector3 position,
            float yawRadians,
            float gridSize,
            bool snapped)
        {
            Ok = ok;
            Reason = reason ?? string.Empty;
            Position = position;
            YawRadians = yawRadians;
            GridSize = gridSize;
            Snapped = snapped;
        }

        public bool Ok { get; }
        public string Reason { get; }
        public Vector3 Position { get; }
        public float YawRadians { get; }
        public float GridSize { get; }
        public bool Snapped { get; }
    }

    [Serializable]
    public struct WofEnginePlaceableRecord
    {
        public string instanceId;
        public string placeableId;
        public string label;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float trainingDummyHealth;
        public double trainingDummyRespawnAt;
        public int trainingDummyHitSequence;
        public int trainingDummyLastSpell;

        public Vector3 Position => new(x, y, z);
    }

    public struct WofNetworkEnginePlaceableRecord : INetworkSerializable, IEquatable<WofNetworkEnginePlaceableRecord>
    {
        public FixedString64Bytes InstanceId;
        public FixedString64Bytes PlaceableId;
        public FixedString64Bytes Label;
        public Vector3 Position;
        public float Yaw;
        public float TrainingDummyHealth;
        public double TrainingDummyRespawnAt;
        public int TrainingDummyHitSequence;
        public int TrainingDummyLastSpell;

        public WofNetworkEnginePlaceableRecord(WofEnginePlaceableRecord record)
        {
            InstanceId = record.instanceId ?? string.Empty;
            PlaceableId = record.placeableId ?? string.Empty;
            Label = record.label ?? string.Empty;
            Position = record.Position;
            Yaw = record.yaw;
            TrainingDummyHealth = record.trainingDummyHealth;
            TrainingDummyRespawnAt = record.trainingDummyRespawnAt;
            TrainingDummyHitSequence = record.trainingDummyHitSequence;
            TrainingDummyLastSpell = record.trainingDummyLastSpell;
        }

        public WofEnginePlaceableRecord ToRuntimeRecord()
        {
            return new WofEnginePlaceableRecord
            {
                instanceId = InstanceId.ToString(),
                placeableId = PlaceableId.ToString(),
                label = Label.ToString(),
                x = Position.x,
                y = Position.y,
                z = Position.z,
                yaw = Yaw,
                trainingDummyHealth = TrainingDummyHealth,
                trainingDummyRespawnAt = TrainingDummyRespawnAt,
                trainingDummyHitSequence = TrainingDummyHitSequence,
                trainingDummyLastSpell = TrainingDummyLastSpell
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref PlaceableId);
            serializer.SerializeValue(ref Label);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref TrainingDummyHealth);
            serializer.SerializeValue(ref TrainingDummyRespawnAt);
            serializer.SerializeValue(ref TrainingDummyHitSequence);
            serializer.SerializeValue(ref TrainingDummyLastSpell);
        }

        public bool Equals(WofNetworkEnginePlaceableRecord other)
        {
            return InstanceId.Equals(other.InstanceId) && PlaceableId.Equals(other.PlaceableId) &&
                   Label.Equals(other.Label) && Position.Equals(other.Position) && Yaw.Equals(other.Yaw) &&
                   TrainingDummyHealth.Equals(other.TrainingDummyHealth) &&
                   TrainingDummyRespawnAt.Equals(other.TrainingDummyRespawnAt) &&
                   TrainingDummyHitSequence == other.TrainingDummyHitSequence &&
                   TrainingDummyLastSpell == other.TrainingDummyLastSpell;
        }
    }

    public static class WofEnginePlacementMath
    {
        internal const float DefaultPlayerPlacementDistance = 7f;
        internal const float TrainingDummyPlacementDistance = 8f;

        public static float SanitizeGridSize(float value, float fallback)
        {
            return !float.IsFinite(value) || value <= 0f ? fallback : Mathf.Clamp(value, 0.25f, 16f);
        }

        public static float SnapCoordinate(float value, float gridSize)
        {
            return !float.IsFinite(value) || !float.IsFinite(gridSize) || gridSize <= 0f
                ? value
                : Mathf.Floor(value / gridSize + 0.5f) * gridSize;
        }

        public static float GetPlacementYaw(
            WofEnginePlaceableDefinition definition,
            float playerYawRadians,
            float x = 0f,
            float z = 0f)
        {
            if (definition.YawMode == WofEnginePlaceableYawMode.Fixed) return 0f;
            if (definition.YawMode == WofEnginePlaceableYawMode.Random)
            {
                var hash = 2166136261u;
                var key = $"{definition.Id}:{Mathf.FloorToInt(x * 100f + 0.5f)}:{Mathf.FloorToInt(z * 100f + 0.5f)}";
                for (var index = 0; index < key.Length; index++)
                {
                    hash ^= key[index];
                    hash = unchecked(hash * 16777619u);
                }
                var unit = hash / (double)uint.MaxValue;
                return (float)((unit * 2d - 1d) * Math.PI);
            }
            return float.IsFinite(playerYawRadians) ? playerYawRadians : 0f;
        }

        public static WofEnginePlacementPlan Plan(
            WofEnginePlaceableDefinition definition,
            Func<float, float, float> getGroundY,
            Vector3 playerPosition,
            float playerYawRadians,
            bool snapToGrid = true,
            float gridSize = float.NaN,
            float? sourceX = null,
            float? sourceZ = null,
            float? yawRadians = null)
        {
            var distance = Mathf.Max(DefaultPlayerPlacementDistance, definition.FootprintRadius + 4f);
            var x = sourceX ?? (playerPosition.x + Mathf.Sin(playerYawRadians) * distance);
            var z = sourceZ ?? (playerPosition.z - Mathf.Cos(playerYawRadians) * distance);
            if (!float.IsFinite(x) || !float.IsFinite(z))
            {
                return Invalid("player position unavailable");
            }

            var fallbackGrid = WofEnginePlaceableCatalog.GetDefaultGridSize(definition);
            var safeGrid = SanitizeGridSize(gridSize, fallbackGrid);
            if (snapToGrid)
            {
                x = SnapCoordinate(x, safeGrid);
                z = SnapCoordinate(z, safeGrid);
            }

            var surface = ValidateSurface(definition, getGroundY, x, z);
            if (!surface.Ok) return surface;
            return new WofEnginePlacementPlan(
                true,
                string.Empty,
                surface.Position,
                yawRadians.HasValue && float.IsFinite(yawRadians.Value)
                    ? yawRadians.Value
                    : GetPlacementYaw(definition, playerYawRadians, x, z),
                safeGrid,
                snapToGrid);
        }

        public static WofEnginePlacementPlan PlanTrainingDummy(Vector3 playerPosition, float playerYawRadians)
        {
            if (!float.IsFinite(playerPosition.x) || !float.IsFinite(playerPosition.y) ||
                !float.IsFinite(playerPosition.z) || !float.IsFinite(playerYawRadians))
                return Invalid("player position unavailable");
            return new WofEnginePlacementPlan(
                true,
                string.Empty,
                new Vector3(
                    playerPosition.x + Mathf.Sin(playerYawRadians) * TrainingDummyPlacementDistance,
                    playerPosition.y,
                    playerPosition.z - Mathf.Cos(playerYawRadians) * TrainingDummyPlacementDistance),
                playerYawRadians,
                1f,
                true);
        }

        public static WofEnginePlacementPlan ValidateSurface(
            WofEnginePlaceableDefinition definition,
            Func<float, float, float> getGroundY,
            float x,
            float z)
        {
            var centerY = getGroundY(x, z);
            if (!float.IsFinite(centerY)) return Invalid("terrain height unavailable", centerY);

            var sampleRadius = Mathf.Max(2.5f, definition.FootprintRadius * 0.72f);
            var heights = new[]
            {
                centerY,
                getGroundY(x + sampleRadius, z),
                getGroundY(x - sampleRadius, z),
                getGroundY(x, z + sampleRadius),
                getGroundY(x, z - sampleRadius)
            };
            var minY = centerY;
            var maxY = centerY;
            var valid = 0;
            for (var index = 0; index < heights.Length; index++)
            {
                if (!float.IsFinite(heights[index])) continue;
                valid++;
                minY = Mathf.Min(minY, heights[index]);
                maxY = Mathf.Max(maxY, heights[index]);
            }
            if (valid < 5) return Invalid("terrain height unavailable", centerY);
            if (maxY - minY > definition.MaxSlopeDelta)
                return Invalid("too steep for that object", centerY);

            return new WofEnginePlacementPlan(
                true,
                string.Empty,
                new Vector3(x, centerY + definition.HeightOffset, z),
                0f,
                WofEnginePlaceableCatalog.GetDefaultGridSize(definition),
                false);
        }

        public static WofEnginePlaceableRecord? FindCollision(
            WofEnginePlaceableDefinition definition,
            float x,
            float z,
            float yaw,
            IReadOnlyList<WofEnginePlaceableRecord> objects,
            string ignoreInstanceId = null)
        {
            GetFootprint(definition, out var circle, out var radius, out var halfX, out var halfZ);
            for (var index = 0; index < objects.Count; index++)
            {
                var other = objects[index];
                if (other.instanceId == ignoreInstanceId) continue;
                if (other.placeableId == "training-spell-dummy") continue;
                var otherDefinition = WofEnginePlaceableCatalog.Find(other.placeableId);
                if (otherDefinition == null) continue;
                GetFootprint(otherDefinition, out var otherCircle, out var otherRadius, out var otherHalfX, out var otherHalfZ);
                if (Overlaps(circle, radius, halfX, halfZ, x, z, yaw,
                        otherCircle, otherRadius, otherHalfX, otherHalfZ, other.x, other.z, other.yaw))
                    return other;
            }
            return null;
        }

        private static bool Overlaps(
            bool circle,
            float radius,
            float halfX,
            float halfZ,
            float x,
            float z,
            float yaw,
            bool otherCircle,
            float otherRadius,
            float otherHalfX,
            float otherHalfZ,
            float otherX,
            float otherZ,
            float otherYaw)
        {
            if (circle && otherCircle)
            {
                var minDistance = Mathf.Max(1.2f, (radius + otherRadius) * 0.82f);
                var dx = x - otherX;
                var dz = z - otherZ;
                return dx * dx + dz * dz < minDistance * minDistance;
            }
            if (!circle && !otherCircle)
                return BoxesOverlap(x, z, halfX, halfZ, yaw, otherX, otherZ, otherHalfX, otherHalfZ, otherYaw);
            return circle
                ? CircleOverlapsBox(x, z, radius, otherX, otherZ, otherHalfX, otherHalfZ, otherYaw)
                : CircleOverlapsBox(otherX, otherZ, otherRadius, x, z, halfX, halfZ, yaw);
        }

        private static bool BoxesOverlap(
            float ax, float az, float ahx, float ahz, float ayaw,
            float bx, float bz, float bhx, float bhz, float byaw)
        {
            GetAxes(ayaw, out var axX, out var axZ);
            GetAxes(byaw, out var bxX, out var bxZ);
            var dx = bx - ax;
            var dz = bz - az;
            return OverlapsAxis(dx, dz, axX, ahx, ahz, axX, axZ, bhx, bhz, bxX, bxZ) &&
                   OverlapsAxis(dx, dz, axZ, ahx, ahz, axX, axZ, bhx, bhz, bxX, bxZ) &&
                   OverlapsAxis(dx, dz, bxX, ahx, ahz, axX, axZ, bhx, bhz, bxX, bxZ) &&
                   OverlapsAxis(dx, dz, bxZ, ahx, ahz, axX, axZ, bhx, bhz, bxX, bxZ);
        }

        private static bool OverlapsAxis(
            float dx, float dz, Vector2 axis,
            float ahx, float ahz, Vector2 axX, Vector2 axZ,
            float bhx, float bhz, Vector2 bxX, Vector2 bxZ)
        {
            var distance = Mathf.Abs(dx * axis.x + dz * axis.y);
            var aRadius = ahx * Mathf.Abs(Vector2.Dot(axX, axis)) + ahz * Mathf.Abs(Vector2.Dot(axZ, axis));
            var bRadius = bhx * Mathf.Abs(Vector2.Dot(bxX, axis)) + bhz * Mathf.Abs(Vector2.Dot(bxZ, axis));
            return distance <= aRadius + bRadius;
        }

        private static bool CircleOverlapsBox(
            float circleX, float circleZ, float radius,
            float boxX, float boxZ, float halfX, float halfZ, float yaw)
        {
            GetAxes(yaw, out var axisX, out var axisZ);
            var dx = circleX - boxX;
            var dz = circleZ - boxZ;
            var localX = dx * axisX.x + dz * axisX.y;
            var localZ = dx * axisZ.x + dz * axisZ.y;
            var distanceX = localX - Mathf.Clamp(localX, -halfX, halfX);
            var distanceZ = localZ - Mathf.Clamp(localZ, -halfZ, halfZ);
            return distanceX * distanceX + distanceZ * distanceZ <= radius * radius;
        }

        private static void GetAxes(float yaw, out Vector2 axisX, out Vector2 axisZ)
        {
            var safeYaw = float.IsFinite(yaw) ? yaw : 0f;
            var cos = Mathf.Cos(safeYaw);
            var sin = Mathf.Sin(safeYaw);
            axisX = new Vector2(cos, -sin);
            axisZ = new Vector2(sin, cos);
        }

        private static void GetFootprint(
            WofEnginePlaceableDefinition definition,
            out bool circle,
            out float radius,
            out float halfX,
            out float halfZ)
        {
            circle = definition.Category is not (WofEnginePlaceableCategory.Huts or WofEnginePlaceableCategory.Village);
            if (circle)
            {
                radius = Mathf.Max(0.5f, definition.FootprintRadius);
                halfX = halfZ = 0f;
                return;
            }
            WofEnginePlaceableCatalog.GetBuildingMetrics(definition, out var width, out var depth, out _, out _, out _);
            halfX = Mathf.Max(width * 0.5f + 1.15f, definition.FootprintRadius * 0.52f);
            halfZ = Mathf.Max(depth * 0.5f + 1.15f, definition.FootprintRadius * 0.52f);
            radius = Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
        }

        private static WofEnginePlacementPlan Invalid(string reason, float y = 0f)
        {
            return new WofEnginePlacementPlan(false, reason, new Vector3(0f, float.IsFinite(y) ? y : 0f, 0f), 0f, 0f, false);
        }
    }
}
