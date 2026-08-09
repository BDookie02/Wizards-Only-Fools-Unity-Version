using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string ChicagoArtRoot = "Assets/WOF/Art/Generated/React/ChicagoCity";
        private const string ChicagoLayoutPath = ChicagoArtRoot + "/runtime-layout.json";
        private const string ChicagoTextureRoot = ChicagoArtRoot + "/Textures";
        private const string ChicagoOperatorRoot = ChicagoArtRoot + "/Operators";
        private const string ChicagoGeometryRoot = GeometryRoot + "/ChicagoCity";

        private static void CreateChicagoCityScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            scene.name = WofChicagoCitySceneLoader.SceneName;
            var world = new GameObject("ChicagoWorld");
            CreateChicagoCity(world.transform);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ChicagoScenePath);
        }

        private static void CreateChicagoCity(Transform parent)
        {
            var document = LoadChicagoCityDocument();
            var materials = CreateChicagoMaterials();
            var root = new GameObject("ReactSurvivalChicagoCity_-3_-3");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofChicagoCityLayout.WorldOrigin;

            CreateChicagoSurface(root.transform, document, materials);
            var operatorSprites = new List<Transform>();
            CreateChicagoBuildings(root.transform, document, materials, operatorSprites);
            CreateChicagoStreetDetails(root.transform, document, materials);
            CreateChicagoBeanPark(root.transform, document, materials);
            CreateChicagoWelcomeSign(root.transform, document, materials, operatorSprites);
            CreateChicagoTrafficRuntime(parent, document, materials);
            root.AddComponent<WofChicagoOperatorBillboards>().Configure(operatorSprites.ToArray());
        }

        private static WofChicagoCityDocument LoadChicagoCityDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(ChicagoLayoutPath);
            var document = JsonUtility.FromJson<WofChicagoCityDocument>(source.text);
            if (document == null || document.schemaVersion != 1 ||
                document.chunk == null || document.chunk.cx != WofChicagoCityLayout.ChunkX ||
                document.chunk.cz != WofChicagoCityLayout.ChunkZ ||
                !string.Equals(document.chunk.biome, "jungle", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.villageKind, "chicago", StringComparison.Ordinal) ||
                document.chunk.hasRiver || !string.Equals(document.chunk.lod, "near", StringComparison.Ordinal) ||
                !Mathf.Approximately(document.baseHeight, WofChicagoCityLayout.ReactBaseHeight) ||
                !WofChicagoCityLayout.HasExactCounts(document.counts) ||
                document.constants == null || document.constants.roadPositions?.Length != 4 ||
                !Mathf.Approximately(document.constants.cityHalfSize, WofChicagoCityLayout.CityHalfSize) ||
                document.layout?.buildings?.Length != 35 || document.layout.pedestrians?.Length != 220 ||
                document.layout.cars?.Length != 46 || document.operators?.Length != 35 ||
                document.street?.trafficLightIntersections?.Length != 16 ||
                document.street.lamps?.Length != 48 || document.street.streetTrees?.Length != 40 ||
                document.street.sidewalkSegments?.Length != 5 || document.street.hydrants?.Length != 16 ||
                document.street.trashCans?.Length != 36 || document.street.benches?.Length != 34 ||
                document.street.grassPatches?.Length != 40 || document.street.crosswalks?.Length != 576 ||
                document.street.sidewalkPlanes?.Length != 80 || document.street.parkingLines?.Length != 64 ||
                document.initialTraffic?.cars?.Length != 46 || document.initialTraffic.pedestrians?.Length != 220 ||
                !IsValidDesertMesh(document.padGeometry))
            {
                throw new InvalidOperationException($"Invalid exact React Chicago city layout at {ChicagoLayoutPath}.");
            }
            return document;
        }

        private static void ConfigureChicagoTextureImports()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ChicagoTextureRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                var repeating = fileName.StartsWith("facade-", StringComparison.Ordinal) ||
                                string.Equals(fileName, "window", StringComparison.Ordinal);
                var led = string.Equals(fileName, "led-sign", StringComparison.Ordinal);
                var wrapU = repeating || led ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var wrapV = repeating ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                              importer.filterMode != FilterMode.Point ||
                              importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              importer.wrapModeU != wrapU || importer.wrapModeV != wrapV || !importer.sRGBTexture;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapModeU = wrapU;
                importer.wrapModeV = wrapV;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                if (changed) importer.SaveAndReimport();
            }
        }

        private static ChicagoMaterialSet CreateChicagoMaterials()
        {
            var facadeRepeats = new[]
            {
                new Vector2(0.56f, 1.7f), new Vector2(0.56f, 1.7f),
                new Vector2(0.5f, 1.7f), new Vector2(0.56f, 1.95f),
                new Vector2(0.44f, 1.45f), new Vector2(0.56f, 1.7f)
            };
            var facadeMaterials = new Material[6];
            for (var index = 0; index < facadeMaterials.Length; index++)
            {
                facadeMaterials[index] = ChicagoMaterial(
                    $"ChicagoFacade{index}", Color.white,
                    LoadRequiredAsset<Texture2D>($"{ChicagoTextureRoot}/facade-{index}.png"),
                    false, facadeRepeats[index]);
            }
            var storeSigns = new Material[6];
            for (var index = 0; index < storeSigns.Length; index++)
            {
                storeSigns[index] = ChicagoMaterial(
                    $"ChicagoStoreSign{index}", Color.white,
                    LoadRequiredAsset<Texture2D>($"{ChicagoTextureRoot}/store-sign-{index}.png"), true);
            }
            var ads = new Material[4];
            for (var index = 0; index < ads.Length; index++)
            {
                ads[index] = ChicagoMaterial(
                    $"ChicagoAd{index}", Color.white,
                    LoadRequiredAsset<Texture2D>($"{ChicagoTextureRoot}/ad-{index}.png"), true);
            }

            return new ChicagoMaterialSet
            {
                Facades = facadeMaterials,
                StoreSigns = storeSigns,
                Ads = ads,
                Ground = ChicagoMaterial("ChicagoGround", HexColor("#52664f")),
                CitySurface = ChicagoMaterial("ChicagoCitySurface", HexColor("#4b5563")),
                Water = ChicagoMaterial("ChicagoWater", new Color(0.145f, 0.388f, 0.922f, 0.74f), null, true),
                Lakefront = ChicagoMaterial("ChicagoLakefront", HexColor("#9ca3af")),
                River = ChicagoMaterial("ChicagoRiver", new Color(0.059f, 0.365f, 0.529f, 0.92f), null, true),
                RoadSidewalk = ChicagoMaterial("ChicagoRoadSidewalk", HexColor("#9ca3af")),
                Road = ChicagoMaterial("ChicagoRoad", HexColor("#1f2937")),
                LaneDash = ChicagoMaterial("ChicagoLaneDash", new Color(0.973f, 0.98f, 0.988f, 0.72f), null, true),
                Roof = ChicagoMaterial("ChicagoBuildingRoof", HexColor("#475569")),
                DoorFrame = ChicagoMaterial("ChicagoDoorFrame", HexColor("#020617")),
                DoorGlass = ChicagoMaterial("ChicagoDoorGlass", new Color(0.059f, 0.09f, 0.165f, 0.86f), null, true),
                DoorLight = ChicagoMaterial("ChicagoDoorLight", new Color(0.133f, 0.773f, 0.369f, 0.88f), null, true),
                StoreTrim = ChicagoMaterial("ChicagoStoreTrim", HexColor("#111827")),
                StoreWindow = ChicagoMaterial("ChicagoStoreWindow", new Color(0.729f, 0.902f, 0.992f, 0.62f), null, true),
                AdBacking = ChicagoMaterial("ChicagoAdBacking", new Color(0.008f, 0.024f, 0.09f, 0.72f), null, true),
                InteriorFloorA = ChicagoMaterial("ChicagoInteriorFloorA", HexColor("#4b5563")),
                InteriorFloorB = ChicagoMaterial("ChicagoInteriorFloorB", HexColor("#3f3f46")),
                InteriorWall = ChicagoMaterial("ChicagoInteriorWall", new Color(0.122f, 0.161f, 0.216f, 0.72f), null, true),
                InteriorCounter = ChicagoMaterial("ChicagoInteriorCounter", HexColor("#7c4a2d")),
                InteriorCounterTop = ChicagoMaterial("ChicagoInteriorCounterTop", HexColor("#a16207")),
                InteriorShelf = ChicagoMaterial("ChicagoInteriorShelf", HexColor("#5b3a25")),
                InteriorMat = ChicagoMaterial("ChicagoInteriorMat", new Color(0.067f, 0.094f, 0.153f, 0.9f), null, true),
                ParkGrass = ChicagoMaterial("ChicagoParkGrass", HexColor("#3f7c3b")),
                ParkPath = ChicagoMaterial("ChicagoParkPath", HexColor("#cbd5e1")),
                SidewalkDetail = ChicagoMaterial("ChicagoSidewalkDetail", HexColor("#b6bec9")),
                Parking = ChicagoMaterial("ChicagoParking", new Color(0.973f, 0.98f, 0.988f, 0.58f), null, true),
                Crosswalk76 = ChicagoMaterial("ChicagoCrosswalk76", new Color(0.973f, 0.98f, 0.988f, 0.76f), null, true),
                Crosswalk66 = ChicagoMaterial("ChicagoCrosswalk66", new Color(0.973f, 0.98f, 0.988f, 0.66f), null, true),
                Crosswalk82 = ChicagoMaterial("ChicagoCrosswalk82", new Color(0.973f, 0.98f, 0.988f, 0.82f), null, true),
                DarkMetal = ChicagoMaterial("ChicagoDarkMetal", HexColor("#1f2937")),
                BlackMetal = ChicagoMaterial("ChicagoBlackMetal", HexColor("#111827")),
                Hydrant = ChicagoMaterial("ChicagoHydrant", HexColor("#ef4444")),
                HydrantLight = ChicagoMaterial("ChicagoHydrantLight", HexColor("#f87171")),
                HydrantDark = ChicagoMaterial("ChicagoHydrantDark", HexColor("#b91c1c")),
                Lamp = ChicagoMaterial("ChicagoLamp", HexColor("#fde68a")),
                LampGlow = ChicagoMaterial("ChicagoLampGlow", new Color(0.992f, 0.902f, 0.541f, 0.18f), null, true),
                Trash = ChicagoMaterial("ChicagoTrash", HexColor("#374151")),
                Steel = ChicagoMaterial("ChicagoSteel", HexColor("#9ca3af")),
                Bench = ChicagoMaterial("ChicagoBench", HexColor("#7c4a2d")),
                BenchBack = ChicagoMaterial("ChicagoBenchBack", HexColor("#5c331f")),
                TreeTrunk = ChicagoMaterial("ChicagoTreeTrunk", HexColor("#6b3f22")),
                TreeLeafA = ChicagoMaterial("ChicagoTreeLeafA", HexColor("#15803d")),
                TreeLeafB = ChicagoMaterial("ChicagoTreeLeafB", HexColor("#166534")),
                BeanBase = ChicagoMaterial("ChicagoBeanBase", new Color(0.58f, 0.64f, 0.72f, 0.12f), null, true),
                Bean = ChicagoMaterial("ChicagoBean", HexColor("#dce8f3")),
                BeanInner = ChicagoMaterial("ChicagoBeanInner", new Color(0.557f, 0.627f, 0.706f, 0.28f), null, true),
                BeanLower = ChicagoMaterial("ChicagoBeanLower", new Color(0.455f, 0.529f, 0.616f, 0.22f), null, true),
                BeanHighlight = ChicagoMaterial("ChicagoBeanHighlight", new Color(0.973f, 0.984f, 1f, 0.22f), null, true),
                BeanCleft = ChicagoMaterial("ChicagoBeanCleft", new Color(0.2f, 0.255f, 0.333f, 0.62f), null, true),
                BeanCleftDark = ChicagoMaterial("ChicagoBeanCleftDark", new Color(0.067f, 0.094f, 0.153f, 0.42f), null, true),
                BeanLowerCleft = ChicagoMaterial("ChicagoBeanLowerCleft", new Color(0.122f, 0.161f, 0.216f, 0.46f), null, true),
                BeanShineA = ChicagoMaterial("ChicagoBeanShineA", new Color(1f, 1f, 1f, 0.38f), null, true),
                BeanShineB = ChicagoMaterial("ChicagoBeanShineB", new Color(1f, 1f, 1f, 0.24f), null, true),
                Bollard = ChicagoMaterial("ChicagoBollard", HexColor("#e5e7eb")),
                ChicagoSign = ChicagoMaterial("ChicagoWelcomeSign", Color.white, LoadRequiredAsset<Texture2D>($"{ChicagoTextureRoot}/chicago-sign.png"), true),
                LedSign = ChicagoMaterial("ChicagoLedSign", Color.white, LoadRequiredAsset<Texture2D>($"{ChicagoTextureRoot}/led-sign.png"), true),
                LedShell = ChicagoMaterial("ChicagoLedShell", new Color(0.008f, 0.024f, 0.09f, 0.9f), null, true)
            };
        }

        private static Material ChicagoMaterial(
            string name,
            Color color,
            Texture texture = null,
            bool transparent = false,
            Vector2? textureScale = null)
        {
            return GetOrCreateDesertUnlit(name, color, texture, transparent, textureScale);
        }

        private static void CreateChicagoSurface(
            Transform parent,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials)
        {
            var root = new GameObject("ChicagoCitySurface");
            root.transform.SetParent(parent, false);
            var padMesh = GetOrCreateMeshAsset(
                ChicagoGeometryRoot + "/VillagePad.asset",
                () => CreateDesertSerializedMesh("ChicagoVillagePad", document.padGeometry));
            var pad = ChicagoMeshVisual("ExactVillagePad", root.transform, Vector3.zero, padMesh, materials.Ground);
            pad.AddComponent<MeshCollider>().sharedMesh = padMesh;
            ChicagoFlat("CitySurface", root.transform, 0f, document.baseHeight + 0.075f, 0f, 472f, 472f, materials.CitySurface);
            ChicagoFlat("Lake", root.transform, 221f, document.baseHeight + 0.16f, 0f, 84f, 512f, materials.Water);
            ChicagoFlat("Lakefront", root.transform, 178f, document.baseHeight + 0.23f, 0f, 10f, 512f, materials.Lakefront);
            ChicagoFlat("River", root.transform, 18f, document.baseHeight + 0.24f, 0f, 372f, 24f, materials.River);
            foreach (var road in document.constants.roadPositions)
            {
                ChicagoFlat($"VerticalRoadSidewalk_{road}", root.transform, road, document.baseHeight + 0.28f, 0f, 42f, 472f, materials.RoadSidewalk);
                ChicagoFlat($"VerticalRoad_{road}", root.transform, road, document.baseHeight + 0.31f, 0f, 28f, 472f, materials.Road);
                ChicagoFlat($"HorizontalRoadSidewalk_{road}", root.transform, 0f, document.baseHeight + 0.29f, road, 472f, 42f, materials.RoadSidewalk);
                ChicagoFlat($"HorizontalRoad_{road}", root.transform, 0f, document.baseHeight + 0.32f, road, 472f, 28f, materials.Road);
                for (var index = 0; index < 9; index++)
                {
                    ChicagoFlat($"LaneDash_{road}_{index}", root.transform, -198f + index * 48f, document.baseHeight + 0.36f, road, 17f, 1.4f, materials.LaneDash);
                }
            }
            ChicagoDisk("BeanParkGrass", root.transform, 39f, 52, new Vector3(-36f, document.baseHeight + 0.42f, 118f), materials.ParkGrass);
            ChicagoRing("BeanParkPath", root.transform, 23f, 34f, 52, new Vector3(-36f, document.baseHeight + 0.455f, 118f), materials.ParkPath);
            ChicagoFlat("BeanParkEntrance", root.transform, -36f, document.baseHeight + 0.47f, 151f, 9f, 18f, materials.ParkPath);
        }

        private static void CreateChicagoBuildings(
            Transform parent,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials,
            List<Transform> operatorSprites)
        {
            var root = new GameObject("ChicagoBuildings");
            root.transform.SetParent(parent, false);
            for (var index = 0; index < document.layout.buildings.Length; index++)
            {
                var building = document.layout.buildings[index];
                var buildingRoot = new GameObject(building.key);
                buildingRoot.transform.SetParent(root.transform, false);
                buildingRoot.transform.localPosition = new Vector3(building.localX, document.baseHeight, building.localZ);
                buildingRoot.transform.localRotation = Quaternion.Euler(0f, building.rotation * Mathf.Rad2Deg, 0f);
                ChicagoCube("Facade", buildingRoot.transform, new Vector3(0f, building.height * 0.5f, 0f), new Vector3(building.width, building.height, building.depth), materials.Facades[building.facadeStyle]);
                ChicagoCube("Roof", buildingRoot.transform, new Vector3(0f, building.height + 1.3f, 0f), new Vector3(building.width + 3.4f, 2.6f, building.depth + 3.4f), materials.Roof);
                CreateChicagoBuildingColliders(buildingRoot.transform, building);
                CreateChicagoLandmarkDetails(buildingRoot.transform, building, document, materials);
                CreateChicagoBuildingDetails(buildingRoot.transform, building, index, materials);
                CreateChicagoBuildingInterior(buildingRoot.transform, building, index, document, materials, operatorSprites);
            }
        }

        private static void CreateChicagoBuildingColliders(Transform parent, WofChicagoBuildingRecord building)
        {
            const float thickness = 1.15f;
            var doorWidth = Mathf.Min(8.6f, building.width * 0.42f);
            var frontSegmentWidth = Mathf.Max(1.4f, (building.width - doorWidth) * 0.5f);
            AddChicagoBoxCollider(parent, "RearWallCollider", new Vector3(0f, building.height * 0.5f, -building.depth * 0.5f + thickness * 0.5f), new Vector3(building.width, building.height, thickness));
            AddChicagoBoxCollider(parent, "LeftWallCollider", new Vector3(-building.width * 0.5f + thickness * 0.5f, building.height * 0.5f, 0f), new Vector3(thickness, building.height, building.depth));
            AddChicagoBoxCollider(parent, "RightWallCollider", new Vector3(building.width * 0.5f - thickness * 0.5f, building.height * 0.5f, 0f), new Vector3(thickness, building.height, building.depth));
            foreach (var side in new[] { -1f, 1f })
            {
                AddChicagoBoxCollider(parent, $"FrontWallCollider_{side}", new Vector3(side * (doorWidth * 0.5f + frontSegmentWidth * 0.5f), building.height * 0.5f, building.depth * 0.5f - thickness * 0.5f), new Vector3(frontSegmentWidth, building.height, thickness));
            }
        }

        private static void AddChicagoBoxCollider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var colliderObject = new GameObject(name);
            colliderObject.transform.SetParent(parent, false);
            var collider = colliderObject.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            MarkStatic(colliderObject);
        }

        private static void CreateChicagoBuildingDetails(
            Transform parent,
            WofChicagoBuildingRecord building,
            int index,
            ChicagoMaterialSet materials)
        {
            var storefrontWidth = Mathf.Min(building.width - 4f, 26f);
            ChicagoCube("DoorFrame", parent, new Vector3(0f, 3.55f, building.depth * 0.5f + 0.24f), new Vector3(8.4f, 8.4f, 0.5f), materials.DoorFrame);
            ChicagoCube("DoorGlass", parent, new Vector3(0f, 3.8f, building.depth * 0.5f + 0.54f), new Vector3(5.9f, 6.5f, 0.34f), materials.DoorGlass);
            ChicagoCube("DoorLight", parent, new Vector3(0f, 7.65f, building.depth * 0.5f + 0.72f), new Vector3(5.8f, 0.54f, 0.4f), materials.DoorLight);
            ChicagoCube("StoreTrim", parent, new Vector3(0f, 8.35f, building.depth * 0.5f + 0.52f), new Vector3(storefrontWidth, 1.15f, 0.48f), materials.StoreTrim);
            CreateDarrelQuad("StoreSign", parent, new Vector3(0f, 10.15f, building.depth * 0.5f + 0.62f), new Vector2(Mathf.Min(building.width * 0.72f, 24f), 5.2f), 0f, materials.StoreSigns[index % materials.StoreSigns.Length], false);
            foreach (var side in new[] { -1f, 1f })
            {
                ChicagoCube($"StoreWindow_{side}", parent, new Vector3(side * storefrontWidth * 0.28f, 4.4f, building.depth * 0.5f + 0.42f), new Vector3(4.1f, 4.7f, 0.38f), materials.StoreWindow);
            }
            if (building.height > 72f && index % 3 == 0)
            {
                CreateDarrelQuad("Ad", parent, new Vector3(0f, building.height * 0.55f, building.depth * 0.5f + 0.72f), new Vector2(Mathf.Min(building.width * 0.68f, 22f), 31f), 0f, materials.Ads[index % materials.Ads.Length], false);
                ChicagoCube("AdBacking", parent, new Vector3(0f, building.height * 0.55f, building.depth * 0.5f + 0.56f), new Vector3(Mathf.Min(building.width * 0.72f, 23.4f), 32.4f, 0.26f), materials.AdBacking);
            }
        }

        private static void CreateChicagoBuildingInterior(
            Transform parent,
            WofChicagoBuildingRecord building,
            int index,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials,
            List<Transform> operatorSprites)
        {
            var roomWidth = Mathf.Max(10f, Mathf.Min(building.width - 4.8f, 20f));
            var roomDepth = Mathf.Max(10f, Mathf.Min(building.depth - 5.2f, 18f));
            var frontZ = building.depth * 0.5f;
            var roomCenterZ = frontZ - roomDepth * 0.5f - 2.2f;
            var interior = new GameObject("Interior");
            interior.transform.SetParent(parent, false);
            interior.transform.localPosition = Vector3.up * 0.5f;
            ChicagoFlat("Floor", interior.transform, 0f, 0.03f, roomCenterZ, roomWidth, roomDepth, index % 2 == 0 ? materials.InteriorFloorA : materials.InteriorFloorB);
            ChicagoCube("RearWall", interior.transform, new Vector3(0f, 2.4f, frontZ - roomDepth - 2.2f), new Vector3(roomWidth, 4.8f, 0.5f), materials.InteriorWall);
            ChicagoCube("Counter", interior.transform, new Vector3(0f, 1.6f, frontZ - roomDepth * 0.72f), new Vector3(Mathf.Min(roomWidth - 2f, 13f), 2.1f, 2.1f), materials.InteriorCounter);
            ChicagoCube("CounterTop", interior.transform, new Vector3(0f, 2.82f, frontZ - roomDepth * 0.72f - 1.1f), new Vector3(Mathf.Min(roomWidth - 2f, 13.4f), 0.42f, 2.5f), materials.InteriorCounterTop);
            foreach (var side in new[] { -1f, 1f })
            {
                ChicagoCube($"Shelf_{side}", interior.transform, new Vector3(side * (roomWidth * 0.5f - 1.3f), 2.2f, roomCenterZ - 0.8f), new Vector3(1.1f, 3.8f, roomDepth * 0.52f), materials.InteriorShelf);
            }
            ChicagoFlat("EntryMat", interior.transform, 0f, 0.08f, frontZ + 1.9f, 8.8f, 3.2f, materials.InteriorMat);

            var operatorRecord = document.operators.First(value => value.index == index && string.Equals(value.buildingKey, building.key, StringComparison.Ordinal));
            var sprite = LoadRequiredAsset<Sprite>($"{ChicagoArtRoot}/{operatorRecord.spritePath.Substring("ChicagoCity/".Length)}");
            var operatorRoot = new GameObject($"Operator_{index:00}");
            operatorRoot.transform.SetParent(interior.transform, false);
            operatorRoot.transform.localPosition = new Vector3(0f, 0.95f + WofVillagerMath.AvatarGroundLift, frontZ - roomDepth * 0.82f);
            operatorRoot.transform.localScale = Vector3.one * WofVillagerMath.AvatarScale;
            var spriteObject = new GameObject("AvatarBillboard", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(operatorRoot.transform, false);
            spriteObject.transform.localPosition = new Vector3(0f, WofVillagerMath.AvatarWorldCenterY, 0f);
            var renderer = spriteObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            operatorSprites.Add(spriteObject.transform);
        }

        private static void CreateChicagoLandmarkDetails(
            Transform parent,
            WofChicagoBuildingRecord building,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials)
        {
            if (string.IsNullOrWhiteSpace(building.landmark)) return;
            var slate = ChicagoMaterial("ChicagoLandmarkSlate", HexColor("#475569"));
            var dark = ChicagoMaterial("ChicagoLandmarkDark", HexColor("#334155"));
            if (string.Equals(building.landmark, "willis", StringComparison.Ordinal))
            {
                ChicagoCylinder("WillisAntennaA", parent, new Vector3(-7.2f, building.height + 18f, -3f), 0.55f, 0.75f, 34f, 5, slate);
                ChicagoCylinder("WillisAntennaB", parent, new Vector3(7.2f, building.height + 18f, 3f), 0.55f, 0.75f, 34f, 5, slate);
                ChicagoCube("WillisTop", parent, new Vector3(0f, building.height + 4.6f, 0f), new Vector3(building.width * 0.52f, 4.4f, building.depth * 0.52f), dark);
            }
            else if (string.Equals(building.landmark, "hancock", StringComparison.Ordinal))
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    var brace = ChicagoCube($"HancockBrace_{side}", parent, new Vector3(0f, building.height * 0.56f, side * (building.depth * 0.5f + 0.14f)), new Vector3(building.width * 1.18f, 1.25f, 0.42f), dark);
                    brace.transform.localRotation = Quaternion.Euler(0f, 0f, side * 0.74f * Mathf.Rad2Deg);
                }
                ChicagoCylinder("HancockAntenna", parent, new Vector3(0f, building.height + 10f, 0f), 0.45f, 0.65f, 20f, 5, slate);
            }
            else if (string.Equals(building.landmark, "watertower", StringComparison.Ordinal))
            {
                ChicagoCylinder("WaterTowerRoof", parent, new Vector3(0f, building.height + 6.2f, 0f), 0f, 9f, 12f, 4, materials.Bollard);
            }
            else if (string.Equals(building.landmark, "skyscraper", StringComparison.Ordinal))
            {
                CreateChicagoSkyscraperDetails(parent, building, document, materials);
            }
        }

        private static void CreateChicagoSkyscraperDetails(
            Transform parent,
            WofChicagoBuildingRecord building,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials)
        {
            var signRoot = new GameObject("RevolvingLedSign");
            signRoot.transform.SetParent(parent, false);
            signRoot.transform.localPosition = new Vector3(0f, 92f, 0f);
            var radius = Mathf.Max(building.width, building.depth) * 0.86f;
            var shell = ChicagoCylinder("LedShell", signRoot.transform, Vector3.zero, radius + 1.15f, radius + 1.15f, 25.6f, 48, materials.LedShell);
            var led = ChicagoCylinder("LedDisplay", signRoot.transform, Vector3.zero, radius, radius, 22.5f, 64, materials.LedSign);
            MarkDarrelDynamic(signRoot);
            MarkDarrelDynamic(shell);
            MarkDarrelDynamic(led);
            var ledRuntime = signRoot.AddComponent<WofChicagoLedSignRuntime>();
            ledRuntime.Configure(signRoot.transform, led.GetComponent<MeshRenderer>(), document.constants.ledSignUpdateIntervalSeconds);
            ChicagoCube("SkyscraperCrown", parent, new Vector3(0f, building.height + 7.4f, 0f), new Vector3(building.width * 0.58f, 12.2f, building.depth * 0.58f), ChicagoMaterial("ChicagoSkyscraperCrown", new Color(0.859f, 0.918f, 0.996f, 0.88f), null, true));
            ChicagoCylinder("SkyscraperCone", parent, new Vector3(0f, building.height + 21.5f, 0f), 0f, building.width * 0.24f, 20f, 4, materials.ParkPath);
            ChicagoCylinder("SkyscraperAntenna", parent, new Vector3(0f, building.height + 44f, 0f), 0.72f, 1.1f, 38f, 6, materials.Bollard);
            var bars = ChicagoMaterial("ChicagoSkyscraperBars", new Color(0.729f, 0.902f, 0.992f, 0.64f), null, true);
            foreach (var side in new[] { -1f, 1f })
            {
                ChicagoCube($"SkyscraperXBar_{side}", parent, new Vector3(side * (building.width * 0.5f + 0.24f), building.height * 0.52f, 0f), new Vector3(0.42f, building.height * 0.84f, 1.2f), bars);
                ChicagoCube($"SkyscraperZBar_{side}", parent, new Vector3(0f, building.height * 0.52f, side * (building.depth * 0.5f + 0.24f)), new Vector3(1.2f, building.height * 0.84f, 0.42f), bars);
            }
        }

        private static GameObject ChicagoCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var result = CreateVisualPrimitive(name, PrimitiveType.Cube, parent, position, scale, material);
            var renderer = result.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return result;
        }

        private static void ChicagoFlat(string name, Transform parent, float x, float y, float z, float width, float depth, Material material)
        {
            CreateDarrelQuad(name, parent, new Vector3(x, y, z), new Vector2(width, depth), 0f, material, true);
        }

        private static GameObject ChicagoCylinder(
            string name,
            Transform parent,
            Vector3 position,
            float topRadius,
            float bottomRadius,
            float height,
            int segments,
            Material material)
        {
            var assetName = $"Frustum_{topRadius:F3}_{bottomRadius:F3}_{height:F3}_{segments}".Replace('.', '_');
            var mesh = GetOrCreateMeshAsset(
                $"{ChicagoGeometryRoot}/{assetName}.asset",
                () => CreateDarrelFrustumMesh(topRadius, bottomRadius, height, segments));
            return ChicagoMeshVisual(name, parent, position, mesh, material);
        }

        private static GameObject ChicagoMeshVisual(string name, Transform parent, Vector3 position, Mesh mesh, Material material)
        {
            var result = CreateMeshVisual(name, parent, position, mesh, material);
            var renderer = result.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return result;
        }

        private static void ChicagoDisk(string name, Transform parent, float radius, int segments, Vector3 position, Material material)
        {
            var mesh = GetOrCreateMeshAsset($"{ChicagoGeometryRoot}/Disk_{radius:F2}_{segments}.asset", () => CreateDarrelRingMesh(0f, radius, segments));
            ChicagoMeshVisual(name, parent, position, mesh, material);
        }

        private static void ChicagoRing(string name, Transform parent, float inner, float outer, int segments, Vector3 position, Material material)
        {
            var mesh = GetOrCreateMeshAsset($"{ChicagoGeometryRoot}/Ring_{inner:F2}_{outer:F2}_{segments}.asset", () => CreateDarrelRingMesh(inner, outer, segments));
            ChicagoMeshVisual(name, parent, position, mesh, material);
        }

        private sealed class ChicagoMaterialSet
        {
            public Material[] Facades;
            public Material[] StoreSigns;
            public Material[] Ads;
            public Material Ground;
            public Material CitySurface;
            public Material Water;
            public Material Lakefront;
            public Material River;
            public Material RoadSidewalk;
            public Material Road;
            public Material LaneDash;
            public Material Roof;
            public Material DoorFrame;
            public Material DoorGlass;
            public Material DoorLight;
            public Material StoreTrim;
            public Material StoreWindow;
            public Material AdBacking;
            public Material InteriorFloorA;
            public Material InteriorFloorB;
            public Material InteriorWall;
            public Material InteriorCounter;
            public Material InteriorCounterTop;
            public Material InteriorShelf;
            public Material InteriorMat;
            public Material ParkGrass;
            public Material ParkPath;
            public Material SidewalkDetail;
            public Material Parking;
            public Material Crosswalk76;
            public Material Crosswalk66;
            public Material Crosswalk82;
            public Material DarkMetal;
            public Material BlackMetal;
            public Material Hydrant;
            public Material HydrantLight;
            public Material HydrantDark;
            public Material Lamp;
            public Material LampGlow;
            public Material Trash;
            public Material Steel;
            public Material Bench;
            public Material BenchBack;
            public Material TreeTrunk;
            public Material TreeLeafA;
            public Material TreeLeafB;
            public Material BeanBase;
            public Material Bean;
            public Material BeanInner;
            public Material BeanLower;
            public Material BeanHighlight;
            public Material BeanCleft;
            public Material BeanCleftDark;
            public Material BeanLowerCleft;
            public Material BeanShineA;
            public Material BeanShineB;
            public Material Bollard;
            public Material ChicagoSign;
            public Material LedSign;
            public Material LedShell;
        }
    }
}
