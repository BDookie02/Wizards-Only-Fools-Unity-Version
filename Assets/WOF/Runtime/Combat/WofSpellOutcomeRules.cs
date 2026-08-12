using UnityEngine;

namespace WOF
{
    /// <summary>
    /// Pure multiplayer outcome math ported from the React spell runtimes.
    /// Keeping it independent from rendering and Netcode makes the exact
    /// gameplay contract testable without loading a scene.
    /// </summary>
    public static class WofSpellOutcomeRules
    {
        public static float ResolveIceSpellOpacity(bool isSource, float squaredDistance)
        {
            if (isSource) return WofSpellRuntimeTuning.IceSpellLocalOpacity;
            if (!float.IsFinite(squaredDistance) || squaredDistance < 0f) return 0f;
            var radius = WofSpellRuntimeTuning.IceSpellFlashbangRadius;
            return squaredDistance < radius * radius
                ? WofSpellRuntimeTuning.IceSpellRemoteOpacity
                : 0f;
        }

        public static float ResolveIceSpellOpacityAtTime(float initialOpacity, float elapsedSeconds)
        {
            if (!float.IsFinite(initialOpacity) || !float.IsFinite(elapsedSeconds)) return 0f;
            return Mathf.Max(
                0f,
                Mathf.Clamp01(initialOpacity) -
                Mathf.Max(0f, elapsedSeconds) * WofSpellRuntimeTuning.IceSpellFadeRatePerSecond);
        }

        public static Vector3 ResolveTornadoPullVelocity(Vector3 playerPosition, Vector3 center)
        {
            var toCenter = center - playerPosition;
            toCenter.y = 0f;
            var distance = toCenter.magnitude;
            if (!float.IsFinite(distance) || distance <= 0.1f || distance >= WofSpellRuntimeTuning.TornadoRadius)
            {
                return Vector3.zero;
            }

            var strength = 1f - distance / WofSpellRuntimeTuning.TornadoRadius;
            var inward = toCenter / distance;
            var spin = new Vector3(-toCenter.z, 0f, toCenter.x).normalized;
            var velocity = inward * (WofSpellRuntimeTuning.TornadoPullInwardSpeed * strength) +
                           spin * (WofSpellRuntimeTuning.TornadoPullSpinSpeed * strength);
            velocity.y = WofSpellRuntimeTuning.TornadoPullVerticalSpeed * strength;
            return velocity;
        }

        public static float ClampGrabDistance(float distance)
        {
            return Mathf.Clamp(
                float.IsFinite(distance) ? distance : WofSpellRuntimeTuning.GrabMinimumDistance,
                WofSpellRuntimeTuning.GrabMinimumDistance,
                WofSpellRuntimeTuning.GrabMaximumDistance);
        }

        public static Vector3 ResolveGrabHoldPoint(Vector3 origin, Vector3 direction, float distance)
        {
            var normalized = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
            return origin + normalized * ClampGrabDistance(distance);
        }

        public static Vector3 ResolveGrabFollowPosition(
            Vector3 currentPosition,
            Vector3 holdPoint,
            float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f) return currentPosition;
            var followAlpha = 1f - Mathf.Exp(-WofSpellRuntimeTuning.GrabFollowSpeed * deltaSeconds);
            return Vector3.Lerp(currentPosition, holdPoint, followAlpha);
        }

        public static Vector3 ResolveGrabThrowVelocity(Vector3 direction)
        {
            var normalized = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
            return new Vector3(
                normalized.x * WofSpellRuntimeTuning.GrabThrowSpeed,
                Mathf.Clamp(
                    normalized.y * WofSpellRuntimeTuning.GrabThrowSpeed,
                    WofSpellRuntimeTuning.GrabMinimumThrowVerticalSpeed,
                    WofSpellRuntimeTuning.GrabMaximumThrowVerticalSpeed),
                normalized.z * WofSpellRuntimeTuning.GrabThrowSpeed);
        }

        public static bool DiscShieldBlocks(
            Vector3 playerPosition,
            Vector3 playerForward,
            Vector3 incomingOrigin)
        {
            var incomingSide = incomingOrigin - playerPosition;
            incomingSide.y = 0f;
            var forward = playerForward;
            forward.y = 0f;
            if (incomingSide.sqrMagnitude <= 0.000001f || forward.sqrMagnitude <= 0.000001f) return false;
            return Vector3.Dot(forward.normalized, incomingSide.normalized) > 0f;
        }
    }
}
