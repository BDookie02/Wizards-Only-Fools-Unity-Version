using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofTransportConfigurationTests
    {
        private GameObject _networkObject;
        private NetworkManager _networkManager;
        private UnityTransport _unityTransport;
        private SinglePlayerTransport _singlePlayerTransport;

        [SetUp]
        public void SetUp()
        {
            _networkObject = new GameObject("TransportConfigurationTests");
            _networkManager = _networkObject.AddComponent<NetworkManager>();
            _networkManager.NetworkConfig ??= new NetworkConfig();
            _unityTransport = _networkObject.AddComponent<UnityTransport>();
            _singlePlayerTransport = _networkObject.AddComponent<SinglePlayerTransport>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            if (_networkObject != null)
            {
                Object.DestroyImmediate(_networkObject);
            }
        }

        [Test]
        public void SoloSelectsSinglePlayerTransportWithoutStartingUnityTransport()
        {
            WofTransportConfiguration.SelectSolo(_networkManager, _singlePlayerTransport);

            Assert.That(
                _networkManager.NetworkConfig.NetworkTransport,
                Is.SameAs(_singlePlayerTransport));
            Assert.That(_unityTransport.GetNetworkDriver().IsCreated, Is.False);
        }

        [Test]
        public void HostUsesUnencryptedWebSocketsAndListensOnEveryInterface()
        {
            WofTransportConfiguration.ConfigureWebSocketMultiplayer(
                _networkManager,
                _unityTransport,
                "127.0.0.1",
                7777,
                isHost: true);

            Assert.That(_networkManager.NetworkConfig.NetworkTransport, Is.SameAs(_unityTransport));
            Assert.That(_unityTransport.UseWebSockets, Is.True);
            Assert.That(_unityTransport.UseEncryption, Is.False);
            Assert.That(_unityTransport.ConnectionData.Address, Is.EqualTo("127.0.0.1"));
            Assert.That(_unityTransport.ConnectionData.Port, Is.EqualTo(7777));
            Assert.That(_unityTransport.ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
        }

        [Test]
        public void ClientUsesUnencryptedWebSocketsAndPreservesRequestedHost()
        {
            _unityTransport.UseEncryption = true;

            WofTransportConfiguration.ConfigureWebSocketMultiplayer(
                _networkManager,
                _unityTransport,
                "game.example.test",
                7777,
                isHost: false);

            Assert.That(_networkManager.NetworkConfig.NetworkTransport, Is.SameAs(_unityTransport));
            Assert.That(_unityTransport.UseWebSockets, Is.True);
            Assert.That(_unityTransport.UseEncryption, Is.False);
            Assert.That(_unityTransport.ConnectionData.Address, Is.EqualTo("game.example.test"));
            Assert.That(_unityTransport.ConnectionData.Port, Is.EqualTo(7777));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RelayTransportMatchesTheProtocolSelectedForTheBuild(bool useWebSockets)
        {
            _unityTransport.UseWebSockets = !useWebSockets;

            WofTransportConfiguration.ConfigureRelayMultiplayer(
                _networkManager,
                _unityTransport,
                useWebSockets);

            Assert.That(_networkManager.NetworkConfig.NetworkTransport, Is.SameAs(_unityTransport));
            Assert.That(_unityTransport.UseWebSockets, Is.EqualTo(useWebSockets));
        }
    }
}
