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
        public void UnityCalderaReshapeChangesOnlyTheRequestedMountainPerimeter()
        {
            var document = LoadLayout();
            var reshape = document.constants.unityPerimeterReshape;
            Assert.That(reshape, Is.Not.Null);
            Assert.That(reshape.protectedRadius, Is.EqualTo(96f));
            Assert.That(reshape.rimPeakRadius, Is.EqualTo(142f));
            Assert.That(reshape.rimOuterRadius, Is.EqualTo(205f));
            Assert.That(reshape.shoulderOuterRadius, Is.EqualTo(720f));
            Assert.That(WofMountainVillageLayout.PerimeterShoulderRadius,
                Is.EqualTo(reshape.shoulderOuterRadius));
            Assert.That(reshape.centerX, Is.EqualTo(WofMountainVillageLayout.WorldOrigin.x));
            Assert.That(reshape.centerZ, Is.EqualTo(WofMountainVillageLayout.WorldOrigin.z));

            // These exact summit contracts prove the protected center structures
            // have not moved while the terrain, trail, cliffs, and waterfall outside it reshape.
            Assert.That(document.summitY, Is.EqualTo(WofMountainVillageLayout.ReactSummitY).Within(0.000001f));
            Assert.That(document.constants.summitColliderRadius, Is.EqualTo(reshape.protectedRadius));
            Assert.That(document.layout.interiorHuts, Has.Length.EqualTo(3));
            Assert.That(document.layout.interiorLadders, Has.Length.EqualTo(4));

            var positions = document.geometries.terrain.positions;
            var crestLift = float.MinValue;
            var shoulderLiftSum = 0f;
            var shoulderVertexCount = 0;
            for (var index = 0; index < positions.Length; index += 3)
            {
                var radius = Mathf.Sqrt(positions[index] * positions[index] +
                                        positions[index + 2] * positions[index + 2]);
                var lift = positions[index + 1] - document.baseHeight;
                if (radius >= 125f && radius <= 160f) crestLift = Mathf.Max(crestLift, lift);
                if (radius >= 300f && radius <= 360f)
                {
                    shoulderLiftSum += lift;
                    shoulderVertexCount++;
                }
            }
            Assert.That(crestLift, Is.GreaterThan(250f), "The perimeter must read as a raised caldera rim.");
            Assert.That(shoulderVertexCount, Is.GreaterThan(0));
            Assert.That(shoulderLiftSum / shoulderVertexCount, Is.GreaterThan(140f),
                "The mountain must keep a broad shoulder instead of collapsing into the old dome.");
        }

        [Test]
        public void ReplacementAccessTrailClimbsOneFaceInsteadOfWrappingAroundTheMountain()
        {
            var points = WofMountainAccessPathLayout.BuildHorizontalPoints();
            Assert.That(WofMountainAccessPathLayout.Width, Is.InRange(5f, 6f));
            Assert.That(points, Has.Length.InRange(80, 120));
            Assert.That(points[0].y, Is.InRange(300f, 340f));
            Assert.That(points[^1].magnitude, Is.LessThan(100f));

            var totalLength = 0f;
            var hasLeftSwitchback = false;
            var hasRightSwitchback = false;
            var previousZ = points[0].y;
            for (var index = 0; index < points.Length; index++)
            {
                var point = points[index];
                Assert.That(point.y, Is.GreaterThanOrEqualTo(88f),
                    "The access trail must remain on the south face instead of circling behind the summit.");
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(155f),
                    "The access trail must remain a compact face switchback instead of forming broad rings.");
                Assert.That(point.y, Is.LessThanOrEqualTo(previousZ + 0.01f),
                    "Every trail step must progress up the south face rather than orbiting the mountain.");
                if (index > 0)
                    Assert.That(Vector2.Distance(points[index - 1], point),
                        Is.LessThanOrEqualTo(WofMountainAccessPathLayout.DensifySegmentLength + 0.01f));
                previousZ = point.y;
                hasLeftSwitchback |= point.x < -50f;
                hasRightSwitchback |= point.x > 50f;
                if (index > 0) totalLength += Vector2.Distance(points[index - 1], point);
            }

            Assert.That(hasLeftSwitchback && hasRightSwitchback, Is.True);
            Assert.That(totalLength, Is.InRange(1000f, 1060f));
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
            Assert.That(WofMountainVillageLayout.ViewProbeSpawn,
                Is.EqualTo(new Vector3(1536f, 110f, 900f)));
            Assert.That(WofMountainVillageLayout.ProfileViewProbeSpawn,
                Is.EqualTo(new Vector3(1536f, 245f, 760f)));
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
        public void UnityMountainUsesDirtStoneSnowBandsAndAContinuousFoothillAccessPath()
        {
            const string scenePath = "Assets/WOF/Generated/Scenes/WofMountainVillage.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var renderers = roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                var terrainRenderer = renderers.Single(item => item.name == "BandedMountainTerrain_DirtStoneSnow");
                var mesh = terrainRenderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                var vertices = mesh.vertices;
                var colors = mesh.colors;
                Assert.That(colors, Has.Length.EqualTo(vertices.Length));
                var low = AverageColor(vertices, colors, WofMountainVillageLayout.ReactBaseHeight + 20f,
                    WofMountainVillageLayout.ReactBaseHeight + 88f);
                var lowerFace = AverageColor(vertices, colors,
                    WofMountainVillageLayout.ReactBaseHeight + 28f,
                    WofMountainVillageLayout.ReactBaseHeight + 88f,
                    480f,
                    660f);
                var middle = AverageColor(vertices, colors, WofMountainVillageLayout.ReactBaseHeight + 132f,
                    WofMountainVillageLayout.ReactBaseHeight + 168f);
                var summit = AverageColor(vertices, colors, WofMountainVillageLayout.ReactBaseHeight + 205f,
                    float.PositiveInfinity);
                Assert.That(low.r, Is.GreaterThan(low.b + 0.08f), "The lower half should read as brown dirt.");
                Assert.That(lowerFace.r, Is.GreaterThan(lowerFace.g + 0.04f),
                    "The expanded lower face must stay dirt instead of blending into a broad green hillside.");
                Assert.That(lowerFace.g, Is.GreaterThan(lowerFace.b + 0.04f),
                    "The expanded lower face must retain the warm dirt band.");
                Assert.That(System.Math.Abs(middle.r - middle.g), Is.LessThan(0.12f),
                    "The middle band should read as neutral stone.");
                Assert.That(summit.r, Is.GreaterThan(0.78f));
                Assert.That(summit.g, Is.GreaterThan(0.82f));
                Assert.That(summit.b, Is.GreaterThan(0.86f));

                var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Assert.That(transforms.Any(item => item.name == "MountainWrappingTrail"), Is.False,
                    "The disconnected React spiral must not be instantiated.");
                var access = roots.SelectMany(root =>
                    root.GetComponentsInChildren<WofMountainAccessPathRuntime>(true)).Single();
                Assert.That(access.PointCount, Is.GreaterThanOrEqualTo(36));
                Assert.That(access.StartLocalPoint.z, Is.GreaterThan(300f));
                Assert.That(new Vector2(access.EndLocalPoint.x, access.EndLocalPoint.z).magnitude, Is.LessThan(100f));
                var terrainCollider = roots.SelectMany(root => root.GetComponentsInChildren<MeshCollider>(true))
                    .Single(item => item.name == "ExactMountainTerrainCollider");
                var terrainVertices = terrainCollider.sharedMesh.vertices;
                var terrainTriangles = terrainCollider.sharedMesh.triangles;
                foreach (var horizontalPoint in WofMountainAccessPathLayout.BuildHorizontalPoints())
                {
                    Assert.That(WofMountainAccessPathRuntime.TrySampleTerrainSurfaceHeight(
                            access.transform.parent,
                            terrainCollider.transform,
                            terrainVertices,
                            terrainTriangles,
                            horizontalPoint.x,
                            horizontalPoint.y,
                            out _),
                        Is.True,
                        $"Replacement trail point {horizontalPoint} must project onto the exact mountain terrain.");
                }
                Physics.SyncTransforms();
                Assert.That(access.TryValidate(out var maximumGrade, out var maximumGap, out var misses), Is.True,
                    $"Path continuity failed: grade={maximumGrade:F3}, gap={maximumGap:F2}, misses={misses}.");
                Assert.That(maximumGrade, Is.LessThanOrEqualTo(WofMountainAccessPathLayout.MaximumGrade + 0.015f));
                Assert.That(maximumGap,
                    Is.LessThanOrEqualTo(WofMountainAccessPathLayout.MaximumSegmentLength + 0.05f));
                Assert.That(misses, Is.Zero);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WofMountainSnowRuntime>(true)).Count(),
                    Is.EqualTo(1));
                Assert.That(WofMountainSnowRuntime.DesktopFlakeCount, Is.EqualTo(240));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
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

        private static Color AverageColor(Vector3[] vertices, Color[] colors, float minimumY, float maximumY)
        {
            return AverageColor(vertices, colors, minimumY, maximumY, 0f, float.PositiveInfinity);
        }

        private static Color AverageColor(
            Vector3[] vertices,
            Color[] colors,
            float minimumY,
            float maximumY,
            float minimumRadius,
            float maximumRadius)
        {
            var total = Color.clear;
            var count = 0;
            for (var index = 0; index < vertices.Length; index++)
            {
                if (vertices[index].y < minimumY || vertices[index].y > maximumY) continue;
                var radius = new Vector2(vertices[index].x, vertices[index].z).magnitude;
                if (radius < minimumRadius || radius > maximumRadius) continue;
                total += colors[index];
                count++;
            }
            Assert.That(count, Is.GreaterThan(0),
                $"No mountain color samples in y={minimumY:F1}..{maximumY:F1}, r={minimumRadius:F1}..{maximumRadius:F1}.");
            return total / count;
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
