using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests.EditMode
{
    public sealed class WofMapFastTravelTests
    {
        [Test]
        public void PlayerFacingDestinationListIncludesMainlandAndLilyCoil()
        {
            var destinations = WofMapFastTravel.Destinations;
            Assert.That(destinations.Length, Is.EqualTo(7));
            Assert.That(destinations[0].Position, Is.EqualTo(new Vector3(0f, 15f, 30f)));
            Assert.That(destinations[1].Position, Is.EqualTo(new Vector3(-1536f, 140f, -1322f)));
            Assert.That(destinations[2].Position, Is.EqualTo(new Vector3(0f, 140f, -1322f)));
            Assert.That(destinations[3].Position, Is.EqualTo(new Vector3(2048f, 140f, -1834f)));
            Assert.That(destinations[4].Position, Is.EqualTo(new Vector3(1536f, 270f, 62f)));
            Assert.That(destinations[5].Position, Is.EqualTo(new Vector3(2560f, 92f, 1156f)));
            Assert.That(destinations[6].Destination, Is.EqualTo(WofMapDestination.LilyCoil));
            Assert.That(destinations[6].Label, Is.EqualTo("LILY COIL DIMENSION"));
            Assert.That(destinations[6].Position, Is.EqualTo(WofLilyCoilLayout.PlayableSpawnPosition));
            Assert.That(destinations[6].ShowOnWorldMap, Is.False,
                "The remote Lily Coil realm must not be drawn as a misleading clamped mainland marker.");

            var menuDestinations = WofMapFastTravel.MenuDestinations;
            Assert.That(menuDestinations.Length, Is.EqualTo(destinations.Length));
            Assert.That(menuDestinations[0].Destination, Is.EqualTo(WofMapDestination.LilyCoil),
                "The remote dimension must be the first visible and controller-selected travel option.");
            Assert.That(menuDestinations[1].Destination, Is.EqualTo(WofMapDestination.Base));
        }

        [Test]
        public void InvalidDestinationValuesAreRejected()
        {
            Assert.That(WofMapFastTravel.IsValid(-1), Is.False);
            Assert.That(WofMapFastTravel.IsValid(6), Is.True);
            Assert.That(WofMapFastTravel.IsValid(7), Is.False);
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

        [Test]
        public void FullWorldMapUsesAllElevenByEightSurvivalRegions()
        {
            Assert.That(WofWorldMapExplorationGraphic.ColumnCount, Is.EqualTo(11));
            Assert.That(WofWorldMapExplorationGraphic.RowCount, Is.EqualTo(8));
            Assert.That(WofWorldMapExplorationGraphic.TryGetCell(-2304f, -2304f, out var firstColumn, out var firstRow), Is.True);
            Assert.That((firstColumn, firstRow), Is.EqualTo((0, 0)));
            Assert.That(WofWorldMapExplorationGraphic.TryGetCell(3328f, 1792f, out var lastColumn, out var lastRow), Is.True);
            Assert.That((lastColumn, lastRow), Is.EqualTo((10, 7)));
            Assert.That(WofWorldMapExplorationGraphic.TryGetCell(3328.01f, 0f, out _, out _), Is.False);
        }

        [Test]
        public void FullWorldMapMarkerAndRegionRowsMatchTheReactTopDownAtlas()
        {
            var northWest = WofWorldMapExplorationGraphic.GetMarkerNormalized(-2304f, -2304f);
            var southEast = WofWorldMapExplorationGraphic.GetMarkerNormalized(3328f, 1792f);
            Assert.That(northWest, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(southEast, Is.EqualTo(new Vector2(1f, 0f)));

            var firstCell = WofWorldMapExplorationGraphic.GetCellNormalizedRect(0, 0);
            var lastCell = WofWorldMapExplorationGraphic.GetCellNormalizedRect(10, 7);
            Assert.That(firstCell.yMax, Is.EqualTo(1f));
            Assert.That(lastCell.yMin, Is.EqualTo(0f));
        }

        [Test]
        public void FullWorldMapCoordinatesRoundTripForWaypointPlacement()
        {
            var points = new[]
            {
                new Vector2(WofMapFastTravel.MapMinX, WofMapFastTravel.MapMinZ),
                new Vector2(0f, 30f),
                new Vector2(1536f, 62f),
                new Vector2(WofMapFastTravel.MapMaxX, WofMapFastTravel.MapMaxZ)
            };
            foreach (var point in points)
            {
                var normalized = WofWorldMapExplorationGraphic.GetMarkerNormalized(point.x, point.y);
                var roundTrip = WofWorldMapExplorationGraphic.GetWorldPosition(normalized);
                Assert.That(roundTrip.x, Is.EqualTo(point.x).Within(0.001f));
                Assert.That(roundTrip.y, Is.EqualTo(point.y).Within(0.001f));
            }
        }

        [Test]
        public void ControllerMapZoomClampsAndViewportMappingAccountsForPan()
        {
            Assert.That(WofNavigationMapRuntime.ClampExpandedZoom(0f), Is.EqualTo(1f));
            Assert.That(WofNavigationMapRuntime.ClampExpandedZoom(4f), Is.EqualTo(3f));
            var viewportSize = new Vector2(1000f, 700f);
            var focus = new Vector2(0.75f, 0.25f);
            var pan = WofNavigationMapRuntime.GetExpandedMapPan(focus, 2f, viewportSize);
            var mappedCenter = WofNavigationMapRuntime.GetMapNormalizedFromViewport(
                new Vector2(0.5f, 0.5f),
                2f,
                pan,
                viewportSize);
            Assert.That(mappedCenter.x, Is.EqualTo(focus.x).Within(0.001f));
            Assert.That(mappedCenter.y, Is.EqualTo(focus.y).Within(0.001f));
            Assert.That(WofNavigationMapRuntime.GetExpandedMapPan(focus, 1f, viewportSize), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void WaypointCompassDirectionUsesWorldNorthAndEast()
        {
            Assert.That(
                WofNavigationMapRuntime.GetWaypointCompassDirection(Vector2.zero, new Vector2(40f, 0f)),
                Is.EqualTo(Vector2.right));
            Assert.That(
                WofNavigationMapRuntime.GetWaypointCompassDirection(Vector2.zero, new Vector2(0f, 40f)),
                Is.EqualTo(Vector2.up));
            Assert.That(
                WofNavigationMapRuntime.GetWaypointCompassDirection(Vector2.one, Vector2.one),
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ExplorationRevealRoundTripsOnlyValidVisitedRegions()
        {
            var gameObject = new GameObject("WorldMapExplorationTest");
            try
            {
                var exploration = gameObject.AddComponent<WofWorldMapExplorationGraphic>();
                exploration.ImportExploredCells("0,0;4,4;10,7;99,99;broken");
                Assert.That(exploration.ExploredCount, Is.EqualTo(3));
                Assert.That(exploration.ExportExploredCells(), Is.EqualTo("0,0;4,4;10,7"));
                Assert.That(exploration.SetWorldPosition(0f, 30f), Is.False);
                Assert.That(exploration.SetWorldPosition(1536f, 62f), Is.True);
                Assert.That(exploration.ExploredCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
