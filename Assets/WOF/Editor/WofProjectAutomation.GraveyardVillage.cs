using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string GraveyardArtRoot = "Assets/WOF/Art/Generated/React/GraveyardVillage";
        private const string GraveyardLayoutPath = GraveyardArtRoot + "/runtime-layout.json";
        private const string GraveyardTextureRoot = GraveyardArtRoot + "/Textures";
        private const string GraveyardGeometryRoot = GeometryRoot + "/GraveyardVillage";

        private static void CreateGraveyardVillageScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = WofGraveyardVillageSceneLoader.SceneName;
            var world = new GameObject("World");
            CreateGraveyardVillage(world.transform);
            EditorSceneManager.SaveScene(scene, GraveyardScenePath);
        }

        private static void CreateGraveyardVillage(Transform parent)
        {
            var document = LoadGraveyardVillageDocument();
            var materials = CreateGraveyardMaterials();
            var root = new GameObject("ReactSurvivalGraveyardVillage_5_2");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofGraveyardVillageLayout.WorldOrigin;

            CreateGraveyardSurface(root.transform, document, materials);
            CreateGraveyardPathStones(root.transform, document, materials);
            CreateGraveyardFence(root.transform, document, materials);
            CreateGraveyardTombs(root.transform, document, materials);
            CreateGraveyardChapel(root.transform, document, materials);
            CreateGraveyardColliders(root.transform, document);
        }

        private static WofGraveyardVillageDocument LoadGraveyardVillageDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(GraveyardLayoutPath);
            var document = JsonUtility.FromJson<WofGraveyardVillageDocument>(source.text);
            if (document == null || document.schemaVersion != 1 || document.chunk == null ||
                document.chunk.cx != WofGraveyardVillageLayout.ChunkX ||
                document.chunk.cz != WofGraveyardVillageLayout.ChunkZ || document.chunk.distance != 0 ||
                !string.Equals(document.chunk.biome, "mushroom", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.villageKind, "graveyard", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.lod, "near", StringComparison.Ordinal) ||
                !document.chunk.hasVillage || document.chunk.hasRiver || !document.chunk.riverVertical ||
                !Mathf.Approximately(document.baseHeight, WofGraveyardVillageLayout.ReactBaseHeight) ||
                !WofGraveyardVillageLayout.HasExactCounts(document.counts) || document.layout == null ||
                document.layout.tombs?.Length != WofGraveyardVillageLayout.TombCount ||
                document.layout.fenceSegments?.Length != WofGraveyardVillageLayout.FenceSegmentCount ||
                document.layout.pathStones?.Length != WofGraveyardVillageLayout.PathStoneCount ||
                document.chapel == null || document.chapel.wallSegments?.Length != 22 ||
                document.chapel.watchTowerPositions?.Length != 4 || document.chapel.gargoyles?.Length != 14 ||
                document.chapel.centerPewColliders?.Length != 10 || document.chapel.sideWingPews?.Length != 12 ||
                document.chapel.centerNpcPlacements?.Length != WofGraveyardVillageLayout.CenterNpcCount ||
                document.chapel.sideWingNpcPlacements?.Length != WofGraveyardVillageLayout.SideWingNpcCount ||
                document.chapel.characters?.Length != WofGraveyardVillageLayout.ChapelCharacterCount ||
                document.chapel.colliderSummary?.cuboidColliderCount != WofGraveyardVillageLayout.CuboidColliderCount ||
                document.geometries == null || !IsValidGraveyardMesh(document.geometries.terrain, true) ||
                !IsValidGraveyardMesh(document.geometries.terrainSkirt, false) ||
                !IsValidGraveyardMesh(document.geometries.rampCollider, true))
            {
                throw new InvalidOperationException($"Invalid exact React graveyard village layout at {GraveyardLayoutPath}.");
            }
            return document;
        }

        private static bool IsValidGraveyardMesh(WofSerializedMeshRecord record, bool requireNormals)
        {
            return record != null && record.vertexCount > 0 &&
                   record.positions?.Length == record.vertexCount * 3 &&
                   (!requireNormals || record.normals?.Length == record.vertexCount * 3) &&
                   (requireNormals || record.normals == null || record.normals.Length == 0 ||
                    record.normals.Length == record.vertexCount * 3) &&
                   (record.uvs == null || record.uvs.Length == 0 || record.uvs.Length == record.vertexCount * 2) &&
                   (record.colors == null || record.colors.Length == 0 || record.colors.Length == record.vertexCount * 3) &&
                   record.indices is { Length: > 0 };
        }

        private static GraveyardMaterialSet CreateGraveyardMaterials()
        {
            var terrainTexture = LoadRequiredAsset<Texture2D>($"{GraveyardTextureRoot}/terrain-detail.png");
            var terrain = GetOrCreateGraveyardTerrainMaterial(terrainTexture);
            var skirt = GetOrCreateGraveyardVertexMaterial("GraveyardTerrainSkirt", null, Vector2.one);
            return new GraveyardMaterialSet
            {
                Terrain = terrain,
                TerrainSkirt = skirt,
                PathStone = GraveyardMaterial("PathStone", new Color(0.792f, 0.8f, 0.749f, 0.68f), true),
                FenceBase = GraveyardMaterial("FenceBase", HexColor("#050505")),
                FenceInset = GraveyardMaterial("FenceInset", HexColor("#020202")),
                FenceTop = GraveyardMaterial("FenceTop", HexColor("#080808")),
                FenceMid = GraveyardMaterial("FenceMid", HexColor("#111111")),
                FenceSpike = GraveyardMaterial("FenceSpike", HexColor("#0c0c0c")),
                FenceTip = GraveyardMaterial("FenceTip", HexColor("#030303")),
                FenceGlint = GraveyardMaterial("FenceGlint", HexColor("#333333")),
                TombShadow = GraveyardMaterial("TombGroundShadow", new Color(0.125f, 0.145f, 0.098f, 0.82f), true),
                TombDetailDark = GraveyardMaterial("TombDetailDark", HexColor("#2d2a27")),
                TombBaseShadow = GraveyardMaterial("TombBaseShadow", new Color(0.082f, 0.075f, 0.059f, 0.54f), true),
                TombEdgeLight = GraveyardMaterial("TombEdgeLight", new Color(0.898f, 0.863f, 0.784f, 0.28f), true),
                TombEdgeDark = GraveyardMaterial("TombEdgeDark", new Color(0.157f, 0.141f, 0.118f, 0.46f), true)
            };
        }

        private static Material GetOrCreateGraveyardTerrainMaterial(Texture texture)
        {
            return GetOrCreateGraveyardVertexMaterial("GraveyardTerrain", texture, new Vector2(9f, 9f));
        }

        private static Material GetOrCreateGraveyardVertexMaterial(string name, Texture texture, Vector2 textureScale)
        {
            var shader = Shader.Find("WOF/Vertex Color Texture");
            if (shader == null) throw new InvalidOperationException("Required graveyard vertex-color shader was not imported.");
            var path = $"{MaterialsRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
            material.SetTextureScale("_BaseMap", textureScale);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GraveyardMaterial(string name, Color color, bool transparent = false)
        {
            return GetOrCreateDesertUnlit("Graveyard_" + name, color, null, transparent);
        }

        private static Material GraveyardTextureMaterial(
            string name,
            string relativeTexture,
            Vector2 textureScale,
            bool transparent = false)
        {
            var texture = LoadRequiredAsset<Texture2D>("Assets/WOF/Art/Generated/React/" + relativeTexture);
            return GetOrCreateDesertUnlit(
                "Graveyard_" + name,
                Color.white,
                texture,
                transparent,
                textureScale,
                CullMode.Off);
        }

        private static void CreateGraveyardSurface(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("GraveyardVillageSurface");
            root.transform.SetParent(parent, false);
            var terrain = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/Terrain.asset",
                () => CreateDesertSerializedMesh("ExactGraveyardTerrain", document.geometries.terrain));
            var skirt = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/TerrainSkirt.asset",
                () => CreateDesertSerializedMesh("ExactGraveyardTerrainSkirt", document.geometries.terrainSkirt));
            CreateMeshVisual("ExactGraveyardTerrain", root.transform, Vector3.zero, terrain, materials.Terrain);
            CreateMeshVisual("ExactGraveyardTerrainSkirt", root.transform, Vector3.zero, skirt, materials.TerrainSkirt);
        }

        private static void CreateGraveyardPathStones(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("graveyard-path-stones");
            root.transform.SetParent(parent, false);
            foreach (var stone in document.layout.pathStones)
            {
                var visual = GraveyardBox(
                    stone.key,
                    root.transform,
                    new Vector3(stone.localX, stone.localY, stone.localZ),
                    new Vector3(stone.width, stone.depth, 0.12f),
                    materials.PathStone);
                visual.transform.localRotation = Quaternion.Euler(-90f, 0f, stone.rotation * Mathf.Rad2Deg);
            }
        }

        private static void CreateGraveyardFence(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("graveyard-black-spiked-fence");
            root.transform.SetParent(parent, false);
            var tipMesh = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/FenceSpikeTip4.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            foreach (var segment in document.layout.fenceSegments)
            {
                var segmentRoot = new GameObject(segment.key);
                segmentRoot.transform.SetParent(root.transform, false);
                segmentRoot.transform.localPosition = new Vector3(segment.localX, segment.localY, segment.localZ);
                segmentRoot.transform.localRotation = Quaternion.Euler(0f, segment.rotation * Mathf.Rad2Deg, 0f);
                GraveyardBox("Base", segmentRoot.transform, new Vector3(0f, -0.46f, 0f),
                    new Vector3(segment.length + 2.1f, 1.08f, 1.28f), materials.FenceBase);
                GraveyardBox("Inset", segmentRoot.transform, new Vector3(0f, 0.38f, 0f),
                    new Vector3(segment.length + 1.4f, 0.76f, 1.05f), materials.FenceInset);
                GraveyardBox("TopRail", segmentRoot.transform, new Vector3(0f, 7.42f, 0f),
                    new Vector3(segment.length, 0.58f, 0.52f), materials.FenceBase);
                GraveyardBox("MiddleRail", segmentRoot.transform, new Vector3(0f, 4.95f, 0f),
                    new Vector3(segment.length, 0.48f, 0.42f), materials.FenceTop);
                GraveyardBox("LowRail", segmentRoot.transform, new Vector3(0f, 2.52f, 0f),
                    new Vector3(segment.length, 0.42f, 0.36f), materials.FenceMid);
                foreach (var offset in new[] { -0.48f, 0.48f })
                {
                    GraveyardBox($"Post_{offset:F2}", segmentRoot.transform,
                        new Vector3(offset * segment.length, 5.05f, 0f),
                        new Vector3(1.18f, 10.1f, 1.18f), materials.FenceTop);
                }
                for (var index = 0; index < 3; index++)
                {
                    var x = -segment.length * 0.32f + index * (segment.length * 0.64f / 2f);
                    GraveyardBox($"Spike_{index}", segmentRoot.transform, new Vector3(x, 4.78f, 0f),
                        new Vector3(0.5f, 8.2f, 0.5f), materials.FenceSpike);
                    var tip = CreateMeshVisual($"SpikeTip_{index}", segmentRoot.transform,
                        new Vector3(x, 9.85f, 0f), tipMesh, materials.FenceTip);
                    tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    tip.transform.localScale = new Vector3(0.72f, 2.15f, 0.72f);
                    GraveyardBox($"SpikeGlint_{index}", segmentRoot.transform,
                        new Vector3(x + 0.14f, 8.8f, -0.24f), new Vector3(0.16f, 1.4f, 0.12f),
                        materials.FenceGlint);
                }
            }
        }

        private static void CreateGraveyardTombs(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("graveyard-joke-tombs");
            root.transform.SetParent(parent, false);
            var disk = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/TombShadowDisk12.asset",
                () => CreateDarrelDiskMesh(1f, 12));
            var cylinder = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/TombArchCylinder16.asset",
                () => CreateDarrelFrustumMesh(1f, 1f, 1f, 16));
            var diamond = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/TombDiamond4.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            for (var index = 0; index < document.layout.tombs.Length; index++)
                CreateGraveyardTomb(root.transform, document.layout.tombs[index], index, materials, disk, cylinder, diamond);
        }

        private static void CreateGraveyardTomb(
            Transform parent,
            WofGraveyardTombRecord tomb,
            int index,
            GraveyardMaterialSet materials,
            Mesh disk,
            Mesh cylinder,
            Mesh diamond)
        {
            var root = new GameObject(tomb.key);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(tomb.localX, tomb.localY, tomb.localZ);
            root.transform.localRotation = Quaternion.Euler(0f, tomb.rotation * Mathf.Rad2Deg, 0f);
            var prefix = $"Tomb{index:00}";
            var body = GraveyardTextureMaterial(prefix + "Body", tomb.textures.bodyTexture, new Vector2(1.1f, 1f));
            var dark = GraveyardTextureMaterial(prefix + "Dark", tomb.textures.darkTexture, new Vector2(1.1f, 1f));
            var accent = GraveyardTextureMaterial(prefix + "Accent", tomb.textures.accentTexture, new Vector2(1.1f, 1f));
            var foundation = GraveyardTextureMaterial(prefix + "Foundation", tomb.textures.foundationTexture,
                new Vector2(1.45f, 1.25f));

            GraveyardBox("Foundation", root.transform, new Vector3(0f, -0.88f, 0.62f),
                new Vector3(tomb.baseWidth * 1.08f, 1.76f, tomb.baseDepth * 1.16f), foundation);
            var shadow = CreateMeshVisual("GroundShadow", root.transform, new Vector3(0f, 0.04f, 2f), disk,
                materials.TombShadow);
            var shadowRadius = 7.4f + tomb.variant * 2.2f;
            shadow.transform.localScale = new Vector3(shadowRadius * 1.9f, 1f, shadowRadius * 1.18f);
            GraveyardBox("Base", root.transform, new Vector3(0f, 0.78f, 0.62f),
                new Vector3(tomb.baseWidth, 1.56f, tomb.baseDepth), dark);

            switch (tomb.styleIndex)
            {
                case 0:
                    GraveyardBox("Marker", root.transform, new Vector3(0f, tomb.height * 0.5f + 1.2f, 0f),
                        new Vector3(tomb.width, tomb.height, tomb.depth), body);
                    GraveyardBox("Cap", root.transform, new Vector3(0f, tomb.height + 1.95f, 0f),
                        new Vector3(tomb.width * 0.74f, 1.5f, tomb.depth + 0.12f), body);
                    GraveyardBox("FrontInset", root.transform,
                        new Vector3(0f, tomb.height * 0.74f + 1.2f, tomb.frontZ - 0.02f),
                        new Vector3(tomb.width * 0.56f, 0.5f, 0.24f), materials.TombDetailDark);
                    break;
                case 1:
                    GraveyardBox("Marker", root.transform, new Vector3(0f, tomb.height * 0.5f + 1.2f, 0f),
                        new Vector3(tomb.width * 0.62f, tomb.height, tomb.depth), body);
                    GraveyardBox("CrossStem", root.transform, new Vector3(0f, tomb.height + 2.7f, 0f),
                        new Vector3(tomb.width * 0.42f, 4.1f, tomb.depth + 0.12f), body);
                    GraveyardBox("CrossArm", root.transform, new Vector3(0f, tomb.height + 3f, 0f),
                        new Vector3(tomb.width * 0.95f, 1.52f, tomb.depth + 0.18f), body);
                    GraveyardBox("FrontCrossStem", root.transform,
                        new Vector3(0f, tomb.height * 0.72f + 1.1f, tomb.frontZ - 0.02f),
                        new Vector3(0.72f, 4.25f, 0.26f), materials.TombDetailDark);
                    GraveyardBox("FrontCrossArm", root.transform,
                        new Vector3(0f, tomb.height * 0.82f + 1.1f, tomb.frontZ - 0.04f),
                        new Vector3(3.25f, 0.62f, 0.24f), materials.TombDetailDark);
                    break;
                case 2:
                    GraveyardBox("Marker", root.transform, new Vector3(0f, tomb.height * 0.46f + 1.1f, 0f),
                        new Vector3(tomb.width, tomb.height * 0.92f, tomb.depth), body);
                    var arch = CreateMeshVisual("RoundedCap", root.transform,
                        new Vector3(0f, tomb.height * 0.92f + 1.1f, 0f), cylinder, body);
                    arch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    arch.transform.localScale = new Vector3(tomb.width * 0.5f, tomb.depth + 0.06f,
                        tomb.width * 0.5f);
                    GraveyardBox("FrontInset", root.transform,
                        new Vector3(0f, tomb.height * 0.86f + 1.1f, tomb.frontZ - 0.04f),
                        new Vector3(tomb.width * 0.62f, 0.46f, 0.24f), materials.TombDetailDark);
                    break;
                case 3:
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var material = side < 0f ? body : accent;
                        GraveyardBox($"DoubleMarker_{side}", root.transform,
                            new Vector3(side * tomb.width * 0.27f, tomb.height * 0.46f + 1.05f, 0f),
                            new Vector3(tomb.width * 0.42f, tomb.height * 0.92f, tomb.depth), material);
                        GraveyardBox($"DoubleCap_{side}", root.transform,
                            new Vector3(side * tomb.width * 0.27f, tomb.height * 0.95f + 1.02f, 0f),
                            new Vector3(tomb.width * 0.36f, 1.35f, tomb.depth + 0.12f), material);
                    }
                    GraveyardBox("FrontInset", root.transform,
                        new Vector3(0f, tomb.height * 0.18f + 1f, tomb.frontZ - 0.04f),
                        new Vector3(tomb.width * 0.22f, 2.8f, 0.22f), materials.TombDetailDark);
                    break;
                case 4:
                    GraveyardBox("Marker", root.transform, new Vector3(0f, tomb.height * 0.42f + 1.12f, 0f),
                        new Vector3(tomb.width * 0.74f, tomb.height * 0.84f, tomb.depth), body);
                    var cap = CreateMeshVisual("DiamondCap", root.transform,
                        new Vector3(0f, tomb.height * 0.92f + 1.02f, 0f), diamond, accent);
                    cap.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    cap.transform.localScale = new Vector3(tomb.width * 0.52f, tomb.height * 0.34f,
                        tomb.width * 0.52f);
                    GraveyardBox("FrontInsetHorizontal", root.transform,
                        new Vector3(0f, tomb.height * 0.62f + 1.1f, tomb.frontZ - 0.04f),
                        new Vector3(tomb.width * 0.42f, 0.5f, 0.24f), materials.TombDetailDark);
                    GraveyardBox("FrontInsetVertical", root.transform,
                        new Vector3(0f, tomb.height * 0.7f + 1.1f, tomb.frontZ - 0.05f),
                        new Vector3(0.54f, 2.7f, 0.22f), materials.TombDetailDark);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported exact graveyard tomb style {tomb.styleIndex}.");
            }

            GraveyardBox("BaseFrontShadow", root.transform,
                new Vector3(0f, 1.72f, -tomb.baseDepth * 0.5f - 0.04f),
                new Vector3(tomb.baseWidth * 0.86f, 0.34f, 0.2f), materials.TombBaseShadow);
            GraveyardBox("LeftEdgeHighlight", root.transform,
                new Vector3(-tomb.baseWidth * 0.38f, tomb.height * 0.32f + 1.1f, tomb.frontZ - 0.045f),
                new Vector3(0.34f, tomb.height * 0.48f, 0.22f), materials.TombEdgeLight);
            GraveyardBox("RightEdgeShadow", root.transform,
                new Vector3(tomb.baseWidth * 0.34f, tomb.height * 0.58f + 1.1f, tomb.frontZ - 0.045f),
                new Vector3(0.28f, tomb.height * 0.36f, 0.22f), materials.TombEdgeDark);

            if (!string.IsNullOrWhiteSpace(tomb.textures.inscriptionTexture))
            {
                var inscription = GraveyardTextureMaterial(prefix + "Inscription", tomb.textures.inscriptionTexture,
                    Vector2.one, true);
                var inscriptionQuad = GetOrCreateMeshAsset(
                    GraveyardGeometryRoot + "/TombInscriptionQuad.asset",
                    CreateGraveyardInscriptionQuadMesh);
                var label = CreateMeshVisual("Inscription", root.transform,
                    new Vector3(tomb.styleIndex == 3 ? -tomb.width * 0.27f : 0f, tomb.labelY,
                        tomb.frontZ - 0.075f),
                    inscriptionQuad, inscription);
                label.transform.localScale = new Vector3(tomb.labelWidth, tomb.labelHeight, 1f);
            }
        }

        private static Mesh CreateGraveyardInscriptionQuadMesh()
        {
            var mesh = new Mesh { name = "GraveyardInscriptionQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateGraveyardColliders(Transform parent, WofGraveyardVillageDocument document)
        {
            var root = new GameObject("GraveyardVillageExactColliders");
            root.transform.SetParent(parent, false);
            var terrain = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/Terrain.asset",
                () => CreateDesertSerializedMesh("ExactGraveyardTerrain", document.geometries.terrain));
            var terrainOwner = new GameObject("TerrainTrimesh");
            terrainOwner.transform.SetParent(root.transform, false);
            terrainOwner.AddComponent<MeshCollider>().sharedMesh = terrain;
            var ramp = GetOrCreateMeshAsset(
                GraveyardGeometryRoot + "/ChapelRampCollider.asset",
                () => CreateDesertSerializedMesh("ExactChapelRampCollider", document.geometries.rampCollider));
            var rampOwner = new GameObject("RampTrimesh");
            rampOwner.transform.SetParent(root.transform, false);
            rampOwner.AddComponent<MeshCollider>().sharedMesh = ramp;

            AddGraveyardBoxCollider(root.transform, "FoundationCenter",
                new Vector3(0f, document.baseHeight + 0.56f, 0f), new Vector3(108f, 1.16f, 164f));
            AddGraveyardBoxCollider(root.transform, "FoundationWest",
                new Vector3(-88f, document.baseHeight + 0.54f, 0f), new Vector3(68f, 1.12f, 114f));
            AddGraveyardBoxCollider(root.transform, "FoundationEast",
                new Vector3(88f, document.baseHeight + 0.54f, 0f), new Vector3(68f, 1.12f, 114f));
            foreach (var pew in document.chapel.centerPewColliders)
                AddGraveyardBoxCollider(root.transform, pew.key,
                    new Vector3(pew.x, document.baseHeight + 2.65f, pew.z), new Vector3(19f, 3.5f, 5.1f));
            foreach (var pew in document.chapel.sideWingPews)
            {
                var yaw = Mathf.Atan2(pew.x, 68.6f + pew.z);
                AddGraveyardBoxCollider(root.transform, "SidePewCollider_" + pew.key,
                    new Vector3(pew.x, document.baseHeight + 2.65f, pew.z),
                    new Vector3(pew.width + 1.8f, 3.5f, 5.3f), yaw);
            }
            AddGraveyardBoxCollider(root.transform, "AltarCollider",
                new Vector3(0f, document.baseHeight + 3.9f, -68f), new Vector3(22.4f, 5.8f, 9.4f));
            AddGraveyardBoxCollider(root.transform, "PulpitCollider",
                new Vector3(30f, document.baseHeight + 4.05f, -54f), new Vector3(8.7f, 6.1f, 6.9f));
            foreach (var wall in document.chapel.wallSegments)
                AddGraveyardBoxCollider(root.transform, "WallCollider_" + wall.key,
                    new Vector3(wall.position[0], document.baseHeight + wall.position[1], wall.position[2]),
                    new Vector3(wall.size[0], wall.size[1], wall.size[2]));
            AddGraveyardBoxCollider(root.transform, "TowerWestLong",
                new Vector3(-17.3f, document.baseHeight + 33.2f, 62f), new Vector3(2.6f, 66.4f, 42f));
            AddGraveyardBoxCollider(root.transform, "TowerEastLong",
                new Vector3(17.3f, document.baseHeight + 33.2f, 62f), new Vector3(2.6f, 66.4f, 42f));
            AddGraveyardBoxCollider(root.transform, "TowerFrontWest",
                new Vector3(-13.7f, document.baseHeight + 33.2f, 78.5f), new Vector3(5.4f, 66.4f, 2.6f));
            AddGraveyardBoxCollider(root.transform, "TowerFrontEast",
                new Vector3(13.7f, document.baseHeight + 33.2f, 78.5f), new Vector3(5.4f, 66.4f, 2.6f));
            AddGraveyardBoxCollider(root.transform, "TowerRearWest",
                new Vector3(-13.7f, document.baseHeight + 33.2f, 41.5f), new Vector3(5.4f, 66.4f, 2.4f));
            AddGraveyardBoxCollider(root.transform, "TowerRearEast",
                new Vector3(13.7f, document.baseHeight + 33.2f, 41.5f), new Vector3(5.4f, 66.4f, 2.4f));
            AddGraveyardBoxCollider(root.transform, "TowerFrontCenter",
                new Vector3(0f, document.baseHeight + 46.4f, 78.5f), new Vector3(22f, 40f, 2.6f));
            AddGraveyardBoxCollider(root.transform, "TowerRearCenter",
                new Vector3(0f, document.baseHeight + 46.4f, 41.5f), new Vector3(22f, 40f, 2.4f));
            foreach (var segment in document.layout.fenceSegments)
                AddGraveyardBoxCollider(root.transform, "FenceCollider_" + segment.key,
                    new Vector3(segment.localX, segment.localY + 5.6f, segment.localZ),
                    new Vector3(segment.length + 3.2f, 14.4f, 3.6f), segment.rotation);
        }

        private static void AddGraveyardBoxCollider(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size,
            float yawRadians = 0f)
        {
            var owner = new GameObject(name);
            owner.transform.SetParent(parent, false);
            owner.transform.localPosition = position;
            owner.transform.localRotation = Quaternion.Euler(0f, yawRadians * Mathf.Rad2Deg, 0f);
            owner.AddComponent<BoxCollider>().size = size;
        }

        private static GameObject GraveyardBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            return CreateVisualPrimitive(name, PrimitiveType.Cube, parent, position, scale, material);
        }

        private sealed class GraveyardMaterialSet
        {
            public Material Terrain;
            public Material TerrainSkirt;
            public Material PathStone;
            public Material FenceBase;
            public Material FenceInset;
            public Material FenceTop;
            public Material FenceMid;
            public Material FenceSpike;
            public Material FenceTip;
            public Material FenceGlint;
            public Material TombShadow;
            public Material TombDetailDark;
            public Material TombBaseShadow;
            public Material TombEdgeLight;
            public Material TombEdgeDark;
        }
    }
}
