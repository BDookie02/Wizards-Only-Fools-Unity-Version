using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static void CreateMountainTrail(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var horizontal = WofMountainAccessPathLayout.BuildHorizontalPoints();
            var surfaceHeights = new float[horizontal.Length];
            var points = BuildMountainAccessPathPoints(parent, document, horizontal, surfaceHeights);
            var root = new GameObject("MountainSwitchbackAccessPath");
            root.transform.SetParent(parent, false);

            var deck = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/SwitchbackAccessDeck.asset",
                () => CreateMountainAccessDeckMesh(points, WofMountainAccessPathLayout.Width));
            var top = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/SwitchbackAccessTop.asset",
                () => CreateMountainAccessTopMesh(points, WofMountainAccessPathLayout.Width * 0.94f));
            var colliderMesh = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/SwitchbackAccessCollider.asset",
                () => CreateMountainAccessDeckMesh(points, WofMountainAccessPathLayout.Width * 0.96f));
            var deckVisual = CreateMeshVisual("ContinuousSwitchbackDeck", root.transform, Vector3.zero, deck, materials.TrailDeck);
            deckVisual.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            CreateMeshVisual("DirtStoneWalkingSurface", root.transform, Vector3.up * 0.03f, top, materials.TrailTop);
            var colliderOwner = new GameObject("ContinuousSwitchbackCollider");
            colliderOwner.transform.SetParent(root.transform, false);
            var pathCollider = colliderOwner.AddComponent<MeshCollider>();
            pathCollider.sharedMesh = colliderMesh;
            root.AddComponent<WofMountainAccessPathRuntime>().Configure(points, pathCollider);
            CreateMountainAccessRailsAndSupports(root.transform, points, surfaceHeights, materials);
        }

        private static Vector3[] BuildMountainAccessPathPoints(
            Transform mountainRoot,
            WofMountainVillageDocument document,
            Vector2[] horizontal,
            float[] surfaceHeights)
        {
            Physics.SyncTransforms();
            var points = new Vector3[horizontal.Length];
            for (var index = 0; index < horizontal.Length; index++)
            {
                var local = horizontal[index];
                surfaceHeights[index] = SampleMountainSurfaceHeight(mountainRoot, local.x, local.y);
                points[index] = new Vector3(
                    local.x,
                    surfaceHeights[index] + WofMountainAccessPathLayout.DeckClearance,
                    local.y);
            }

            points[points.Length - 1].y = Mathf.Max(
                points[points.Length - 1].y,
                document.summitY + 1.35f);
            for (var index = 1; index < points.Length; index++)
            {
                points[index].y = Mathf.Max(points[index].y, points[index - 1].y + 0.02f);
            }
            for (var index = points.Length - 2; index >= 0; index--)
            {
                var horizontalDistance = Vector2.Distance(
                    new Vector2(points[index].x, points[index].z),
                    new Vector2(points[index + 1].x, points[index + 1].z));
                var required = points[index + 1].y - horizontalDistance * WofMountainAccessPathLayout.MaximumGrade;
                points[index].y = Mathf.Max(points[index].y, required);
            }
            return points;
        }

        private static float SampleMountainSurfaceHeight(Transform mountainRoot, float localX, float localZ)
        {
            var world = mountainRoot.TransformPoint(new Vector3(localX, 800f, localZ));
            foreach (var hit in Physics.RaycastAll(world, Vector3.down, 1600f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!string.Equals(hit.collider.gameObject.name, "ExactMountainTerrainCollider", StringComparison.Ordinal))
                    continue;
                return mountainRoot.InverseTransformPoint(hit.point).y;
            }
            var worldX = mountainRoot.position.x + localX;
            var worldZ = mountainRoot.position.z + localZ;
            var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
            var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
            return (float)WofSurvivalTerrainMath.GetTerrainHeight(
                chunkX,
                chunkZ,
                worldX - chunkX * WofSurvivalTerrainMath.BlockSize,
                worldZ - chunkZ * WofSurvivalTerrainMath.BlockSize);
        }

        private static Mesh CreateMountainAccessTopMesh(IReadOnlyList<Vector3> points, float width)
        {
            var vertices = new Vector3[points.Count * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(points.Count - 1) * 6];
            for (var index = 0; index < points.Count; index++)
            {
                var right = GetMountainAccessRight(points, index);
                vertices[index * 2] = points[index] - right * (width * 0.5f) + Vector3.up * 0.49f;
                vertices[index * 2 + 1] = points[index] + right * (width * 0.5f) + Vector3.up * 0.49f;
                uvs[index * 2] = new Vector2(0f, index * 0.35f);
                uvs[index * 2 + 1] = new Vector2(1f, index * 0.35f);
                if (index >= points.Count - 1) continue;
                var write = index * 6;
                var current = index * 2;
                triangles[write] = current;
                triangles[write + 1] = current + 2;
                triangles[write + 2] = current + 1;
                triangles[write + 3] = current + 1;
                triangles[write + 4] = current + 2;
                triangles[write + 5] = current + 3;
            }
            var mesh = new Mesh { name = "MountainSwitchbackWalkingSurface" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateMountainAccessDeckMesh(IReadOnlyList<Vector3> points, float width)
        {
            const float topOffset = 0.44f;
            const float bottomOffset = -0.52f;
            var vertices = new Vector3[points.Count * 4];
            var triangles = new List<int>((points.Count - 1) * 24 + 12);
            for (var index = 0; index < points.Count; index++)
            {
                var right = GetMountainAccessRight(points, index);
                var leftPosition = points[index] - right * (width * 0.5f);
                var rightPosition = points[index] + right * (width * 0.5f);
                var write = index * 4;
                vertices[write] = leftPosition + Vector3.up * topOffset;
                vertices[write + 1] = rightPosition + Vector3.up * topOffset;
                vertices[write + 2] = leftPosition + Vector3.up * bottomOffset;
                vertices[write + 3] = rightPosition + Vector3.up * bottomOffset;
                if (index >= points.Count - 1) continue;
                var next = write + 4;
                AddQuad(triangles, write, next, write + 1, next + 1);
                AddQuad(triangles, write + 2, write + 3, next + 2, next + 3);
                AddQuad(triangles, write, write + 2, next, next + 2);
                AddQuad(triangles, write + 1, next + 1, write + 3, next + 3);
            }
            AddQuad(triangles, 0, 1, 2, 3);
            var last = (points.Count - 1) * 4;
            AddQuad(triangles, last, last + 2, last + 1, last + 3);
            var mesh = new Mesh { name = "MountainSwitchbackAccessDeck" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(d);
        }

        private static Vector3 GetMountainAccessRight(IReadOnlyList<Vector3> points, int index)
        {
            var previous = points[Mathf.Max(0, index - 1)];
            var next = points[Mathf.Min(points.Count - 1, index + 1)];
            var tangent = next - previous;
            tangent.y = 0f;
            tangent.Normalize();
            return new Vector3(tangent.z, 0f, -tangent.x);
        }

        private static void CreateMountainAccessRailsAndSupports(
            Transform parent,
            IReadOnlyList<Vector3> points,
            IReadOnlyList<float> surfaceHeights,
            MountainMaterialSet materials)
        {
            for (var index = 0; index < points.Count - 1; index++)
            {
                var nextIndex = index + 1;
                var currentRight = GetMountainAccessRight(points, index);
                var nextRight = GetMountainAccessRight(points, nextIndex);
                foreach (var side in new[] { -1f, 1f })
                {
                    var from = points[index] + currentRight * (WofMountainAccessPathLayout.Width * 0.48f * side) + Vector3.up * 2f;
                    var to = points[nextIndex] + nextRight * (WofMountainAccessPathLayout.Width * 0.48f * side) + Vector3.up * 2f;
                    CreateMountainBeamBetween($"Rail_{index:00}_{side:+0;-0}", parent, from, to, 0.32f, materials.LightWood);
                }
            }

            for (var index = 0; index < points.Count; index += 4)
            {
                var right = GetMountainAccessRight(points, index);
                foreach (var side in new[] { -1f, 1f })
                {
                    var railPosition = points[index] + right * (WofMountainAccessPathLayout.Width * 0.48f * side);
                    MountainBox($"RailPost_{index:00}_{side:+0;-0}", parent,
                        railPosition + Vector3.up, new Vector3(0.55f, 2f, 0.55f), materials.TrailDark);
                    var supportHeight = points[index].y - surfaceHeights[index];
                    if (supportHeight <= 2.2f) continue;
                    MountainBox($"BridgeSupport_{index:00}_{side:+0;-0}", parent,
                        new Vector3(railPosition.x, surfaceHeights[index] + supportHeight * 0.5f, railPosition.z),
                        new Vector3(0.9f, supportHeight, 0.9f), materials.TrailDark);
                }
            }
        }

        private static void CreateMountainBeamBetween(
            string name,
            Transform parent,
            Vector3 from,
            Vector3 to,
            float thickness,
            Material material)
        {
            var direction = to - from;
            var beam = MountainBox(name, parent, (from + to) * 0.5f,
                new Vector3(thickness, thickness, direction.magnitude), material);
            beam.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static Mesh CreateMountainBandedTerrainMesh(WofMountainVillageDocument document)
        {
            var mesh = CreateDesertSerializedMesh("BandedMountainTerrain", document.geometries.terrain);
            var vertices = mesh.vertices;
            ApplyMountainOuterHeightBlend(vertices, document);
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            var original = mesh.colors;
            var colors = new Color[vertices.Length];
            var dirtDark = HexColor("#493724");
            var dirtLight = HexColor("#765738");
            var stoneDark = HexColor("#4b5053");
            var stoneLight = HexColor("#7c878a");
            var snow = HexColor("#eef8ff");
            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var lift = vertex.y - document.baseHeight;
                var radius = new Vector2(vertex.x, vertex.z).magnitude;
                var grain = Mathf.Sin(vertex.x * 0.071f + vertex.z * 0.043f + lift * 0.031f) * 0.5f + 0.5f;
                var dirt = Color.Lerp(dirtDark, dirtLight, grain * 0.72f);
                var stone = Color.Lerp(stoneDark, stoneLight, grain * 0.5f);
                var stoneMix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(102f, 148f, lift));
                var snowMix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(184f, 208f, lift));
                var banded = Color.Lerp(dirt, stone, stoneMix);
                banded = Color.Lerp(banded, snow, snowMix * (0.82f + grain * 0.18f));
                // Keep the mountain's lower face visibly dirt. Only the final
                // perimeter seam blends back into the surrounding grass so the
                // expanded shoulder does not become one broad green hillside.
                var edgeBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(680f, 720f, radius));
                edgeBlend *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 28f, lift));
                var originalColor = original != null && original.Length == vertices.Length ? original[index] : banded;
                colors[index] = Color.Lerp(banded, originalColor, edgeBlend);
            }
            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateMountainBiomeBlendedColliderMesh(WofMountainVillageDocument document)
        {
            var mesh = CreateDesertSerializedMesh(
                "MountainTerrainColliderBiomeBlended",
                document.geometries.terrainCollider);
            var vertices = mesh.vertices;
            ApplyMountainOuterHeightBlend(vertices, document);
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ApplyMountainOuterHeightBlend(
            Vector3[] vertices,
            WofMountainVillageDocument document)
        {
            var reshape = document.constants.unityPerimeterReshape;
            var sourceOuterRadius = 0f;
            for (var index = 0; index < vertices.Length; index++)
            {
                sourceOuterRadius = Mathf.Max(
                    sourceOuterRadius,
                    new Vector2(vertices[index].x, vertices[index].z).magnitude);
            }

            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var radius = new Vector2(vertex.x, vertex.z).magnitude;
                if (radius <= reshape.rimOuterRadius) continue;

                // The React mesh ends at roughly 362 m even though the approved
                // perimeter contract calls for a 720 m shoulder. Expand only the
                // perimeter vertices; the protected summit and all structures
                // inside the rim remain at their exact baked positions.
                var radialBlend = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(reshape.rimOuterRadius, sourceOuterRadius, radius));
                var expandedRadius = Mathf.Lerp(
                    reshape.rimOuterRadius,
                    reshape.shoulderOuterRadius,
                    radialBlend);
                var radialScale = expandedRadius / radius;
                vertex.x *= radialScale;
                vertex.z *= radialScale;

                var heightBlend = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        reshape.rimOuterRadius,
                        reshape.shoulderOuterRadius,
                        expandedRadius));
                var worldX = WofMountainVillageLayout.WorldOrigin.x + vertex.x;
                var worldZ = WofMountainVillageLayout.WorldOrigin.z + vertex.z;
                var chunkX = WofSurvivalTerrainMath.GetChunkCoordinate(worldX);
                var chunkZ = WofSurvivalTerrainMath.GetChunkCoordinate(worldZ);
                var naturalHeight = (float)WofSurvivalTerrainMath.GetTerrainHeight(
                    chunkX,
                    chunkZ,
                    worldX - chunkX * WofSurvivalTerrainMath.BlockSize,
                    worldZ - chunkZ * WofSurvivalTerrainMath.BlockSize);
                vertex.y = Mathf.Lerp(vertex.y, naturalHeight, heightBlend);
                vertices[index] = vertex;
            }
        }

        private static Mesh CreateMountainBaseFringeGrassMesh(WofMountainVillageDocument document)
        {
            var mesh = CreateDesertSerializedMesh("MountainBaseFringeGrass", document.geometries.slopeGrass);
            var vertices = mesh.vertices;
            var sourceTriangles = mesh.triangles;
            var triangles = new List<int>(sourceTriangles.Length / 3);
            for (var index = 0; index < sourceTriangles.Length; index += 3)
            {
                var a = vertices[sourceTriangles[index]];
                var b = vertices[sourceTriangles[index + 1]];
                var c = vertices[sourceTriangles[index + 2]];
                var averageY = (a.y + b.y + c.y) / 3f - document.baseHeight;
                var averageRadius = (new Vector2(a.x, a.z).magnitude + new Vector2(b.x, b.z).magnitude +
                                     new Vector2(c.x, c.z).magnitude) / 3f;
                if (averageY > 58f || averageRadius < 480f) continue;
                triangles.Add(sourceTriangles[index]);
                triangles.Add(sourceTriangles[index + 1]);
                triangles.Add(sourceTriangles[index + 2]);
            }
            mesh.triangles = triangles.ToArray();
            var colors = new Color[vertices.Length];
            for (var index = 0; index < colors.Length; index++)
            {
                var noise = Mathf.Sin(vertices[index].x * 0.09f + vertices[index].z * 0.05f) * 0.5f + 0.5f;
                colors[index] = Color.Lerp(HexColor("#5e6f3b"), HexColor("#839150"), noise);
            }
            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
