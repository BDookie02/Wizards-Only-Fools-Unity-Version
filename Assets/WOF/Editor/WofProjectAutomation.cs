using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string GeneratedRoot = "Assets/WOF/Generated";
        private const string MaterialsRoot = GeneratedRoot + "/Materials";
        private const string GeometryRoot = GeneratedRoot + "/Geometry";
        private const string SettingsRoot = GeneratedRoot + "/Settings";
        private const string PrefabsRoot = GeneratedRoot + "/Prefabs";
        private const string ScenesRoot = GeneratedRoot + "/Scenes";
        private const string ScenePath = ScenesRoot + "/WofBootstrap.unity";
        private const string ChicagoScenePath = ScenesRoot + "/WofChicagoCity.unity";
        private const string SwampScenePath = ScenesRoot + "/WofSwampVillage.unity";
        private const string MountainScenePath = ScenesRoot + "/WofMountainVillage.unity";
        private const string GraveyardScenePath = ScenesRoot + "/WofGraveyardVillage.unity";
        private const string LilyCoilScenePath = ScenesRoot + "/WofLilyCoil.unity";
        private const string PlayerPrefabPath = PrefabsRoot + "/WofNetworkPlayer.prefab";
        private const string FireballPrefabPath = PrefabsRoot + "/WofFireball.prefab";
        private const string NetworkPrefabsPath = SettingsRoot + "/WofNetworkPrefabs.asset";
        private const string RendererPath = SettingsRoot + "/WofUniversalRenderer.asset";
        private const string PipelinePath = SettingsRoot + "/WofUniversalPipeline.asset";
        private const string VillageTerrainMeshPath = GeometryRoot + "/BaseVillageTerrain.asset";
        private const string VillageTerrainCollisionMeshPath = GeometryRoot + "/BaseVillageTerrainCollision.asset";
        private const string VillageTerrainTexturePath = GeometryRoot + "/BaseVillageTerrainColor.asset";
        private const string MushroomCapMeshPath = GeometryRoot + "/ReactMushroomCap.asset";
        private const string MoundShellMeshPath = GeometryRoot + "/ReactMoundShell.asset";
        private const string CampfireSphereMeshPath = GeometryRoot + "/ReactCampfireSphere.asset";
        private const string WaterPlaneMeshPath = GeometryRoot + "/ReactBaseVillageWaterPlane.asset";
        private const string DesktopWaterRingMeshPath = GeometryRoot + "/ReactWaterRing32.asset";
        private const string MobileWaterRingMeshPath = GeometryRoot + "/ReactWaterRing18.asset";
        private const string CircularUiMaskPath = GeometryRoot + "/CircularUiMask.asset";
        private const string BushGeometryJsonPath = "Assets/WOF/Art/Generated/React/Geometry/bush-dodecahedron.json";
        private const string BushMeshPath = GeometryRoot + "/ReactBushDodeca.asset";
        private const string VillagerLayoutJsonPath = "Assets/WOF/Art/Generated/React/Villagers/base-village.json";
        private const string ReactHutTextureRoot = "Assets/WOF/Art/Generated/React/Huts";
        private const string ReactTreeHouseTextureRoot = "Assets/WOF/Art/Generated/React/TreeHouse";

        [Serializable]
        private sealed class AndroidBuildReceipt
        {
            public int schemaVersion;
            public string completedUtc;
            public string packageName;
            public string versionName;
            public int versionCode;
            public long apkLength;
            public string apkSha256;
        }

        [Serializable]
        private sealed class BuildArtifactReceipt
        {
            public int schemaVersion;
            public string completedUtc;
            public string target;
            public ulong reportedTotalSize;
            public string primaryArtifact;
            public long primaryLength;
            public string primarySha256;
            public string payloadArtifact;
            public long payloadLength;
            public string payloadSha256;
            public string additivePayloadArtifact;
            public long additivePayloadLength;
            public string additivePayloadSha256;
            public BuildScenePayloadReceipt[] scenePayloads;
        }

        [Serializable]
        private sealed class BuildScenePayloadReceipt
        {
            public string artifact;
            public long length;
            public string sha256;
        }

        private static readonly string[] BuildScenePaths =
        {
            ScenePath,
            ChicagoScenePath,
            SwampScenePath,
            MountainScenePath,
            GraveyardScenePath,
            LilyCoilScenePath
        };

        private static readonly string[] RequiredNetworkPrefabPaths =
        {
            PlayerPrefabPath,
            FireballPrefabPath
        };

        [MenuItem("WOF/Automation/Bootstrap Project")]
        public static void BootstrapProject()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureProjectSettings();
            ConfigureSpriteImports();
            ConfigureRenderPipeline();

            var palette = CreateMaterials();
            var fireballPrefab = CreateFireballPrefab(palette.Fireball);
            var playerPrefab = CreatePlayerPrefab(palette, fireballPrefab);
            RefreshNetworkPrefabIdentities();
            fireballPrefab = LoadRequiredAsset<GameObject>(FireballPrefabPath);
            playerPrefab = LoadRequiredAsset<GameObject>(PlayerPrefabPath);
            var networkPrefabs = ConfigureNetworkPrefabs(playerPrefab, fireballPrefab);
            CreateBootstrapScene(palette, playerPrefab, networkPrefabs);
            CreateChicagoCityScene();
            CreateSwampVillageScene();
            CreateMountainVillageScene();
            CreateGraveyardVillageScene();
            CreateLilyCoilScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedNetworkConfiguration();
            Debug.Log("[WOF-AUTOMATION] BOOTSTRAP_COMPLETE");
        }

        public static void BuildWindowsBatch()
        {
            BootstrapProject();
            BuildPlayer(
                BuildTarget.StandaloneWindows64,
                ResolveProjectPath("Builds", "Windows", "WizardsOnlyFools.exe"));
        }

        public static void BuildWebGlBatch()
        {
            BootstrapProject();
            BuildPlayer(
                BuildTarget.WebGL,
                ResolveProjectPath("Builds", "WebGL"));
        }

        public static void BuildAndroidBatch()
        {
            BootstrapProject();
            var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            var previousExportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                BuildPlayer(
                    BuildTarget.Android,
                    ResolveProjectPath("Builds", "Android", "WizardsOnlyFools.apk"));
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportAsGoogleAndroidProject;
            }
        }

        [MenuItem("WOF/Automation/Build Windows")]
        private static void BuildWindowsMenu() => BuildWindowsBatch();

        [MenuItem("WOF/Automation/Build WebGL")]
        private static void BuildWebGlMenu() => BuildWebGlBatch();

        [MenuItem("WOF/Automation/Build Android APK")]
        private static void BuildAndroidMenu() => BuildAndroidBatch();

        private static void EnsureFolders()
        {
            EnsureAssetFolder("Assets/WOF");
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(MaterialsRoot);
            EnsureAssetFolder(GeometryRoot);
            EnsureAssetFolder(DarrelGeometryRoot);
            EnsureAssetFolder(DesertGeometryRoot);
            EnsureAssetFolder(SwampGeometryRoot);
            EnsureAssetFolder(ChicagoGeometryRoot);
            EnsureAssetFolder(MountainGeometryRoot);
            EnsureAssetFolder(GraveyardGeometryRoot);
            EnsureAssetFolder(LilyCoilGeometryRoot);
            EnsureAssetFolder(SettingsRoot);
            EnsureAssetFolder(PrefabsRoot);
            EnsureAssetFolder(ScenesRoot);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureAssetFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "Wizards Only Fools";
            PlayerSettings.productName = "Wizards Only Fools";
            PlayerSettings.bundleVersion = "0.4.11";
            PlayerSettings.Android.bundleVersionCode = 14;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Physics.gravity = new Vector3(0f, WofGameConstants.Gravity, 0f);
            Time.fixedDeltaTime = 1f / WofGameConstants.ServerTickRate;
        }

        private static void ConfigureSpriteImports()
        {
            ConfigureSpriteFolder("Assets/WOF/Art/Sprites", 100f);
            ConfigureSpriteFolder(
                "Assets/WOF/Art/Generated/React/Avatar",
                512f / 2.95f);
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/HUD", 100f);
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/Launch", 100f);
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/DarrelGrove/Dragon", 100f);
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/DarrelGrove/Textures/Clamped", 100f);
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/Quest", 64f);
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/Huts");
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/TreeHouse");
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/DarrelGrove/Textures/Repeating");
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/DesertVillage/Textures");
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/SwampVillage/Textures");
            ConfigureMountainTextureImports();
            ConfigureGraveyardTextureImports();
            ConfigureLilyCoilTextureImports();
            ConfigureBotwGrassTextureImport();
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/SwampVillage/Toad", 288f);
            ConfigureChicagoTextureImports();
            ConfigureSpriteFolder("Assets/WOF/Art/Generated/React/ChicagoCity/Operators", 512f / 2.95f);
        }

        private static void ConfigureSpriteFolder(string folder, float pixelsPerUnit)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                var changed = importer.textureType != TextureImporterType.Sprite ||
                              !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                              importer.mipmapEnabled ||
                              importer.filterMode != FilterMode.Point ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              !importer.sRGBTexture;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ConfigureRepeatingTextureFolder(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                var changed = importer.textureType != TextureImporterType.Default ||
                              importer.mipmapEnabled ||
                              importer.filterMode != FilterMode.Point ||
                              importer.wrapMode != TextureWrapMode.Repeat ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              !importer.sRGBTexture;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ConfigureBotwGrassTextureImport()
        {
            const string path = "Assets/WOF/Art/Generated/React/Vegetation/botw-grass.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Required exact React BOTW grass texture importer is missing: {path}");
            }

            var changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                          importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          !importer.alphaIsTransparency || !importer.sRGBTexture;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureMountainTextureImports()
        {
            const string path = "Assets/WOF/Art/Generated/React/MountainVillage/Textures/terrain-detail.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Required exact React mountain texture importer is missing: {path}");
            }

            var changed = importer.textureType != TextureImporterType.Default ||
                          !importer.mipmapEnabled ||
                          importer.filterMode != FilterMode.Trilinear ||
                          importer.wrapMode != TextureWrapMode.Repeat ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          !importer.sRGBTexture;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureGraveyardTextureImports()
        {
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/GraveyardVillage/Tombs");
            ConfigureRepeatingTextureFolder("Assets/WOF/Art/Generated/React/GraveyardVillage/Textures");

            var inscriptionGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { "Assets/WOF/Art/Generated/React/GraveyardVillage/Tombs" });
            foreach (var guid in inscriptionGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("-inscription.png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ConfigureClampedReactTexture(path, alphaIsTransparency: true);
            }

            ConfigureClampedReactTexture(
                "Assets/WOF/Art/Generated/React/GraveyardVillage/Textures/chapel-pope-miter.png",
                alphaIsTransparency: true);
            ConfigureGraveyardTerrainTexture();
        }

        private static void ConfigureGraveyardTerrainTexture()
        {
            const string path = "Assets/WOF/Art/Generated/React/GraveyardVillage/Textures/terrain-detail.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Required exact React graveyard terrain texture importer is missing: {path}");
            }

            var changed = importer.textureType != TextureImporterType.Default ||
                          !importer.mipmapEnabled ||
                          importer.filterMode != FilterMode.Trilinear ||
                          importer.wrapMode != TextureWrapMode.Repeat ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          !importer.sRGBTexture;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureClampedReactTexture(string path, bool alphaIsTransparency)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Required exact React texture importer is missing: {path}");
            }

            var changed = importer.textureType != TextureImporterType.Default ||
                          importer.mipmapEnabled ||
                          importer.filterMode != FilterMode.Point ||
                          importer.wrapMode != TextureWrapMode.Clamp ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed ||
                          importer.alphaIsTransparency != alphaIsTransparency ||
                          !importer.sRGBTexture;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.sRGBTexture = true;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "WOF Universal Pipeline";
                pipeline.shadowDistance = 70f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Match the React oracle's intentionally low-resolution WebGL canvas
            // (desktop DPR 0.46, antialias disabled, CSS image-rendering: pixelated)
            // without reducing the resolution of Unity's overlay HUD.
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_MSAA").intValue = 1;
            pipelineSettings.FindProperty("m_RenderScale").floatValue = 0.46f;
            pipelineSettings.FindProperty("m_UpscalingFilter").intValue = 2;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        private static WofMaterialPalette CreateMaterials()
        {
            var terrainTexture = GetOrCreateVillageTerrainTexture();
            var stemTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/stem_wall.png");
            var grassTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/grass.png");
            var logTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/log.png");
            var dirtGrassTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/dirt_grass.png");
            var woodPlankTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/wood_plank.png");
            var dirtWallTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/dirt_wall.png");
            var dirtDoorTexture = LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/dirt_door.png");
            var treeHouseBarkTexture = LoadRequiredAsset<Texture2D>(ReactTreeHouseTextureRoot + "/bark.png");
            var treeHousePlankTexture = LoadRequiredAsset<Texture2D>(ReactTreeHouseTextureRoot + "/plank.png");
            return new WofMaterialPalette
            {
                Ground = GetOrCreateMaterial("Ground", Color.white, texture: terrainTexture),
                Stone = GetOrCreateMaterial("Stone", new Color(0.30f, 0.32f, 0.34f)),
                Wood = GetOrCreateMaterial("Wood", new Color(0.30f, 0.14f, 0.06f)),
                Roof = GetOrCreateMaterial("Roof", new Color(0.34f, 0.08f, 0.08f)),
                Leaf = GetOrCreateMaterial("Leaf", new Color(0.08f, 0.28f, 0.10f)),
                Grass = GetOrCreateMaterial("VillageGrass", new Color32(58, 104, 40, 255)),
                Dirt = GetOrCreateMaterial("VillageDirt", new Color32(92, 64, 51, 255)),
                Path = GetOrCreateMaterial("VillagePath", new Color32(194, 160, 119, 255)),
                Fire = GetOrCreateUnlitMaterial("Campfire", new Color32(255, 85, 0, 204), transparent: true),
                CampfireLog = GetOrCreateMaterial("CampfireLog", new Color32(74, 50, 33, 255), roughness: 1f),
                Water = GetOrCreateMaterial("BaseVillageWater", new Color32(45, 90, 136, 204),
                    roughness: 1f, transparent: true),
                WaterRipple = GetOrCreateUnlitMaterial("WaterRipple", Color.white, transparent: true),
                Bushes = CreateBushMaterials(),
                Villager = GetOrCreateVillagerMaterial(),
                Stem = GetOrCreateMaterial("HutStem", Color.white, texture: stemTexture,
                    textureScale: new Vector2(2f, 1f), roughness: 1f, doubleSided: true),
                HutGrass = GetOrCreateMaterial("HutGrass", Color.white, texture: grassTexture,
                    textureScale: new Vector2(4f, 4f), roughness: 1f, doubleSided: true),
                Stonework = GetOrCreateMaterial("HutStonework", Color.white, texture: dirtWallTexture,
                    textureScale: new Vector2(2f, 1f), roughness: 0.9f, doubleSided: true),
                Door = GetOrCreateMaterial("HutDoor", Color.white, texture: dirtDoorTexture,
                    roughness: 1f, doubleSided: true),
                WoodPlank = GetOrCreateMaterial("HutWoodPlank", Color.white, texture: woodPlankTexture,
                    textureScale: new Vector2(4f, 4f), roughness: 0.9f, doubleSided: true),
                Log = GetOrCreateMaterial("HutLog", Color.white, texture: logTexture,
                    textureScale: new Vector2(2f, 1f), roughness: 0.9f, doubleSided: true),
                DirtGrass = GetOrCreateMaterial("HutDirtGrass", Color.white, texture: dirtGrassTexture,
                    textureScale: new Vector2(4f, 1f), roughness: 1f, doubleSided: true),
                HutGlass = GetOrCreateMaterial("HutGlass", new Color32(136, 204, 255, 255), roughness: 0.2f),
                HutIron = GetOrCreateMaterial("HutIron", new Color32(34, 34, 34, 255), roughness: 0.8f),
                LanternGlow = GetOrCreateMaterial("LanternGlow", new Color32(255, 211, 111, 250),
                    new Color(3.5f, 2.2f, 0.6f)),
                LanternHalo = GetOrCreateMaterial("LanternHalo", new Color(1f, 0.616f, 0.212f, 0.34f),
                    new Color(2.8f, 1.2f, 0.25f), transparent: true, additive: true),
                LanternOuterGlow = GetOrCreateMaterial("LanternOuterGlow", new Color(1f, 0.835f, 0.435f, 0.13f),
                    new Color(1.8f, 1.1f, 0.3f), transparent: true, additive: true),
                TreeHouseBark = GetOrCreateMaterial("TreeHouseBark", Color.white, texture: treeHouseBarkTexture,
                    textureScale: new Vector2(2f, 4f), roughness: 1f),
                TreeHousePlank = GetOrCreateMaterial("TreeHousePlank", Color.white, texture: treeHousePlankTexture,
                    textureScale: new Vector2(4f, 1f), roughness: 0.9f),
                TreeHouseRoof = GetOrCreateMaterial("TreeHouseRoof", new Color32(52, 34, 17, 255), roughness: 0.9f),
                TreeHouseWindowGlow = GetOrCreateMaterial("TreeHouseWindowGlow", new Color32(255, 179, 71, 255),
                    new Color(3f, 2.105f, 0.835f), roughness: 0.4f, doubleSided: true),
                TreeHouseRope = GetOrCreateMaterial("TreeHouseRope", new Color32(139, 90, 43, 255), roughness: 0.9f),
                TreeHouseLeafEdge = GetOrCreateMaterial("TreeHouseLeafEdge", new Color(0.141f, 0.290f, 0.110f, 0.44f),
                    roughness: 1f, transparent: true),
                TreeHouseDetailLeaf = GetOrCreateMaterial("TreeHouseDetailLeaf", new Color32(46, 90, 34, 255), roughness: 1f),
                TreeHouseLeaves = new[]
                {
                    GetOrCreateMaterial("TreeHouseLeaf0", new Color32(31, 59, 24, 255), roughness: 1f),
                    GetOrCreateMaterial("TreeHouseLeaf1", new Color32(45, 90, 34, 255), roughness: 1f),
                    GetOrCreateMaterial("TreeHouseLeaf2", new Color32(40, 79, 29, 255), roughness: 1f),
                    GetOrCreateMaterial("TreeHouseLeaf3", new Color32(58, 106, 42, 255), roughness: 1f),
                    GetOrCreateMaterial("TreeHouseLeaf4", new Color32(51, 95, 37, 255), roughness: 1f),
                    GetOrCreateMaterial("TreeHouseLeaf5", new Color32(36, 71, 25, 255), roughness: 1f)
                },
                MushroomCaps = new[]
                {
                    GetOrCreateMaterial("MushroomRed", Color.white,
                        texture: LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/mushroom_cap_0.png"), roughness: 0.9f, doubleSided: true),
                    GetOrCreateMaterial("MushroomGreen", Color.white,
                        texture: LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/mushroom_cap_1.png"), roughness: 0.9f, doubleSided: true),
                    GetOrCreateMaterial("MushroomBlue", Color.white,
                        texture: LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/mushroom_cap_2.png"), roughness: 0.9f, doubleSided: true),
                    GetOrCreateMaterial("MushroomPurple", Color.white,
                        texture: LoadRequiredAsset<Texture2D>(ReactHutTextureRoot + "/mushroom_cap_3.png"), roughness: 0.9f, doubleSided: true)
                },
                Player = GetOrCreateMaterial("Player", new Color(0.30f, 0.15f, 0.62f)),
                Fireball = GetOrCreateMaterial("Fireball", new Color(1f, 0.18f, 0.02f), new Color(3.5f, 0.25f, 0.02f)),
                Mana = GetOrCreateMaterial("Mana", new Color(0.05f, 0.65f, 0.95f), new Color(0.05f, 1.5f, 3.5f))
            };
        }

        private static Material GetOrCreateMaterial(
            string name,
            Color color,
            Color? emission = null,
            Texture texture = null,
            Vector2? textureScale = null,
            float? roughness = null,
            bool doubleSided = false,
            bool transparent = false,
            bool additive = false)
        {
            var path = $"{MaterialsRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", textureScale ?? Vector2.one);
                }
                else if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                    material.SetTextureScale("_MainTex", textureScale ?? Vector2.one);
                }
            }

            if (roughness.HasValue && material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(roughness.Value));
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            }
            material.doubleSidedGI = doubleSided;

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive
                    ? (float)BlendMode.One
                    : (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetOverrideTag("RenderType", "Opaque");
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Geometry;
            }

            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateUnlitMaterial(string name, Color color, bool transparent)
        {
            var path = $"{MaterialsRoot}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1f : 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", transparent
                ? (float)BlendMode.SrcAlpha
                : (float)BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", transparent
                ? (float)BlendMode.OneMinusSrcAlpha
                : (float)BlendMode.Zero);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateVillagerMaterial()
        {
            var shader = Shader.Find("WOF/Villager Sprite Alpha Clip");
            if (shader == null)
            {
                throw new InvalidOperationException("Required WOF villager sprite shader was not imported.");
            }

            var path = $"{MaterialsRoot}/VillagerSprite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Villager Sprite" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Cutoff", 0.12f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material[] CreateBushMaterials()
        {
            var shader = Shader.Find("WOF/Bush Faceted");
            if (shader == null)
            {
                throw new InvalidOperationException("Required WOF/Bush Faceted shader was not imported.");
            }

            var fillColors = new[]
            {
                new Color(0.052860647f, 0.116970668f, 0.035601315f, 1f),
                new Color(0.102241733f, 0.238397574f, 0.056128490f, 1f),
                new Color(0.205078736f, 0.417885071f, 0.084376212f, 1f)
            };
            var lineColors = new[]
            {
                new Color(0.527208872f, 0.731419211f, 0.258757417f, 1f),
                new Color(0.540047954f, 0.762990206f, 0.264094483f, 1f),
                new Color(0.566785575f, 0.809656956f, 0.271438890f, 1f)
            };
            var materials = new Material[3];
            for (var index = 0; index < materials.Length; index++)
            {
                var path = $"{MaterialsRoot}/BushFaceted{index}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = $"Bush Faceted {index}" };
                    AssetDatabase.CreateAsset(material, path);
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                }
                material.SetColor("_BaseColor", fillColors[index]);
                material.SetColor("_BushLineColor", lineColors[index]);
                material.SetFloat("_BushLineWidth", 0.044f);
                material.SetFloat("_BushLineOpacity", 0.74f);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                materials[index] = material;
            }
            return materials;
        }

        private static Texture2D GetOrCreateVillageTerrainTexture()
        {
            const int resolution = WofBaseVillageLayout.TerrainSegments + 1;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(VillageTerrainTexturePath);
            if (texture == null || texture.width != resolution || texture.height != resolution)
            {
                if (texture != null)
                {
                    AssetDatabase.DeleteAsset(VillageTerrainTexturePath);
                }

                texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false)
                {
                    name = "Base Village Terrain Color",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(texture, VillageTerrainTexturePath);
            }

            var colors = new Color[resolution * resolution];
            for (var zIndex = 0; zIndex < resolution; zIndex++)
            {
                var z = -WofBaseVillageLayout.MapSize * 0.5d +
                        zIndex * (WofBaseVillageLayout.MapSize / (double)WofBaseVillageLayout.TerrainSegments);
                for (var xIndex = 0; xIndex < resolution; xIndex++)
                {
                    var x = -WofBaseVillageLayout.MapSize * 0.5d +
                            xIndex * (WofBaseVillageLayout.MapSize / (double)WofBaseVillageLayout.TerrainSegments);
                    colors[zIndex * resolution + xIndex] = WofBaseVillageLayout.GetTerrainColor(x, z);
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static GameObject CreateFireballPrefab(Material fireballMaterial)
        {
            var root = new GameObject("WOF_Fireball");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var projectile = root.AddComponent<WofFireballProjectile>();

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * 0.38f;
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            var glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = fireballMaterial;

            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            spriteObject.transform.localScale = Vector3.one * 1.25f;
            var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            var frames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Fireball", "fireball_");
            if (frames.Length > 0)
            {
                spriteRenderer.sprite = frames[0];
            }

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.22f, 0.04f);
            light.intensity = 4f;
            light.range = 5f;

            SetObjectReference(projectile, "spriteRenderer", spriteRenderer);
            SetObjectReferenceArray(projectile, "frames", frames);
            SetObjectReferenceArray(
                projectile,
                "spellThumbnails",
                WofSpellLoadout.PlayableSpells
                    .Select(spell => LoadRequiredAsset<Sprite>(
                        $"Assets/WOF/Art/Generated/React/HUD/SpellMenu/{WofSpellLoadout.GetReactId(spell)}.png"))
                    .ToArray());
            SetObjectReference(projectile, "glowRenderer", glowRenderer);
            SetObjectReference(projectile, "spellLight", light);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FireballPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePlayerPrefab(WofMaterialPalette palette, GameObject fireballPrefab)
        {
            var root = new GameObject("WOF_NetworkPlayer");
            root.AddComponent<NetworkObject>();
            var character = root.AddComponent<CharacterController>();
            character.center = new Vector3(0f, 1f, 0f);
            character.height = 2f;
            character.radius = 0.42f;
            character.stepOffset = 0.35f;
            character.slopeLimit = 48f;

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            var avatarObject = new GameObject("AvatarBillboard");
            avatarObject.transform.SetParent(visualRoot.transform, false);
            avatarObject.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            var avatarRenderer = avatarObject.AddComponent<SpriteRenderer>();
            var idleAvatarFrames = LoadAvatarFrames("idle");
            var walkAvatarFrames = LoadAvatarFrames("walk");
            var sprintAvatarFrames = LoadAvatarFrames("sprint");
            var slideAvatarFrames = LoadAvatarFrames("slide");
            var crouchAvatarFrames = LoadAvatarFrames("crouch");
            var crouchWalkAvatarFrames = LoadAvatarFrames("crouchwalk");
            var jumpAvatarFrames = LoadAvatarFrames("jump");
            var castingAvatarFrames = LoadAvatarFrames("casting");
            var damagedAvatarFrames = LoadAvatarFrames("damaged");
            avatarRenderer.sprite = idleAvatarFrames.FirstOrDefault();
            var avatarAnimator = avatarObject.AddComponent<WofAvatarAnimator>();
            SetObjectReferenceArray(avatarAnimator, "idleFrames", idleAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "walkFrames", walkAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "sprintFrames", sprintAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "slideFrames", slideAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "crouchFrames", crouchAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "crouchWalkFrames", crouchWalkAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "jumpFrames", jumpAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "castingFrames", castingAvatarFrames);
            SetObjectReferenceArray(avatarAnimator, "damagedFrames", damagedAvatarFrames);

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camera = pivot.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 600f;
            pivot.tag = "MainCamera";
            var listener = pivot.AddComponent<AudioListener>();

            var controller = root.AddComponent<WofPlayerController>();
            SetObjectReference(controller, "cameraPivot", pivot.transform);
            SetObjectReference(controller, "playerCamera", camera);
            SetObjectReference(controller, "playerAudioListener", listener);
            SetObjectReference(controller, "visualRoot", visualRoot);
            SetObjectReference(controller, "fireballPrefab", fireballPrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static NetworkPrefabsList ConfigureNetworkPrefabs(GameObject playerPrefab, GameObject fireballPrefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                list.name = "WOF Network Prefabs";
                AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            }

            foreach (var existing in list.PrefabList.ToArray())
            {
                list.Remove(existing);
            }

            list.Add(CreateNetworkPrefab(playerPrefab));
            list.Add(CreateNetworkPrefab(fireballPrefab));
            EditorUtility.SetDirty(list);
            return list;
        }

        private static NetworkPrefab CreateNetworkPrefab(GameObject prefab)
        {
            return new NetworkPrefab
            {
                Override = NetworkPrefabOverride.None,
                Prefab = prefab,
                SourcePrefabToOverride = null,
                SourceHashToOverride = 0,
                OverridingTargetPrefab = null
            };
        }

        private static void CreateBootstrapScene(
            WofMaterialPalette palette,
            GameObject playerPrefab,
            NetworkPrefabsList networkPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WofBootstrap";

            CreateWorld(palette);
            var menuCamera = CreateMenuCamera();

            var networkObject = new GameObject("NetworkSession");
            var networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            var singlePlayerTransport = networkObject.AddComponent<SinglePlayerTransport>();
            networkObject.AddComponent<WofChicagoCitySceneLoader>();
            networkObject.AddComponent<WofSwampVillageSceneLoader>();
            networkObject.AddComponent<WofMountainVillageSceneLoader>();
            networkObject.AddComponent<WofGraveyardVillageSceneLoader>();
            networkObject.AddComponent<WofLilyCoilSceneLoader>();
            transport.UseWebSockets = true;
            transport.UseEncryption = false;
            transport.SetConnectionData("127.0.0.1", WofGameConstants.DefaultPort, "0.0.0.0");
            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ProtocolVersion = WofGameConstants.ProtocolVersion;
            networkManager.NetworkConfig.TickRate = WofGameConstants.ServerTickRate;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(networkPrefabs);

            var ui = CreateUi();
            var bootstrap = networkObject.AddComponent<WofBootstrap>();
            SetObjectReference(bootstrap, "networkManager", networkManager);
            SetObjectReference(bootstrap, "transport", transport);
            SetObjectReference(bootstrap, "singlePlayerTransport", singlePlayerTransport);
            SetObjectReference(bootstrap, "launchPanel", ui.LaunchPanel);
            SetObjectReference(bootstrap, "pressPanel", ui.PressPanel);
            SetObjectReference(bootstrap, "sessionPanel", ui.SessionPanel);
            SetObjectReference(bootstrap, "pressAnywhereButton", ui.PressAnywhereButton);
            SetObjectReference(bootstrap, "addressInput", ui.AddressInput);
            SetObjectReference(bootstrap, "soloButton", ui.SoloButton);
            SetObjectReference(bootstrap, "hostButton", ui.HostButton);
            SetObjectReference(bootstrap, "joinButton", ui.JoinButton);
            SetObjectReference(bootstrap, "launchStatus", ui.LaunchStatus);
            SetObjectReference(bootstrap, "menuCamera", menuCamera);
            SetObjectReference(bootstrap, "hud", ui.Hud);
            SetObjectReference(ui.LaunchFlow, "bootstrap", bootstrap);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateWorld(WofMaterialPalette palette)
        {
            var world = new GameObject("World");
            CreateBaseVillageTerrain(world.transform, palette.Ground);
            CreateSurvivalOpenWorldTerrain(world.transform);
            CreateBaseVillageWater(world.transform, palette);
            CreateBaseVillageBushes(world.transform, palette.Bushes);
            CreateVillagePerimeterWalls(world.transform, palette.Stone);
            CreateBaseVillageHuts(world.transform, palette);
            CreateBaseVillageVillagers(world.transform, palette.Villager);
            CreateCampfire(world.transform, palette);
            CreateTreeHouseVillage(world.transform, palette);
            CreateDarrelGrove(world.transform);
            CreateQuestNavigation(world.transform);
            CreateDesertVillage(world.transform, palette.Villager);

            world.AddComponent<WofSurvivalBotwGrassRuntime>().Configure(
                LoadRequiredAsset<Texture2D>("Assets/WOF/Art/Generated/React/Vegetation/botw-grass.png"));

            var sunlightObject = new GameObject("Sun");
            sunlightObject.transform.position = WofGameWorldLightingLayout.DirectionalLightPosition;
            sunlightObject.transform.rotation = WofGameWorldLightingLayout.GetDirectionalLightRotation();
            var sunlight = sunlightObject.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.color = Color.white;
            sunlight.intensity = WofGameWorldLightingLayout.ClassicDirectionalIntensity;
            sunlight.shadows = LightShadows.None;
            sunlightObject.AddComponent<WofSurvivalSkyRuntime>().Configure(sunlight);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = WofGameWorldLightingLayout.GetClassicAmbientColor();
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.42f, 0.58f, 0.66f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 170f;
            RenderSettings.fogEndDistance = 420f;
        }

        private static void CreateBaseVillageTerrain(Transform parent, Material terrainMaterial)
        {
            var terrain = new GameObject("BaseVillageTerrain");
            terrain.transform.SetParent(parent, false);
            var filter = terrain.AddComponent<MeshFilter>();
            filter.sharedMesh = GetOrCreateVillageTerrainMesh(
                VillageTerrainMeshPath,
                WofBaseVillageLayout.TerrainSegments);
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            renderer.receiveShadows = true;
            var collider = terrain.AddComponent<MeshCollider>();
            collider.sharedMesh = GetOrCreateVillageTerrainMesh(
                VillageTerrainCollisionMeshPath,
                WofBaseVillageLayout.CollisionSegments);
            collider.sharedMaterial = null;
            MarkStatic(terrain);
        }

        private static Mesh GetOrCreateVillageTerrainMesh(string path, int segments)
        {
            var generated = BuildVillageTerrainMesh(segments);
            generated.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh BuildVillageTerrainMesh(int segments)
        {
            var stride = segments + 1;
            var vertices = new Vector3[stride * stride];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * segments * 6];
            var step = WofBaseVillageLayout.MapSize / (float)segments;
            var halfSize = WofBaseVillageLayout.MapSize * 0.5f;

            for (var zIndex = 0; zIndex <= segments; zIndex++)
            {
                var z = -halfSize + zIndex * step;
                for (var xIndex = 0; xIndex <= segments; xIndex++)
                {
                    var x = -halfSize + xIndex * step;
                    var vertexIndex = zIndex * stride + xIndex;
                    vertices[vertexIndex] = new Vector3(x, WofBaseVillageLayout.GetTerrainHeight(x, z), z);
                    uvs[vertexIndex] = new Vector2(xIndex / (float)segments, zIndex / (float)segments);
                }
            }

            var triangleIndex = 0;
            for (var zIndex = 0; zIndex < segments; zIndex++)
            {
                for (var xIndex = 0; xIndex < segments; xIndex++)
                {
                    var bottomLeft = zIndex * stride + xIndex;
                    var topLeft = (zIndex + 1) * stride + xIndex;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topLeft + 1;
                }
            }

            var mesh = new Mesh
            {
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateVillagePerimeterWalls(Transform parent, Material material)
        {
            var walls = new GameObject("ReactVillagePerimeterWalls");
            walls.transform.SetParent(parent, false);

            CreatePrimitive("NorthWallWest", PrimitiveType.Cube, walls.transform,
                new Vector3(-136f, 6f, -238f), new Vector3(208f, 12f, 8f), material);
            CreatePrimitive("NorthWallEast", PrimitiveType.Cube, walls.transform,
                new Vector3(136f, 6f, -238f), new Vector3(208f, 12f, 8f), material);
            CreatePrimitive("SouthWallWest", PrimitiveType.Cube, walls.transform,
                new Vector3(-136f, 6f, 238f), new Vector3(208f, 12f, 8f), material);
            CreatePrimitive("SouthWallEast", PrimitiveType.Cube, walls.transform,
                new Vector3(136f, 6f, 238f), new Vector3(208f, 12f, 8f), material);
            CreatePrimitive("WestWallNorth", PrimitiveType.Cube, walls.transform,
                new Vector3(-238f, 6f, -136f), new Vector3(8f, 12f, 208f), material);
            CreatePrimitive("WestWallSouth", PrimitiveType.Cube, walls.transform,
                new Vector3(-238f, 6f, 136f), new Vector3(8f, 12f, 208f), material);
            CreatePrimitive("EastWallNorth", PrimitiveType.Cube, walls.transform,
                new Vector3(238f, 6f, -136f), new Vector3(8f, 12f, 208f), material);
            CreatePrimitive("EastWallSouth", PrimitiveType.Cube, walls.transform,
                new Vector3(238f, 6f, 136f), new Vector3(8f, 12f, 208f), material);

            CreateVillageGateArch(walls.transform, "North", new Vector3(0f, 0f, -238f), true, material);
            CreateVillageGateArch(walls.transform, "South", new Vector3(0f, 0f, 238f), true, material);
            CreateVillageGateArch(walls.transform, "East", new Vector3(238f, 0f, 0f), false, material);
            CreateVillageGateArch(walls.transform, "West", new Vector3(-238f, 0f, 0f), false, material);
            MarkStatic(walls);
        }

        private static void CreateVillageGateArch(
            Transform parent,
            string side,
            Vector3 center,
            bool northSouth,
            Material material)
        {
            var gate = new GameObject($"VillageGateArch{side}");
            gate.transform.SetParent(parent, false);
            if (northSouth)
            {
                CreatePrimitive("LeftPillar", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(-38f, 8f, 0f), new Vector3(8f, 16f, 10f), material);
                CreatePrimitive("RightPillar", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(38f, 8f, 0f), new Vector3(8f, 16f, 10f), material);
                CreatePrimitive("Beam", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 17.5f, 0f), new Vector3(84f, 5f, 10f), material);
                CreatePrimitive("Crown", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 21f, 0f), new Vector3(18f, 4f, 10f), material);
                var diamond = CreatePrimitive("Diamond", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 15.25f, 0f), new Vector3(8f, 8f, 10f), material);
                diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else
            {
                CreatePrimitive("LeftPillar", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 8f, -38f), new Vector3(10f, 16f, 8f), material);
                CreatePrimitive("RightPillar", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 8f, 38f), new Vector3(10f, 16f, 8f), material);
                CreatePrimitive("Beam", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 17.5f, 0f), new Vector3(10f, 5f, 84f), material);
                CreatePrimitive("Crown", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 21f, 0f), new Vector3(10f, 4f, 18f), material);
                var diamond = CreatePrimitive("Diamond", PrimitiveType.Cube, gate.transform,
                    center + new Vector3(0f, 15.25f, 0f), new Vector3(10f, 8f, 8f), material);
                diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        private static void CreateBaseVillageHuts(Transform parent, WofMaterialPalette palette)
        {
            var hutsRoot = new GameObject("DeterministicHuts");
            hutsRoot.transform.SetParent(parent, false);
            var mushroomCapMesh = GetOrCreateMeshAsset(MushroomCapMeshPath, CreateMushroomCapMesh);
            var moundShellMesh = GetOrCreateMeshAsset(MoundShellMeshPath, CreateMoundShellMesh);
            foreach (var placement in WofBaseVillageLayout.BuildHutPlacements())
            {
                CreateParityHut(hutsRoot.transform, placement, palette, mushroomCapMesh, moundShellMesh);
            }
            MarkStatic(hutsRoot);
        }

        private static void CreateParityHut(
            Transform parent,
            WofHutPlacement placement,
            WofMaterialPalette palette,
            Mesh mushroomCapMesh,
            Mesh moundShellMesh)
        {
            var root = new GameObject($"Hut_{placement.X}_{placement.Z}_{placement.HutType}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(placement.X, placement.Y, placement.Z);
            root.transform.localRotation = Quaternion.Euler(0f, placement.YawRadians * Mathf.Rad2Deg, 0f);

            if (placement.HasPath)
            {
                var pathRoot = new GameObject("Path");
                pathRoot.transform.SetParent(parent, false);
                pathRoot.transform.localPosition = new Vector3(placement.X, placement.Y + 0.05f, placement.Z);
                pathRoot.transform.localRotation = Quaternion.Euler(0f, placement.PathYawRadians * Mathf.Rad2Deg, 0f);
                CreateVisualPrimitive("PathSurface", PrimitiveType.Cube, pathRoot.transform,
                    new Vector3(0f, 0f, 10f), new Vector3(3f, 0.08f, 12f), palette.Path);
            }

            if (placement.HutType == WofHutType.Mushroom)
            {
                AddCompoundHutColliders(root, halfSize: 6f, innerHalf: 5f, height: 8f, doorWidth: 3f, doorHeight: 4.2f);
                AddBoxCollider(root, new Vector3(0f, 0.12f, 0f), new Vector3(10f, 0.24f, 10f));
                AddBoxCollider(root, new Vector3(0f, 13f, 0f), new Vector3(18f, 4.8f, 18f));
                CreateVisualPrimitive("Floor", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0.1f, 0f), new Vector3(10f, 0.08f, 10f), palette.WoodPlank);
                CreateVisualPrimitive("StemLeft", PrimitiveType.Cube, root.transform,
                    new Vector3(-5.5f, 4f, 0f), new Vector3(1f, 8f, 12f), palette.Stem);
                CreateVisualPrimitive("StemRight", PrimitiveType.Cube, root.transform,
                    new Vector3(5.5f, 4f, 0f), new Vector3(1f, 8f, 12f), palette.Stem);
                CreateVisualPrimitive("StemBack", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 4f, -5.5f), new Vector3(10f, 8f, 1f), palette.Stem);
                CreateVisualPrimitive("StemFrontLeft", PrimitiveType.Cube, root.transform,
                    new Vector3(-3.75f, 4f, 5.5f), new Vector3(4.5f, 8f, 1f), palette.Stem);
                CreateVisualPrimitive("StemFrontRight", PrimitiveType.Cube, root.transform,
                    new Vector3(3.75f, 4f, 5.5f), new Vector3(4.5f, 8f, 1f), palette.Stem);
                CreateVisualPrimitive("StemLintel", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 6.1f, 5.5f), new Vector3(3f, 3.8f, 1f), palette.Stem);
                CreateMeshVisual("Cap", root.transform, new Vector3(0f, 13f, 0f),
                    mushroomCapMesh, palette.MushroomCaps[placement.ColorIndex]);
                CreateVisualPrimitive("Door", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 2f, 6.01f), new Vector3(3f, 4f, 0.04f), palette.Door);
                CreateVisualPrimitive("WindowLeft", PrimitiveType.Cube, root.transform,
                    new Vector3(-3.5f, 4.5f, 6.015f), new Vector3(1.5f, 1.5f, 0.03f), palette.HutGlass);
                CreateVisualPrimitive("WindowRight", PrimitiveType.Cube, root.transform,
                    new Vector3(3.5f, 4.5f, 6.015f), new Vector3(1.5f, 1.5f, 0.03f), palette.HutGlass);
                CreateLantern(root.transform, new Vector3(7.5f, 6f, 7.5f), 2.5f, palette);
            }
            else
            {
                AddCompoundHutColliders(root, halfSize: 9f, innerHalf: 8f, height: 12f, doorWidth: 3f, doorHeight: 4.2f);
                AddBoxCollider(root, new Vector3(0f, 0.12f, 0f), new Vector3(16f, 0.24f, 16f));
                var shellMaterial = placement.HutType == WofHutType.GrassMound
                    ? palette.HutGrass
                    : placement.HutType == WofHutType.Log
                        ? palette.Log
                        : palette.DirtGrass;
                CreateVisualPrimitive("Floor", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0.1f, 0f), new Vector3(16f, 0.08f, 16f), palette.WoodPlank);
                CreateMeshVisual("MoundShell", root.transform, new Vector3(0f, 6f, 0f),
                    moundShellMesh, shellMaterial);
                var entranceMaterial = placement.HutType == WofHutType.Log ? palette.Log : palette.Stonework;
                CreateVisualPrimitive("EntranceLeft", PrimitiveType.Cube, root.transform,
                    new Vector3(-2.25f, 3f, 7.5f), new Vector3(1.5f, 6f, 2f), entranceMaterial);
                CreateVisualPrimitive("EntranceRight", PrimitiveType.Cube, root.transform,
                    new Vector3(2.25f, 3f, 7.5f), new Vector3(1.5f, 6f, 2f), entranceMaterial);
                CreateVisualPrimitive("EntranceLintel", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 5.05f, 7.5f), new Vector3(3f, 1.9f, 2f), entranceMaterial);
                CreateVisualPrimitive("Door", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 2f, 8.51f), new Vector3(3f, 4f, 0.04f), palette.Door);
                if (placement.HutType == WofHutType.Log || placement.HutType == WofHutType.DirtAndStone)
                {
                    AddBoxCollider(root, new Vector3(0f, 12f, 0f), new Vector3(8f, 0.4f, 8f));
                    CreateVisualPrimitive("FlatRoof", PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 12f, 0f), new Vector3(8f, 0.4f, 8f),
                        placement.HutType == WofHutType.Log ? palette.WoodPlank : palette.HutGrass);
                }

                var angledBrace = placement.HutType == WofHutType.GrassMound;
                CreateLanternPole(root, palette, angledBrace);
                CreateLantern(root.transform, new Vector3(-3f, 13f, 9.2f), 3f, palette);
            }

            MarkStatic(root);
        }

        private static void CreateLanternPole(GameObject root, WofMaterialPalette palette, bool angledBrace)
        {
            CreateVisualPrimitive("LanternPoleVertical", PrimitiveType.Cube, root.transform,
                new Vector3(-3f, 8f, 2f), new Vector3(0.8f, 16f, 0.8f), palette.HutIron);
            var horizontalPosition = angledBrace ? new Vector3(-3f, 15.6f, 5.6f) : new Vector3(-3f, 15.5f, 5f);
            CreateVisualPrimitive("LanternPoleHorizontal", PrimitiveType.Cube, root.transform,
                horizontalPosition, new Vector3(0.8f, 0.8f, 6f), palette.HutIron);
            AddBoxCollider(root, new Vector3(-3f, 8f, 2f), new Vector3(0.8f, 16f, 0.8f));
            AddBoxCollider(root, horizontalPosition, new Vector3(0.8f, 0.8f, 6f));
            if (!angledBrace)
            {
                return;
            }

            var brace = CreateVisualPrimitive("LanternPoleBrace", PrimitiveType.Cube, root.transform,
                new Vector3(-3f, 14.2f, 3.2f), new Vector3(0.6f, 0.6f, 3f), palette.HutIron);
            brace.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
        }

        private static void CreateLantern(
            Transform parent,
            Vector3 position,
            float chainLength,
            WofMaterialPalette palette)
        {
            var group = new GameObject("Lantern");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = position;
            AddBoxCollider(group, new Vector3(0f, 1.2f + chainLength * 0.5f, 0f),
                new Vector3(0.2f, chainLength, 0.2f));
            AddBoxCollider(group, new Vector3(0f, -0.1f, 0f), new Vector3(1.6f, 2.6f, 1.6f));
            CreateVisualPrimitive("Chain", PrimitiveType.Cube, group.transform,
                new Vector3(0f, 1.2f + chainLength * 0.5f, 0f), new Vector3(0.2f, chainLength, 0.2f), palette.HutIron);
            CreateVisualPrimitive("Cap", PrimitiveType.Cube, group.transform,
                new Vector3(0f, 1f, 0f), new Vector3(1f, 0.4f, 1f), palette.HutIron);
            CreateVisualPrimitive("Top", PrimitiveType.Cube, group.transform,
                new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 0.4f, 1.6f), palette.HutIron);
            CreateVisualPrimitive("OuterGlow", PrimitiveType.Sphere, group.transform,
                new Vector3(0f, -0.4f, 0f), Vector3.one * 4.7f, palette.LanternOuterGlow);
            CreateVisualPrimitive("GlowHalo", PrimitiveType.Sphere, group.transform,
                new Vector3(0f, -0.4f, 0f), Vector3.one * 3.1f, palette.LanternHalo);
            CreateVisualPrimitive("Glass", PrimitiveType.Cube, group.transform,
                new Vector3(0f, -0.4f, 0f), new Vector3(1.1f, 1.6f, 1.1f), palette.LanternGlow);
            foreach (var corner in new[]
                     {
                         new Vector3(-0.7f, -0.4f, -0.7f),
                         new Vector3(0.7f, -0.4f, -0.7f),
                         new Vector3(-0.7f, -0.4f, 0.7f),
                         new Vector3(0.7f, -0.4f, 0.7f)
                     })
            {
                CreateVisualPrimitive("Frame", PrimitiveType.Cube, group.transform,
                    corner, new Vector3(0.2f, 1.6f, 0.2f), palette.HutIron);
            }
            CreateVisualPrimitive("Base", PrimitiveType.Cube, group.transform,
                new Vector3(0f, -1.4f, 0f), new Vector3(1.6f, 0.4f, 1.6f), palette.HutIron);
            MarkStatic(group);
        }

        private static void AddCompoundHutColliders(
            GameObject root,
            float halfSize,
            float innerHalf,
            float height,
            float doorWidth,
            float doorHeight)
        {
            const float wallThickness = 1f;
            var frontSegmentWidth = (halfSize * 2f - doorWidth) * 0.5f;
            AddBoxCollider(root, new Vector3(-innerHalf - wallThickness * 0.5f, height * 0.5f, 0f),
                new Vector3(wallThickness, height, halfSize * 2f));
            AddBoxCollider(root, new Vector3(innerHalf + wallThickness * 0.5f, height * 0.5f, 0f),
                new Vector3(wallThickness, height, halfSize * 2f));
            AddBoxCollider(root, new Vector3(0f, height * 0.5f, -innerHalf - wallThickness * 0.5f),
                new Vector3(halfSize * 2f, height, wallThickness));
            AddBoxCollider(root,
                new Vector3(-doorWidth * 0.5f - frontSegmentWidth * 0.5f, height * 0.5f, innerHalf + wallThickness * 0.5f),
                new Vector3(frontSegmentWidth, height, wallThickness));
            AddBoxCollider(root,
                new Vector3(doorWidth * 0.5f + frontSegmentWidth * 0.5f, height * 0.5f, innerHalf + wallThickness * 0.5f),
                new Vector3(frontSegmentWidth, height, wallThickness));
            var lintelHeight = height - doorHeight;
            AddBoxCollider(root,
                new Vector3(0f, doorHeight + lintelHeight * 0.5f, innerHalf + wallThickness * 0.5f),
                new Vector3(doorWidth, lintelHeight, wallThickness));
        }

        private static void AddBoxCollider(GameObject target, Vector3 center, Vector3 size)
        {
            var collider = target.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static Mesh GetOrCreateMeshAsset(string assetPath, Func<Mesh> factory)
        {
            var generated = factory();
            generated.name = Path.GetFileNameWithoutExtension(assetPath);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh CreateMushroomCapMesh()
        {
            const float bottomHalf = 9f;
            const float topHalf = 3f;
            const float bottomY = -5f;
            const float topY = 5f;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            AddQuad(vertices, uv, triangles,
                new Vector3(-bottomHalf, bottomY, bottomHalf),
                new Vector3(bottomHalf, bottomY, bottomHalf),
                new Vector3(topHalf, topY, topHalf),
                new Vector3(-topHalf, topY, topHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(bottomHalf, bottomY, -bottomHalf),
                new Vector3(-bottomHalf, bottomY, -bottomHalf),
                new Vector3(-topHalf, topY, -topHalf),
                new Vector3(topHalf, topY, -topHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-bottomHalf, bottomY, -bottomHalf),
                new Vector3(-bottomHalf, bottomY, bottomHalf),
                new Vector3(-topHalf, topY, topHalf),
                new Vector3(-topHalf, topY, -topHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(bottomHalf, bottomY, bottomHalf),
                new Vector3(bottomHalf, bottomY, -bottomHalf),
                new Vector3(topHalf, topY, -topHalf),
                new Vector3(topHalf, topY, topHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-topHalf, topY, topHalf),
                new Vector3(topHalf, topY, topHalf),
                new Vector3(topHalf, topY, -topHalf),
                new Vector3(-topHalf, topY, -topHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-bottomHalf, bottomY, -bottomHalf),
                new Vector3(bottomHalf, bottomY, -bottomHalf),
                new Vector3(bottomHalf, bottomY, bottomHalf),
                new Vector3(-bottomHalf, bottomY, bottomHalf));
            return BuildMesh("ReactMushroomCap", vertices, uv, triangles);
        }

        private static Mesh CreateMoundShellMesh()
        {
            const float outerBottomY = -6f;
            const float outerTopY = 6f;
            const float outerBottomHalf = 9f;
            const float outerTopHalf = 4f;
            const float doorTopY = -1.9f;
            const float doorHalfWidth = 1.5f;
            var outerDoorHalf = Mathf.Lerp(outerBottomHalf, outerTopHalf,
                (doorTopY - outerBottomY) / (outerTopY - outerBottomY));
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            AddFrustumSide(vertices, uv, triangles, outerBottomHalf, outerTopHalf, outerBottomY, outerTopY, 0);
            AddFrustumSide(vertices, uv, triangles, outerBottomHalf, outerTopHalf, outerBottomY, outerTopY, 1);
            AddFrustumSide(vertices, uv, triangles, outerBottomHalf, outerTopHalf, outerBottomY, outerTopY, 2);
            AddQuad(vertices, uv, triangles,
                new Vector3(-outerBottomHalf, outerBottomY, outerBottomHalf),
                new Vector3(-doorHalfWidth, outerBottomY, outerBottomHalf),
                new Vector3(-doorHalfWidth, doorTopY, outerDoorHalf),
                new Vector3(-outerDoorHalf, doorTopY, outerDoorHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(doorHalfWidth, outerBottomY, outerBottomHalf),
                new Vector3(outerBottomHalf, outerBottomY, outerBottomHalf),
                new Vector3(outerDoorHalf, doorTopY, outerDoorHalf),
                new Vector3(doorHalfWidth, doorTopY, outerDoorHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-outerDoorHalf, doorTopY, outerDoorHalf),
                new Vector3(outerDoorHalf, doorTopY, outerDoorHalf),
                new Vector3(outerTopHalf, outerTopY, outerTopHalf),
                new Vector3(-outerTopHalf, outerTopY, outerTopHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-outerTopHalf, outerTopY, outerTopHalf),
                new Vector3(outerTopHalf, outerTopY, outerTopHalf),
                new Vector3(outerTopHalf, outerTopY, -outerTopHalf),
                new Vector3(-outerTopHalf, outerTopY, -outerTopHalf));

            const float innerBottomY = -6.5f;
            const float innerTopY = 5.5f;
            const float innerBottomHalf = 8f;
            const float innerTopHalf = 3.5f;
            var innerDoorHalf = Mathf.Lerp(innerBottomHalf, innerTopHalf,
                (doorTopY - innerBottomY) / (innerTopY - innerBottomY));
            AddFrustumSide(vertices, uv, triangles, innerBottomHalf, innerTopHalf, innerBottomY, innerTopY, 0);
            AddFrustumSide(vertices, uv, triangles, innerBottomHalf, innerTopHalf, innerBottomY, innerTopY, 1);
            AddFrustumSide(vertices, uv, triangles, innerBottomHalf, innerTopHalf, innerBottomY, innerTopY, 2);
            AddQuad(vertices, uv, triangles,
                new Vector3(-innerBottomHalf, innerBottomY, innerBottomHalf),
                new Vector3(-doorHalfWidth, innerBottomY, innerBottomHalf),
                new Vector3(-doorHalfWidth, doorTopY, innerDoorHalf),
                new Vector3(-innerDoorHalf, doorTopY, innerDoorHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(doorHalfWidth, innerBottomY, innerBottomHalf),
                new Vector3(innerBottomHalf, innerBottomY, innerBottomHalf),
                new Vector3(innerDoorHalf, doorTopY, innerDoorHalf),
                new Vector3(doorHalfWidth, doorTopY, innerDoorHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-innerDoorHalf, doorTopY, innerDoorHalf),
                new Vector3(innerDoorHalf, doorTopY, innerDoorHalf),
                new Vector3(innerTopHalf, innerTopY, innerTopHalf),
                new Vector3(-innerTopHalf, innerTopY, innerTopHalf));
            AddQuad(vertices, uv, triangles,
                new Vector3(-innerTopHalf, innerTopY, -innerTopHalf),
                new Vector3(innerTopHalf, innerTopY, -innerTopHalf),
                new Vector3(innerTopHalf, innerTopY, innerTopHalf),
                new Vector3(-innerTopHalf, innerTopY, innerTopHalf));
            return BuildMesh("ReactMoundShell", vertices, uv, triangles);
        }

        private static void AddFrustumSide(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            float bottomHalf,
            float topHalf,
            float bottomY,
            float topY,
            int side)
        {
            switch (side)
            {
                case 0:
                    AddQuad(vertices, uv, triangles,
                        new Vector3(bottomHalf, bottomY, -bottomHalf),
                        new Vector3(-bottomHalf, bottomY, -bottomHalf),
                        new Vector3(-topHalf, topY, -topHalf),
                        new Vector3(topHalf, topY, -topHalf));
                    break;
                case 1:
                    AddQuad(vertices, uv, triangles,
                        new Vector3(-bottomHalf, bottomY, -bottomHalf),
                        new Vector3(-bottomHalf, bottomY, bottomHalf),
                        new Vector3(-topHalf, topY, topHalf),
                        new Vector3(-topHalf, topY, -topHalf));
                    break;
                case 2:
                    AddQuad(vertices, uv, triangles,
                        new Vector3(bottomHalf, bottomY, bottomHalf),
                        new Vector3(bottomHalf, bottomY, -bottomHalf),
                        new Vector3(topHalf, topY, -topHalf),
                        new Vector3(topHalf, topY, topHalf));
                    break;
            }
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 bottomLeft,
            Vector3 bottomRight,
            Vector3 topRight,
            Vector3 topLeft)
        {
            var start = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(topLeft);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(1f, 1f));
            uv.Add(new Vector2(0f, 1f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Mesh BuildMesh(
            string name,
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateMeshVisual(
            string name,
            Transform parent,
            Vector3 localPosition,
            Mesh mesh,
            Material material)
        {
            var instance = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.GetComponent<MeshFilter>().sharedMesh = mesh;
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            MarkStatic(instance);
            return instance;
        }

        private static void CreateCampfire(Transform parent, WofMaterialPalette palette)
        {
            var y = WofBaseVillageLayout.GetTerrainHeight(WofBaseVillageLayout.CampfireX, WofBaseVillageLayout.CampfireZ);
            var root = new GameObject("Campfire");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(WofBaseVillageLayout.CampfireX, y, WofBaseVillageLayout.CampfireZ);
            var firstLog = CreateVisualPrimitive("LogA", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.4f, 0.4f), palette.CampfireLog);
            firstLog.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var secondLog = CreateVisualPrimitive("LogB", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.4f, 0.4f), palette.CampfireLog);
            secondLog.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            var fireMesh = GetOrCreateMeshAsset(CampfireSphereMeshPath, () => CreateUvSphereMesh(0.4f, 8, 8));
            CreateMeshVisual("Fire", root.transform, new Vector3(0f, 0.6f, 0f), fireMesh, palette.Fire);
            var lightObject = new GameObject("CampfireLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(255, 136, 0, 255);
            light.range = 15f;
            var initialFlicker = WofBaseVillageLayout.GetCampfireFlicker(0f);
            light.intensity = initialFlicker.Intensity;
            lightObject.transform.localPosition = new Vector3(0f, initialFlicker.LightY, 0f);
            root.AddComponent<WofCampfireRuntime>().Configure(lightObject.transform, light);
        }

        private static void CreateBaseVillageWater(Transform parent, WofMaterialPalette palette)
        {
            var root = new GameObject("BaseVillageWater");
            root.transform.SetParent(parent, false);

            var waterMesh = GetOrCreateMeshAsset(WaterPlaneMeshPath, CreateWaterPlaneMesh);
            var waterPlane = CreateMeshVisual(
                "WaterPlane",
                root.transform,
                new Vector3(0f, WofWaterRippleLayout.WaterPlaneY, 0f),
                waterMesh,
                palette.Water);
            waterPlane.GetComponent<MeshRenderer>().sortingOrder = -2;

            var desktopRing = GetOrCreateMeshAsset(
                DesktopWaterRingMeshPath,
                () => CreateRingMesh(
                    WofWaterRippleLayout.InnerRadius,
                    WofWaterRippleLayout.OuterRadius,
                    WofWaterRippleLayout.DesktopSegments));
            var mobileRing = GetOrCreateMeshAsset(
                MobileWaterRingMeshPath,
                () => CreateRingMesh(
                    WofWaterRippleLayout.InnerRadius,
                    WofWaterRippleLayout.OuterRadius,
                    WofWaterRippleLayout.MobileSegments));
            root.AddComponent<WofWaterRippleRuntime>().Configure(desktopRing, mobileRing, palette.WaterRipple);
        }

        private static void CreateBaseVillageBushes(Transform parent, Material[] materials)
        {
            var root = new GameObject("FacetedVillageBushes");
            root.transform.SetParent(parent, false);
            var bushMesh = GetOrCreateMeshAsset(BushMeshPath, CreateReactBushMesh);
            root.AddComponent<WofBushRenderer>().Configure(bushMesh, materials);
        }

        private static void CreateBaseVillageVillagers(Transform parent, Material material)
        {
            var json = LoadRequiredAsset<TextAsset>(VillagerLayoutJsonPath);
            var document = JsonUtility.FromJson<WofVillagerLayoutDocument>(json.text);
            if (document == null ||
                document.schemaVersion != 1 ||
                document.count != 307 ||
                document.villagers == null ||
                document.villagers.Length != 307 ||
                string.IsNullOrWhiteSpace(document.darrelArchiveFile) ||
                document.darrelArchiveBytes <= 0 ||
                string.IsNullOrWhiteSpace(document.darrelArchiveSha256) ||
                document.frameContract == null ||
                document.frameContract.archiveEntriesPerVillager != 52)
            {
                throw new InvalidOperationException($"Invalid baked React villager layout at {VillagerLayoutJsonPath}.");
            }

            var root = new GameObject("ReactBaseVillageVillagers");
            root.transform.SetParent(parent, false);
            var billboards = new WofVillagerBillboard[document.villagers.Length];
            for (var index = 0; index < document.villagers.Length; index++)
            {
                var record = document.villagers[index];
                if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.archiveFile))
                {
                    throw new InvalidOperationException($"Invalid React villager record at index {index}.");
                }

                var villager = new GameObject($"Villager_{record.id}");
                villager.transform.SetParent(root.transform, false);
                var visual = new GameObject("AvatarBillboard");
                visual.transform.SetParent(villager.transform, false);
                visual.transform.localPosition = new Vector3(0f, WofVillagerMath.AvatarWorldCenterY, 0f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;

                var billboard = villager.AddComponent<WofVillagerBillboard>();
                var archiveFile = string.Equals(record.id, WofQuestDialogRules.DarrelNpcId, StringComparison.Ordinal)
                    ? document.darrelArchiveFile
                    : record.archiveFile;
                billboard.Configure(
                    record.id,
                    archiveFile,
                    new Vector3(record.x, record.y + WofVillagerMath.AvatarGroundLift, record.z),
                    record.baseYaw,
                    record.lookUpdateDesktopMs,
                    record.lookUpdateMobileMs,
                    record.hut,
                    visual.transform,
                    renderer,
                    record.id == WofQuestDialogRules.DarrelNpcId
                        ? "Darrel"
                        : $"Town Villager {index + 1}",
                    "base-village");
                billboards[index] = billboard;
            }

            root.AddComponent<WofVillagerManager>().Configure(billboards);
        }

        private static Mesh CreateReactBushMesh()
        {
            var json = LoadRequiredAsset<TextAsset>(BushGeometryJsonPath);
            var data = JsonUtility.FromJson<ReactBushGeometryDocument>(json.text);
            if (data == null || data.schemaVersion != 1 || data.vertexCount <= 0 ||
                data.positions == null || data.normals == null || data.barycentric == null ||
                data.positions.Length != data.vertexCount * 3 ||
                data.normals.Length != data.vertexCount * 3 ||
                data.barycentric.Length != data.vertexCount * 3 ||
                data.vertexCount % 3 != 0)
            {
                throw new InvalidOperationException($"Invalid baked React bush geometry at {BushGeometryJsonPath}.");
            }

            var vertices = new List<Vector3>(data.vertexCount);
            var normals = new List<Vector3>(data.vertexCount);
            var barycentric = new List<Vector3>(data.vertexCount);
            for (var index = 0; index < data.vertexCount; index++)
            {
                var offset = index * 3;
                vertices.Add(new Vector3(data.positions[offset], data.positions[offset + 1], data.positions[offset + 2]));
                normals.Add(new Vector3(data.normals[offset], data.normals[offset + 1], data.normals[offset + 2]));
                barycentric.Add(new Vector3(data.barycentric[offset], data.barycentric[offset + 1], data.barycentric[offset + 2]));
            }

            var mesh = new Mesh { name = "React Bush Dodecahedron" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(1, barycentric);
            mesh.SetTriangles(Enumerable.Range(0, data.vertexCount).ToArray(), 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateWaterPlaneMesh()
        {
            var halfSize = WofBaseVillageLayout.MapSize * 0.5f;
            var mesh = new Mesh { name = "React Base Village Water Plane" };
            mesh.SetVertices(new List<Vector3>
            {
                new(-halfSize, 0f, -halfSize),
                new(halfSize, 0f, -halfSize),
                new(-halfSize, 0f, halfSize),
                new(halfSize, 0f, halfSize)
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2>
            {
                new(0f, 0f),
                new(1f, 0f),
                new(0f, 1f),
                new(1f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRingMesh(float innerRadius, float outerRadius, int thetaSegments)
        {
            thetaSegments = Mathf.Max(3, thetaSegments);
            var vertices = new List<Vector3>((thetaSegments + 1) * 2);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            for (var row = 0; row <= 1; row++)
            {
                var radius = Mathf.Lerp(innerRadius, outerRadius, row);
                for (var segment = 0; segment <= thetaSegments; segment++)
                {
                    var angle = segment / (float)thetaSegments * Mathf.PI * 2f;
                    var x = radius * Mathf.Cos(angle);
                    var y = radius * Mathf.Sin(angle);
                    vertices.Add(new Vector3(x, y, 0f));
                    normals.Add(Vector3.forward);
                    uvs.Add(new Vector2(x / outerRadius * 0.5f + 0.5f, y / outerRadius * 0.5f + 0.5f));
                }
            }

            var triangles = new List<int>(thetaSegments * 6);
            var rowLength = thetaSegments + 1;
            for (var segment = 0; segment < thetaSegments; segment++)
            {
                var a = segment;
                var b = segment + rowLength;
                var c = segment + 1 + rowLength;
                var d = segment + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }

            var mesh = new Mesh { name = $"React Water Ring {thetaSegments}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateUvSphereMesh(float radius, int widthSegments, int heightSegments)
        {
            widthSegments = Mathf.Max(3, widthSegments);
            heightSegments = Mathf.Max(2, heightSegments);
            var vertices = new List<Vector3>((widthSegments + 1) * (heightSegments + 1));
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var rows = new List<int[]>(heightSegments + 1);

            for (var y = 0; y <= heightSegments; y++)
            {
                var row = new int[widthSegments + 1];
                var v = y / (float)heightSegments;
                var uOffset = y == 0 ? 0.5f / widthSegments : y == heightSegments ? -0.5f / widthSegments : 0f;
                var theta = v * Mathf.PI;
                for (var x = 0; x <= widthSegments; x++)
                {
                    var u = x / (float)widthSegments;
                    var phi = u * Mathf.PI * 2f;
                    var vertex = new Vector3(
                        -radius * Mathf.Cos(phi) * Mathf.Sin(theta),
                        radius * Mathf.Cos(theta),
                        radius * Mathf.Sin(phi) * Mathf.Sin(theta));
                    row[x] = vertices.Count;
                    vertices.Add(vertex);
                    normals.Add(vertex.normalized);
                    uvs.Add(new Vector2(u + uOffset, 1f - v));
                }
                rows.Add(row);
            }

            var triangles = new List<int>(widthSegments * (heightSegments - 1) * 6);
            for (var y = 0; y < heightSegments; y++)
            {
                for (var x = 0; x < widthSegments; x++)
                {
                    var a = rows[y][x + 1];
                    var b = rows[y][x];
                    var c = rows[y + 1][x];
                    var d = rows[y + 1][x + 1];
                    if (y != 0)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(d);
                    }
                    if (y != heightSegments - 1)
                    {
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(d);
                    }
                }
            }

            var mesh = new Mesh { name = "React Campfire Sphere" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateVisualPrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var instance = CreatePrimitive(name, type, parent, position, scale, material);
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            MarkStatic(instance);
            return instance;
        }

        private static void MarkStatic(GameObject target)
        {
            target.isStatic = true;
            GameObjectUtility.SetStaticEditorFlags(target,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.NavigationStatic);
        }

        private static Camera CreateMenuCamera()
        {
            var cameraObject = new GameObject("MenuCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 15f, -23f), Quaternion.Euler(22f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static WofUiReferences CreateUi()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
            inputModule.AssignDefaultActions();
            inputModule.moveRepeatDelay = 0.26f;
            inputModule.moveRepeatRate = 0.13f;
            eventSystemObject.AddComponent<WofControllerDeviceRuntime>();

            var canvasObject = new GameObject("WOF_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasObject.AddComponent<WofHud>();
            var font = LoadRequiredAsset<Font>("Assets/WOF/Art/Fonts/PressStart2P-Regular.ttf");

            var launchPanel = CreatePanel("LaunchPanel", canvasObject.transform, Vector2.zero, Vector2.one, new Color(0.025f, 0.02f, 0.06f, 0.78f));
            var launchSafeArea = CreateSafeAreaRoot("LaunchSafeArea", launchPanel.transform);
            var pressButton = CreateButton("PressAnywhereButton", launchSafeArea.transform, font, string.Empty, new Color(0.02f, 0.008f, 0.027f, 0.96f));
            SetRect(pressButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var pressBackground = LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/Launch/press-background.png");
            pressButton.GetComponent<Image>().sprite = pressBackground;
            pressButton.GetComponent<Image>().color = Color.white;
            var pressLabel = pressButton.transform.Find("Label")?.GetComponent<Text>();
            if (pressLabel != null)
            {
                pressLabel.enabled = false;
            }

            var title = CreateText("Title", pressButton.transform, font, "WIZARDS\nONLY\nFOOLS!", 88, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            title.fontStyle = FontStyle.Bold;
            title.lineSpacing = 0.85f;
            SetRect(title.rectTransform, new Vector2(0.14f, 0.38f), new Vector2(0.86f, 0.78f));
            var pressPrompt = CreateText("Prompt", pressButton.transform, font, "PRESS ANYWHERE TO PLAY", 32, TextAnchor.MiddleCenter, new Color32(207, 250, 254, 255));
            pressPrompt.fontStyle = FontStyle.Bold;
            SetRect(pressPrompt.rectTransform, new Vector2(0.12f, 0.27f), new Vector2(0.88f, 0.36f));
            var controllerPrompt = CreateText("ControllerPrompt", pressButton.transform, font, "CONTROLLER: A / START", 19, TextAnchor.MiddleCenter, new Color32(207, 250, 254, 153));
            SetRect(controllerPrompt.rectTransform, new Vector2(0.20f, 0.21f), new Vector2(0.80f, 0.27f));
            var pressLayout = pressButton.gameObject.AddComponent<WofLaunchPressLayout>();
            SetObjectReference(pressLayout, "title", title);
            SetObjectReference(pressLayout, "prompt", pressPrompt);
            SetObjectReference(pressLayout, "controllerPrompt", controllerPrompt);

            var sessionPanel = CreatePanel("SessionPanel", launchSafeArea.transform, Vector2.zero, Vector2.one, new Color32(5, 2, 7, 245));
            var saveStage = CreateLaunchSaveStage(sessionPanel.transform, font, out var newButton, out var continueButton, out var continueButtonLabel, out var multiplayerButton);
            var newWizardStage = CreateLaunchNewWizardStage(
                sessionPanel.transform,
                font,
                out var newTitle,
                out var preview,
                out var playerNameInput,
                out var optionCards,
                out var xpCard,
                out var actionButtons);
            var outfitButton = optionCards[0];
            var skinButton = optionCards[1];
            var hairColorButton = optionCards[2];
            var hatButton = optionCards[3];
            var hairButton = optionCards[4];
            var startSoloButton = actionButtons[0];
            var startSurvivalMultiplayerButton = actionButtons[1];
            var newBackButton = actionButtons[2];
            var multiplayerStage = CreateLaunchMultiplayerStage(
                sessionPanel.transform,
                font,
                out var customLobbyButton,
                out var survivalMultiplayerButton,
                out var multiplayerBackButton);
            var lobbyStage = CreateLaunchLobbyStage(
                sessionPanel.transform,
                font,
                out var lobbyTitle,
                out var addressInput,
                out var mobileLinkInput,
                out var createLobbyButton,
                out var copyMobileLinkButton,
                out var lobbyBackButton);
            var launchStatus = CreateText("LaunchStatus", sessionPanel.transform, font, string.Empty, 14, TextAnchor.LowerCenter, new Color32(207, 250, 254, 220));
            SetRect(launchStatus.rectTransform, new Vector2(0.08f, 0.01f), new Vector2(0.92f, 0.08f));

            var launchFlow = sessionPanel.AddComponent<WofLaunchFlow>();
            SetObjectReference(launchFlow, "saveStage", saveStage);
            SetObjectReference(launchFlow, "newWizardStage", newWizardStage);
            SetObjectReference(launchFlow, "multiplayerStage", multiplayerStage);
            SetObjectReference(launchFlow, "lobbyStage", lobbyStage);
            SetObjectReference(launchFlow, "newButton", newButton);
            SetObjectReference(launchFlow, "continueButton", continueButton);
            SetObjectReference(launchFlow, "continueButtonLabel", continueButtonLabel);
            SetObjectReference(launchFlow, "multiplayerButton", multiplayerButton);
            SetObjectReference(launchFlow, "wizardPreview", preview.GetComponent<WofLaunchWizardPreviewRenderer>());
            SetObjectReference(launchFlow, "playerNameInput", playerNameInput);
            SetObjectReference(launchFlow, "outfitButton", outfitButton);
            SetObjectReference(launchFlow, "outfitButtonLabel", outfitButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "skinButton", skinButton);
            SetObjectReference(launchFlow, "skinButtonLabel", skinButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "hairColorButton", hairColorButton);
            SetObjectReference(launchFlow, "hairColorButtonLabel", hairColorButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "hatButton", hatButton);
            SetObjectReference(launchFlow, "hatButtonLabel", hatButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "hairButton", hairButton);
            SetObjectReference(launchFlow, "hairButtonLabel", hairButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "startSoloButton", startSoloButton);
            SetObjectReference(launchFlow, "startSurvivalMultiplayerButton", startSurvivalMultiplayerButton);
            SetObjectReference(launchFlow, "newBackButton", newBackButton);
            SetObjectReference(launchFlow, "customLobbyButton", customLobbyButton);
            SetObjectReference(launchFlow, "survivalMultiplayerButton", survivalMultiplayerButton);
            SetObjectReference(launchFlow, "multiplayerBackButton", multiplayerBackButton);
            SetObjectReference(launchFlow, "lobbyTitle", lobbyTitle);
            SetObjectReference(launchFlow, "inviteCodeInput", addressInput);
            SetObjectReference(launchFlow, "mobileLinkInput", mobileLinkInput);
            SetObjectReference(launchFlow, "createLobbyButton", createLobbyButton);
            SetObjectReference(launchFlow, "createLobbyButtonLabel", createLobbyButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "copyMobileLinkButton", copyMobileLinkButton);
            SetObjectReference(launchFlow, "copyMobileLinkButtonLabel", copyMobileLinkButton.transform.Find("Label").GetComponent<Text>());
            SetObjectReference(launchFlow, "lobbyBackButton", lobbyBackButton);
            SetObjectReference(launchFlow, "launchStatus", launchStatus);

            var stageLayout = sessionPanel.AddComponent<WofLaunchStageLayout>();
            SetObjectReference(stageLayout, "savePanel", saveStage.GetComponent<RectTransform>());
            SetObjectReference(stageLayout, "newWizardPanel", newWizardStage.GetComponent<RectTransform>());
            SetObjectReference(stageLayout, "multiplayerPanel", multiplayerStage.GetComponent<RectTransform>());
            SetObjectReference(stageLayout, "lobbyPanel", lobbyStage.GetComponent<RectTransform>());
            SetObjectReference(stageLayout, "newTitle", newTitle.rectTransform);
            SetObjectReference(stageLayout, "preview", preview.rectTransform);
            SetObjectReference(stageLayout, "playerName", playerNameInput.GetComponent<RectTransform>());
            SetObjectReference(stageLayout, "xpCard", xpCard.GetComponent<RectTransform>());
            SetObjectReferenceArray(stageLayout, "optionCards", optionCards.Select(button => button.GetComponent<RectTransform>()).ToArray());
            SetObjectReferenceArray(stageLayout, "actionButtons", actionButtons.Select(button => button.GetComponent<RectTransform>()).ToArray());

            newWizardStage.SetActive(false);
            multiplayerStage.SetActive(false);
            lobbyStage.SetActive(false);
            sessionPanel.SetActive(false);

            var gameplayRoot = CreatePanel("GameplayHUD", canvasObject.transform, Vector2.zero, Vector2.one, Color.clear);
            gameplayRoot.GetComponent<Image>().raycastTarget = false;
            var gameplaySafeArea = CreateSafeAreaRoot("GameplaySafeArea", gameplayRoot.transform);
            var roomText = CreateText("Room", gameplaySafeArea.transform, font, "", 18, TextAnchor.UpperLeft, new Color(0.90f, 0.92f, 1f));
            SetRect(roomText.rectTransform, new Vector2(0.02f, 0.91f), new Vector2(0.50f, 0.98f));
            var statusText = CreateText("Status", gameplaySafeArea.transform, font, "", 20, TextAnchor.LowerCenter, new Color(1f, 0.88f, 0.35f));
            SetRect(statusText.rectTransform, new Vector2(0.22f, 0.205f), new Vector2(0.78f, 0.265f));

            var crosshair = CreateText("Crosshair", gameplaySafeArea.transform, font, "+", 30, TextAnchor.MiddleCenter, Color.white);
            SetRect(crosshair.rectTransform, new Vector2(0.47f, 0.45f), new Vector2(0.53f, 0.55f));

            var leftHandFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Hands/Equipped", "left_idle_");
            var rightHandFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Hands/Equipped", "right_idle_");
            var leftFiringHandFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Hands/Firing", "left_idle_");
            var rightFiringHandFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Hands/Firing", "right_idle_");
            var leftSpellFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Fireball/Equipped", "left_fireballidle_");
            var rightSpellFrames = LoadSprites("Assets/WOF/Art/Generated/React/HUD/Fireball/Equipped", "right_fireballidle_");
            var leftHandImage = CreateImage("LeftEquippedHand", gameplaySafeArea.transform, leftHandFrames.FirstOrDefault(), Color.white);
            leftHandImage.raycastTarget = false;
            var rightHandImage = CreateImage("RightEquippedHand", gameplaySafeArea.transform, rightHandFrames.FirstOrDefault(), Color.white);
            rightHandImage.raycastTarget = false;
            var leftSpellImage = CreateImage("LeftHeldFireball", gameplaySafeArea.transform, leftSpellFrames.FirstOrDefault(), Color.white);
            leftSpellImage.raycastTarget = false;
            var spellImage = CreateImage("RightHeldFireball", gameplaySafeArea.transform, rightSpellFrames.FirstOrDefault(), Color.white);
            spellImage.raycastTarget = false;
            var magicHandsLayout = gameplaySafeArea.gameObject.AddComponent<WofMagicHandsLayout>();
            SetObjectReference(magicHandsLayout, "leftHandFrame", leftHandImage.rectTransform);
            SetObjectReference(magicHandsLayout, "rightHandFrame", rightHandImage.rectTransform);
            SetObjectReference(magicHandsLayout, "leftSpellFrame", leftSpellImage.rectTransform);
            SetObjectReference(magicHandsLayout, "rightSpellFrame", spellImage.rectTransform);

            var gameplayHud = CreateReactGameplayHud(gameplaySafeArea.transform, font);

            var mobileRoot = CreatePanel("MobileControls", gameplaySafeArea.transform, Vector2.zero, Vector2.one, Color.clear);
            mobileRoot.GetComponent<Image>().raycastTarget = false;
            CreateMobileControls(mobileRoot.transform, font);

            SetObjectReference(hud, "gameplayRoot", gameplayRoot);
            SetObjectReference(hud, "mobileRoot", mobileRoot);
            SetObjectReference(hud, "healthFill", gameplayHud.HealthFill);
            SetObjectReference(hud, "armorFill", gameplayHud.ArmorFill);
            SetObjectReference(hud, "healthText", gameplayHud.HealthText);
            SetObjectReference(hud, "armorText", gameplayHud.ArmorText);
            SetObjectReference(hud, "aetherFill", gameplayHud.AetherFill);
            SetObjectReference(hud, "leftManaFill", gameplayHud.LeftManaFill);
            SetObjectReference(hud, "rightManaFill", gameplayHud.RightManaFill);
            SetObjectReference(hud, "leftSpellText", gameplayHud.LeftSpellText);
            SetObjectReference(hud, "rightSpellText", gameplayHud.RightSpellText);
            SetObjectReference(hud, "leftHotkeysText", gameplayHud.LeftHotkeysText);
            SetObjectReference(hud, "rightHotkeysText", gameplayHud.RightHotkeysText);
            SetObjectReference(hud, "statusText", statusText);
            SetObjectReference(hud, "roomText", roomText);
            SetObjectReference(hud, "leftHandImage", leftHandImage);
            SetObjectReference(hud, "rightHandImage", rightHandImage);
            SetObjectReference(hud, "leftHeldSpellImage", leftSpellImage);
            SetObjectReference(hud, "heldSpellImage", spellImage);
            SetObjectReference(hud, "magicHandsLayout", magicHandsLayout);
            SetObjectReferenceArray(hud, "leftHandFrames", leftHandFrames);
            SetObjectReferenceArray(hud, "rightHandFrames", rightHandFrames);
            SetObjectReferenceArray(hud, "leftFiringHandFrames", leftFiringHandFrames);
            SetObjectReferenceArray(hud, "rightFiringHandFrames", rightFiringHandFrames);
            SetObjectReferenceArray(hud, "leftHeldSpellFrames", leftSpellFrames);
            SetObjectReferenceArray(hud, "rightHeldSpellFrames", rightSpellFrames);

            // Construct the dialog hierarchy after scene load. Serializing the complete modal
            // object graph makes Unity 6 emit an unreadable level0 player payload.
            var questDialog = canvasObject.AddComponent<WofQuestDialogRuntime>();
            questDialog.ConfigureGeneratedView(
                hud,
                LoadRequiredAsset<Font>("Assets/WOF/Art/Fonts/VT323-Regular.ttf"));

            var inventory = canvasObject.AddComponent<WofInventoryRuntime>();
            inventory.ConfigureGeneratedView(
                hud,
                font,
                LoadRequiredAsset<Font>("Assets/WOF/Art/Fonts/VT323-Regular.ttf"));

            var commandConsole = canvasObject.AddComponent<WofCommandConsoleRuntime>();
            commandConsole.ConfigureGeneratedView(hud, font);

            var spellMenu = canvasObject.AddComponent<WofSpellMenuRuntime>();
            spellMenu.ConfigureGeneratedView(
                hud,
                canvasObject.transform,
                mobileRoot.transform,
                font,
                LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/HUD/SpellMenu/spellbook_icon.png"),
                LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/HUD/Fireball/fireball_1.png"),
                LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/HUD/SpellMenu/speedboost.png"),
                LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/HUD/SpellMenu/jumpboost.png"),
                WofSpellLoadout.PlayableSpells
                    .Select(spell => LoadRequiredAsset<Sprite>(
                        $"Assets/WOF/Art/Generated/React/HUD/SpellMenu/{WofSpellLoadout.GetReactId(spell)}.png"))
                    .ToArray());

            var spellHotbar = canvasObject.AddComponent<WofSpellHotbarRuntime>();
            spellHotbar.ConfigureGeneratedView(hud);

            var navigationMap = canvasObject.AddComponent<WofNavigationMapRuntime>();
            navigationMap.ConfigureGeneratedView(
                hud,
                canvasObject.transform,
                font,
                GetOrCreateCircularUiMaskSprite());

            var pauseAndScoreboard = canvasObject.AddComponent<WofPauseAndScoreboardRuntime>();
            pauseAndScoreboard.ConfigureGeneratedView(hud, canvasObject.transform, font);

            var engineMenu = canvasObject.AddComponent<WofEngineMenuRuntime>();
            engineMenu.ConfigureGeneratedView(hud, canvasObject.transform, font);

            return new WofUiReferences
            {
                LaunchPanel = launchPanel,
                PressPanel = pressButton.gameObject,
                SessionPanel = sessionPanel,
                PressAnywhereButton = pressButton,
                AddressInput = addressInput,
                SoloButton = startSoloButton,
                HostButton = createLobbyButton,
                JoinButton = null,
                LaunchStatus = launchStatus,
                LaunchFlow = launchFlow,
                Hud = hud
            };
        }

        private static void CreateQuestDialog(Transform parent, WofHud hud)
        {
            var font = LoadRequiredAsset<Font>("Assets/WOF/Art/Fonts/VT323-Regular.ttf");
            var overlay = CreatePanel(
                "QuestDialogOverlay",
                parent,
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.35f));

            var border = CreatePanel(
                "DialogBorder",
                overlay.transform,
                new Vector2(0.302f, 0.205f),
                new Vector2(0.698f, 0.795f),
                new Color32(255, 251, 220, 166));
            var card = CreatePanel(
                "DialogCard",
                border.transform,
                new Vector2(0.003f, 0.004f),
                new Vector2(0.997f, 0.996f),
                new Color32(7, 6, 17, 245));

            var header = CreatePanel(
                "Header",
                card.transform,
                new Vector2(0f, 0.79f),
                Vector2.one,
                new Color32(7, 6, 17, 255));
            var headerDivider = CreatePanel(
                "HeaderDivider",
                header.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.012f),
                new Color32(255, 251, 220, 64));
            headerDivider.GetComponent<Image>().raycastTarget = false;
            var questText = CreateText(
                "QuestKicker",
                header.transform,
                font,
                "QUEST",
                18,
                TextAnchor.LowerLeft,
                new Color32(255, 251, 220, 153));
            SetRect(questText.rectTransform, new Vector2(0.025f, 0.53f), new Vector2(0.72f, 0.84f));
            DisableTextOutline(questText);
            var speakerText = CreateText(
                "Speaker",
                header.transform,
                font,
                "Darrel",
                34,
                TextAnchor.UpperLeft,
                new Color32(255, 253, 230, 255));
            SetRect(speakerText.rectTransform, new Vector2(0.025f, 0.12f), new Vector2(0.72f, 0.56f));
            DisableTextOutline(speakerText);

            var closeBorder = CreatePanel(
                "CloseBorder",
                header.transform,
                new Vector2(0.82f, 0.26f),
                new Vector2(0.968f, 0.74f),
                new Color32(255, 251, 220, 128));
            var closeButton = CreateButton(
                "CloseButton",
                closeBorder.transform,
                font,
                "CLOSE",
                new Color32(250, 240, 190, 24));
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.012f, 0.03f), new Vector2(0.988f, 0.97f));
            var closeLabel = closeButton.transform.Find("Label")?.GetComponent<Text>();
            if (closeLabel != null)
            {
                closeLabel.fontSize = 19;
                closeLabel.resizeTextMaxSize = 19;
                DisableTextOutline(closeLabel);
            }

            var lineBorder = CreatePanel(
                "LineBorder",
                card.transform,
                new Vector2(0.025f, 0.43f),
                new Vector2(0.975f, 0.755f),
                new Color32(165, 243, 252, 51));
            var linePanel = CreatePanel(
                "LinePanel",
                lineBorder.transform,
                new Vector2(0.002f, 0.006f),
                new Vector2(0.998f, 0.994f),
                new Color32(8, 51, 68, 51));
            var lineText = CreateText(
                "Line",
                linePanel.transform,
                font,
                WofQuestDialogRules.OpeningLine,
                24,
                TextAnchor.UpperLeft,
                new Color32(236, 254, 255, 255));
            SetRect(lineText.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.975f, 0.92f));
            lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            lineText.verticalOverflow = VerticalWrapMode.Truncate;
            lineText.resizeTextMinSize = 15;
            DisableTextOutline(lineText);

            var choiceButtons = new Button[2];
            var choiceNumbers = new Text[2];
            var choiceLabels = new Text[2];
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                var top = 0.385f - index * 0.14f;
                var bottom = top - 0.115f;
                var choiceBorder = CreatePanel(
                    $"Choice{index + 1}Border",
                    card.transform,
                    new Vector2(0.025f, bottom),
                    new Vector2(0.975f, top),
                    new Color32(255, 251, 220, 102));
                var button = CreateButton(
                    $"Choice{index + 1}",
                    choiceBorder.transform,
                    font,
                    string.Empty,
                    index == 0 ? new Color32(250, 240, 190, 46) : new Color32(0, 0, 0, 89));
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0.002f, 0.02f), new Vector2(0.998f, 0.98f));
                var hiddenLabel = button.transform.Find("Label")?.GetComponent<Text>();
                if (hiddenLabel != null)
                {
                    hiddenLabel.enabled = false;
                }

                var numberBorder = CreatePanel(
                    "NumberBorder",
                    button.transform,
                    new Vector2(0.018f, 0.18f),
                    new Vector2(0.075f, 0.82f),
                    new Color32(255, 251, 220, 128));
                var number = CreateText(
                    "Number",
                    numberBorder.transform,
                    font,
                    (index + 1).ToString(),
                    20,
                    TextAnchor.MiddleCenter,
                    new Color32(255, 251, 220, 255));
                SetRect(number.rectTransform, Vector2.zero, Vector2.one);
                DisableTextOutline(number);
                var label = CreateText(
                    "ChoiceLabel",
                    button.transform,
                    font,
                    index == 0 ? "None of your business." : "What kind of wizard has only 2 spells?",
                    22,
                    TextAnchor.MiddleLeft,
                    new Color32(255, 253, 230, 255));
                SetRect(label.rectTransform, new Vector2(0.095f, 0.08f), new Vector2(0.975f, 0.92f));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.resizeTextMinSize = 15;
                DisableTextOutline(label);
                choiceButtons[index] = button;
                choiceNumbers[index] = number;
                choiceLabels[index] = label;
            }

            var helpText = CreateText(
                "ControllerHelp",
                card.transform,
                font,
                "D-PAD / STICK CHOOSE  -  A SELECT  -  B CLOSE",
                16,
                TextAnchor.MiddleLeft,
                new Color32(255, 251, 220, 115));
            SetRect(helpText.rectTransform, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.105f));
            DisableTextOutline(helpText);

            var runtime = parent.gameObject.AddComponent<WofQuestDialogRuntime>();
            SetObjectReference(runtime, "dialogRoot", overlay);
            SetObjectReference(runtime, "speakerText", speakerText);
            SetObjectReference(runtime, "lineText", lineText);
            SetObjectReference(runtime, "closeButton", closeButton);
            SetObjectReferenceArray(runtime, "choiceButtons", choiceButtons);
            SetObjectReferenceArray(runtime, "choiceNumberTexts", choiceNumbers);
            SetObjectReferenceArray(runtime, "choiceLabelTexts", choiceLabels);
            SetObjectReference(runtime, "hud", hud);
            for (var index = 0; index < choiceButtons.Length; index++)
            {
                choiceButtons[index].gameObject.AddComponent<WofQuestChoiceHover>().Configure(runtime, index);
            }
            overlay.SetActive(false);
        }

        private static void DisableTextOutline(Text text)
        {
            var outline = text == null ? null : text.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private static GameObject CreateLaunchSaveStage(
            Transform parent,
            Font font,
            out Button newButton,
            out Button continueButton,
            out Text continueButtonLabel,
            out Button multiplayerButton)
        {
            var stage = CreatePanel("SaveStage", parent, Vector2.zero, Vector2.one, new Color32(16, 7, 24, 244));
            AddLaunchCardOutline(stage);
            var title = CreateText("Title", stage.transform, font, "SURVIVAL SAVE", 34, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            SetRect(title.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.96f));
            newButton = CreateButton("NewButton", stage.transform, font, "NEW", new Color32(79, 31, 125, 255));
            SetRect(newButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.72f));
            continueButton = CreateButton("ContinueButton", stage.transform, font, "CONTINUE", new Color32(29, 55, 70, 255));
            SetRect(continueButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.51f));
            continueButtonLabel = continueButton.transform.Find("Label").GetComponent<Text>();
            multiplayerButton = CreateButton("MultiplayerButton", stage.transform, font, "MULTIPLAYER", new Color32(21, 90, 66, 255));
            SetRect(multiplayerButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.30f));
            return stage;
        }

        private static GameObject CreateLaunchNewWizardStage(
            Transform parent,
            Font font,
            out Text title,
            out Image preview,
            out InputField playerNameInput,
            out Button[] optionCards,
            out GameObject xpCard,
            out Button[] actionButtons)
        {
            var stage = CreatePanel("NewWizardStage", parent, Vector2.zero, Vector2.one, new Color32(16, 7, 24, 244));
            AddLaunchCardOutline(stage);
            title = CreateText("Title", stage.transform, font, "NEW WIZARD", 31, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            SetRect(title.rectTransform, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));

            var previewSprite = LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/Avatar/Default/launch-preview.png");
            preview = CreateImage("WizardPreview", stage.transform, previewSprite, Color.white);
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            preview.gameObject.AddComponent<WofLaunchWizardPreviewRenderer>();
            SetRect(preview.rectTransform, new Vector2(0.02f, 0.32f), new Vector2(0.27f, 0.84f));

            playerNameInput = CreateInputField(stage.transform, font);
            playerNameInput.gameObject.name = "PlayerName";
            playerNameInput.characterLimit = 18;
            playerNameInput.text = string.Empty;
            playerNameInput.textComponent.text = string.Empty;
            ((Text)playerNameInput.placeholder).text = "WIZARD NAME";
            SetRect(playerNameInput.GetComponent<RectTransform>(), new Vector2(0.31f, 0.73f), new Vector2(0.98f, 0.84f));

            optionCards = new[]
            {
                CreateButton("OutfitButton", stage.transform, font, "OUTFIT", new Color32(71, 31, 107, 255)),
                CreateButton("SkinButton", stage.transform, font, "SKIN", new Color32(71, 31, 107, 255)),
                CreateButton("HairColorButton", stage.transform, font, "HAIR COLOR", new Color32(71, 31, 107, 255)),
                CreateButton("HatButton", stage.transform, font, "HAT", new Color32(71, 31, 107, 255)),
                CreateButton("HairButton", stage.transform, font, "HAIR", new Color32(71, 31, 107, 255))
            };
            foreach (var optionCard in optionCards)
            {
                optionCard.transform.Find("Label").GetComponent<Text>().fontSize = 16;
            }

            xpCard = CreatePanel("XpCard", stage.transform, new Vector2(0.31f, 0.25f), new Vector2(0.98f, 0.31f), new Color32(22, 16, 36, 255));
            var xpText = CreateText("Xp", xpCard.transform, font, "LVL 1    0 / 100 XP", 16, TextAnchor.MiddleCenter, new Color32(207, 250, 254, 230));
            SetRect(xpText.rectTransform, Vector2.zero, Vector2.one);

            actionButtons = new[]
            {
                CreateButton("StartSoloButton", stage.transform, font, "START SOLO SURVIVAL", new Color32(79, 31, 125, 255)),
                CreateButton("StartSurvivalMultiplayerButton", stage.transform, font, "SURVIVAL MULTIPLAYER", new Color32(21, 90, 66, 255)),
                CreateButton("NewBackButton", stage.transform, font, "BACK", new Color32(54, 48, 67, 255))
            };
            foreach (var actionButton in actionButtons)
            {
                actionButton.transform.Find("Label").GetComponent<Text>().fontSize = 15;
            }

            return stage;
        }

        private static GameObject CreateLaunchMultiplayerStage(
            Transform parent,
            Font font,
            out Button customLobbyButton,
            out Button survivalMultiplayerButton,
            out Button backButton)
        {
            var stage = CreatePanel("MultiplayerStage", parent, Vector2.zero, Vector2.one, new Color32(16, 7, 24, 244));
            AddLaunchCardOutline(stage);
            var title = CreateText("Title", stage.transform, font, "MULTIPLAYER", 34, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            SetRect(title.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.96f));
            customLobbyButton = CreateButton("CustomLobbyButton", stage.transform, font, "CUSTOM LOBBY", new Color32(79, 31, 125, 255));
            SetRect(customLobbyButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.72f));
            survivalMultiplayerButton = CreateButton("SurvivalMultiplayerButton", stage.transform, font, "SURVIVAL MULTIPLAYER", new Color32(21, 90, 66, 255));
            SetRect(survivalMultiplayerButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.51f));
            backButton = CreateButton("MultiplayerBackButton", stage.transform, font, "BACK", new Color32(54, 48, 67, 255));
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.30f));
            return stage;
        }

        private static GameObject CreateLaunchLobbyStage(
            Transform parent,
            Font font,
            out Text title,
            out InputField inviteCodeInput,
            out InputField mobileLinkInput,
            out Button createButton,
            out Button copyLinkButton,
            out Button backButton)
        {
            var stage = CreatePanel("LobbyStage", parent, Vector2.zero, Vector2.one, new Color32(16, 7, 24, 244));
            AddLaunchCardOutline(stage);
            title = CreateText("Title", stage.transform, font, "CUSTOM LOBBY", 31, TextAnchor.MiddleCenter, new Color32(255, 179, 71, 255));
            SetRect(title.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f));
            var inviteLabel = CreateText("InviteCodeLabel", stage.transform, font, "INVITE CODE", 15, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 230));
            SetRect(inviteLabel.rectTransform, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.82f));
            inviteCodeInput = CreateInputField(stage.transform, font);
            inviteCodeInput.gameObject.name = "InviteCode";
            inviteCodeInput.text = string.Empty;
            inviteCodeInput.textComponent.text = string.Empty;
            ((Text)inviteCodeInput.placeholder).text = "ROOM CODE";
            SetRect(inviteCodeInput.GetComponent<RectTransform>(), new Vector2(0.08f, 0.64f), new Vector2(0.92f, 0.74f));
            var linkLabel = CreateText("MobileLinkLabel", stage.transform, font, "MOBILE CROSSPLAY LINK", 15, TextAnchor.MiddleLeft, new Color32(207, 250, 254, 230));
            SetRect(linkLabel.rectTransform, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.62f));
            mobileLinkInput = CreateInputField(stage.transform, font);
            mobileLinkInput.gameObject.name = "MobileLink";
            mobileLinkInput.readOnly = true;
            mobileLinkInput.text = string.Empty;
            mobileLinkInput.textComponent.text = string.Empty;
            ((Text)mobileLinkInput.placeholder).text = string.Empty;
            SetRect(mobileLinkInput.GetComponent<RectTransform>(), new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.54f));
            createButton = CreateButton("CreateLobbyButton", stage.transform, font, "CREATE CUSTOM LOBBY", new Color32(79, 31, 125, 255));
            SetRect(createButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.41f));
            copyLinkButton = CreateButton("CopyMobileLinkButton", stage.transform, font, "COPY MOBILE LINK", new Color32(21, 90, 66, 255));
            SetRect(copyLinkButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.17f), new Vector2(0.60f, 0.27f));
            backButton = CreateButton("LobbyBackButton", stage.transform, font, "BACK", new Color32(54, 48, 67, 255));
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0.63f, 0.17f), new Vector2(0.92f, 0.27f));
            var help = CreateText("ControllerHelp", stage.transform, font, "CONTROLLER: A SELECTS / B GOES BACK", 13, TextAnchor.MiddleCenter, new Color32(207, 250, 254, 150));
            SetRect(help.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.15f));
            return stage;
        }

        private static void AddLaunchCardOutline(GameObject card)
        {
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color32(131, 72, 181, 220);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static void CreateMobileControls(Transform parent, Font font)
        {
            var lookZone = CreatePanel("LookZone", parent, new Vector2(0.45f, 0f), Vector2.one, new Color(1f, 1f, 1f, 0.002f));
            lookZone.AddComponent<WofMobileLookZone>();

            var joystick = CreatePanel("MoveJoystick", parent, new Vector2(0.04f, 0.05f), new Vector2(0.24f, 0.34f), new Color(0.15f, 0.16f, 0.24f, 0.62f));
            var handle = CreatePanel("Handle", joystick.transform, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), new Color(0.75f, 0.76f, 0.90f, 0.75f));
            var joystickComponent = joystick.AddComponent<WofVirtualJoystick>();
            SetObjectReference(joystickComponent, "handle", handle.GetComponent<RectTransform>());

            var leftCast = CreateButton("LeftCast", parent, font, "L", new Color(0.78f, 0.16f, 0.06f, 0.82f));
            SetRect(leftCast.GetComponent<RectTransform>(), new Vector2(0.68f, 0.08f), new Vector2(0.79f, 0.25f));
            var leftCastAction = leftCast.gameObject.AddComponent<WofMobileActionButton>();
            SetEnum(leftCastAction, "action", (int)WofMobileAction.CastLeft);

            var rightCast = CreateButton("RightCast", parent, font, "R", new Color(0.78f, 0.16f, 0.06f, 0.82f));
            SetRect(rightCast.GetComponent<RectTransform>(), new Vector2(0.86f, 0.08f), new Vector2(0.97f, 0.25f));
            var rightCastAction = rightCast.gameObject.AddComponent<WofMobileActionButton>();
            SetEnum(rightCastAction, "action", (int)WofMobileAction.CastRight);

            var jump = CreateButton("Jump", parent, font, "JUMP", new Color(0.20f, 0.38f, 0.70f, 0.82f));
            SetRect(jump.GetComponent<RectTransform>(), new Vector2(0.76f, 0.28f), new Vector2(0.89f, 0.46f));
            var jumpAction = jump.gameObject.AddComponent<WofMobileActionButton>();
            SetEnum(jumpAction, "action", (int)WofMobileAction.Jump);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ChicagoScenePath, true),
                new EditorBuildSettingsScene(SwampScenePath, true),
                new EditorBuildSettingsScene(MountainScenePath, true),
                new EditorBuildSettingsScene(GraveyardScenePath, true),
                new EditorBuildSettingsScene(LilyCoilScenePath, true)
            };
        }

        private static void BuildPlayer(BuildTarget target, string outputPath)
        {
            ValidateGeneratedNetworkConfiguration();
            EnsurePathIsInsideProject(outputPath);

            var outputDirectory = target == BuildTarget.WebGL
                ? outputPath
                : Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Build output directory is invalid.");
            }

            PrepareCleanBuildOutput(target, outputPath, outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = BuildScenePaths,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.CleanBuildCache
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"{target} build failed: {report.summary.result}");
            }

            ValidateBuildArtifact(target, outputPath);
            if (target == BuildTarget.Android)
            {
                WriteAndroidBuildReceipt(outputPath);
            }
            else
            {
                WriteBuildArtifactReceipt(target, outputPath, report.summary.totalSize);
            }

            Debug.Log($"[WOF-AUTOMATION] BUILD_COMPLETE target={target} bytes={report.summary.totalSize} output={outputPath}");
        }

        private static void WriteBuildArtifactReceipt(BuildTarget target, string outputPath, ulong reportedTotalSize)
        {
            var primaryArtifact = target == BuildTarget.WebGL
                ? Path.Combine(outputPath, "index.html")
                : outputPath;
            var receiptPath = target == BuildTarget.WebGL
                ? Path.Combine(outputPath, "WofBuildReceipt.json")
                : outputPath + ".build.json";
            var artifactInfo = new FileInfo(primaryArtifact);
            var payloadArtifact = target == BuildTarget.StandaloneWindows64
                ? Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputPath) + "_Data",
                    "level0")
                : string.Empty;
            var additivePayloadArtifact = target == BuildTarget.StandaloneWindows64
                ? Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputPath) + "_Data",
                    "level1")
                : string.Empty;
            var scenePayloads = target == BuildTarget.StandaloneWindows64
                ? CreateBuildScenePayloadReceipts(outputPath)
                : Array.Empty<BuildScenePayloadReceipt>();
            var receipt = new BuildArtifactReceipt
            {
                schemaVersion = 2,
                completedUtc = DateTime.UtcNow.ToString("o"),
                target = target.ToString(),
                reportedTotalSize = reportedTotalSize,
                primaryArtifact = primaryArtifact,
                primaryLength = artifactInfo.Length,
                primarySha256 = ComputeSha256(primaryArtifact),
                payloadArtifact = payloadArtifact,
                payloadLength = string.IsNullOrEmpty(payloadArtifact) ? 0 : new FileInfo(payloadArtifact).Length,
                payloadSha256 = string.IsNullOrEmpty(payloadArtifact) ? string.Empty : ComputeSha256(payloadArtifact),
                additivePayloadArtifact = additivePayloadArtifact,
                additivePayloadLength = string.IsNullOrEmpty(additivePayloadArtifact) ? 0 : new FileInfo(additivePayloadArtifact).Length,
                additivePayloadSha256 = string.IsNullOrEmpty(additivePayloadArtifact) ? string.Empty : ComputeSha256(additivePayloadArtifact),
                scenePayloads = scenePayloads
            };

            File.WriteAllText(
                receiptPath,
                JsonUtility.ToJson(receipt, true) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static BuildScenePayloadReceipt[] CreateBuildScenePayloadReceipts(string outputPath)
        {
            var dataPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath) + "_Data");
            var receipts = new BuildScenePayloadReceipt[BuildScenePaths.Length];
            for (var index = 0; index < receipts.Length; index++)
            {
                var artifact = Path.Combine(dataPath, $"level{index}");
                var info = new FileInfo(artifact);
                receipts[index] = new BuildScenePayloadReceipt
                {
                    artifact = artifact,
                    length = info.Length,
                    sha256 = ComputeSha256(artifact)
                };
            }
            return receipts;
        }

        private static void WriteAndroidBuildReceipt(string apkPath)
        {
            var apkInfo = new FileInfo(apkPath);
            var receipt = new AndroidBuildReceipt
            {
                schemaVersion = 1,
                completedUtc = DateTime.UtcNow.ToString("o"),
                packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                versionName = PlayerSettings.bundleVersion,
                versionCode = PlayerSettings.Android.bundleVersionCode,
                apkLength = apkInfo.Length,
                apkSha256 = ComputeSha256(apkPath)
            };

            if (string.IsNullOrWhiteSpace(receipt.packageName) ||
                string.IsNullOrWhiteSpace(receipt.versionName) ||
                receipt.versionCode <= 0)
            {
                throw new InvalidOperationException("Android build identity is incomplete; refusing to write an unverifiable APK receipt.");
            }

            var receiptPath = apkPath + ".build.json";
            File.WriteAllText(
                receiptPath,
                JsonUtility.ToJson(receipt, true) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static string ComputeSha256(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static void PrepareCleanBuildOutput(
            BuildTarget target,
            string outputPath,
            string outputDirectory)
        {
            if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.WebGL)
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
                return;
            }

            foreach (var artifactPath in new[] { outputPath, outputPath + ".build.json" })
            {
                if (File.Exists(artifactPath))
                {
                    File.Delete(artifactPath);
                }
            }
        }

        private static void ValidateBuildArtifact(BuildTarget target, string outputPath)
        {
            if (target == BuildTarget.WebGL)
            {
                var indexPath = Path.Combine(outputPath, "index.html");
                if (!Directory.Exists(outputPath) || !File.Exists(indexPath))
                {
                    throw new InvalidOperationException($"{target} build reported success but its entry point is missing: {indexPath}");
                }

                return;
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                throw new InvalidOperationException($"{target} build reported success but its output file is missing or empty: {outputPath}");
            }


            if (target == BuildTarget.StandaloneWindows64)
            {
                var dataPath = Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputPath) + "_Data");
                for (var sceneIndex = 0; sceneIndex < BuildScenePaths.Length; sceneIndex++)
                {
                    var scenePayloadName = $"level{sceneIndex}";
                    var scenePayloadPath = Path.Combine(dataPath, scenePayloadName);
                    if (!File.Exists(scenePayloadPath) || new FileInfo(scenePayloadPath).Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"{target} build reported success but its scene payload is missing or empty: {scenePayloadPath}");
                    }
                }
            }
        }

        private static void RefreshNetworkPrefabIdentities()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var prefabPath in RequiredNetworkPrefabPaths)
            {
                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var prefab = LoadRequiredAsset<GameObject>(prefabPath);
                var networkObject = prefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    throw new InvalidOperationException($"Generated network prefab is missing NetworkObject: {prefabPath}");
                }

                if (networkObject.PrefabIdHash == 0)
                {
                    var onValidate = typeof(NetworkObject).GetMethod(
                        "OnValidate",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (onValidate == null)
                    {
                        throw new InvalidOperationException(
                            $"NGO did not generate a GlobalObjectIdHash for {prefabPath}, and NetworkObject.OnValidate is unavailable.");
                    }

                    try
                    {
                        onValidate.Invoke(networkObject, null);
                    }
                    catch (TargetInvocationException exception)
                    {
                        throw new InvalidOperationException(
                            $"NGO failed to generate a GlobalObjectIdHash for {prefabPath}.",
                            exception.InnerException ?? exception);
                    }

                    EditorUtility.SetDirty(networkObject);
                    EditorUtility.SetDirty(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(RequiredNetworkPrefabPaths);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateNetworkPrefabIdentities();
        }

        private static void ValidateGeneratedNetworkConfiguration()
        {
            ValidateNetworkPrefabIdentities();

            var list = LoadRequiredAsset<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list.PrefabList.Count != RequiredNetworkPrefabPaths.Length)
            {
                throw new InvalidOperationException(
                    $"Network prefab list contains {list.PrefabList.Count} entries; expected {RequiredNetworkPrefabPaths.Length}.");
            }

            var expectedPaths = new HashSet<string>(RequiredNetworkPrefabPaths, StringComparer.Ordinal);
            var registeredPaths = new HashSet<string>(StringComparer.Ordinal);
            var registeredHashes = new HashSet<uint>();

            foreach (var entry in list.PrefabList)
            {
                if (entry == null || entry.Override != NetworkPrefabOverride.None || entry.Prefab == null)
                {
                    throw new InvalidOperationException("Network prefab list contains a null or unsupported override entry.");
                }

                var prefabPath = AssetDatabase.GetAssetPath(entry.Prefab);
                if (!expectedPaths.Contains(prefabPath) || !registeredPaths.Add(prefabPath))
                {
                    throw new InvalidOperationException($"Unexpected or duplicate network prefab registration: {prefabPath}");
                }

                var hash = entry.SourcePrefabGlobalObjectIdHash;
                if (hash == 0 || !registeredHashes.Add(hash))
                {
                    throw new InvalidOperationException(
                        $"Network prefab {prefabPath} has a zero or duplicate GlobalObjectIdHash ({hash}).");
                }
            }

            if (!registeredPaths.SetEquals(expectedPaths))
            {
                throw new InvalidOperationException("Network prefab list does not exactly match the generated prefab set.");
            }

            Debug.Log(
                $"[WOF-AUTOMATION] NETWORK_PREFABS_VALIDATED count={registeredPaths.Count} hashes={string.Join(",", registeredHashes.OrderBy(hash => hash))}");
        }

        private static void ValidateNetworkPrefabIdentities()
        {
            var hashes = new Dictionary<uint, string>();
            foreach (var prefabPath in RequiredNetworkPrefabPaths)
            {
                var prefab = LoadRequiredAsset<GameObject>(prefabPath);
                var networkObject = prefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    throw new InvalidOperationException($"Generated network prefab is missing NetworkObject: {prefabPath}");
                }

                var hash = networkObject.PrefabIdHash;
                if (hash == 0)
                {
                    throw new InvalidOperationException($"Generated network prefab has a zero GlobalObjectIdHash: {prefabPath}");
                }

                if (hashes.TryGetValue(hash, out var duplicatePath))
                {
                    throw new InvalidOperationException(
                        $"Generated network prefabs share GlobalObjectIdHash {hash}: {duplicatePath} and {prefabPath}");
                }

                hashes.Add(hash, prefabPath);
            }
        }

        private static T LoadRequiredAsset<T>(string assetPath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required generated asset is missing or invalid: {assetPath}");
            }

            return asset;
        }

        private static string ResolveProjectPath(params string[] segments)
        {
            var path = GetProjectRootPath();
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            var resolved = Path.GetFullPath(path);
            EnsurePathIsInsideProject(resolved);
            return resolved;
        }

        private static string GetProjectRootPath()
        {
            var assetsPath = Path.GetFullPath(Application.dataPath);
            var projectRoot = Directory.GetParent(assetsPath);
            if (projectRoot == null || !string.Equals(Path.GetFileName(assetsPath), "Assets", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot resolve Unity project root from Application.dataPath: {Application.dataPath}");
            }

            if (!string.Equals(Path.GetPathRoot(assetsPath), @"D:\", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"WOF Unity automation is restricted to D:. Refusing project path: {projectRoot.FullName}");
            }

            return projectRoot.FullName;
        }

        private static void EnsurePathIsInsideProject(string path)
        {
            var projectRoot = Path.GetFullPath(GetProjectRootPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(path);
            if (!resolved.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Output path must stay inside the Unity project root: {resolved}");
            }
        }

        private static Sprite[] LoadSprites(string folder, string prefix)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(NaturalFrameNumber)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
                .Where(sprite => sprite != null)
                .ToArray();
        }

        private static Sprite[] LoadAvatarFrames(string animation)
        {
            var folder = $"Assets/WOF/Art/Generated/React/Avatar/Default/{animation}";
            var frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(AvatarFrameOrder)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
                .Where(sprite => sprite != null)
                .ToArray();
            if (frames.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Expected 32 React-baked default avatar frames for {animation}; found {frames.Length} in {folder}.");
            }
            return frames;
        }

        private static int AvatarFrameOrder(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Length != 5 || name[0] != 'd' || name[2] != '_' || name[3] != 'f' ||
                !char.IsDigit(name[1]) || !char.IsDigit(name[4]))
            {
                return int.MaxValue;
            }
            return (name[1] - '0') * 4 + (name[4] - '0');
        }

        private static int NaturalFrameNumber(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var digits = new string(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            return int.TryParse(digits, out var frame) ? frame : int.MaxValue;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            return instance;
        }

        private static void CreateHut(Transform parent, Vector3 position, float yaw, WofMaterialPalette palette)
        {
            var root = new GameObject("Hut");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            CreatePrimitive("Walls", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(5.5f, 3.2f, 4.5f), palette.Wood);
            CreatePrimitive("Roof", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3.35f, 0f), new Vector3(3.8f, 0.4f, 3.3f), palette.Roof);
            CreatePrimitive("Door", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.15f, -2.29f), new Vector3(1.2f, 2.3f, 0.12f), palette.Stone);
        }

        private static void CreateTree(Transform parent, Vector3 position, WofMaterialPalette palette)
        {
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            CreatePrimitive("Trunk", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.2f, 0f), new Vector3(0.8f, 2.2f, 0.8f), palette.Wood);
            CreatePrimitive("Crown", PrimitiveType.Sphere, root.transform, new Vector3(0f, 5.0f, 0f), new Vector3(4.2f, 3.4f, 4.2f), palette.Leaf);
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static GameObject CreateSafeAreaRoot(string name, Transform parent)
        {
            var safeArea = new GameObject(name, typeof(RectTransform), typeof(WofSafeAreaFitter));
            safeArea.transform.SetParent(parent, false);
            SetRect(safeArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            return safeArea;
        }

        private static Text CreateText(string name, Transform parent, Font font, string value, int size, TextAnchor anchor, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size / 2);
            text.resizeTextMaxSize = size;
            textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Color color)
        {
            var buttonObject = CreatePanel(name, parent, Vector2.zero, Vector2.one, color);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var labelText = CreateText("Label", buttonObject.transform, font, label, 22, TextAnchor.MiddleCenter, Color.white);
            SetRect(labelText.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static InputField CreateInputField(Transform parent, Font font)
        {
            var fieldObject = CreatePanel("Address", parent, Vector2.zero, Vector2.one, new Color(0.11f, 0.11f, 0.17f, 1f));
            var field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = fieldObject.GetComponent<Image>();
            var text = CreateText("Text", fieldObject.transform, font, "127.0.0.1", 21, TextAnchor.MiddleLeft, Color.white);
            SetRect(text.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            var placeholder = CreateText("Placeholder", fieldObject.transform, font, "Host IP address", 21, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.7f));
            SetRect(placeholder.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = "127.0.0.1";
            return field;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Sprite GetOrCreateCircularUiMaskSprite()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(CircularUiMaskPath);
            var existing = assets.OfType<Sprite>().FirstOrDefault();
            if (existing != null)
            {
                return existing;
            }

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "CircularUiMaskTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1f - distance) * 255f);
                    colors[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, CircularUiMaskPath);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "CircularUiMask";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            EditorUtility.SetDirty(sprite);
            AssetDatabase.ImportAsset(CircularUiMaskPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAllAssetsAtPath(CircularUiMaskPath).OfType<Sprite>().First();
        }

        private static Image CreateFilledBar(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var background = CreatePanel(name + "Background", parent, min, max, new Color(0.12f, 0.12f, 0.14f, 0.94f));
            var fill = CreateImage(name + "Fill", background.transform, null, color);
            SetRect(fill.rectTransform, new Vector2(0.02f, 0.10f), new Vector2(0.98f, 0.90f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ?? throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReferenceArray<T>(Object target, string propertyName, IReadOnlyList<T> values) where T : Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ?? throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ?? throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}");
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class WofMaterialPalette
        {
            public Material Ground;
            public Material Stone;
            public Material Wood;
            public Material Roof;
            public Material Leaf;
            public Material Grass;
            public Material Dirt;
            public Material Path;
            public Material Fire;
            public Material CampfireLog;
            public Material Water;
            public Material WaterRipple;
            public Material Villager;
            public Material Stem;
            public Material HutGrass;
            public Material Stonework;
            public Material Door;
            public Material WoodPlank;
            public Material Log;
            public Material DirtGrass;
            public Material HutGlass;
            public Material HutIron;
            public Material LanternGlow;
            public Material LanternHalo;
            public Material LanternOuterGlow;
            public Material TreeHouseBark;
            public Material TreeHousePlank;
            public Material TreeHouseRoof;
            public Material TreeHouseWindowGlow;
            public Material TreeHouseRope;
            public Material TreeHouseLeafEdge;
            public Material TreeHouseDetailLeaf;
            public Material[] TreeHouseLeaves;
            public Material[] Bushes;
            public Material[] MushroomCaps;
            public Material Player;
            public Material Fireball;
            public Material Mana;
        }

        private sealed class WofUiReferences
        {
            public GameObject LaunchPanel;
            public GameObject PressPanel;
            public GameObject SessionPanel;
            public Button PressAnywhereButton;
            public InputField AddressInput;
            public Button SoloButton;
            public Button HostButton;
            public Button JoinButton;
            public Text LaunchStatus;
            public WofLaunchFlow LaunchFlow;
            public WofHud Hud;
        }

        [Serializable]
        private sealed class ReactBushGeometryDocument
        {
            public int schemaVersion;
            public int vertexCount;
            public float[] positions;
            public float[] normals;
            public float[] barycentric;
        }
    }
}
