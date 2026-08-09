using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests.EditMode
{
    public sealed class WofMapFastTravelTests
    {
        [Test]
        public void PlayerFacingDestinationListMatchesReactMapOverlay()
        {
            var destinations = WofMapFastTravel.Destinations;
            Assert.That(destinations.Length, Is.EqualTo(6));
            Assert.That(destinations[0].Position, Is.EqualTo(new Vector3(0f, 15f, 30f)));
            Assert.That(destinations[1].Position, Is.EqualTo(new Vector3(-1536f, 140f, -1322f)));
            Assert.That(destinations[2].Position, Is.EqualTo(new Vector3(0f, 140f, -1322f)));
            Assert.That(destinations[3].Position, Is.EqualTo(new Vector3(2048f, 140f, -1834f)));
            Assert.That(destinations[4].Position, Is.EqualTo(new Vector3(1536f, 270f, 62f)));
            Assert.That(destinations[5].Position, Is.EqualTo(new Vector3(2560f, 92f, 1156f)));
        }

        [Test]
        public void InvalidDestinationValuesAreRejected()
        {
            Assert.That(WofMapFastTravel.IsValid(-1), Is.False);
            Assert.That(WofMapFastTravel.IsValid(6), Is.False);
            Assert.That(WofMapFastTravel.TryGet((WofMapDestination)99, out _), Is.False);
        }

        [Test]
        public void ReactMapBoundsRemainExact()
        {
            Assert.That(WofMapFastTravel.MapMinX, Is.EqualTo(-2304f));
            Assert.That(WofMapFastTravel.MapMaxX, Is.EqualTo(3328f));
            Assert.That(WofMapFastTravel.MapMinZ, Is.EqualTo(-2304f));
            Assert.That(WofMapFastTravel.MapMaxZ, Is.EqualTo(1792f));
        }
    }
}
