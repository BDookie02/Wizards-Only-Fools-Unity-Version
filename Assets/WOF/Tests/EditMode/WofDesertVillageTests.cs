using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofDesertVillageTests
    {
        [Test]
        public void BakedLayoutRetainsExactReactChunkAndPopulationCounts()
        {
            var document = LoadLayout();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.chunk.key, Is.EqualTo("4:-4"));
            Assert.That(document.chunk.cx, Is.EqualTo(4));
            Assert.That(document.chunk.cz, Is.EqualTo(-4));
            Assert.That(document.chunk.biome, Is.EqualTo("desert"));
            Assert.That(document.chunk.villageKind, Is.EqualTo("desert"));
            Assert.That(document.baseHeight, Is.EqualTo(17.885722662941443f).Within(0.000001f));
            Assert.That(WofDesertVillageLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.layout.buildings, Has.Length.EqualTo(55));
            Assert.That(document.layout.wallSegments, Has.Length.EqualTo(52));
            Assert.That(document.layout.marketStalls, Has.Length.EqualTo(10));
            Assert.That(document.layout.palms, Has.Length.EqualTo(22));
            Assert.That(document.layout.ladders, Has.Length.EqualTo(37));
            Assert.That(document.layout.fences, Has.Length.EqualTo(41));
            Assert.That(document.layout.clothesLines, Has.Length.EqualTo(15));
            Assert.That(document.layout.streetProps, Has.Length.EqualTo(94));
            Assert.That(document.villagers, Has.Length.EqualTo(55));
        }

        [Test]
        public void FirstAndLastBuildingsRetainDeterministicReactPlacement()
        {
            var buildings = LoadLayout().layout.buildings;
            Assert.That(buildings[0].key, Is.EqualTo("4:-4-desert-building-0"));
            Assert.That(buildings[0].localX, Is.EqualTo(52.043037048844255f).Within(0.0001f));
            Assert.That(buildings[0].localZ, Is.EqualTo(58.16886498723668f).Within(0.0001f));
            Assert.That(buildings[0].rotation, Is.EqualTo(-2.411719629835707f).Within(0.000001f));
            Assert.That(buildings[0].color, Is.EqualTo("#d1a062"));
            Assert.That(buildings[^1].key, Is.EqualTo("4:-4-desert-building-54"));
            Assert.That(buildings[^1].localX, Is.EqualTo(-47.55587233510867f).Within(0.0001f));
            Assert.That(buildings[^1].localZ, Is.EqualTo(201.46783258044326f).Within(0.0001f));
        }

        [Test]
        public void VillagerIdentityAndTownOwnershipMatchReactRuntime()
        {
            var villagers = LoadLayout().villagers;
            Assert.That(villagers[0].id, Is.EqualTo("4:-4-desert-building-0"));
            Assert.That(villagers[0].displayName, Is.EqualTo("Town Villager 1"));
            Assert.That(villagers[0].townId, Is.EqualTo("survival-desert-villagers-4:-4"));
            Assert.That(villagers[0].archiveFile, Is.EqualTo("desert-00.wofavatar"));
            Assert.That(villagers[0].archiveBytes, Is.EqualTo(172567));
            Assert.That(villagers[^1].displayName, Is.EqualTo("Town Villager 55"));
            Assert.That(villagers[^1].archiveFile, Is.EqualTo("desert-54.wofavatar"));
        }

        [Test]
        public void ExactPadAndRoadMeshesRemainFullySerialized()
        {
            var document = LoadLayout();
            Assert.That(document.padGeometry.vertexCount, Is.EqualTo(361));
            Assert.That(document.padGeometry.indices, Has.Length.EqualTo(1944));
            var surfaces = new[]
            {
                document.surfaceGeometries.northSouthRoad,
                document.surfaceGeometries.eastWestRoad,
                document.surfaceGeometries.diagonalRoadA,
                document.surfaceGeometries.diagonalRoadB,
                document.surfaceGeometries.northSouthLeft,
                document.surfaceGeometries.northSouthRight,
                document.surfaceGeometries.eastWestLeft,
                document.surfaceGeometries.eastWestRight,
                document.surfaceGeometries.diagonalALeft,
                document.surfaceGeometries.diagonalARight,
                document.surfaceGeometries.diagonalBLeft,
                document.surfaceGeometries.diagonalBRight
            };
            Assert.That(surfaces, Has.Length.EqualTo(12));
            foreach (var mesh in surfaces)
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(57));
                Assert.That(mesh.indices, Has.Length.EqualTo(216));
            }
        }

        [Test]
        public void CompactDesertVillagerArchivesContainAllRuntimeFrames()
        {
            foreach (var archiveName in new[] { "desert-00.wofavatar", "desert-54.wofavatar" })
            {
                var bytes = File.ReadAllBytes(ResolveProjectPath(
                    "Assets", "StreamingAssets", "WOF", "Villagers", "Base", archiveName));
                Assert.That(WofVillagerFrameArchive.TryParse(bytes, out var archive, out var error), Is.True, error);
                Assert.That(archive.EntryCount, Is.EqualTo(52));
                Assert.That(archive.Contains("idle/d0"), Is.True);
                Assert.That(archive.Contains("angry-blink/d7"), Is.True);
            }
        }

        [Test]
        public void RuntimeWorldOriginAndProbeRemainPinnedToReactChunk()
        {
            Assert.That(WofDesertVillageLayout.WorldOrigin, Is.EqualTo(new Vector3(2048f, 0f, -2048f)));
            Assert.That(WofDesertVillageLayout.ViewProbeSpawn,
                Is.EqualTo(new Vector3(2048f, WofDesertVillageLayout.ReactBaseHeight + 2.2f, -2262f)));
            Assert.That(WofDesertVillageLayout.FirstVillagerWorldPosition,
                Is.EqualTo(new Vector3(
                    2101.443264570222f,
                    18.835722662941443f + WofVillagerMath.AvatarGroundLift,
                    -1988.2660909595888f)));
            Assert.That(Vector3.Distance(
                    WofDesertVillageLayout.FirstVillagerControllerProbeSpawn,
                    WofDesertVillageLayout.FirstVillagerWorldPosition),
                Is.LessThan(WofQuestTargetMath.CloseRange));
        }

        private static WofDesertVillageDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "DesertVillage", "runtime-layout.json"));
            var document = JsonUtility.FromJson<WofDesertVillageDocument>(text);
            Assert.That(document, Is.Not.Null);
            return document;
        }

        private static string ResolveProjectPath(params string[] segments)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            Assert.That(Path.GetPathRoot(projectRoot), Is.EqualTo(@"D:\"));
            var path = projectRoot;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }
            return path;
        }
    }
}
