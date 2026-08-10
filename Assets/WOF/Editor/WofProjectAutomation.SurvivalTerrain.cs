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
        private const string SurvivalFoliageMaterialPath = MaterialsRoot + "/SurvivalReactFoliage.mat";
        private const string SurvivalStreamWaterMaterialPath = "Assets/WOF/Resources/SurvivalStreamWater.mat";
        private const string SurvivalFoliageGeometryRoot = SurvivalTerrainGeometryRoot + "/Foliage";

        [Serializable]
        private sealed class WofSurvivalTerrainDocument
        {
            public int schemaVersion;
            public string generator;
            public string sourceSignature;
            public int blockSize;
            public int segments;
            public WofSurvivalTerrainBounds bounds;
            public string[] includedChunks;
            public string[] skippedChunks;
            public WofSurvivalStreamingOracle streamingOracle;
            public WofSurvivalFoliageDocument foliage;
            public WofSerializedMeshRecord mesh;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingOracle
        {
            public string source;
            public int renderRadius;
            public int nearRadius;
            public int collisionRadius;
            public float centerHysteresis;
            public WofSurvivalStreamingChunkCoordinate[] chunkCoordinates;
            public WofSurvivalStreamingWindowChunk[] window;
            public WofSurvivalStreamingChunkFixture[] chunks;
            public WofSurvivalStreamingTreeFixture[] trees;
            public WofSurvivalStreamingWaterFixture[] waters;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingChunkCoordinate
        {
            public float value;
            public int chunk;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingWindowChunk
        {
            public int dx;
            public int dz;
            public int distance;
            public string lod;
            public int renderSegments;
            public int collisionSegments;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingChunkFixture
        {
            public int cx;
            public int cz;
            public string biome;
            public bool hasRiver;
            public bool riverVertical;
            public WofSurvivalStreamingSample[] samples;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingSample
        {
            public float localX;
            public float localZ;
            public float height;
            public float colorR;
            public float colorG;
            public float colorB;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingTreeFixture
        {
            public int cx;
            public int cz;
            public int distance;
            public string lod;
            public WofSurvivalFoliagePlacementRecord[] trees;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingWaterFixture
        {
            public int cx;
            public int cz;
            public int distance;
            public string lod;
            public int riverVertexCount;
            public int riverIndexCount;
            public WofSurvivalStreamingWaterPosition[] riverPositionSamples;
            public WofSurvivalStreamingPond[] ponds;
            public WofSurvivalStreamingLily[] lilies;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingWaterPosition
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingPond
        {
            public float localX;
            public float localZ;
            public float radiusX;
            public float radiusZ;
            public float y;
        }

        [Serializable]
        private sealed class WofSurvivalStreamingLily
        {
            public float localX;
            public float localZ;
            public float scale;
        }

        [Serializable]
        private sealed class WofSurvivalFoliageDocument
        {
            public string source;
            public int meshCount;
            public int placementCount;
            public WofSurvivalFoliageBiomeCounts countsByBiome;
            public WofSurvivalFoliageMeshRecord[] meshes;
            public WofSurvivalFoliagePlacementRecord[] placements;
        }

        [Serializable]
        private sealed class WofSurvivalFoliageBiomeCounts
        {
            public int plains;
            public int jungle;
            public int desert;
            public int swamp;
            public int mushroom;
            public int tallgrass;
        }

        [Serializable]
        private sealed class WofSurvivalFoliageMeshRecord
        {
            public string biome;
            public int variant;
            public WofSerializedMeshRecord mesh;
        }

        [Serializable]
        private sealed class WofSurvivalFoliagePlacementRecord
        {
            public int meshIndex;
            public string biome;
            public float x;
            public float y;
            public float z;
            public float pitch;
            public float yaw;
            public float roll;
            public float scaleX;
            public float scaleY;
            public float scaleZ;
        }

        [Serializable]
        private sealed class WofSurvivalTerrainBounds
        {
            public int minimumChunkX;
            public int maximumChunkX;
            public int minimumChunkZ;
            public int maximumChunkZ;
        }

        private static void CreateSurvivalOpenWorldTerrain(Transform parent)
        {
            var source = LoadRequiredAsset<TextAsset>(SurvivalTerrainDocumentPath);
            var document = JsonUtility.FromJson<WofSurvivalTerrainDocument>(source.text);
            if (document == null || document.schemaVersion != 2 || document.blockSize != 512 ||
                document.segments != 32 || document.bounds == null ||
                document.bounds.minimumChunkX != -4 || document.bounds.maximumChunkX != 6 ||
                document.bounds.minimumChunkZ != -4 || document.bounds.maximumChunkZ != 3 ||
                document.includedChunks == null || document.includedChunks.Length != 82 ||
                document.skippedChunks == null || document.skippedChunks.Length != 6 ||
                string.IsNullOrWhiteSpace(document.sourceSignature) || document.sourceSignature.Length != 64 ||
                !IsValidSurvivalStreamingOracle(document.streamingOracle) ||
                !IsValidSurvivalTerrainMesh(document.mesh) || !IsValidSurvivalFoliage(document.foliage))
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
            var foliageRuntime = CreateSurvivalFoliage(parent, document.foliage);
            var streamingObject = new GameObject("ReactSurvivalTerrainStreamingRuntime");
            streamingObject.transform.SetParent(parent, false);
            streamingObject.AddComponent<WofSurvivalTerrainStreamingRuntime>().Configure(
                material,
                foliageRuntime,
                GetOrCreateSurvivalStreamWaterMaterial());
        }

        private static bool IsValidSurvivalStreamingOracle(WofSurvivalStreamingOracle oracle)
        {
            if (oracle == null || string.IsNullOrWhiteSpace(oracle.source) ||
                oracle.renderRadius != WofSurvivalTerrainMath.RenderRadius ||
                oracle.nearRadius != WofSurvivalTerrainMath.NearRadius ||
                oracle.collisionRadius != WofSurvivalTerrainMath.CollisionRadius ||
                Math.Abs(oracle.centerHysteresis - WofSurvivalTerrainMath.CenterHysteresis) > 0.001d ||
                oracle.chunkCoordinates?.Length != 10 || oracle.window?.Length != 37 ||
                oracle.chunks?.Length != 6 || oracle.trees?.Length != 3 || oracle.waters?.Length != 3)
                return false;
            foreach (var chunk in oracle.chunks)
            {
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.biome) || chunk.samples?.Length != 5)
                    return false;
            }
            foreach (var treeFixture in oracle.trees)
            {
                if (treeFixture == null || string.IsNullOrWhiteSpace(treeFixture.lod) || treeFixture.trees == null)
                    return false;
            }
            foreach (var waterFixture in oracle.waters)
            {
                if (waterFixture == null || string.IsNullOrWhiteSpace(waterFixture.lod) ||
                    waterFixture.riverPositionSamples == null || waterFixture.ponds == null ||
                    waterFixture.lilies == null)
                    return false;
            }
            return true;
        }

        private static bool IsValidSurvivalFoliage(WofSurvivalFoliageDocument foliage)
        {
            if (foliage == null || foliage.meshCount != WofSurvivalFoliageRuntime.ExactReactMeshCount ||
                foliage.placementCount != WofSurvivalFoliageRuntime.ExactReactDenseTreeCount ||
                foliage.meshes?.Length != foliage.meshCount || foliage.placements?.Length != foliage.placementCount ||
                foliage.countsByBiome == null || foliage.countsByBiome.plains != 594 ||
                foliage.countsByBiome.jungle != 360 || foliage.countsByBiome.desert != 380 ||
                foliage.countsByBiome.swamp != 490 || foliage.countsByBiome.mushroom != 372 ||
                foliage.countsByBiome.tallgrass != 330)
                return false;
            foreach (var mesh in foliage.meshes)
            {
                if (mesh == null || string.IsNullOrWhiteSpace(mesh.biome) || mesh.variant < 0 || mesh.variant > 3 ||
                    mesh.mesh == null || mesh.mesh.vertexCount <= 0 || mesh.mesh.positions?.Length != mesh.mesh.vertexCount * 3 ||
                    mesh.mesh.normals?.Length != mesh.mesh.vertexCount * 3 ||
                    mesh.mesh.uvs?.Length != mesh.mesh.vertexCount * 2 ||
                    mesh.mesh.colors?.Length != mesh.mesh.vertexCount * 3 || mesh.mesh.indices == null ||
                    mesh.mesh.indices.Length <= 0)
                    return false;
            }
            return true;
        }

        private static WofSurvivalFoliageRuntime CreateSurvivalFoliage(
            Transform parent,
            WofSurvivalFoliageDocument foliage)
        {
            EnsureAssetFolder(SurvivalFoliageGeometryRoot);
            var meshes = new Mesh[foliage.meshes.Length];
            for (var index = 0; index < foliage.meshes.Length; index++)
            {
                var record = foliage.meshes[index];
                meshes[index] = GetOrCreateMeshAsset(
                    $"{SurvivalFoliageGeometryRoot}/{index:00}_{record.biome}_{record.variant}.asset",
                    () => CreateDesertSerializedMesh(
                        $"ReactSurvivalTree_{record.biome}_{record.variant}",
                        record.mesh));
            }

            var placements = new WofSurvivalFoliagePlacement[foliage.placements.Length];
            for (var index = 0; index < foliage.placements.Length; index++)
            {
                var source = foliage.placements[index];
                placements[index] = new WofSurvivalFoliagePlacement
                {
                    meshIndex = source.meshIndex,
                    x = source.x,
                    y = source.y,
                    z = source.z,
                    pitch = source.pitch,
                    yaw = source.yaw,
                    roll = source.roll,
                    scaleX = source.scaleX,
                    scaleY = source.scaleY,
                    scaleZ = source.scaleZ
                };
            }

            var runtimeObject = new GameObject("ReactSurvivalBiomeTreeGroves_2526");
            runtimeObject.transform.SetParent(parent, false);
            var runtime = runtimeObject.AddComponent<WofSurvivalFoliageRuntime>();
            runtime.Configure(
                meshes,
                GetOrCreateSurvivalFoliageMaterial(),
                placements);
            return runtime;
        }

        private static bool IsValidSurvivalTerrainMesh(WofSerializedMeshRecord mesh)
        {
            return mesh != null && mesh.vertexCount == 89298 && mesh.indexCount == 503808 &&
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

        private static Material GetOrCreateSurvivalFoliageMaterial()
        {
            var shader = Shader.Find("WOF/Instanced Foliage");
            if (shader == null) throw new InvalidOperationException("Required foliage vertex-color shader was not imported.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(SurvivalFoliageMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SurvivalReactFoliage" };
                AssetDatabase.CreateAsset(material, SurvivalFoliageMaterialPath);
            }
            else material.shader = shader;
            material.SetColor("_Color", Color.white);
            material.enableInstancing = true;
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateSurvivalStreamWaterMaterial()
        {
            var shader = Shader.Find("WOF/Survival Stream Water");
            if (shader == null)
                throw new InvalidOperationException("Required survival stream water shader was not imported.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(SurvivalStreamWaterMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "SurvivalStreamWater" };
                AssetDatabase.CreateAsset(material, SurvivalStreamWaterMaterialPath);
            }
            else material.shader = shader;
            material.SetColor("_Color", Color.white);
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
