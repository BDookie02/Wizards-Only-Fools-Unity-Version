using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofPublicSessionRulesTests
    {
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("  a7b9c  ", "A7B9C")]
        public void NormalizeJoinCode_TrimsAndNormalizesCase(string input, string expected)
        {
            Assert.That(WofPublicSessionRules.NormalizeJoinCode(input), Is.EqualTo(expected));
        }

        [TestCase("--wof-auth-profile=host_01", "host_01")]
        [TestCase("--WOF-AUTH-PROFILE=client-02", "client-02")]
        [TestCase("--wof-auth-profile=invalid profile", "")]
        [TestCase("--wof-auth-profile=1234567890123456789012345678901", "")]
        public void AuthenticationProfileAcceptsOnlyUnitySafeExplicitOverrides(
            string argument,
            string expected)
        {
            Assert.That(
                WofPublicSessionRules.ResolveAuthenticationProfile(new[] { argument }),
                Is.EqualTo(expected));
        }

        [Test]
        public void Availability_FailsClosedWhenCloudProjectIsNotLinked()
        {
            Assert.That(
                WofPublicSessionRules.GetAvailabilityError(string.Empty, true, true, false),
                Is.EqualTo(WofPublicSessionRules.CloudProjectRequired));
        }

        [Test]
        public void Availability_DoesNotHideMissingNetworkConfiguration()
        {
            Assert.That(
                WofPublicSessionRules.GetAvailabilityError("cloud-project", true, false, false),
                Is.EqualTo(WofPublicSessionRules.NetworkConfigurationRequired));
        }

        [Test]
        public void Availability_RejectsStartingOverAnExistingSession()
        {
            Assert.That(
                WofPublicSessionRules.GetAvailabilityError("cloud-project", true, true, true),
                Is.EqualTo(WofPublicSessionRules.SessionAlreadyRunning));
        }

        [Test]
        public void Availability_AllowsConfiguredIdlePublicSession()
        {
            Assert.That(
                WofPublicSessionRules.GetAvailabilityError("cloud-project", true, true, false),
                Is.Empty);
        }

        [TestCase("SessionNotFound", "PUBLIC LOBBY NOT FOUND. CHECK THE INVITE CODE.")]
        [TestCase("InvalidSessionIdentifier", "THE PUBLIC INVITE CODE IS INVALID.")]
        [TestCase("NetworkSetupFailed", "PUBLIC RELAY CONNECTION FAILED.")]
        [TestCase("Unknown", "PUBLIC ONLINE CONNECTION FAILED.")]
        public void SessionErrors_AreActionableAndDoNotSuggestLanFallback(string error, string expected)
        {
            Assert.That(WofPublicSessionRules.FormatSessionError(error), Is.EqualTo(expected));
            Assert.That(WofPublicSessionRules.FormatSessionError(error), Does.Not.Contain("LAN"));
        }
    }
}
