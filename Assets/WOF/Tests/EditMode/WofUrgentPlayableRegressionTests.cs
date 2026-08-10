using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofUrgentPlayableRegressionTests
    {
        [Test]
        public void RenderPipelinePreservesExactReactPixelTreatment()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/WOF/Generated/Settings/WofUniversalPipeline.asset");
            Assert.That(pipeline, Is.Not.Null);

            var serialized = new SerializedObject(pipeline);
            Assert.That(serialized.FindProperty("m_MSAA").intValue, Is.EqualTo(1));
            Assert.That(serialized.FindProperty("m_RenderScale").floatValue, Is.EqualTo(0.46f).Within(0.0001f));
            Assert.That(serialized.FindProperty("m_UpscalingFilter").intValue, Is.EqualTo(2));
        }

        [Test]
        public void ReactDefaultLoadoutAndSelfBuffTuningRemainExact()
        {
            Assert.That(WofSpellLoadout.ReactDefaultLeft, Is.EqualTo(WofSpellId.SpeedBoost));
            Assert.That(WofSpellLoadout.ReactDefaultRight, Is.EqualTo(WofSpellId.JumpBoost));
            Assert.That(WofSpellLoadout.SelfBuffDurationSeconds, Is.EqualTo(12f));
            Assert.That(WofSpellLoadout.SelfBuffHandChargeSeconds, Is.EqualTo(0.18f));
            Assert.That(WofSpellLoadout.SpeedBoostMultiplier, Is.EqualTo(2f));
            Assert.That(WofSpellLoadout.JumpBoostMultiplier, Is.EqualTo(2f));
            Assert.That(WofSpellLoadout.PlayableSpells, Is.EqualTo(new[]
            {
                WofSpellId.Fireball,
                WofSpellId.IceShard,
                WofSpellId.ArcaneBeam,
                WofSpellId.Heal,
                WofSpellId.IceSpell,
                WofSpellId.RingsOfPower,
                WofSpellId.Lightning,
                WofSpellId.SmokeBomb,
                WofSpellId.Portal,
                WofSpellId.Blink,
                WofSpellId.Grab,
                WofSpellId.Tornado,
                WofSpellId.MeteorShower,
                WofSpellId.Flamethrower,
                WofSpellId.DiscShield,
                WofSpellId.OrbShield,
                WofSpellId.Kunai,
                WofSpellId.HealingCrystals,
                WofSpellId.MagicArmor,
                WofSpellId.JumpBoost,
                WofSpellId.SpeedBoost,
                WofSpellId.TungstonBallsack,
                WofSpellId.Sleep,
                WofSpellId.Poison,
                WofSpellId.Acid,
                WofSpellId.MagicGlassOrb
            }));
            Assert.That(WofSpellLoadout.GetDisplayName(WofSpellId.IceShard), Is.EqualTo("Biden Blast"));
            Assert.That(WofSpellLoadout.GetDisplayName(WofSpellId.Lightning), Is.EqualTo("Chidori"));
            Assert.That(WofSpellLoadout.GetFamilyName(WofSpellId.MagicArmor), Is.EqualTo("DEFENSE"));
            Assert.That(WofSpellLoadout.IsValid(25), Is.True);
            Assert.That(WofSpellLoadout.IsValid(26), Is.False);
        }

        [Test]
        public void ReactTenSlotHotbarsExposeBothHandsAndAllOriginalDefaults()
        {
            Assert.That(WofSpellHotbarRuntime.SlotCount, Is.EqualTo(10));
            Assert.That(WofSpellHotbarRuntime.ReactDefaultLeft, Is.EqualTo(new[]
            {
                WofSpellId.SpeedBoost, WofSpellId.Fireball, WofSpellId.IceShard,
                WofSpellId.ArcaneBeam, WofSpellId.Heal, WofSpellId.IceSpell,
                WofSpellId.RingsOfPower, WofSpellId.Lightning, WofSpellId.SmokeBomb,
                WofSpellId.Portal
            }));
            Assert.That(WofSpellHotbarRuntime.ReactDefaultRight, Is.EqualTo(new[]
            {
                WofSpellId.JumpBoost, WofSpellId.Lightning, WofSpellId.Portal,
                WofSpellId.Grab, WofSpellId.Tornado, WofSpellId.MeteorShower,
                WofSpellId.Fireball, WofSpellId.IceSpell, WofSpellId.SmokeBomb,
                WofSpellId.Kunai
            }));
            Assert.That(WofSpellHotbarRuntime.WrapSlot(-1), Is.EqualTo(9));
            Assert.That(WofSpellHotbarRuntime.WrapSlot(10), Is.EqualTo(0));
        }

        [Test]
        public void MagicHandsRetainSubtleContinuousIdleBreathing()
        {
            Assert.That(WofMagicHandsLayout.IdleBreathCycleSeconds, Is.InRange(2f, 4f));
            Assert.That(WofMagicHandsLayout.IdleBreathAmplitudePixels, Is.InRange(2f, 5f));
            Assert.That(WofMagicHandsLayout.IdleBreathScaleAmplitude, Is.InRange(0.002f, 0.008f));
        }

        [Test]
        public void NavigationBlockCenterMatchesReactBoundaries()
        {
            Assert.That(WofNavigationMapRuntime.GetSurvivalBlockCenter(0f), Is.EqualTo(0f));
            Assert.That(WofNavigationMapRuntime.GetSurvivalBlockCenter(255.999f), Is.EqualTo(0f));
            Assert.That(WofNavigationMapRuntime.GetSurvivalBlockCenter(256f), Is.EqualTo(512f));
            Assert.That(WofNavigationMapRuntime.GetSurvivalBlockCenter(-256f), Is.EqualTo(0f));
            Assert.That(WofNavigationMapRuntime.GetSurvivalBlockCenter(-256.001f), Is.EqualTo(-512f));
        }

        [Test]
        public void SpellGridUsesReactDesktopAndMobileColumnCountsWithControllerRows()
        {
            Assert.That(WofSpellMenuRuntime.ResolveGridColumnCount(1280, 720), Is.EqualTo(5));
            Assert.That(WofSpellMenuRuntime.ResolveGridColumnCount(412, 915), Is.EqualTo(3));
            Assert.That(WofSpellMenuRuntime.ResolveGridCellHeight(720), Is.EqualTo(62f));
            Assert.That(WofSpellMenuRuntime.ResolveGridCellHeight(600), Is.EqualTo(51f));
            Assert.That(WofSpellMenuRuntime.ResolveControllerIndex(0, 0, 1, 5, 26), Is.EqualTo(5));
            Assert.That(WofSpellMenuRuntime.ResolveControllerIndex(25, 1, 0, 5, 26), Is.EqualTo(0));
        }

        [Test]
        public void ReactSurvivalTerrainBakeCoversTheFullWorldMap()
        {
            var path = Path.GetFullPath(
                "Assets/WOF/Art/Generated/React/SurvivalTerrain/base-region.json");
            var text = File.ReadAllText(path);
            StringAssert.Contains("\"generator\":\"Tools/bake-survival-terrain-assets.mts\"", text);
            StringAssert.Contains("\"bounds\":{\"minimumChunkX\":-4,\"maximumChunkX\":6,\"minimumChunkZ\":-4,\"maximumChunkZ\":3}", text);
            StringAssert.Contains("\"segments\":32", text);
            StringAssert.Contains("\"vertexCount\":89298", text);
            StringAssert.Contains("\"indexCount\":503808", text);
            StringAssert.Contains("\"-3:-3:chicago\"", text);
            StringAssert.Contains("\"0:-3:swamp\"", text);
            StringAssert.Contains("\"3:0:mountain\"", text);
            StringAssert.Contains("\"4:-4:desert\"", text);
            StringAssert.Contains("\"5:2:graveyard\"", text);
            StringAssert.Contains("\"0:0:base-village\"", text);
        }

        [Test]
        public void GeneratedSpawnVillageUsesFourOpenReactGatesAndRequiredHudRuntimes()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofBootstrap.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var perimeter = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(item => item.name == "ReactVillagePerimeterWalls");
                Assert.That(perimeter, Is.Not.Null);
                Assert.That(perimeter.Find("VillageGateArchNorth"), Is.Not.Null);
                Assert.That(perimeter.Find("VillageGateArchSouth"), Is.Not.Null);
                Assert.That(perimeter.Find("VillageGateArchEast"), Is.Not.Null);
                Assert.That(perimeter.Find("VillageGateArchWest"), Is.Not.Null);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Any(item => item.name == "ClosedArenaWalls"), Is.False);

                var openWorld = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "ReactSurvivalOpenWorldBaseRegion");
                var openWorldMesh = openWorld.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(openWorldMesh.vertexCount, Is.EqualTo(89298));
                Assert.That(openWorld.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(
                    openWorldMesh));
                var mountainDirt = FindNearestVertexColor(openWorldMesh, new Vector3(1536f, 0f, 512f));
                var mountainStone = FindNearestVertexColor(openWorldMesh, new Vector3(1536f, 0f, 384f));
                var mountainSnow = FindNearestVertexColor(openWorldMesh, new Vector3(1536f, 0f, 256f));
                Assert.That(mountainDirt.r, Is.GreaterThan(mountainDirt.g + 0.035f),
                    "The baked outer mountain must show its dirt lower band instead of grass green.");
                Assert.That(mountainDirt.g, Is.GreaterThan(mountainDirt.b + 0.035f));
                Assert.That(Mathf.Abs(mountainStone.r - mountainStone.g), Is.LessThan(0.12f),
                    "The baked outer mountain must show a neutral stone middle band.");
                Assert.That(mountainSnow.r, Is.GreaterThan(0.60f),
                    "The baked outer mountain must lead into a visible snow cap.");
                Assert.That(mountainSnow.b, Is.GreaterThan(mountainSnow.r + 0.04f),
                    "The upper outer band must transition from neutral stone toward blue-white snow.");

                var colliders = perimeter.GetComponentsInChildren<Collider>(true);
                AssertGateIsOpen(colliders, new Vector3(0f, 1f, -238f));
                AssertGateIsOpen(colliders, new Vector3(0f, 1f, 238f));
                AssertGateIsOpen(colliders, new Vector3(238f, 1f, 0f));
                AssertGateIsOpen(colliders, new Vector3(-238f, 1f, 0f));
                Assert.That(colliders.Any(item => item.bounds.Contains(new Vector3(60f, 1f, -238f))), Is.True);

                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofSpellMenuRuntime>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofSpellHotbarRuntime>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofPauseAndScoreboardRuntime>(true)).Count(), Is.EqualTo(1));
                var navigation = roots.SelectMany(root => root.GetComponentsInChildren<WofNavigationMapRuntime>(true)).Single();
                var serializedNavigation = new SerializedObject(navigation);
                Assert.That(serializedNavigation.FindProperty("circularMaskSprite").objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertGateIsOpen(Collider[] colliders, Vector3 point)
        {
            Assert.That(colliders.Any(item => item.bounds.Contains(point)), Is.False,
                $"React gate opening is blocked at {point}.");
        }

        private static Color FindNearestVertexColor(Mesh mesh, Vector3 target)
        {
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            Assert.That(colors, Has.Length.EqualTo(vertices.Length));
            var nearestIndex = 0;
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < vertices.Length; index++)
            {
                var dx = vertices[index].x - target.x;
                var dz = vertices[index].z - target.z;
                var distance = dx * dx + dz * dz;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestIndex = index;
            }
            Assert.That(nearestDistance, Is.LessThan(1f), $"No baked terrain vertex near {target}.");
            return colors[nearestIndex];
        }
    }
}
