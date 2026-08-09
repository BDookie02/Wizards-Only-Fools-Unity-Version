using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofSwampVillageTests
    {
        [Test]
        public void BakedLayoutRetainsExactReactChunkAndFeatureCounts()
        {
            var document = LoadLayout();
            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.chunk.key, Is.EqualTo("0:-3"));
            Assert.That(document.chunk.cx, Is.EqualTo(0));
            Assert.That(document.chunk.cz, Is.EqualTo(-3));
            Assert.That(document.chunk.biome, Is.EqualTo("swamp"));
            Assert.That(document.chunk.villageKind, Is.EqualTo("swamp"));
            Assert.That(document.chunk.hasRiver, Is.True);
            Assert.That(document.chunk.riverVertical, Is.False);
            Assert.That(document.chunk.lod, Is.EqualTo("near"));
            Assert.That(document.baseHeight, Is.EqualTo(2.7529895363497836f).Within(0.000001f));
            Assert.That(WofSwampVillageLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.layout.huts, Has.Length.EqualTo(13));
            Assert.That(document.layout.walkways, Has.Length.EqualTo(17));
            Assert.That(document.layout.ramps, Has.Length.EqualTo(4));
            Assert.That(document.layout.lilyPads, Has.Length.EqualTo(28));
            Assert.That(document.layout.stumps, Has.Length.EqualTo(18));
            Assert.That(document.layout.reeds, Has.Length.EqualTo(36));
            Assert.That(document.ropeSegments, Has.Length.EqualTo(91));
            Assert.That(document.ropeBulbs, Has.Length.EqualTo(39));
            Assert.That(document.villagers, Has.Length.EqualTo(13));
        }

        [Test]
        public void FirstHutAndVillagerRetainExactReactPlacementAndIdentity()
        {
            var document = LoadLayout();
            var hut = document.layout.huts[0];
            Assert.That(hut.key, Is.EqualTo("0:-3-swamp-hut-0"));
            Assert.That(hut.localX, Is.EqualTo(16.4528264380891f).Within(0.0001f));
            Assert.That(hut.localZ, Is.EqualTo(91.56591170152812f).Within(0.0001f));
            Assert.That(hut.width, Is.EqualTo(17f));
            Assert.That(hut.depth, Is.EqualTo(16f));
            Assert.That(hut.height, Is.EqualTo(11.5f));
            Assert.That(hut.rotation, Is.EqualTo(-2.963806903126242f).Within(0.000001f));
            Assert.That(hut.wallColor, Is.EqualTo("#5c4a2e"));
            Assert.That(hut.roofColor, Is.EqualTo("#223516"));

            var villager = document.villagers[0];
            Assert.That(villager.id, Is.EqualTo("0:-3-swamp-hut-0"));
            Assert.That(villager.townId, Is.EqualTo("survival-swamp-villagers-0:-3"));
            Assert.That(villager.archiveFile, Is.EqualTo("swamp-00.wofavatar"));
            Assert.That(villager.archiveBytes, Is.EqualTo(177868));
            Assert.That(villager.archiveSha256, Is.EqualTo("8efec3e6a16a102fd6f3443bb2db3de08d3c854b17a637bcb2562337a0e5e2e2"));
        }

        [Test]
        public void ExactPadLilyRopeAndToadContractsRemainSerialized()
        {
            var document = LoadLayout();
            Assert.That(document.padGeometry.vertexCount, Is.EqualTo(361));
            Assert.That(document.padGeometry.indices, Has.Length.EqualTo(1944));
            Assert.That(document.lilyPadGeometry.vertexCount, Is.EqualTo(30));
            Assert.That(document.lilyPadGeometry.indices, Has.Length.EqualTo(84));
            Assert.That(document.ropeSegments[0].length, Is.EqualTo(1.910359289334226f).Within(0.000001f));
            Assert.That(document.ropeBulbs.Count(bulb => bulb.hasPointLight), Is.EqualTo(3));
            Assert.That(document.toad.frameSize, Is.EqualTo(new[] { 288, 187 }));
            Assert.That(document.toad.idle, Has.Length.EqualTo(28));
            Assert.That(document.toad.yawn, Has.Length.EqualTo(12));
            Assert.That(document.toad.idleFrameMs, Is.EqualTo(200));
            Assert.That(document.toad.yawnFrameMs, Is.EqualTo(120));
            Assert.That(document.toad.sleep, Is.EqualTo("SwampVillage/Toad/toad_sleep.png"));
        }

        [Test]
        public void CompactSwampVillagerArchivesContainAllRuntimeFrames()
        {
            foreach (var archiveName in new[] { "swamp-00.wofavatar", "swamp-12.wofavatar" })
            {
                var bytes = File.ReadAllBytes(ResolveProjectPath("Assets", "StreamingAssets", "WOF", "Villagers", "Base", archiveName));
                Assert.That(WofVillagerFrameArchive.TryParse(bytes, out var archive, out var error), Is.True, error);
                Assert.That(archive.EntryCount, Is.EqualTo(52));
                Assert.That(archive.Contains("idle/d0"), Is.True);
                Assert.That(archive.Contains("angry-blink/d7"), Is.True);
            }
        }

        [Test]
        public void RuntimeWorldOriginViewAndControllerProbesRemainPinnedToReactChunk()
        {
            Assert.That(WofSwampVillageLayout.WorldOrigin, Is.EqualTo(new Vector3(0f, 0f, -1536f)));
            Assert.That(WofSwampVillageLayout.ViewProbeSpawn,
                Is.EqualTo(new Vector3(0f, WofSwampVillageLayout.ReactPlatformY + 2.2f, -1604f)));
            Assert.That(Vector3.Distance(WofSwampVillageLayout.FirstVillagerControllerProbeSpawn,
                WofSwampVillageLayout.FirstVillagerWorldPosition), Is.LessThan(WofQuestTargetMath.CloseRange));
        }

        [Test]
        public void GeneratedSwampSceneHasExactRuntimePopulationAndFiniteGeometry()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofSwampVillage.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Assert.That(transforms.Any(item => item.name == "ReactSurvivalSwampVillage_0_-3"), Is.True);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WofVillagerBillboard>(true)).Count(), Is.EqualTo(13));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).Count(light => light.type == LightType.Point), Is.EqualTo(3));
                foreach (var item in transforms)
                {
                    AssertFinite(item.localPosition, item.name + " position");
                    AssertFinite(item.localScale, item.name + " scale");
                    Assert.That(float.IsFinite(item.localRotation.x) && float.IsFinite(item.localRotation.y) &&
                                float.IsFinite(item.localRotation.z) && float.IsFinite(item.localRotation.w), Is.True, item.name + " rotation");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void GeneratedSwampRuntimeBehaviourResolvesToDedicatedMonoScript()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofSwampVillage.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var component = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WofSwampToadRuntime>(true)).SingleOrDefault();
                Assert.That(component, Is.Not.Null);
                var script = MonoScript.FromMonoBehaviour(component);
                Assert.That(script, Is.Not.Null);
                Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(script)), Is.EqualTo("WofSwampToadRuntime.cs"));
                Assert.That(script.GetClass(), Is.EqualTo(typeof(WofSwampToadRuntime)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertFinite(Vector3 value, string context)
        {
            Assert.That(float.IsFinite(value.x), Is.True, context + " x");
            Assert.That(float.IsFinite(value.y), Is.True, context + " y");
            Assert.That(float.IsFinite(value.z), Is.True, context + " z");
        }

        private static WofSwampVillageDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath("Assets", "WOF", "Art", "Generated", "React", "SwampVillage", "runtime-layout.json"));
            var document = JsonUtility.FromJson<WofSwampVillageDocument>(text);
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
