using UnityEngine;

namespace WOF
{
    public static class WofFireballCastMath
    {
        public const float MinimumPitchDegrees = -82f;
        public const float MaximumPitchDegrees = 82f;
        public const float AuthoritativeEyeHeight = 1.65f;
        public const float SpawnForwardOffset = 0.9f;

        private const float MinimumDirectionSqrMagnitude = 0.01f;

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool TryNormalizeFiniteDirection(Vector3 value, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!IsFinite(value))
            {
                return false;
            }

            var sqrMagnitude = value.sqrMagnitude;
            if (!IsFinite(sqrMagnitude) || sqrMagnitude < MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            direction = value / Mathf.Sqrt(sqrMagnitude);
            return IsFinite(direction);
        }

        public static bool TryResolveAuthoritativeLaunch(
            Vector3 authoritativePlayerPosition,
            float authoritativeYawDegrees,
            float authoritativePitchDegrees,
            out Vector3 origin,
            out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.zero;
            if (!IsFinite(authoritativePlayerPosition) ||
                !IsFinite(authoritativeYawDegrees) ||
                !IsFinite(authoritativePitchDegrees))
            {
                return false;
            }

            var normalizedYaw = authoritativeYawDegrees % 360f;
            var clampedPitch = Mathf.Clamp(
                authoritativePitchDegrees,
                MinimumPitchDegrees,
                MaximumPitchDegrees);
            var authoritativeDirection =
                Quaternion.Euler(clampedPitch, normalizedYaw, 0f) * Vector3.forward;

            return TryResolveTrustedServerDirectedLaunch(
                authoritativePlayerPosition,
                authoritativeDirection,
                out origin,
                out direction);
        }

        // This path is only for aims authored by server-side systems such as the combat probe.
        public static bool TryResolveTrustedServerDirectedLaunch(
            Vector3 authoritativePlayerPosition,
            Vector3 serverDirection,
            out Vector3 origin,
            out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.zero;
            if (!IsFinite(authoritativePlayerPosition) ||
                !TryNormalizeFiniteDirection(serverDirection, out direction))
            {
                return false;
            }

            var eyePosition = authoritativePlayerPosition + (Vector3.up * AuthoritativeEyeHeight);
            origin = eyePosition + (direction * SpawnForwardOffset);
            if (!IsFinite(origin))
            {
                origin = Vector3.zero;
                direction = Vector3.zero;
                return false;
            }

            return true;
        }

        public static bool TryResolveOrientedLaunch(
            Vector3 authoritativePlayerPosition,
            Vector3 playerUp,
            float eyeHeight,
            Vector3 serverDirection,
            out Vector3 origin,
            out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.zero;
            if (!IsFinite(authoritativePlayerPosition) || !IsFinite(playerUp) || !IsFinite(eyeHeight) ||
                eyeHeight < 0f || !TryNormalizeFiniteDirection(playerUp, out var normalizedUp) ||
                !TryNormalizeFiniteDirection(serverDirection, out direction))
            {
                return false;
            }

            origin = authoritativePlayerPosition + normalizedUp * eyeHeight + direction * SpawnForwardOffset;
            if (IsFinite(origin)) return true;
            origin = Vector3.zero;
            direction = Vector3.zero;
            return false;
        }

        public static bool TryResolveTrustedServerTargetedLaunch(
            Vector3 authoritativePlayerPosition,
            Vector3 serverTargetPoint,
            out Vector3 origin,
            out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.zero;
            if (!IsFinite(authoritativePlayerPosition) || !IsFinite(serverTargetPoint))
            {
                return false;
            }

            var eyePosition = authoritativePlayerPosition + (Vector3.up * AuthoritativeEyeHeight);
            return TryResolveTrustedServerDirectedLaunch(
                authoritativePlayerPosition,
                serverTargetPoint - eyePosition,
                out origin,
                out direction);
        }
    }
}
