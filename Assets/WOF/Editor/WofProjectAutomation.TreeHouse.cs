using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string TreeHouseDodecaMeshPath = GeometryRoot + "/ReactTreeHouseDodeca.asset";
        private const string TreeHouseDodecaEdgeMeshPath = GeometryRoot + "/ReactTreeHouseDodecaEdges.asset";

        private static void CreateTreeHouseVillage(Transform parent, WofMaterialPalette palette)
        {
            var village = new GameObject("TreeHouseVillage");
            village.transform.SetParent(parent, false);
            var dodeca = GetOrCreateMeshAsset(TreeHouseDodecaMeshPath, CreateTreeHouseDodecaMesh);
            var dodecaEdges = GetOrCreateMeshAsset(TreeHouseDodecaEdgeMeshPath, CreateTreeHouseDodecaEdgeMesh);

            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                CreateTreeHouseGiantTree(village.transform, treeIndex, palette, dodeca, dodecaEdges);
            }

            for (var treeIndex = 0; treeIndex < WofTreeHouseVillageLayout.Trees.Count; treeIndex++)
            {
                foreach (var connection in WofTreeHouseVillageLayout.InternalRopes)
                {
                    CreateTreeHouseRope(
                        village.transform,
                        $"InternalRope_{treeIndex}_{connection.StartHouse}_{connection.EndHouse}",
                        WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.StartHouse),
                        WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, connection.EndHouse),
                        palette);
                }

                CreateTreeHouseRope(
                    village.transform,
                    $"GroundRope_{treeIndex}",
                    WofTreeHouseVillageLayout.GetTreeBasePosition(treeIndex),
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(treeIndex, 0),
                    palette);
            }

            for (var bridgeIndex = 0; bridgeIndex < WofTreeHouseVillageLayout.Bridges.Count; bridgeIndex++)
            {
                var connection = WofTreeHouseVillageLayout.Bridges[bridgeIndex];
                CreateTreeHouseBridge(
                    village.transform,
                    $"Bridge_{bridgeIndex}_{connection.StartTree}_{connection.EndTree}",
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(connection.StartTree, connection.StartHouse),
                    WofTreeHouseVillageLayout.GetHouseBalconyPosition(connection.EndTree, connection.EndHouse),
                    palette);
            }

            MarkStatic(village);
        }

        private static void CreateTreeHouseGiantTree(
            Transform parent,
            int treeIndex,
            WofMaterialPalette palette,
            Mesh dodeca,
            Mesh dodecaEdges)
        {
            var placement = WofTreeHouseVillageLayout.Trees[treeIndex];
            var tree = new GameObject($"GiantTree_{treeIndex}");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = placement.Position;
            tree.transform.localRotation = Quaternion.Euler(0f, placement.YawRadians * Mathf.Rad2Deg, 0f);

            CreatePrimitive("TrunkMain", PrimitiveType.Cube, tree.transform,
                new Vector3(0f, 20f, 0f), new Vector3(10f, 40f, 10f), palette.TreeHouseBark);
            var twistedTrunk = CreatePrimitive("TrunkTwisted", PrimitiveType.Cube, tree.transform,
                new Vector3(2f, 20f, 2f), new Vector3(8f, 40f, 8f), palette.TreeHouseBark);
            twistedTrunk.transform.localRotation = Quaternion.Euler(0f, 0.5f * Mathf.Rad2Deg, 0f);

            var rootPlacements = new[]
            {
                (Position: new Vector3(4f, 0f, 4f), Yaw: Mathf.PI * 0.25f),
                (Position: new Vector3(-4f, 0f, -4f), Yaw: -Mathf.PI * 0.75f),
                (Position: new Vector3(-4f, 0f, 4f), Yaw: -Mathf.PI * 0.25f),
                (Position: new Vector3(4f, 0f, -4f), Yaw: Mathf.PI * 0.75f)
            };
            for (var rootIndex = 0; rootIndex < rootPlacements.Length; rootIndex++)
            {
                var rootPlacement = rootPlacements[rootIndex];
                var root = new GameObject($"Root_{rootIndex}");
                root.transform.SetParent(tree.transform, false);
                root.transform.localPosition = rootPlacement.Position;
                root.transform.localRotation = Quaternion.Euler(0f, rootPlacement.Yaw * Mathf.Rad2Deg, 0f);
                var rootBlock = CreatePrimitive("Block", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, -2f, 4f), new Vector3(4f, 4f, 15f), palette.TreeHouseBark);
                rootBlock.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            }

            CreateTreeHouseSpiralVariant(
                tree.transform,
                palette,
                mobileOnly: false,
                steps: WofTreeHouseVillageLayout.DesktopSpiralStepCount);
            CreateTreeHouseSpiralVariant(
                tree.transform,
                palette,
                mobileOnly: true,
                steps: WofTreeHouseVillageLayout.MobileSpiralStepCount);

            for (var houseIndex = 0; houseIndex < WofTreeHouseVillageLayout.Houses.Count; houseIndex++)
            {
                CreateTreeHouse(tree.transform, treeIndex, houseIndex, palette);
            }

            CreateTreeHouseCanopy(tree.transform, palette, dodeca, dodecaEdges);
            MarkStatic(tree);
        }

        private static void CreateTreeHouseSpiralVariant(
            Transform parent,
            WofMaterialPalette palette,
            bool mobileOnly,
            int steps)
        {
            var variant = CreateTreeHousePerformanceVariant(
                parent,
                mobileOnly ? "SpiralStepsMobile" : "SpiralStepsDesktop",
                mobileOnly);
            foreach (var step in WofTreeHouseVillageLayout.BuildSpiralSteps(steps: steps))
            {
                var stepObject = CreatePrimitive(
                    $"SpiralStep_{(mobileOnly ? "Mobile" : "Desktop")}_{step.Index:00}",
                    PrimitiveType.Cube,
                    variant,
                    step.Position,
                    new Vector3(3f, 0.2f, 1.5f),
                    palette.TreeHousePlank);
                stepObject.transform.localRotation = Quaternion.Euler(0f, step.YawRadians * Mathf.Rad2Deg, 0f);
            }
        }

        private static void CreateTreeHouse(
            Transform parent,
            int treeIndex,
            int houseIndex,
            WofMaterialPalette palette)
        {
            var spec = WofTreeHouseVillageLayout.Houses[houseIndex];
            var house = new GameObject($"House_{treeIndex}_{houseIndex}");
            house.transform.SetParent(parent, false);
            house.transform.localPosition = spec.Position;
            house.transform.localRotation = Quaternion.Euler(0f, spec.YawRadians * Mathf.Rad2Deg, 0f);
            house.transform.localScale = Vector3.one * spec.Scale;

            CreatePrimitive("Body", PrimitiveType.Cube, house.transform,
                Vector3.zero, new Vector3(5f, 5f, 5f), palette.TreeHousePlank);
            CreatePrimitive("Roof", PrimitiveType.Cube, house.transform,
                new Vector3(0f, 3f, 0f), new Vector3(6f, 2f, 6f), palette.TreeHouseRoof);
            CreatePrimitive("Balcony", PrimitiveType.Cube, house.transform,
                new Vector3(0f, -2.5f, 0f), new Vector3(7f, 0.5f, 7f), palette.TreeHousePlank);

            CreateTreeHouseWindow(house.transform, "WindowFront", new Vector3(0f, 0.5f, 2.51f), 0f, palette);
            CreateTreeHouseWindow(house.transform, "WindowRight", new Vector3(2.51f, 0.5f, 0f), 90f, palette);
            CreateTreeHouseWindow(house.transform, "WindowLeft", new Vector3(-2.51f, 0.5f, 0f), -90f, palette);
        }

        private static void CreateTreeHouseWindow(
            Transform parent,
            string name,
            Vector3 position,
            float yawDegrees,
            WofMaterialPalette palette)
        {
            var window = new GameObject(name);
            window.transform.SetParent(parent, false);
            window.transform.localPosition = position;
            window.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            CreateVisualPrimitive("Glow", PrimitiveType.Cube, window.transform,
                Vector3.zero, new Vector3(1f, 1.2f, 0.025f), palette.TreeHouseWindowGlow);
            var desktopDetails = CreateTreeHousePerformanceVariant(
                window.transform,
                "DesktopDetails",
                mobileOnly: false);
            CreateVisualPrimitive("FrameVertical", PrimitiveType.Cube, desktopDetails,
                new Vector3(0f, 0f, 0.05f), new Vector3(0.1f, 1.2f, 0.1f), palette.TreeHouseBark);
            CreateVisualPrimitive("FrameHorizontal", PrimitiveType.Cube, desktopDetails,
                new Vector3(0f, 0f, 0.05f), new Vector3(1f, 0.1f, 0.1f), palette.TreeHouseBark);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(desktopDetails, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(255, 179, 71, 255);
            light.intensity = 3f;
            light.range = 15f;
            light.shadows = LightShadows.None;
        }

        private static void CreateTreeHouseCanopy(
            Transform parent,
            WofMaterialPalette palette,
            Mesh dodeca,
            Mesh dodecaEdges)
        {
            var blocks = new[]
            {
                (Position: new Vector3(0f, 40f, 0f), Size: new Vector3(30f, 15f, 30f), Material: 0),
                (Position: new Vector3(12f, 35f, 10f), Size: new Vector3(20f, 15f, 20f), Material: 1),
                (Position: new Vector3(-15f, 38f, -12f), Size: new Vector3(25f, 20f, 25f), Material: 2),
                (Position: new Vector3(-10f, 36f, 15f), Size: new Vector3(20f, 12f, 20f), Material: 3),
                (Position: new Vector3(16f, 43f, -8f), Size: new Vector3(17f, 10f, 18f), Material: 4),
                (Position: new Vector3(-4f, 43.5f, 8f), Size: new Vector3(16f, 7f, 14f), Material: 5)
            };

            for (var index = 0; index < blocks.Length; index++)
            {
                var block = blocks[index];
                var group = new GameObject($"CanopyBlock_{index}");
                group.transform.SetParent(parent, false);
                group.transform.localPosition = block.Position;

                CreateTreeHouseDodecaVisual(
                    group.transform,
                    "Body",
                    Vector3.zero,
                    Vector3.Scale(block.Size, Vector3.one * 0.52f),
                    palette.TreeHouseLeaves[block.Material],
                    dodeca,
                    dodecaEdges,
                    palette.TreeHouseLeafEdge);

                var desktopDetail = CreateTreeHousePerformanceVariant(
                    group.transform,
                    "DesktopDetail",
                    mobileOnly: false);
                CreateTreeHouseDodecaVisual(
                    desktopDetail,
                    "Detail",
                    new Vector3(0f, block.Size.y * 0.18f, -block.Size.z * 0.28f),
                    new Vector3(block.Size.x * 0.32f, block.Size.y * 0.12f, block.Size.z * 0.16f),
                    palette.TreeHouseDetailLeaf,
                    dodeca,
                    dodecaEdges,
                    palette.TreeHouseLeafEdge);
            }
        }

        private static void CreateTreeHouseDodecaVisual(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material bodyMaterial,
            Mesh dodeca,
            Mesh dodecaEdges,
            Material edgeMaterial)
        {
            var body = CreateMeshVisual(name, parent, position, dodeca, bodyMaterial);
            body.transform.localScale = scale;
            var edge = CreateMeshVisual(name + "Edges", parent, position, dodecaEdges, edgeMaterial);
            edge.transform.localScale = scale * 1.01f;
        }

        private static void CreateTreeHouseBridge(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            WofMaterialPalette palette)
        {
            var span = WofTreeHouseVillageLayout.GetSpan(start, end);
            var bridge = new GameObject(name);
            bridge.transform.SetParent(parent, false);
            bridge.transform.localPosition = span.Position;
            bridge.transform.localRotation = span.Rotation;
            CreatePrimitive("Walkway", PrimitiveType.Cube, bridge.transform,
                Vector3.zero, new Vector3(4f, 0.5f, span.Length), palette.TreeHousePlank);
            CreatePrimitive("RailRight", PrimitiveType.Cube, bridge.transform,
                new Vector3(2f, 1f, 0f), new Vector3(0.2f, 0.2f, span.Length), palette.TreeHouseBark);
            CreatePrimitive("RailLeft", PrimitiveType.Cube, bridge.transform,
                new Vector3(-2f, 1f, 0f), new Vector3(0.2f, 0.2f, span.Length), palette.TreeHouseBark);
            MarkStatic(bridge);
        }

        private static void CreateTreeHouseRope(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            WofMaterialPalette palette)
        {
            var span = WofTreeHouseVillageLayout.GetSpan(start, end);
            var rope = new GameObject(name);
            rope.transform.SetParent(parent, false);
            rope.transform.localPosition = span.Position;
            rope.transform.localRotation = span.Rotation;
            AddBoxCollider(rope, Vector3.zero, new Vector3(1.1f, 0.28f, span.Length));

            var strand = CreateVisualPrimitive("Strand", PrimitiveType.Cylinder, rope.transform,
                Vector3.zero, new Vector3(0.4f, span.Length * 0.5f, 0.4f), palette.TreeHouseRope);
            strand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateTreeHouseRungVariant(
                rope.transform,
                palette,
                span.Length,
                mobileOnly: false,
                rungStep: WofTreeHouseVillageLayout.DesktopRopeRungStep);
            CreateTreeHouseRungVariant(
                rope.transform,
                palette,
                span.Length,
                mobileOnly: true,
                rungStep: WofTreeHouseVillageLayout.MobileRopeRungStep);
            MarkStatic(rope);
        }

        private static void CreateTreeHouseRungVariant(
            Transform parent,
            WofMaterialPalette palette,
            float length,
            bool mobileOnly,
            float rungStep)
        {
            var variant = CreateTreeHousePerformanceVariant(
                parent,
                mobileOnly ? "RungsMobile" : "RungsDesktop",
                mobileOnly);
            foreach (var rung in WofTreeHouseVillageLayout.BuildRopeRungs(length, rungStep))
            {
                CreateVisualPrimitive(
                    $"Rung_{(mobileOnly ? "Mobile" : "Desktop")}_{rung.Index:00}",
                    PrimitiveType.Cube,
                    variant,
                    rung.Position,
                    new Vector3(1.5f, 0.2f, 0.2f),
                    palette.TreeHouseBark);
            }
        }

        private static Transform CreateTreeHousePerformanceVariant(
            Transform parent,
            string name,
            bool mobileOnly)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<WofPerformanceVariantGroup>().Configure(mobileOnly);
            return root.transform;
        }

        private static Mesh CreateTreeHouseDodecaMesh()
        {
            var data = BuildTreeHouseDodecaData();
            var vertices = new List<Vector3>(data.Faces.Count * 5);
            var uvs = new List<Vector2>(data.Faces.Count * 5);
            var triangles = new List<int>(data.Faces.Count * 9);
            foreach (var face in data.Faces)
            {
                var start = vertices.Count;
                for (var index = 0; index < face.Length; index++)
                {
                    vertices.Add(data.Vertices[face[index]]);
                    var angle = index / 5f * Mathf.PI * 2f;
                    uvs.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f + Vector2.one * 0.5f);
                }
                for (var index = 1; index < face.Length - 1; index++)
                {
                    triangles.Add(start);
                    triangles.Add(start + index);
                    triangles.Add(start + index + 1);
                }
            }
            return BuildMesh("ReactTreeHouseDodeca", vertices, uvs, triangles);
        }

        private static Mesh CreateTreeHouseDodecaEdgeMesh()
        {
            var data = BuildTreeHouseDodecaData();
            var edges = new HashSet<(int A, int B)>();
            foreach (var face in data.Faces)
            {
                for (var index = 0; index < face.Length; index++)
                {
                    var a = face[index];
                    var b = face[(index + 1) % face.Length];
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            }

            var mesh = new Mesh { name = "ReactTreeHouseDodecaEdges" };
            mesh.SetVertices(data.Vertices);
            mesh.SetIndices(edges.SelectMany(edge => new[] { edge.A, edge.B }).ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static DodecaData BuildTreeHouseDodecaData()
        {
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var inversePhi = 1f / phi;
            var vertices = new List<Vector3>(20);
            foreach (var x in new[] { -1f, 1f })
            foreach (var y in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                vertices.Add(new Vector3(x, y, z).normalized);
            foreach (var y in new[] { -inversePhi, inversePhi })
            foreach (var z in new[] { -phi, phi })
                vertices.Add(new Vector3(0f, y, z).normalized);
            foreach (var x in new[] { -inversePhi, inversePhi })
            foreach (var y in new[] { -phi, phi })
                vertices.Add(new Vector3(x, y, 0f).normalized);
            foreach (var x in new[] { -phi, phi })
            foreach (var z in new[] { -inversePhi, inversePhi })
                vertices.Add(new Vector3(x, 0f, z).normalized);

            var planes = new List<(Vector3 Normal, float Distance)>();
            const float sideEpsilon = 0.0001f;
            for (var a = 0; a < vertices.Count - 2; a++)
            for (var b = a + 1; b < vertices.Count - 1; b++)
            for (var c = b + 1; c < vertices.Count; c++)
            {
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude < 0.000001f) continue;
                normal.Normalize();
                var positive = false;
                var negative = false;
                for (var index = 0; index < vertices.Count; index++)
                {
                    var side = Vector3.Dot(normal, vertices[index] - vertices[a]);
                    if (side > sideEpsilon) positive = true;
                    if (side < -sideEpsilon) negative = true;
                }
                if (positive && negative) continue;
                if (positive) normal = -normal;
                var distance = Vector3.Dot(normal, vertices[a]);
                if (distance < 0f)
                {
                    normal = -normal;
                    distance = -distance;
                }
                if (planes.Any(plane => Vector3.Dot(plane.Normal, normal) > 0.9999f &&
                                           Mathf.Abs(plane.Distance - distance) < 0.0001f))
                {
                    continue;
                }
                planes.Add((normal, distance));
            }

            var faces = new List<int[]>(12);
            foreach (var plane in planes)
            {
                var indices = Enumerable.Range(0, vertices.Count)
                    .Where(index => Mathf.Abs(Vector3.Dot(plane.Normal, vertices[index]) - plane.Distance) < 0.001f)
                    .ToArray();
                if (indices.Length != 5) continue;
                var center = Vector3.zero;
                foreach (var index in indices) center += vertices[index];
                center /= indices.Length;
                var axis = (vertices[indices[0]] - center).normalized;
                var bitangent = Vector3.Cross(plane.Normal, axis).normalized;
                indices = indices.OrderBy(index =>
                {
                    var delta = vertices[index] - center;
                    return Mathf.Atan2(Vector3.Dot(delta, bitangent), Vector3.Dot(delta, axis));
                }).ToArray();
                if (Vector3.Dot(
                        Vector3.Cross(vertices[indices[1]] - vertices[indices[0]], vertices[indices[2]] - vertices[indices[0]]),
                        plane.Normal) < 0f)
                {
                    Array.Reverse(indices);
                }
                faces.Add(indices);
            }

            if (faces.Count != 12)
            {
                throw new InvalidOperationException($"Expected 12 dodecahedron faces, generated {faces.Count}.");
            }
            return new DodecaData(vertices, faces);
        }

        private sealed class DodecaData
        {
            public DodecaData(List<Vector3> vertices, List<int[]> faces)
            {
                Vertices = vertices;
                Faces = faces;
            }

            public List<Vector3> Vertices { get; }
            public List<int[]> Faces { get; }
        }
    }
}
