using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofLilyCoilTests
    {
        private const string LayoutPath = "Assets/WOF/Art/Generated/React/LilyCoil/runtime-layout.json";
        private const string ScenePath = "Assets/WOF/Generated/Scenes/WofLilyCoil.unity";

        [Test]
        public void BakedDocumentMatchesTheExactReactDesktopRealm()
        {
            var document = LoadLayout();
            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.sourceSignature, Is.EqualTo(WofLilyCoilLayout.SourceSignature));
            Assert.That(document.chunk.key, Is.EqualTo("48:-48"));
            Assert.That(document.chunk.cx, Is.EqualTo(WofLilyCoilLayout.ChunkX));
            Assert.That(document.chunk.cz, Is.EqualTo(WofLilyCoilLayout.ChunkZ));
            Assert.That(document.chunk.villageKind, Is.EqualTo("lily-coil"));
            Assert.That(document.chunk.lod, Is.EqualTo("near"));
            Assert.That(WofLilyCoilLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.flora.tubeGrass, Has.Length.EqualTo(WofLilyCoilLayout.TubeGrassTuftCount));
            Assert.That(document.flora.tubeLilies, Has.Length.EqualTo(WofLilyCoilLayout.TubeLilyCount));
            Assert.That(document.flora.tubeFlowers, Has.Length.EqualTo(WofLilyCoilLayout.TubeFlowerCount));
            Assert.That(document.flora.smallTubeFlowers, Has.Length.EqualTo(WofLilyCoilLayout.SmallTubeFlowerCount));
            Assert.That(document.flora.smallBloomParticles, Has.Length.EqualTo(WofLilyCoilLayout.SmallBloomParticleCount));
            Assert.That(document.flora.fireflies, Has.Length.EqualTo(WofLilyCoilLayout.FireflyCount));
            Assert.That(document.flora.butterflies, Has.Length.EqualTo(WofLilyCoilLayout.ButterflyCount));
            Assert.That(document.flora.groundGrass, Has.Length.EqualTo(WofLilyCoilLayout.GroundGrassTuftCount));
            Assert.That(document.flora.groundLilies, Has.Length.EqualTo(WofLilyCoilLayout.GroundLilyCount));
            Assert.That(document.eyeFrames, Has.Length.EqualTo(WofLilyCoilLayout.EyeFrameCount));
        }

        [Test]
        public void BakedTubeAndColliderRetainTheThreeGeometryPayloads()
        {
            var geometry = LoadLayout().geometries;
            Assert.That(geometry.tunnel.vertexCount, Is.EqualTo(2465));
            Assert.That(geometry.tunnel.positions, Has.Length.EqualTo(7395));
            Assert.That(geometry.tunnel.indices, Has.Length.EqualTo(13824));
            Assert.That(geometry.tunnelCollider.vertexCount, Is.EqualTo(657));
            Assert.That(geometry.tunnelCollider.positions, Has.Length.EqualTo(1971));
            Assert.That(geometry.tunnelCollider.indices, Has.Length.EqualTo(3456));
        }

        [Test]
        public void TubeFrameAndProbeMathRemainAtTheAuthoredWorldChunk()
        {
            Assert.That(WofLilyCoilLayout.WorldOrigin, Is.EqualTo(new Vector3(24576f, 0f, -24576f)));
            Assert.That(WofLilyCoilLayout.SpawnPosition,
                Is.EqualTo(new Vector3(24813.11f, 72.15f, -24596.54f)));
            Assert.That(Vector3.Distance(WofLilyCoilLayout.PlayableSpawnPosition,
                WofLilyCoilLayout.SpawnPosition), Is.LessThan(6f));
            var playableSpawnState = WofLilyCoilLayout.GetNearestState(WofLilyCoilLayout.PlayableSpawnPosition);
            Assert.That(Vector3.Distance(WofLilyCoilLayout.PlayableSpawnPosition,
                WofLilyCoilLayout.GetFrame(playableSpawnState.T).Center),
                Is.EqualTo(WofLilyCoilLayout.TubePlayerRadius).Within(0.001f));
            Assert.That(WofLilyCoilLayout.ExteriorViewProbeSpawn,
                Is.EqualTo(new Vector3(24576f, 350f, -25296f)));
            var start = WofLilyCoilLayout.GetFrame(0f);
            var end = WofLilyCoilLayout.GetFrame(1f);
            Assert.That(start.Center.y, Is.EqualTo(WofLilyCoilLayout.TubeStartY).Within(0.0001f));
            Assert.That(end.Center.y,
                Is.EqualTo(WofLilyCoilLayout.TubeStartY + WofLilyCoilLayout.TubeRise).Within(0.0001f));
            Assert.That(Vector3.Distance(WofLilyCoilLayout.GetTunnelViewProbeSpawn(),
                WofLilyCoilLayout.GetFrame(0.34f).Center),
                Is.EqualTo(WofLilyCoilLayout.TubePlayerRadius).Within(0.001f));
        }

        [Test]
        public void GeneratedSceneContainsExactRealmTunnelFloraAndColliders()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = GameObject.Find("ReactSurvivalLilyCoil_48_-48");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.transform.position, Is.EqualTo(WofLilyCoilLayout.WorldOrigin));
                Assert.That(root.transform.Find("ExactLilyCoilGround"), Is.Not.Null);
                Assert.That(root.transform.Find("LilyCoilTunnelInner"), Is.Not.Null);
                Assert.That(root.transform.Find("LilyCoilTunnelOuter"), Is.Not.Null);
                Assert.That(root.transform.Find("TubeGrassGroup_0"), Is.Not.Null);
                Assert.That(root.transform.Find("TubeLilyPetals_6590"), Is.Not.Null);
                Assert.That(root.transform.Find("GroundGrass_10400"), Is.Not.Null);
                Assert.That(root.transform.Find("GroundLilyPetals_2800"), Is.Not.Null);
                Assert.That(root.transform.Find("SmallBloomParticles_750"), Is.Not.Null);
                Assert.That(root.transform.Find("Fireflies_160"), Is.Not.Null);
                var ambientEffects = root.GetComponent<WofLilyCoilAmbientEffectsRuntime>();
                Assert.That(ambientEffects, Is.Not.Null);
                Assert.That(ambientEffects.BloomParticleCount, Is.EqualTo(WofLilyCoilLayout.SmallBloomParticleCount));
                Assert.That(ambientEffects.FireflyCount, Is.EqualTo(WofLilyCoilLayout.FireflyCount));
                Assert.That(ambientEffects.ButterflyCount, Is.EqualTo(WofLilyCoilLayout.ButterflyCount));
                Assert.That(root.GetComponentsInChildren<WofLilyCoilEyeAnimator>(true), Has.Length.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<BoxCollider>(true), Has.Length.EqualTo(39));
                Assert.That(root.GetComponentsInChildren<MeshCollider>(true), Has.Length.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        private static WofLilyCoilDocument LoadLayout()
        {
            var text = File.ReadAllText(LayoutPath);
            return JsonUtility.FromJson<WofLilyCoilDocument>(text);
        }
    }
}
