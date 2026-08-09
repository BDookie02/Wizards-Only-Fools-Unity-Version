using NUnit.Framework;

namespace WOF.Tests
{
    public sealed class WofBootstrapPresentationTests
    {
        [Test]
        public void SoloSessionLeavesReactNotificationCornerClear()
        {
            Assert.That(
                WofBootstrap.ResolveRoomLabel(WofSessionMode.Solo, "127.0.0.1", "wof-test", 7777),
                Is.Empty);
        }

        [Test]
        public void HostSessionKeepsInviteCodeAndPort()
        {
            Assert.That(
                WofBootstrap.ResolveRoomLabel(WofSessionMode.Host, "127.0.0.1", "wof-test", 7777),
                Is.EqualTo("wof-test  |  7777"));
        }

        [Test]
        public void ClientSessionKeepsJoinedAddressAndPort()
        {
            Assert.That(
                WofBootstrap.ResolveRoomLabel(WofSessionMode.Client, "192.168.4.10", "wof-test", 7777),
                Is.EqualTo("JOINED  192.168.4.10:7777"));
        }
    }
}
