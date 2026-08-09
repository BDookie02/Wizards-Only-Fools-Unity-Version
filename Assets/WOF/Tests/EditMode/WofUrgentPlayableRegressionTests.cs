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
                WofSpellId.SpeedBoost,
                WofSpellId.JumpBoost
            }));
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
        public void ReactSurvivalTerrainBakeCoversTheExactBaseRenderRadius()
        {
            var path = Path.GetFullPath(
                "Assets/WOF/Art/Generated/React/SurvivalTerrain/base-region.json");
            var text = File.ReadAllText(path);
            StringAssert.Contains("\"generator\":\"Tools/bake-survival-terrain-assets.mts\"", text);
            StringAssert.Contains("\"radius\":3", text);
            StringAssert.Contains("\"segments\":32", text);
            StringAssert.Contains("\"vertexCount\":49005", text);
            StringAssert.Contains("\"indexCount\":276480", text);
            StringAssert.Contains("\"-3:-3:chicago\"", text);
            StringAssert.Contains("\"0:-3:swamp\"", text);
            StringAssert.Contains("\"3:0:mountain\"", text);
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
                Assert.That(openWorld.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(49005));
                Assert.That(openWorld.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(
                    openWorld.GetComponent<MeshFilter>().sharedMesh));

                var colliders = perimeter.GetComponentsInChildren<Collider>(true);
                AssertGateIsOpen(colliders, new Vector3(0f, 1f, -238f));
                AssertGateIsOpen(colliders, new Vector3(0f, 1f, 238f));
                AssertGateIsOpen(colliders, new Vector3(238f, 1f, 0f));
                AssertGateIsOpen(colliders, new Vector3(-238f, 1f, 0f));
                Assert.That(colliders.Any(item => item.bounds.Contains(new Vector3(60f, 1f, -238f))), Is.True);

                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofSpellMenuRuntime>(true)).Count(), Is.EqualTo(1));
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
    }
}
