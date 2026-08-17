using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    /// <summary>
    /// Fills the narrow square perimeter left between React's circular desert
    /// village pad and its authored 512-metre chunk. The object is generated at
    /// runtime so the protected village scene and all central structures remain
    /// byte-for-byte untouched.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class WofDesertVillageFoundationRuntime : MonoBehaviour
    {
        internal const string FoundationObjectName = "ReactDesertVillageFoundation";
        private const int Segments = 16;
        private const float SurfaceInset = 0.04f;

        private Mesh _mesh;

        internal static WofDesertVillageFoundationRuntime InstallIfNeeded(Transform parent, Material material)
        {
            var existingObject = GameObject.Find(FoundationObjectName);
            if (existingObject != null)
                return existingObject.GetComponent<WofDesertVillageFoundationRuntime>() ??
                       existingObject.AddComponent<WofDesertVillageFoundationRuntime>();
            if (material == null) return null;

            var root = new GameObject(FoundationObjectName)
            {
                hideFlags = HideFlags.DontSave
            };
            root.transform.SetParent(parent, false);
            var runtime = root.AddComponent<WofDesertVillageFoundationRuntime>();
            runtime._mesh = BuildFoundationMesh();

            var filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = runtime._mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            root.AddComponent<MeshCollider>().sharedMesh = runtime._mesh;

            Debug.Log(
                $"[WOF-AUTOMATION] DESERT_FOUNDATION_READY vertices={runtime._mesh.vertexCount} " +
                $"baseHeight={WofDesertVillageLayout.ReactBaseHeight:F3}");
            return runtime;
        }

        internal static Mesh BuildFoundationMeshForTests()
        {
            return BuildFoundationMesh();
        }

        private static Mesh BuildFoundationMesh()
        {
            var gridSize = Segments + 1;
            var vertices = new Vector3[gridSize * gridSize];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var indices = new int[Segments * Segments * 6];
            var half = WofDesertVillageLayout.SurvivalBlockSize * 0.5f;
            var step = WofDesertVillageLayout.SurvivalBlockSize / Segments;
            var cursor = 0;
            for (var zIndex = 0; zIndex <= Segments; zIndex++)
            {
                var localZ = -half + zIndex * step;
                for (var xIndex = 0; xIndex <= Segments; xIndex++)
                {
                    var localX = -half + xIndex * step;
                    var worldX = WofDesertVillageLayout.WorldOrigin.x + localX;
                    var worldZ = WofDesertVillageLayout.WorldOrigin.z + localZ;
                    vertices[cursor] = new Vector3(
                        worldX,
                        WofDesertVillageLayout.ReactBaseHeight - SurfaceInset,
                        worldZ);
                    colors[cursor] = WofSurvivalTerrainMath.GetRenderedTerrainColor(
                        worldX,
                        worldZ,
                        WofDesertVillageLayout.ReactBaseHeight);
                    uvs[cursor] = new Vector2(
                        worldX / (float)WofSurvivalTerrainMath.DetailUvWorldSize,
                        worldZ / (float)WofSurvivalTerrainMath.DetailUvWorldSize);
                    cursor++;
                }
            }

            cursor = 0;
            for (var zIndex = 0; zIndex < Segments; zIndex++)
            for (var xIndex = 0; xIndex < Segments; xIndex++)
            {
                var a = zIndex * gridSize + xIndex;
                var b = a + 1;
                var c = a + gridSize;
                var d = c + 1;
                indices[cursor++] = a;
                indices[cursor++] = c;
                indices[cursor++] = b;
                indices[cursor++] = b;
                indices[cursor++] = c;
                indices[cursor++] = d;
            }

            var mesh = new Mesh
            {
                name = "ReactDesertVillageFoundation_Runtime",
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                colors = colors,
                uv = uvs,
                triangles = indices
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
