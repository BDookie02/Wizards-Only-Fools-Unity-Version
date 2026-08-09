using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WOF.Tests
{
    public sealed class WofChicagoCityTests
    {
        [Test]
        public void BakedLayoutRetainsExactReactChunkAndPopulationCounts()
        {
            var document = LoadLayout();

            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.chunk.key, Is.EqualTo("-3:-3"));
            Assert.That(document.chunk.cx, Is.EqualTo(-3));
            Assert.That(document.chunk.cz, Is.EqualTo(-3));
            Assert.That(document.chunk.biome, Is.EqualTo("jungle"));
            Assert.That(document.chunk.villageKind, Is.EqualTo("chicago"));
            Assert.That(document.chunk.hasRiver, Is.False);
            Assert.That(document.baseHeight, Is.EqualTo(21.912045982731858f).Within(0.000001f));
            Assert.That(WofChicagoCityLayout.HasExactCounts(document.counts), Is.True);
            Assert.That(document.layout.buildings, Has.Length.EqualTo(35));
            Assert.That(document.layout.pedestrians, Has.Length.EqualTo(220));
            Assert.That(document.layout.cars, Has.Length.EqualTo(46));
            Assert.That(document.operators, Has.Length.EqualTo(35));
        }

        [Test]
        public void BuildingsAndLandmarksRetainDeterministicReactPlacement()
        {
            var buildings = LoadLayout().layout.buildings;
            var first = buildings[0];
            Assert.That(first.key, Is.EqualTo("-3:-3-chicago-building-0"));
            Assert.That(first.localX, Is.EqualTo(-194f));
            Assert.That(first.localZ, Is.EqualTo(-194f));
            Assert.That(first.width, Is.EqualTo(37f));
            Assert.That(first.depth, Is.EqualTo(41f));
            Assert.That(first.height, Is.EqualTo(53f));
            Assert.That(first.rotation, Is.EqualTo(-0.033724610728677364f).Within(0.000001f));
            Assert.That(first.facadeStyle, Is.EqualTo(0));
            Assert.That(first.enterable, Is.True);
            Assert.That(buildings[^1].key, Is.EqualTo("-3:-3-chicago-building-34"));
            Assert.That(buildings[^1].localX, Is.EqualTo(186f));
            Assert.That(buildings[^1].localZ, Is.EqualTo(186f));

            var landmarks = buildings.Where(building => !string.IsNullOrWhiteSpace(building.landmark)).ToArray();
            Assert.That(landmarks, Has.Length.EqualTo(4));
            Assert.That(landmarks.Single(building => building.landmark == "watertower").key,
                Is.EqualTo("-3:-3-chicago-building-10"));
            Assert.That(landmarks.Single(building => building.landmark == "willis").height, Is.EqualTo(188f));
            Assert.That(landmarks.Single(building => building.landmark == "skyscraper").height, Is.EqualTo(320f));
            Assert.That(landmarks.Single(building => building.landmark == "hancock").height, Is.EqualTo(148f));
        }

        [Test]
        public void StreetLayoutRetainsEveryExactReactArray()
        {
            var street = LoadLayout().street;
            Assert.That(street.trafficLightIntersections, Has.Length.EqualTo(16));
            Assert.That(street.lamps, Has.Length.EqualTo(48));
            Assert.That(street.streetTrees, Has.Length.EqualTo(40));
            Assert.That(street.sidewalkSegments, Has.Length.EqualTo(5));
            Assert.That(street.hydrants, Has.Length.EqualTo(16));
            Assert.That(street.trashCans, Has.Length.EqualTo(36));
            Assert.That(street.benches, Has.Length.EqualTo(34));
            Assert.That(street.grassPatches, Has.Length.EqualTo(40));
            Assert.That(street.crosswalks, Has.Length.EqualTo(576));
            Assert.That(street.sidewalkPlanes, Has.Length.EqualTo(80));
            Assert.That(street.parkingLines, Has.Length.EqualTo(64));
        }

        [Test]
        public void InitialTrafficTransformsMatchReactRuntimeMath()
        {
            var initial = LoadLayout().initialTraffic;
            Assert.That(initial.cars[0].x, Is.EqualTo(-150.47606682183687f).Within(0.0001f));
            Assert.That(initial.cars[0].z, Is.EqualTo(-79.2f).Within(0.0001f));
            Assert.That(initial.cars[0].yaw, Is.EqualTo(Mathf.PI * 0.5f).Within(0.000001f));
            Assert.That(initial.pedestrians[0].x, Is.EqualTo(-85.60928220913047f).Within(0.0001f));
            Assert.That(initial.pedestrians[0].z, Is.EqualTo(-91.32132688975143f).Within(0.0001f));
            Assert.That(initial.pedestrians[0].yaw, Is.EqualTo(Mathf.PI * 0.5f).Within(0.000001f));
        }

        [Test]
        public void InteriorOperatorsUseTheirExactGeneratedCharactersAndSprites()
        {
            var document = LoadLayout();
            var first = document.operators[0];
            Assert.That(first.index, Is.EqualTo(0));
            Assert.That(first.buildingKey, Is.EqualTo("-3:-3-chicago-building-0"));
            Assert.That(first.spritePath, Is.EqualTo("ChicagoCity/Operators/operator-00.png"));
            Assert.That(first.character.skinColor, Is.EqualTo("#f1c27d"));
            Assert.That(first.character.topColor, Is.EqualTo("#16a34a"));
            Assert.That(first.character.hatStyle, Is.EqualTo("cap"));
            Assert.That(first.character.eyeStyle, Is.EqualTo("content"));
            Assert.That(document.operators[^1].spritePath, Is.EqualTo("ChicagoCity/Operators/operator-34.png"));
        }

        [Test]
        public void ExactChicagoPadMeshRemainsFullySerialized()
        {
            var mesh = LoadLayout().padGeometry;
            Assert.That(mesh.vertexCount, Is.EqualTo(361));
            Assert.That(mesh.positions, Has.Length.EqualTo(mesh.vertexCount * 3));
            Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount * 3));
            Assert.That(mesh.uvs, Has.Length.EqualTo(mesh.vertexCount * 2));
            Assert.That(mesh.indices, Has.Length.EqualTo(1944));
        }

        [Test]
        public void RuntimeWorldOriginAndDesktopProbeRemainPinnedToReactChunk()
        {
            Assert.That(WofChicagoCityLayout.WorldOrigin, Is.EqualTo(new Vector3(-1536f, 0f, -1536f)));
            Assert.That(WofChicagoCityLayout.ViewProbeSpawn,
                Is.EqualTo(new Vector3(-1536f, WofChicagoCityLayout.ReactBaseHeight + 2.2f, -1750f)));
        }

        [Test]
        public void EveryGeneratedChicagoMeshHasFiniteVerticesAndBounds()
        {
            var guids = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/WOF/Generated/Geometry/ChicagoCity" });
            Assert.That(guids.Length, Is.GreaterThan(0));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.That(mesh, Is.Not.Null, path);
                foreach (var vertex in mesh.vertices)
                {
                    AssertFinite(vertex, path + " vertex");
                    Assert.That(Mathf.Abs(vertex.x), Is.LessThan(1000f), path);
                    Assert.That(Mathf.Abs(vertex.y), Is.LessThan(1000f), path);
                    Assert.That(Mathf.Abs(vertex.z), Is.LessThan(1000f), path);
                }
                AssertFinite(mesh.bounds.center, path + " bounds center");
                AssertFinite(mesh.bounds.extents, path + " bounds extents");
            }
        }

        [Test]
        public void GeneratedChicagoSceneHasOnlyFiniteTransformsAndColliders()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofChicagoCity.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var city = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(transform => transform.name == "ReactSurvivalChicagoCity_-3_-3");
                Assert.That(city, Is.Not.Null);
                foreach (var item in city.GetComponentsInChildren<Transform>(true))
                {
                    AssertFinite(item.localPosition, item.name + " localPosition");
                    AssertFinite(item.localScale, item.name + " localScale");
                    AssertFinite(new Vector3(item.localRotation.x, item.localRotation.y, item.localRotation.z),
                        item.name + " localRotation xyz");
                    Assert.That(float.IsFinite(item.localRotation.w), Is.True, item.name + " localRotation w");
                }
                foreach (var collider in city.GetComponentsInChildren<BoxCollider>(true))
                {
                    AssertFinite(collider.center, collider.name + " collider center");
                    AssertFinite(collider.size, collider.name + " collider size");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void GeneratedChicagoRuntimeBehavioursResolveToDedicatedMonoScripts()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofChicagoCity.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                AssertDedicatedScript<WofChicagoTrafficRuntime>(scene, "WofChicagoTrafficRuntime.cs");
                AssertDedicatedScript<WofChicagoLedSignRuntime>(scene, "WofChicagoLedSignRuntime.cs");
                AssertDedicatedScript<WofChicagoOperatorBillboards>(scene, "WofChicagoOperatorBillboards.cs");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertDedicatedScript<T>(Scene scene, string expectedFileName) where T : MonoBehaviour
        {
            var component = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .SingleOrDefault();
            Assert.That(component, Is.Not.Null, typeof(T).Name);
            var script = MonoScript.FromMonoBehaviour(component);
            Assert.That(script, Is.Not.Null, typeof(T).Name);
            Assert.That(Path.GetFileName(AssetDatabase.GetAssetPath(script)), Is.EqualTo(expectedFileName));
            Assert.That(script.GetClass(), Is.EqualTo(typeof(T)));
        }

        private static void AssertFinite(Vector3 value, string context)
        {
            Assert.That(float.IsFinite(value.x), Is.True, context + " x");
            Assert.That(float.IsFinite(value.y), Is.True, context + " y");
            Assert.That(float.IsFinite(value.z), Is.True, context + " z");
        }

        private static WofChicagoCityDocument LoadLayout()
        {
            var text = File.ReadAllText(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "ChicagoCity", "runtime-layout.json"));
            var document = JsonUtility.FromJson<WofChicagoCityDocument>(text);
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
