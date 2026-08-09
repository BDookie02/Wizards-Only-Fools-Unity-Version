using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofFireballCastMathTests
    {
        [Test]
        public void AuthoritativeLaunchRejectsEveryNonFiniteInput()
        {
            Assert.That(
                WofFireballCastMath.TryResolveAuthoritativeLaunch(
                    new Vector3(float.NaN, 0f, 0f),
                    0f,
                    0f,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                WofFireballCastMath.TryResolveAuthoritativeLaunch(
                    Vector3.zero,
                    float.PositiveInfinity,
                    0f,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                WofFireballCastMath.TryResolveAuthoritativeLaunch(
                    Vector3.zero,
                    0f,
                    float.NegativeInfinity,
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void AuthoritativeLaunchNormalizesYawAndClampsPitch()
        {
            var resolved = WofFireballCastMath.TryResolveAuthoritativeLaunch(
                new Vector3(2f, 3f, 4f),
                450f,
                200f,
                out var origin,
                out var direction);

            var expectedDirection =
                (Quaternion.Euler(WofFireballCastMath.MaximumPitchDegrees, 90f, 0f) * Vector3.forward).normalized;
            var expectedOrigin =
                new Vector3(2f, 3f, 4f) +
                (Vector3.up * WofFireballCastMath.AuthoritativeEyeHeight) +
                (expectedDirection * WofFireballCastMath.SpawnForwardOffset);

            Assert.That(resolved, Is.True);
            AssertVectorApproximately(direction, expectedDirection);
            AssertVectorApproximately(origin, expectedOrigin);
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void ServerDirectedLaunchNormalizesDirectionAndUsesAuthoritativePosition()
        {
            var playerPosition = new Vector3(-6f, 0.05f, -6f);
            var resolved = WofFireballCastMath.TryResolveTrustedServerDirectedLaunch(
                playerPosition,
                new Vector3(0f, 0f, 12f),
                out var origin,
                out var direction);

            Assert.That(resolved, Is.True);
            AssertVectorApproximately(direction, Vector3.forward);
            AssertVectorApproximately(
                origin,
                playerPosition +
                (Vector3.up * WofFireballCastMath.AuthoritativeEyeHeight) +
                (Vector3.forward * WofFireballCastMath.SpawnForwardOffset));
        }

        [Test]
        public void ServerDirectedLaunchRejectsZeroAndNonFiniteDirections()
        {
            Assert.That(
                WofFireballCastMath.TryResolveTrustedServerDirectedLaunch(
                    Vector3.zero,
                    Vector3.zero,
                    out _,
                    out _),
                Is.False);
            Assert.That(
                WofFireballCastMath.TryResolveTrustedServerDirectedLaunch(
                    Vector3.zero,
                    new Vector3(0f, float.NaN, 1f),
                    out _,
                    out _),
                Is.False);
            Assert.That(
                WofFireballCastMath.TryResolveTrustedServerDirectedLaunch(
                    Vector3.zero,
                    new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                    out _,
                    out _),
                Is.False);
        }

        [Test]
        public void ServerTargetedLaunchRejectsNonFiniteTarget()
        {
            Assert.That(
                WofFireballCastMath.TryResolveTrustedServerTargetedLaunch(
                    Vector3.zero,
                    new Vector3(0f, 1f, float.PositiveInfinity),
                    out _,
                    out _),
                Is.False);
        }

        private static void AssertVectorApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.00001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.00001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.00001f));
        }
    }
}
