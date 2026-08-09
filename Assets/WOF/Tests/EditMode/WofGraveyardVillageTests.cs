using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofGraveyardVillageTests
    {
        [Test]
        public void BakedLayoutRetainsExactReactChunkAndPopulationCounts()
        {
            var document = LoadLayout();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.sourceSignature,
                Is.EqualTo("c912e41f24db629cb75d7f725a1ce3e4c5499dc364c7165e84120ccfc6f8f016"));
            Assert.That(document.chunk.key, Is.EqualTo("5:2"));
            Assert.That(document.chunk.cx, Is.EqualTo(WofGraveyardVillageLayout.ChunkX));
            Assert.That(document.chunk.cz, Is.EqualTo(WofGraveyardVillageLayout.ChunkZ));
            Assert.That(document.chunk.biome, Is.EqualTo("mushroom"));
            Assert.That(document.chunk.villageKind, Is.EqualTo("graveyard"));
            Assert.That(document.chunk.hasRiver, Is.False);
            Assert.That(document.chunk.riverVertical, Is.True);
            Assert.That(document.chunk.lod, Is.EqualTo("near"));
            Assert.That(document.baseHeight,
                Is.EqualTo(WofGraveyardVillageLayout.ReactBaseHeight).Within(0.000001f));
            Assert.That(WofGraveyardVillageLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.layout.tombs, Has.Length.EqualTo(WofGraveyardVillageLayout.TombCount));
            Assert.That(document.layout.fenceSegments,
                Has.Length.EqualTo(WofGraveyardVillageLayout.FenceSegmentCount));
            Assert.That(document.layout.pathStones,
                Has.Length.EqualTo(WofGraveyardVillageLayout.PathStoneCount));
            Assert.That(document.chapel.characters,
                Has.Length.EqualTo(WofGraveyardVillageLayout.ChapelCharacterCount));
            Assert.That(document.chapel.centerNpcPlacements,
                Has.Length.EqualTo(WofGraveyardVillageLayout.CenterNpcCount));
            Assert.That(document.chapel.sideWingNpcPlacements,
                Has.Length.EqualTo(WofGraveyardVillageLayout.SideWingNpcCount));
        }

        [Test]
        public void FirstTombRetainsExactReactIdentityStyleAndPlacement()
        {
            var tomb = LoadLayout().layout.tombs[0];

            Assert.That(tomb.key, Is.EqualTo("5:2-grave-0"));
            Assert.That(tomb.localX, Is.EqualTo(-51.6440927030053f).Within(0.0001f));
            Assert.That(tomb.localY, Is.EqualTo(56.588398940225105f).Within(0.0001f));
            Assert.That(tomb.localZ, Is.EqualTo(-191.3699760949239f).Within(0.0001f));
            Assert.That(tomb.rotation, Is.EqualTo(-0.05180384324979968f).Within(0.000001f));
            Assert.That(tomb.name, Is.EqualTo("XENA MARK"));
            Assert.That(tomb.joke, Is.EqualTo("Found the floor trap."));
            Assert.That(tomb.styleIndex, Is.EqualTo(0));
            Assert.That(tomb.colors.stoneColor, Is.EqualTo("#7d7972"));
            Assert.That(tomb.textures.bodyTexture, Is.EqualTo("GraveyardVillage/Tombs/00-body.png"));
            Assert.That(tomb.textures.inscriptionTexture,
                Is.EqualTo("GraveyardVillage/Tombs/00-inscription.png"));
        }

        [Test]
        public void BakedMeshesRetainExactReactTopology()
        {
            var geometries = LoadLayout().geometries;
            AssertMesh(geometries.terrain, 2809, 16224, expectNormals: true, expectUvs: true);
            AssertMesh(geometries.terrainSkirt, 424, 1248, expectNormals: false, expectUvs: false);
            AssertMesh(geometries.rampCollider, 20, 30, expectNormals: true, expectUvs: false);
        }

        [Test]
        public void ChapelAuthoredPopulationAndColliderContractsRemainExact()
        {
            var chapel = LoadLayout().chapel;
            Assert.That(chapel.viewSummary.towerCount, Is.EqualTo(4));
            Assert.That(chapel.viewSummary.gargoyleCount, Is.EqualTo(14));
            Assert.That(chapel.viewSummary.doorCount, Is.EqualTo(5));
            Assert.That(chapel.viewSummary.windowCount, Is.EqualTo(12));
            Assert.That(chapel.viewSummary.buttressCount, Is.EqualTo(12));
            Assert.That(chapel.viewSummary.centerPewCount, Is.EqualTo(10));
            Assert.That(chapel.viewSummary.sideWingPewCount, Is.EqualTo(12));
            Assert.That(chapel.viewSummary.centerNpcCount, Is.EqualTo(20));
            Assert.That(chapel.viewSummary.sideWingNpcCount, Is.EqualTo(24));
            Assert.That(chapel.viewSummary.interiorCandleCount, Is.EqualTo(14));
            Assert.That(chapel.viewSummary.chandelierCandleCount, Is.EqualTo(8));
            Assert.That(chapel.colliderSummary.wallColliderCount, Is.EqualTo(22));
            Assert.That(chapel.colliderSummary.fenceColliderCount, Is.EqualTo(36));
            Assert.That(chapel.colliderSummary.cuboidColliderCount,
                Is.EqualTo(WofGraveyardVillageLayout.CuboidColliderCount));
            Assert.That(chapel.wallSegments, Has.Length.EqualTo(22));
            Assert.That(chapel.watchTowerPositions, Has.Length.EqualTo(4));
            Assert.That(chapel.gargoyles, Has.Length.EqualTo(14));
            Assert.That(chapel.gargoyles[0].scale, Is.EqualTo(1f));
            Assert.That(chapel.gargoyles[^1].scale, Is.EqualTo(0.78f));
            Assert.That(chapel.centerPewRows, Has.Length.EqualTo(5));
            Assert.That(chapel.centerPewColliders, Has.Length.EqualTo(10));
            Assert.That(chapel.sideWingPews, Has.Length.EqualTo(12));
            Assert.That(chapel.interiorCandles, Has.Length.EqualTo(14));
            Assert.That(chapel.chandelierCandles, Has.Length.EqualTo(8));
        }

        [Test]
        public void CompactChapelCharacterArchivesContainAllRuntimeFrames()
        {
            foreach (var archiveName in new[]
                     {
                         "graveyard-chapel-npc-00.wofavatar",
                         "graveyard-chapel-pope.wofavatar"
                     })
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
        public void RuntimeWorldOriginIsPinnedToReactChunk()
        {
            Assert.That(WofGraveyardVillageLayout.WorldOrigin, Is.EqualTo(new Vector3(2560f, 0f, 1024f)));
            Assert.That(WofGraveyardVillageLayout.TombsViewProbeSpawn,
                Is.EqualTo(new Vector3(2508.4f, WofGraveyardVillageLayout.ReactBaseHeight + 2.2f, 798f)));
            Assert.That(WofGraveyardVillageLayout.FenceViewProbeSpawn,
                Is.EqualTo(new Vector3(2560f, WofGraveyardVillageLayout.ReactBaseHeight + 2.2f, 1299f)));
        }

        [Test]
        public void GeneratedGraveyardSceneHasExactRuntimePopulationAndFiniteGeometry()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofGraveyardVillage.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var document = LoadLayout();
                var tombKeys = document.layout.tombs.Select(item => item.key).ToHashSet();
                var fenceKeys = document.layout.fenceSegments.Select(item => item.key).ToHashSet();
                var pathKeys = document.layout.pathStones.Select(item => item.key).ToHashSet();
                Assert.That(transforms.Any(item => item.name == "ReactSurvivalGraveyardVillage_5_2"), Is.True);
                Assert.That(transforms.Count(item => tombKeys.Contains(item.name)),
                    Is.EqualTo(WofGraveyardVillageLayout.TombCount));
                Assert.That(transforms.Count(item => fenceKeys.Contains(item.name)),
                    Is.EqualTo(WofGraveyardVillageLayout.FenceSegmentCount));
                Assert.That(transforms.Count(item => pathKeys.Contains(item.name)),
                    Is.EqualTo(WofGraveyardVillageLayout.PathStoneCount));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofStaticAvatarBillboard>(true)).Count(),
                    Is.EqualTo(45));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<BoxCollider>(true)).Count(),
                    Is.EqualTo(WofGraveyardVillageLayout.CuboidColliderCount));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<MeshCollider>(true)).Count(),
                    Is.EqualTo(2));
                Assert.That(transforms.Count(item => item.name.StartsWith("chapel-corner-watch-tower-")),
                    Is.EqualTo(4));
                Assert.That(transforms.Count(item => item.name.StartsWith("chapel-roof-gargoyle-")),
                    Is.EqualTo(14));
                Assert.That(transforms.Count(item => item.name.StartsWith("DoorPanel_")), Is.EqualTo(10));
                Assert.That(transforms.Count(item => item.name.StartsWith("CenterPew_")), Is.EqualTo(10));
                Assert.That(transforms.Count(item => item.name.StartsWith("SidePew_")), Is.EqualTo(12));
                Assert.That(transforms.Count(item => item.name.StartsWith("InteriorCandle_")), Is.EqualTo(14));
                Assert.That(transforms.Count(item => item.name.StartsWith("ChapelChandelier_")), Is.EqualTo(3));

                foreach (var item in transforms)
                {
                    AssertFinite(item.localPosition, item.name + " position");
                    AssertFinite(item.localScale, item.name + " scale");
                    Assert.That(float.IsFinite(item.localRotation.x) && float.IsFinite(item.localRotation.y) &&
                                float.IsFinite(item.localRotation.z) && float.IsFinite(item.localRotation.w),
                        Is.True, item.name + " rotation");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertMesh(
            WofSerializedMeshRecord mesh,
            int vertices,
            int indices,
            bool expectNormals,
            bool expectUvs)
        {
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(vertices));
            Assert.That(mesh.positions, Has.Length.EqualTo(vertices * 3));
            Assert.That(mesh.indices, Has.Length.EqualTo(indices));
            Assert.That(mesh.normals, Has.Length.EqualTo(expectNormals ? vertices * 3 : 0));
            Assert.That(mesh.uvs, Has.Length.EqualTo(expectUvs ? vertices * 2 : 0));
            Assert.That(mesh.colors == null || mesh.colors.Length == 0 || mesh.colors.Length == vertices * 3,
                Is.True);
        }

        private static WofGraveyardVillageDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "GraveyardVillage", "runtime-layout.json"));
            var document = JsonUtility.FromJson<WofGraveyardVillageDocument>(text);
            Assert.That(document, Is.Not.Null);
            return document;
        }

        private static void AssertFinite(Vector3 value, string context)
        {
            Assert.That(float.IsFinite(value.x), Is.True, context + " x");
            Assert.That(float.IsFinite(value.y), Is.True, context + " y");
            Assert.That(float.IsFinite(value.z), Is.True, context + " z");
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
