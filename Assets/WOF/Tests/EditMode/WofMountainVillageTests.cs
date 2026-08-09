using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofMountainVillageTests
    {
        [Test]
        public void BakedLayoutRetainsExactReactChunkAndFeatureCounts()
        {
            var document = LoadLayout();
            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.chunk.key, Is.EqualTo("3:0"));
            Assert.That(document.chunk.cx, Is.EqualTo(3));
            Assert.That(document.chunk.cz, Is.EqualTo(0));
            Assert.That(document.chunk.biome, Is.EqualTo("mushroom"));
            Assert.That(document.chunk.villageKind, Is.EqualTo("mountain"));
            Assert.That(document.chunk.hasRiver, Is.False);
            Assert.That(document.chunk.lod, Is.EqualTo("near"));
            Assert.That(document.baseHeight, Is.EqualTo(WofMountainVillageLayout.ReactBaseHeight).Within(0.000001f));
            Assert.That(document.summitY, Is.EqualTo(WofMountainVillageLayout.ReactSummitY).Within(0.000001f));
            Assert.That(WofMountainVillageLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.layout.trailPoints, Has.Length.EqualTo(25));
            Assert.That(document.layout.trailSegments, Has.Length.EqualTo(24));
            Assert.That(document.layout.cliffPatches, Has.Length.EqualTo(48));
            Assert.That(document.layout.cabins, Has.Length.EqualTo(8));
            Assert.That(document.layout.interiorHuts, Has.Length.EqualTo(3));
            Assert.That(document.layout.interiorLadders, Has.Length.EqualTo(4));
            Assert.That(document.villagers, Has.Length.EqualTo(11));
        }

        [Test]
        public void BakedMeshesRetainExactReactTopology()
        {
            var geometries = LoadLayout().geometries;
            AssertMesh(geometries.terrain, 11025, 63912);
            AssertMesh(geometries.terrainCollider, 11025, 63912);
            AssertMesh(geometries.slopeGrass, 53790, 96822);
            AssertMesh(geometries.trailDeck, 100, 588);
            AssertMesh(geometries.trailTop, 50, 144);
            AssertMesh(geometries.trailCollider, 100, 588);
            AssertMesh(geometries.summitCollider, 64, 192);
        }

        [Test]
        public void OpeningWallBanquetAndLadderContractsRemainExact()
        {
            var document = LoadLayout();
            Assert.That(document.opening.summitSnowDrifts, Has.Length.EqualTo(28));
            Assert.That(document.opening.rimBeams, Has.Length.EqualTo(12));
            Assert.That(document.opening.supportFrames, Has.Length.EqualTo(4));
            Assert.That(document.opening.bottomRocks, Has.Length.EqualTo(14));
            Assert.That(document.wallDecor.lanterns, Has.Length.EqualTo(9));
            Assert.That(document.wallDecor.paintings, Has.Length.EqualTo(6));
            Assert.That(document.wallDecor.ropeLights, Has.Length.EqualTo(20));
            Assert.That(document.banquet.bottomLights, Has.Length.EqualTo(8));
            Assert.That(document.banquet.chairs, Has.Length.EqualTo(7));
            Assert.That(document.banquet.table.planks, Has.Length.EqualTo(9));
            Assert.That(document.banquet.table.legs, Has.Length.EqualTo(6));
            Assert.That(document.banquet.table.breads, Has.Length.EqualTo(4));
            Assert.That(document.banquet.table.fruitBowls, Has.Length.EqualTo(4));
            Assert.That(document.banquet.table.fruitBowls.Sum(bowl => bowl.fruits.Length), Is.EqualTo(20));
            Assert.That(document.banquet.table.plates, Has.Length.EqualTo(8));
            Assert.That(document.banquet.table.candles, Has.Length.EqualTo(2));

            var first = document.layout.interiorLadders[0];
            Assert.That(first.localX, Is.EqualTo(2.598990542748065f).Within(0.000001f));
            Assert.That(first.localZ, Is.EqualTo(11.663950795451175f).Within(0.000001f));
            Assert.That(first.startY, Is.EqualTo(7.344967894227929f).Within(0.000001f));
            Assert.That(first.endY, Is.EqualTo(52.99136789422793f).Within(0.000001f));
            Assert.That(first.rotation, Is.EqualTo(3.360833545213715f).Within(0.000001f));
            Assert.That(first.width, Is.EqualTo(4.2f));

            var last = document.layout.interiorLadders[3];
            Assert.That(last.localX, Is.EqualTo(6.80489098817974f).Within(0.000001f));
            Assert.That(last.localZ, Is.EqualTo(-9.823235650181163f).Within(0.000001f));
            Assert.That(last.startY, Is.EqualTo(164.12896789422794f).Within(0.000001f));
            Assert.That(last.endY, Is.EqualTo(220.54496789422794f).Within(0.000001f));
            Assert.That(last.rotation, Is.EqualTo(5.677352402651483f).Within(0.000001f));
            Assert.That(last.width, Is.EqualTo(4.2f));
        }

        [Test]
        public void NativeControllerLadderAndVillagerContractsRemainPinnedToReact()
        {
            var document = LoadLayout();
            Assert.That(WofMountainLadderZone.ClimbSpeed, Is.EqualTo(8.4f));
            Assert.That(WofMountainLadderZone.PlanarDamping, Is.EqualTo(0.22f));
            Assert.That(WofMountainVillageLayout.ExactSlopeGrassCount, Is.EqualTo(1793));
            Assert.That(document.constants.slopeGrassNearCount, Is.EqualTo(2200));
            Assert.That(WofMountainVillageLayout.WorldOrigin, Is.EqualTo(new Vector3(1536f, 0f, 0f)));
            Assert.That(WofMountainVillageLayout.SummitViewProbeSpawn,
                Is.EqualTo(new Vector3(1536f, 225.54496789422794f, 92f)));
            Assert.That(WofMountainVillageLayout.BanquetViewProbeSpawn,
                Is.EqualTo(new Vector3(1524f, 10.564967894227928f, 20f)));
            Assert.That(WofMountainVillageLayout.CatwalkViewProbeSpawn,
                Is.EqualTo(new Vector3(1524f, 109.43536789422794f, 12f)));
            Assert.That(WofMountainVillageLayout.FirstLadderControllerProbeSpawn,
                Is.EqualTo(new Vector3(1538.598990542748f, 8.344967894227929f, 11.663950795451175f)));

            var villager = document.villagers[0];
            Assert.That(villager.id, Is.EqualTo("3:0-mountain-hut-0"));
            Assert.That(villager.townId, Is.EqualTo("survival-mountain-villagers-3:0"));
            Assert.That(villager.archiveFile, Is.EqualTo("mountain-00.wofavatar"));
            Assert.That(villager.x, Is.EqualTo(1559.0938176301752f).Within(0.0001f));
            Assert.That(villager.y, Is.EqualTo(218.49496789422793f).Within(0.0001f));
            Assert.That(villager.z, Is.EqualTo(55.893706884455895f).Within(0.0001f));
            Assert.That(Vector3.Distance(WofMountainVillageLayout.FirstVillagerControllerProbeSpawn,
                WofMountainVillageLayout.FirstVillagerWorldPosition), Is.LessThan(WofQuestTargetMath.CloseRange));
        }

        [Test]
        public void CompactMountainVillagerArchivesContainAllRuntimeFrames()
        {
            foreach (var archiveName in new[] { "mountain-00.wofavatar", "mountain-10.wofavatar" })
            {
                var bytes = File.ReadAllBytes(ResolveProjectPath("Assets", "StreamingAssets", "WOF", "Villagers", "Base", archiveName));
                Assert.That(WofVillagerFrameArchive.TryParse(bytes, out var archive, out var error), Is.True, error);
                Assert.That(archive.EntryCount, Is.EqualTo(52));
                Assert.That(archive.Contains("idle/d0"), Is.True);
                Assert.That(archive.Contains("angry-blink/d7"), Is.True);
            }
        }

        [Test]
        public void GeneratedMountainSceneHasExactRuntimePopulationAndFiniteGeometry()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofMountainVillage.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Assert.That(transforms.Any(item => item.name == "ReactSurvivalMountainVillage_3_0"), Is.True);
                Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<WofVillagerBillboard>(true)).Count(), Is.EqualTo(11));
                Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<WofMountainLadderZone>(true)).Count(), Is.EqualTo(4));
                Assert.That(transforms.Count(item => item.name == "DoorFrameLeft"), Is.EqualTo(8));
                Assert.That(transforms.Count(item => item.name == "PlatformOrb"), Is.EqualTo(3));
                Assert.That(transforms.Count(item => item.name == "RoastBoneLeft"), Is.EqualTo(1));
                Assert.That(transforms.Count(item => item.name == "RoastBoneRight"), Is.EqualTo(1));
                Assert.That(transforms.Count(item => item.name == "Crown"), Is.EqualTo(1));
                Assert.That(transforms.Count(item => item.name.StartsWith("Spire_")), Is.EqualTo(3));
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

        private static void AssertMesh(WofSerializedMeshRecord mesh, int vertices, int indices)
        {
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.EqualTo(vertices));
            Assert.That(mesh.positions, Has.Length.EqualTo(vertices * 3));
            Assert.That(mesh.normals, Has.Length.EqualTo(vertices * 3));
            Assert.That(mesh.colors == null || mesh.colors.Length == 0 || mesh.colors.Length == vertices * 3, Is.True);
            Assert.That(mesh.uvs == null || mesh.uvs.Length == 0 || mesh.uvs.Length == vertices * 2, Is.True);
            Assert.That(mesh.indices, Has.Length.EqualTo(indices));
        }

        private static void AssertFinite(Vector3 value, string context)
        {
            Assert.That(float.IsFinite(value.x), Is.True, context + " x");
            Assert.That(float.IsFinite(value.y), Is.True, context + " y");
            Assert.That(float.IsFinite(value.z), Is.True, context + " z");
        }

        private static WofMountainVillageDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "MountainVillage", "runtime-layout.json"));
            var document = JsonUtility.FromJson<WofMountainVillageDocument>(text);
            Assert.That(document, Is.Not.Null);
            return document;
        }

        private static string ResolveProjectPath(params string[] segments)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            Assert.That(Path.GetPathRoot(projectRoot), Is.EqualTo(@"D:\"));
            var path = projectRoot;
            foreach (var segment in segments) path = Path.Combine(path, segment);
            return path;
        }
    }
}
