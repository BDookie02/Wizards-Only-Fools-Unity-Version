using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofNavigationRecorderRuntimeTests
    {
        [Test]
        public void Constants_MatchReactNavigationRecorder()
        {
            Assert.That(WofNavigationRecorderRuntime.RecordingVersion, Is.EqualTo(1));
            Assert.That(WofNavigationRecorderRuntime.SampleIntervalMilliseconds, Is.EqualTo(125));
            Assert.That(WofNavigationRecorderRuntime.MaximumSamplesPerSession, Is.EqualTo(9000));
            Assert.That(WofNavigationRecorderRuntime.MaximumStoredSessions, Is.EqualTo(8));
            Assert.That(WofNavigationRecorderRuntime.SurvivalBlockSize, Is.EqualTo(512f));
        }

        [TestCase(null, "survival navigation")]
        [TestCase("  crater   route!!  ", "crater route")]
        [TestCase("<>!", "survival navigation")]
        public void SanitizeLabel_PreservesReactRules(string value, string expected)
        {
            Assert.That(WofNavigationRecorderRuntime.SanitizeLabel(value), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeLabel_ClampsToReactFortyEightCharacterLimit()
        {
            var label = WofNavigationRecorderRuntime.SanitizeLabel(new string('a', 80));

            Assert.That(label, Has.Length.EqualTo(48));
        }

        [TestCase(1.320000052f, 3, 1.32d)]
        [TestCase(-1.25f, 1, -1.2d)]
        [TestCase(float.PositiveInfinity, 3, 0d)]
        public void RoundForRecording_UsesReactDecimalShape(float value, int places, double expected)
        {
            Assert.That(WofNavigationRecorderRuntime.RoundForRecording(value, places), Is.EqualTo(expected));
        }
    }
}
