using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofVillagerTests
    {
        [Test]
        public void BakedBaseVillageLayoutRetainsReactInventoryAndFirstIdentity()
        {
            var document = LoadLayout();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.count, Is.EqualTo(307));
            Assert.That(document.villagers, Has.Length.EqualTo(307));
            Assert.That(document.frameContract.archiveEntriesPerVillager, Is.EqualTo(52));
            Assert.That(document.avatarScale, Is.EqualTo(2.25f).Within(0.000001f));
            Assert.That(document.avatarGroundLift, Is.EqualTo(1.06875f).Within(0.000001f));

            var first = document.villagers[0];
            Assert.That(first.id, Is.EqualTo("-224--224"));
            Assert.That(first.x, Is.EqualTo(-224f).Within(0.0001f));
            Assert.That(first.y, Is.EqualTo(2.95f).Within(0.0001f));
            Assert.That(first.z, Is.EqualTo(-222.75f).Within(0.0001f));
            Assert.That(first.baseYaw, Is.EqualTo(Mathf.PI).Within(0.000001f));
            Assert.That(first.character.skinColor, Is.EqualTo("#d7a77f"));
            Assert.That(first.character.topColor, Is.EqualTo("#0f766e"));
            Assert.That(first.character.hairStyle, Is.EqualTo("bob"));
            Assert.That(first.character.facialHairStyle, Is.EqualTo("goatee"));

            var last = document.villagers[^1];
            Assert.That(last.id, Is.EqualTo("224-224"));
            Assert.That(last.x, Is.EqualTo(222.75f).Within(0.0001f));
            Assert.That(last.z, Is.EqualTo(224f).Within(0.0001f));
        }

        [Test]
        public void CompactFirstVillagerArchiveContainsEveryExactRuntimeFrame()
        {
            var bytes = File.ReadAllBytes(ResolveProjectPath(
                "Assets", "StreamingAssets", "WOF", "Villagers", "Base", "-224--224.wofavatar"));

            Assert.That(WofVillagerFrameArchive.TryParse(bytes, out var archive, out var error), Is.True, error);
            Assert.That(archive.EntryCount, Is.EqualTo(52));
            Assert.That(archive.Contains("idle/d0"), Is.True);
            Assert.That(archive.Contains("idle-blink/d7"), Is.True);
            Assert.That(archive.Contains("startled/d4/f1"), Is.True);
            Assert.That(archive.Contains("startled-blink/d2/f0"), Is.True);
            Assert.That(archive.Contains("angry/d6"), Is.True);
            Assert.That(archive.Contains("angry-blink/d7"), Is.True);
            Assert.That(archive.Contains("idle-blink/d4"), Is.False);

            Assert.That(archive.TryExtractPng("idle/d0", out var png), Is.True);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, png, false), Is.True);
                Assert.That(texture.width, Is.EqualTo(512));
                Assert.That(texture.height, Is.EqualTo(512));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void VillagerArchiveRejectsTruncationAndCorruptMagic()
        {
            Assert.That(WofVillagerFrameArchive.TryParse(new byte[4], out _, out _), Is.False);
            var corrupt = new byte[12];
            Assert.That(WofVillagerFrameArchive.TryParse(corrupt, out _, out _), Is.False);
        }

        [TestCase(0f, 0f, -10f, 0)]
        [TestCase(0f, 10f, 0f, 2)]
        [TestCase(0f, 0f, 10f, 4)]
        [TestCase(0f, -10f, 0f, 6)]
        public void DirectionSelectionMatchesReactEightWayBillboard(float yaw, float cameraX, float cameraZ, int expected)
        {
            Assert.That(
                WofVillagerMath.ResolveDirection(yaw, Vector3.zero, new Vector3(cameraX, 0f, cameraZ)),
                Is.EqualTo(expected));
        }

        [Test]
        public void VillagerFacingAndVisibilityUseReactStrictBoundaries()
        {
            Assert.That(
                WofVillagerMath.TryResolveFacingYaw(Vector3.zero, new Vector3(0f, 0f, -17.99f), 1f, out var yaw),
                Is.True);
            Assert.That(yaw, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                WofVillagerMath.TryResolveFacingYaw(Vector3.zero, new Vector3(0f, 0f, -18f), 1f, out _),
                Is.False);
            Assert.That(WofVillagerMath.ShouldRender(Vector3.zero, new Vector3(89.99f, 0f, 0f), false, false), Is.True);
            Assert.That(WofVillagerMath.ShouldRender(Vector3.zero, new Vector3(90f, 0f, 0f), false, false), Is.False);
            Assert.That(WofVillagerMath.ShouldRender(Vector3.zero, new Vector3(500f, 0f, 0f), true, false), Is.True);
        }

        [Test]
        public void VillagerFacingChoosesNearestLocalOrRemoteAlivePositionWithReactTieRule()
        {
            var positions = new[]
            {
                new Vector3(0f, 0f, -10f),
                new Vector3(4f, 0f, 0f),
                new Vector3(-4f, 0f, 0f)
            };
            Assert.That(
                WofVillagerMath.TryResolveNearestFacingYaw(Vector3.zero, positions, 1.25f, out var yaw),
                Is.True);
            Assert.That(yaw, Is.EqualTo(Mathf.PI * 0.5f).Within(0.0001f));

            var rejected = new[] { new Vector3(1f, 7.01f, 0f), new Vector3(18f, 0f, 0f) };
            Assert.That(
                WofVillagerMath.TryResolveNearestFacingYaw(Vector3.zero, rejected, 1.25f, out yaw),
                Is.False);
            Assert.That(yaw, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void ReactionFrameKeysPreserveBlinkAndBackFacingRules()
        {
            Assert.That(
                WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Idle, 0, 3, true),
                Is.EqualTo("idle-blink/d0"));
            Assert.That(
                WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Idle, 4, 3, true),
                Is.EqualTo("idle/d4"));
            Assert.That(
                WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Startled, 2, 3, true),
                Is.EqualTo("startled-blink/d2/f1"));
            Assert.That(
                WofVillagerMath.ResolveFrameKey(WofVillagerPhase.Angry, 7, 0, true),
                Is.EqualTo("angry-blink/d7"));
        }

        [Test]
        public void HutInsideTestMatchesReactMushroomAndRectangularRules()
        {
            var mushroom = new WofVillagerHutRecord { x = 4f, y = 2f, z = 5f, isMushroom = true };
            Assert.That(WofVillagerMath.IsPlayerInsideHut(new Vector3(4f, 3f, 10.34f), mushroom), Is.True);
            Assert.That(WofVillagerMath.IsPlayerInsideHut(new Vector3(4f, 3f, 10.35f), mushroom), Is.False);

            var rectangular = new WofVillagerHutRecord { y = 2f, rotation = 0f, isMushroom = false };
            Assert.That(WofVillagerMath.IsPlayerInsideHut(new Vector3(7.34f, 3f, 0f), rectangular), Is.True);
            Assert.That(WofVillagerMath.IsPlayerInsideHut(new Vector3(7.35f, 3f, 0f), rectangular), Is.False);
        }

        [TestCase(-10f, 0.12f)]
        [TestCase(0.12f, 0.12f)]
        [TestCase(0.42f, 0.42f)]
        [TestCase(0.8f, 0.8f)]
        [TestCase(10f, 0.8f)]
        public void VillagerYelpVolumeClampMatchesReact(float volume, float expected)
        {
            Assert.That(WofVillagerYelp.ClampVolume(volume), Is.EqualTo(expected).Within(0.000001f));
        }

        [Test]
        public void VillagerYelpPitchAndEnvelopeMatchReactAutomation()
        {
            Assert.That(WofVillagerYelp.ResolveFrequency(0f), Is.EqualTo(760f).Within(0.001f));
            Assert.That(WofVillagerYelp.ResolveFrequency(0.08f), Is.EqualTo(1320f).Within(0.001f));
            Assert.That(WofVillagerYelp.ResolveFrequency(0.24f), Is.EqualTo(520f).Within(0.001f));
            Assert.That(WofVillagerYelp.ResolveGain(0f, 0.42f), Is.EqualTo(0.0001f).Within(0.000001f));
            Assert.That(WofVillagerYelp.ResolveGain(0.025f, 0.42f), Is.EqualTo(0.0756f).Within(0.000001f));
            Assert.That(WofVillagerYelp.ResolveGain(0.28f, 0.42f), Is.EqualTo(0.0001f).Within(0.000001f));
        }

        private static WofVillagerLayoutDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "Villagers", "base-village.json"));
            var document = JsonUtility.FromJson<WofVillagerLayoutDocument>(text);
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
