using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string SurvivalTerrainDocumentPath =
            "Assets/WOF/Art/Generated/React/SurvivalTerrain/base-region.json";
        private const string SurvivalTerrainGeometryRoot = GeometryRoot + "/SurvivalTerrain";
        private const string SurvivalTerrainMeshPath = SurvivalTerrainGeometryRoot + "/BaseRegion.asset";
        private const string SurvivalTerrainMaterialPath = MaterialsRoot + "/SurvivalOpenWorldTerrain.mat";

        [Serializable]
        private sealed class WofSurvivalTerrainDocument
        {
            public int schemaVersion;
            public string generator;
            public string sourceSignature;
            public int blockSize;
            public int radius;
            public int segments;
            public string[] includedChunks;
            public string[] skippedChunks;
            public WofSerializedMeshRecord mesh;
        }

        private static void CreateSurvivalOpenWorldTerrain(Transform parent)
        {
            var source = LoadRequiredAsset<TextAsset>(SurvivalTerrainDocumentPath);
            var document = JsonUtility.FromJson<WofSurvivalTerrainDocument>(source.text);
            if (document == null || document.schemaVersion != 1 || document.blockSize != 512 ||
                document.radius != 3 || document.segments != 32 ||
                document.includedChunks == null || document.includedChunks.Length != 45 ||
                document.skippedChunks == null || document.skippedChunks.Length != 4 ||
                string.IsNullOrWhiteSpace(document.sourceSignature) || document.sourceSignature.Length != 64 ||
                !IsValidSurvivalTerrainMesh(document.mesh))
            {
                throw new InvalidOperationException(
                    $"Invalid exact React survival terrain document at {SurvivalTerrainDocumentPath}.");
            }

            EnsureAssetFolder(SurvivalTerrainGeometryRoot);
            var mesh = GetOrCreateMeshAsset(
                SurvivalTerrainMeshPath,
                () => CreateDesertSerializedMesh("ReactSurvivalOpenWorldBaseRegion", document.mesh));
            var material = GetOrCreateSurvivalTerrainMaterial();
            var terrain = new GameObject("ReactSurvivalOpenWorldBaseRegion");
            terrain.transform.SetParent(parent, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
            MarkStatic(terrain);
        }

        private static bool IsValidSurvivalTerrainMesh(WofSerializedMeshRecord mesh)
        {
            return mesh != null && mesh.vertexCount == 49005 && mesh.indexCount == 276480 &&
                   mesh.positions?.Length == mesh.vertexCount * 3 &&
                   mesh.colors?.Length == mesh.vertexCount * 3 &&
                   mesh.uvs?.Length == mesh.vertexCount * 2 &&
                   mesh.indices?.Length == mesh.indexCount;
        }

        private static Material GetOrCreateSurvivalTerrainMaterial()
        {
            var shader = Shader.Find("WOF/Vertex Color Texture");
            if (shader == null)
            {
                throw new InvalidOperationException("Required survival terrain vertex-color shader was not imported.");
            }
            var texture = LoadRequiredAsset<Texture2D>(
                "Assets/WOF/Art/Generated/React/SwampVillage/Textures/terrain-detail.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(SurvivalTerrainMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SurvivalOpenWorldTerrain" };
                AssetDatabase.CreateAsset(material, SurvivalTerrainMaterialPath);
            }
            else
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.enableInstancing = true;
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
