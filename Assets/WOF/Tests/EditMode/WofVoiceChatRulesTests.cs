using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofVoiceChatRulesTests
    {
        [TestCase(" ABC-123 ", "wof-voice-abc-123")]
        [TestCase("ROOM !@# ONE", "wof-voice-roomone")]
        [TestCase(null, "")]
        public void CreateChannelNameProducesStableVivoxSafeIdentity(string source, string expected)
        {
            Assert.That(WofVoiceChatRules.CreateChannelName(source), Is.EqualTo(expected));
        }

        [Test]
        public void ParticipantDisplayNameRoundTripsNetworkOwner()
        {
            var displayName = WofVoiceChatRules.CreateParticipantDisplayName(914UL, " Bad Wizard!? ");
            Assert.That(displayName, Is.EqualTo("wof_914_BadWizard"));
            Assert.That(WofVoiceChatRules.TryParseOwnerClientId(displayName, out var ownerClientId), Is.True);
            Assert.That(ownerClientId, Is.EqualTo(914UL));
        }

        [TestCase(0f, 28f, 0.85f, 0.85f)]
        [TestCase(14f, 28f, 0.85f, 0.2125f)]
        [TestCase(28f, 28f, 0.85f, 0f)]
        [TestCase(40f, 28f, 0.85f, 0f)]
        public void ProximityVolumeMatchesReactQuadraticFalloff(float distance, float range, float output, float expected)
        {
            Assert.That(WofVoiceChatRules.CalculateProximityVolume(distance, range, output),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void PushToTalkHonorsKeyboardControllerAndGameplaySuppression()
        {
            Assert.That(WofVoiceChatRules.ShouldTransmit(true, "openMic", false, false, true), Is.True);
            Assert.That(WofVoiceChatRules.ShouldTransmit(true, "pushToTalk", true, false, false), Is.True);
            Assert.That(WofVoiceChatRules.ShouldTransmit(true, "pushToTalk", false, true, false), Is.True);
            Assert.That(WofVoiceChatRules.ShouldTransmit(true, "pushToTalk", true, true, true), Is.False);
            Assert.That(WofVoiceChatRules.ShouldTransmit(false, "openMic", true, true, false), Is.False);
        }
    }
}
