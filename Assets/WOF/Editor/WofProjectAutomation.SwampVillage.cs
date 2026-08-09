using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string SwampArtRoot = "Assets/WOF/Art/Generated/React/SwampVillage";
        private const string SwampLayoutPath = SwampArtRoot + "/runtime-layout.json";
        private const string SwampTextureRoot = SwampArtRoot + "/Textures";
        private const string SwampToadRoot = SwampArtRoot + "/Toad";
        private const string SwampGeometryRoot = GeometryRoot + "/SwampVillage";
        private static readonly string[] SwampMossColors = { "#5d7d34", "#425f27", "#728644", "#30491f" };

        private static void CreateSwampVillageScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = WofSwampVillageSceneLoader.SceneName;
            var world = new GameObject("World");
            CreateSwampVillage(world.transform, GetOrCreateVillagerMaterial());
            EditorSceneManager.SaveScene(scene, SwampScenePath);
        }

        private static void CreateSwampVillage(Transform parent, Material villagerMaterial)
        {
            var document = LoadSwampVillageDocument();
            var materials = CreateSwampMaterials();
            var root = new GameObject("ReactSurvivalSwampVillage_0_-3");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofSwampVillageLayout.WorldOrigin;

            CreateSwampSurface(root.transform, document, materials);
            CreateSwampWater(root.transform, document, materials);
            CreateSwampCentralPlatform(root.transform, document, materials);
            CreateSwampWalkways(root.transform, document, materials);
            CreateSwampRamps(root.transform, document, materials);
            CreateSwampHuts(root.transform, document, materials);
            CreateSwampRopeLights(root.transform, document, materials);
            CreateSwampToad(root.transform, document, materials);
            CreateSwampVillagers(parent, document, villagerMaterial);
        }

        private static WofSwampVillageDocument LoadSwampVillageDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(SwampLayoutPath);
            var document = JsonUtility.FromJson<WofSwampVillageDocument>(source.text);
            if (document == null || document.schemaVersion != 1 || document.chunk == null ||
                document.chunk.cx != WofSwampVillageLayout.ChunkX ||
                document.chunk.cz != WofSwampVillageLayout.ChunkZ || document.chunk.distance != 0 ||
                !string.Equals(document.chunk.biome, "swamp", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.villageKind, "swamp", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.lod, "near", StringComparison.Ordinal) ||
                !document.chunk.hasVillage || !document.chunk.hasRiver || document.chunk.riverVertical ||
                !Mathf.Approximately(document.baseHeight, WofSwampVillageLayout.ReactBaseHeight) ||
                !WofSwampVillageLayout.HasExactCounts(document.counts) || document.constants == null ||
                !Mathf.Approximately(document.constants.villageRadius, WofSwampVillageLayout.VillageRadius) ||
                !Mathf.Approximately(document.constants.platformSize, WofSwampVillageLayout.PlatformSize) ||
                document.layout == null ||
                !Mathf.Approximately(document.layout.waterY, WofSwampVillageLayout.ReactWaterY) ||
                !Mathf.Approximately(document.layout.platformY, WofSwampVillageLayout.ReactPlatformY) ||
                document.layout.huts?.Length != 13 || document.layout.walkways?.Length != 17 ||
                document.layout.ramps?.Length != 4 || document.layout.lilyPads?.Length != 28 ||
                document.layout.stumps?.Length != 18 || document.layout.reeds?.Length != 36 ||
                document.layout.ropes?.Length != 13 || document.ropeSegments?.Length != 91 ||
                document.ropeBulbs?.Length != 39 || document.villagers?.Length != 13 ||
                document.toad == null || document.toad.frameSize?.Length != 2 ||
                document.toad.frameSize[0] != 288 || document.toad.frameSize[1] != 187 ||
                document.toad.idle?.Length != 28 || document.toad.yawn?.Length != 12 ||
                string.IsNullOrWhiteSpace(document.toad.sleep) || string.IsNullOrWhiteSpace(document.toad.sleepZ) ||
                !IsValidDesertMesh(document.padGeometry) || !IsValidDesertMesh(document.lilyPadGeometry))
            {
                throw new InvalidOperationException($"Invalid exact React swamp village layout at {SwampLayoutPath}.");
            }
            return document;
        }

        private static SwampMaterialSet CreateSwampMaterials()
        {
            var terrainDetail = LoadRequiredAsset<Texture2D>($"{SwampTextureRoot}/terrain-detail.png");
            return new SwampMaterialSet
            {
                Terrain = GetOrCreateSwampTerrainMaterial(terrainDetail),
                DeepWater = GetOrCreateDesertUnlit("SwampDeepWater", new Color(0.145f, 0.239f, 0.184f, 0.72f), null, true),
                Water = GetOrCreateDesertUnlit("SwampWater", new Color(0.122f, 0.365f, 0.345f, 0.82f), null, true),
                Ripple = GetOrCreateDesertUnlit("SwampRipple", new Color(0.541f, 0.831f, 0.776f, 0.18f), null, true),
                Platform = GetOrCreateDesertUnlit("SwampPlatform", HexColor("#3d2818"), null, false),
                PlatformTop = GetOrCreateDesertUnlit("SwampPlatformTop", HexColor("#6a4729"), null, false),
                PlankA = GetOrCreateDesertUnlit("SwampPlankA", HexColor("#7b5730"), null, false),
                PlankB = GetOrCreateDesertUnlit("SwampPlankB", HexColor("#4e331d"), null, false),
                Walkway = GetOrCreateDesertUnlit("SwampWalkway", HexColor("#4a301d"), null, false),
                WalkwayPlankA = GetOrCreateDesertUnlit("SwampWalkwayPlankA", HexColor("#6a4729"), null, false),
                WalkwayPlankB = GetOrCreateDesertUnlit("SwampWalkwayPlankB", HexColor("#5a3a22"), null, false),
                DarkWood = GetOrCreateDesertUnlit("SwampDarkWood", HexColor("#21150c"), null, false),
                WetWood = GetOrCreateDesertUnlit("SwampWetWood", new Color(0.169f, 0.110f, 0.071f, 0.72f), null, true),
                Support = GetOrCreateDesertUnlit("SwampSupport", HexColor("#2d1c10"), null, false),
                HutStilt = GetOrCreateDesertUnlit("SwampHutStilt", HexColor("#24170d"), null, false),
                HutDeck = GetOrCreateDesertUnlit("SwampHutDeck", HexColor("#3e2817"), null, false),
                HutDeckTop = GetOrCreateDesertUnlit("SwampHutDeckTop", HexColor("#6b4828"), null, false),
                Door = GetOrCreateDesertUnlit("SwampHutDoor", HexColor("#1c140d"), null, false),
                WindowFront = GetOrCreateDesertUnlit("SwampWindowFront", HexColor("#91d7b7"), null, false),
                WindowSide = GetOrCreateDesertUnlit("SwampWindowSide", HexColor("#6cb69d"), null, false),
                RoofBase = GetOrCreateDesertUnlit("SwampRoofBase", HexColor("#18220f"), null, false),
                Stump = GetOrCreateDesertUnlit("SwampStump", HexColor("#2f2417"), null, false),
                StumpTop = GetOrCreateDesertUnlit("SwampStumpTop", HexColor("#5a4022"), null, false),
                ReedA = GetOrCreateDesertUnlit("SwampReedA", HexColor("#465a28"), null, false),
                ReedB = GetOrCreateDesertUnlit("SwampReedB", HexColor("#5e7434"), null, false),
                ReedHead = GetOrCreateDesertUnlit("SwampReedHead", HexColor("#5a3620"), null, false),
                Rope = GetOrCreateDesertUnlit("SwampRope", HexColor("#2a1a0f"), null, false),
                BulbCord = GetOrCreateDesertUnlit("SwampBulbCord", HexColor("#1b120a"), null, false),
                ToadShadow = GetOrCreateDesertUnlit("SwampToadShadow", new Color(0.106f, 0.165f, 0.086f, 0.52f), null, true),
                Sprite = GetOrCreateSwampSpriteMaterial()
            };
        }

        private static Material GetOrCreateSwampTerrainMaterial(Texture texture)
        {
            var shader = Shader.Find("WOF/Vertex Color Texture");
            if (shader == null) throw new InvalidOperationException("Required swamp vertex-color terrain shader was not imported.");
            var path = $"{MaterialsRoot}/SwampTerrain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Swamp Terrain" };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", HexColor("#35492e"));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateSwampSpriteMaterial()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("Required Unity sprite shader was not imported.");
            var path = $"{MaterialsRoot}/SwampToadSprite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Swamp Toad Sprite" };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateSwampSurface(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var mesh = GetOrCreateMeshAsset(
                SwampGeometryRoot + "/VillagePad.asset",
                () => CreateDesertSerializedMesh("SwampVillagePad", document.padGeometry));
            var surface = CreateMeshVisual("ExactVillagePad", parent, Vector3.zero, mesh, materials.Terrain);
            surface.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void CreateSwampWater(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var root = new GameObject("SwampVillageWater");
            root.transform.SetParent(parent, false);
            var disk = GetOrCreateMeshAsset(SwampGeometryRoot + "/WaterDisk28.asset", () => CreateDarrelRingMesh(0f, 1f, 28));
            var ripple = GetOrCreateMeshAsset(SwampGeometryRoot + "/RippleRing18.asset", () => CreateDarrelRingMesh(0.82f, 1f, 18));
            var deep = CreateMeshVisual("DeepWater", root.transform, new Vector3(0f, document.layout.waterY - 0.08f, 0f), disk, materials.DeepWater);
            deep.transform.localScale = new Vector3(WofSwampVillageLayout.VillageRadius + 72f, 1f, WofSwampVillageLayout.VillageRadius + 48f);
            var water = CreateMeshVisual("Water", root.transform, new Vector3(0f, document.layout.waterY + 0.02f, 0f), disk, materials.Water);
            water.transform.localScale = new Vector3(WofSwampVillageLayout.VillageRadius + 50f, 1f, WofSwampVillageLayout.VillageRadius + 30f);
            for (var index = 0; index < 10; index++)
            {
                var angle = index * Mathf.PI * 2f / 10f + 0.34f;
                var radius = 34f + index % 5 * 28f;
                var ring = CreateMeshVisual($"Ripple_{index:00}", root.transform,
                    new Vector3(Mathf.Sin(angle) * radius, document.layout.waterY + 0.055f, Mathf.Cos(angle) * radius), ripple, materials.Ripple);
                ring.transform.localScale = new Vector3(10f + index % 3 * 4f, 1f, 7f + index % 4 * 2f);
            }
            CreateSwampLilyPads(root.transform, document, materials);
            CreateSwampStumps(root.transform, document, materials);
            CreateSwampReeds(root.transform, document, materials);
        }

        private static void CreateSwampLilyPads(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var lilyMesh = GetOrCreateMeshAsset(
                SwampGeometryRoot + "/ExactLilyPad.asset",
                () => CreateDesertSerializedMesh("ExactSwampLilyPad", document.lilyPadGeometry));
            var flowerMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/LilyFlower7.asset", () => CreateDarrelRingMesh(0f, 0.72f, 7));
            var veinA = GetOrCreateDesertUnlit("SwampLilyVeinA", new Color(0.690f, 0.843f, 0.475f, 0.74f), null, true);
            var veinB = GetOrCreateDesertUnlit("SwampLilyVeinB", new Color(0.690f, 0.843f, 0.475f, 0.66f), null, true);
            var veinC = GetOrCreateDesertUnlit("SwampLilyVeinC", new Color(0.690f, 0.843f, 0.475f, 0.62f), null, true);
            var flower = GetOrCreateDesertUnlit("SwampLilyFlower", HexColor("#e8b4d8"), null, false);
            foreach (var pad in document.layout.lilyPads)
            {
                var padRoot = new GameObject(pad.key);
                padRoot.transform.SetParent(parent, false);
                padRoot.transform.localPosition = new Vector3(pad.localX, document.layout.waterY + 0.26f, pad.localZ);
                padRoot.transform.localRotation = Quaternion.Euler(0f, pad.rotation * Mathf.Rad2Deg, 0f);
                var padMaterial = GetOrCreateDesertUnlit($"SwampLily_{pad.color.TrimStart('#')}", HexColor(pad.color), null, false);
                var visual = CreateMeshVisual("Pad", padRoot.transform, Vector3.zero, lilyMesh, padMaterial);
                visual.transform.localScale = new Vector3(pad.scale * 1.28f, 1f, pad.scale);
                CreateVisualPrimitive("VeinA", PrimitiveType.Cube, padRoot.transform, new Vector3(pad.scale * 0.05f, 0.13f, 0f), new Vector3(pad.scale * 1.1f, 0.08f, 0.1f), veinA);
                var second = CreateVisualPrimitive("VeinB", PrimitiveType.Cube, padRoot.transform, new Vector3(pad.scale * 0.06f, 0.14f, pad.scale * 0.16f), new Vector3(pad.scale * 0.62f, 0.07f, 0.08f), veinB);
                second.transform.localRotation = Quaternion.Euler(0f, 0.58f * Mathf.Rad2Deg, 0f);
                var third = CreateVisualPrimitive("VeinC", PrimitiveType.Cube, padRoot.transform, new Vector3(pad.scale * 0.04f, 0.14f, -pad.scale * 0.18f), new Vector3(pad.scale * 0.58f, 0.07f, 0.08f), veinC);
                third.transform.localRotation = Quaternion.Euler(0f, -0.58f * Mathf.Rad2Deg, 0f);
                if (pad.scale > 13f)
                {
                    CreateMeshVisual("Flower", padRoot.transform, new Vector3(pad.scale * 0.08f, 0.12f, -pad.scale * 0.12f), flowerMesh, flower);
                }
            }
        }

        private static void CreateSwampStumps(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var stumpMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/StumpUnit7.asset", () => CreateDarrelFrustumMesh(0.8f, 1f, 1f, 7));
            var capMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/StumpCapUnit7.asset", () => CreateDarrelFrustumMesh(0.9f, 0.9f, 0.28f, 7));
            foreach (var stump in document.layout.stumps)
            {
                var root = new GameObject(stump.key);
                root.transform.SetParent(parent, false);
                root.transform.localPosition = new Vector3(stump.localX, document.layout.waterY, stump.localZ);
                var body = CreateMeshVisual("Body", root.transform, new Vector3(0f, stump.height * 0.5f, 0f), stumpMesh, materials.Stump);
                body.transform.localScale = new Vector3(stump.radius, stump.height, stump.radius);
                var cap = CreateMeshVisual("Top", root.transform, new Vector3(0f, stump.height + 0.14f, 0f), capMesh, materials.StumpTop);
                cap.transform.localScale = new Vector3(stump.radius, 1f, stump.radius);
            }
        }

        private static void CreateSwampReeds(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var stem = GetOrCreateMeshAsset(SwampGeometryRoot + "/ReedStemUnit5.asset", () => CreateDarrelFrustumMesh(0.08f, 0.16f, 1f, 5));
            var head = GetOrCreateMeshAsset(SwampGeometryRoot + "/ReedHead6.asset", () => CreateDarrelFrustumMesh(0.16f, 0.2f, 0.85f, 6));
            foreach (var reed in document.layout.reeds)
            {
                var patch = new GameObject(reed.key);
                patch.transform.SetParent(parent, false);
                patch.transform.localPosition = new Vector3(reed.localX, document.layout.waterY + 0.12f, reed.localZ);
                patch.transform.localRotation = Quaternion.Euler(0f, reed.rotation * Mathf.Rad2Deg, 0f);
                patch.transform.localScale = Vector3.one * reed.scale;
                for (var index = 0; index < 5; index++)
                {
                    var offsetX = (index - 2) * 0.82f + (index % 2 == 0 ? 0.22f : -0.12f);
                    var offsetZ = (index % 3 - 1) * 0.58f;
                    var height = 4.2f + index * 0.72f;
                    var reedRoot = new GameObject($"Reed_{index}");
                    reedRoot.transform.SetParent(patch.transform, false);
                    reedRoot.transform.localPosition = new Vector3(offsetX, 0f, offsetZ);
                    reedRoot.transform.localRotation = Quaternion.Euler(0.03f * (index - 2) * Mathf.Rad2Deg, 0f, 0.08f * (index % 2 == 0 ? 1f : -1f) * Mathf.Rad2Deg);
                    var visual = CreateMeshVisual("Stem", reedRoot.transform, new Vector3(0f, height * 0.5f, 0f), stem, index % 2 == 0 ? materials.ReedA : materials.ReedB);
                    visual.transform.localScale = new Vector3(1f, height, 1f);
                    if (index % 2 == 0) CreateMeshVisual("Head", reedRoot.transform, new Vector3(0f, height + 0.28f, 0f), head, materials.ReedHead);
                }
            }
        }

        private static void CreateSwampCentralPlatform(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var root = new GameObject("CentralPlatform");
            root.transform.SetParent(parent, false);
            CreateVisualPrimitive("Body", PrimitiveType.Cube, root.transform, new Vector3(0f, document.layout.platformY, 0f), new Vector3(76f, 1.1f, 76f), materials.Platform);
            CreateVisualPrimitive("Top", PrimitiveType.Cube, root.transform, new Vector3(0f, document.layout.platformY + 0.7f, 0f), new Vector3(80.8f, 0.32f, 80.8f), materials.PlatformTop);
            for (var index = 0; index < 9; index++)
            {
                var z = -38f + (index + 0.5f) * (76f / 9f);
                CreateVisualPrimitive($"Plank_{index:00}", PrimitiveType.Cube, root.transform, new Vector3(0f, document.layout.platformY + 0.9f, z), new Vector3(81.8f, 0.14f, 1.1f), index % 2 == 0 ? materials.PlankA : materials.PlankB);
            }
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI * 2f / 8f;
                var moss = CreateVisualPrimitive($"Moss_{index:00}", PrimitiveType.Cube, root.transform,
                    new Vector3(Mathf.Sin(angle) * 24f, document.layout.platformY + 1.02f, Mathf.Cos(angle) * 24f),
                    new Vector3(8.5f, 0.12f, 3.4f), GetSwampMossMaterial(index, 0.74f));
                moss.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
            CreateSwampBoxCollider(root, new Vector3(0f, document.layout.platformY, 0f), new Vector3(76f, 0.96f, 76f));
        }

        private static void CreateSwampWalkways(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var supportMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/WalkwaySupportUnit6.asset", () => CreateDarrelFrustumMesh(0.42f, 0.54f, 1f, 6));
            var root = new GameObject("Walkways");
            root.transform.SetParent(parent, false);
            foreach (var walkway in document.layout.walkways)
            {
                var item = new GameObject(walkway.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(walkway.localX, 0f, walkway.localZ);
                item.transform.localRotation = Quaternion.Euler(0f, walkway.rotation * Mathf.Rad2Deg, 0f);
                CreateVisualPrimitive("Body", PrimitiveType.Cube, item.transform, new Vector3(0f, walkway.y, 0f), new Vector3(walkway.width, 0.82f, walkway.length), materials.Walkway);
                var plankCount = Mathf.Max(4, Mathf.FloorToInt(walkway.length / 9f));
                for (var index = 0; index < plankCount; index++)
                {
                    var z = -walkway.length * 0.5f + (index + 0.5f) * walkway.length / plankCount;
                    CreateVisualPrimitive($"Plank_{index:00}", PrimitiveType.Cube, item.transform, new Vector3(0f, walkway.y + 0.5f, z), new Vector3(walkway.width + 1.1f, 0.22f, 1.7f), index % 2 == 0 ? materials.WalkwayPlankA : materials.WalkwayPlankB);
                }
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateVisualPrimitive($"Rail_{side}", PrimitiveType.Cube, item.transform, new Vector3(side * walkway.width * 0.52f, walkway.y + 1.15f, 0f), new Vector3(0.42f, 0.48f, walkway.length * 0.96f), materials.DarkWood);
                }
                var mossCount = Mathf.Max(3, Mathf.FloorToInt(walkway.length / 34f));
                for (var index = 0; index < mossCount; index++)
                {
                    var z = -walkway.length * 0.5f + 12f + index * walkway.length / mossCount;
                    var x = (index % 2 == 0 ? -1f : 1f) * walkway.width * 0.22f;
                    CreateVisualPrimitive($"Moss_{index:00}", PrimitiveType.Cube, item.transform, new Vector3(x, walkway.y + 0.66f, z), new Vector3(walkway.width * 0.28f, 0.1f, 5.5f), GetSwampMossMaterial(index, 0.88f));
                }
                var stiltHeight = Mathf.Max(0.4f, walkway.y - document.layout.waterY);
                var supportCount = Mathf.Max(2, Mathf.Min(7, Mathf.FloorToInt(walkway.length / 28f)));
                for (var index = 0; index < supportCount; index++)
                {
                    var z = -walkway.length * 0.5f + (index + 0.5f) * walkway.length / supportCount;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var support = CreateMeshVisual($"Support_{index}_{side}", item.transform, new Vector3(side * walkway.width * 0.42f, document.layout.waterY + stiltHeight * 0.5f, z), supportMesh, materials.Support);
                        support.transform.localScale = new Vector3(1f, stiltHeight, 1f);
                    }
                }
                CreateSwampBoxCollider(item, new Vector3(0f, walkway.y, 0f), new Vector3(walkway.width, 0.84f, walkway.length));
            }
        }

        private static void CreateSwampRamps(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var root = new GameObject("Ramps");
            root.transform.SetParent(parent, false);
            foreach (var ramp in document.layout.ramps)
            {
                var item = new GameObject(ramp.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(ramp.localX, (ramp.highY + ramp.lowY) * 0.5f, ramp.localZ);
                item.transform.localRotation = Quaternion.Euler(0f, ramp.rotation * Mathf.Rad2Deg, 0f);
                var slopeRoot = new GameObject("Slope");
                slopeRoot.transform.SetParent(item.transform, false);
                var slope = Mathf.Atan2(ramp.highY - ramp.lowY, ramp.length);
                slopeRoot.transform.localRotation = Quaternion.Euler(slope * Mathf.Rad2Deg, 0f, 0f);
                CreateVisualPrimitive("Body", PrimitiveType.Cube, slopeRoot.transform, Vector3.zero, new Vector3(ramp.width, 0.78f, ramp.length), materials.Walkway);
                var plankCount = Mathf.Max(4, Mathf.FloorToInt(ramp.length / 8f));
                for (var index = 0; index < plankCount; index++)
                {
                    var z = -ramp.length * 0.5f + (index + 0.5f) * ramp.length / plankCount;
                    CreateVisualPrimitive($"Plank_{index:00}", PrimitiveType.Cube, slopeRoot.transform, new Vector3(0f, 0.48f, z), new Vector3(ramp.width + 1.4f, 0.18f, 1.75f), index % 2 == 0 ? materials.WalkwayPlankA : materials.WalkwayPlankB);
                }
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateVisualPrimitive($"Rail_{side}", PrimitiveType.Cube, slopeRoot.transform, new Vector3(side * ramp.width * 0.54f, 1.08f, 0f), new Vector3(0.46f, 0.46f, ramp.length * 0.92f), materials.DarkWood);
                }
                CreateSwampBoxCollider(slopeRoot, Vector3.zero, new Vector3(ramp.width, 0.8f, ramp.length));
            }
        }

        private static void CreateSwampHuts(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var stiltMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/HutStiltUnit6.asset", () => CreateDarrelFrustumMesh(0.54f, 0.82f, 1f, 6));
            var roofMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/HutRoofCone4.asset", () => CreateDarrelFrustumMesh(0f, 1f, 5.2f, 4));
            var root = new GameObject("StiltHuts");
            root.transform.SetParent(parent, false);
            foreach (var hut in document.layout.huts)
            {
                var item = new GameObject(hut.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(hut.localX, 0f, hut.localZ);
                item.transform.localRotation = Quaternion.Euler(0f, hut.rotation * Mathf.Rad2Deg, 0f);
                var wallThickness = Mathf.Min(1.1f, hut.width * 0.12f, hut.depth * 0.12f);
                var doorWidth = Mathf.Min(5.3f, hut.width - wallThickness * 4f);
                var doorHeight = Mathf.Min(7.1f, hut.height - 1.2f);
                var frontWallWidth = Mathf.Max(1.1f, (hut.width - doorWidth) * 0.5f);
                var lintelHeight = Mathf.Max(0.85f, hut.height - doorHeight);
                var stiltHeight = Mathf.Max(0.5f, hut.platformY - document.layout.waterY);
                var wall = GetOrCreateDesertUnlit($"SwampHutWall_{hut.wallColor.TrimStart('#')}", HexColor(hut.wallColor), null, false);
                var roof = GetOrCreateDesertUnlit($"SwampHutRoof_{hut.roofColor.TrimStart('#')}", HexColor(hut.roofColor), null, false);
                foreach (var xSign in new[] { -1f, 1f }) foreach (var zSign in new[] { -1f, 1f })
                {
                    var stilt = CreateMeshVisual($"Stilt_{xSign}_{zSign}", item.transform,
                        new Vector3(xSign * hut.width * 0.42f, document.layout.waterY + stiltHeight * 0.5f, zSign * hut.depth * 0.42f), stiltMesh, materials.HutStilt);
                    stilt.transform.localScale = new Vector3(1f, stiltHeight, 1f);
                }
                CreateVisualPrimitive("Platform", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY, 0f), new Vector3(hut.width + 4.8f, 1f, hut.depth + 4.8f), materials.HutDeck);
                CreateVisualPrimitive("PlatformTop", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + 0.72f, 0f), new Vector3(hut.width + 5.6f, 0.32f, hut.depth + 5.6f), materials.HutDeckTop);
                CreateVisualPrimitive("WallLeft", PrimitiveType.Cube, item.transform, new Vector3(-hut.width * 0.5f + wallThickness * 0.5f, hut.platformY + hut.height * 0.5f, 0f), new Vector3(wallThickness, hut.height, hut.depth), wall);
                CreateVisualPrimitive("WallRight", PrimitiveType.Cube, item.transform, new Vector3(hut.width * 0.5f - wallThickness * 0.5f, hut.platformY + hut.height * 0.5f, 0f), new Vector3(wallThickness, hut.height, hut.depth), wall);
                CreateVisualPrimitive("WallBack", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + hut.height * 0.5f, -hut.depth * 0.5f + wallThickness * 0.5f), new Vector3(hut.width, hut.height, wallThickness), wall);
                CreateVisualPrimitive("WallFrontLeft", PrimitiveType.Cube, item.transform, new Vector3(-doorWidth * 0.5f - frontWallWidth * 0.5f, hut.platformY + hut.height * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, hut.height, wallThickness), wall);
                CreateVisualPrimitive("WallFrontRight", PrimitiveType.Cube, item.transform, new Vector3(doorWidth * 0.5f + frontWallWidth * 0.5f, hut.platformY + hut.height * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, hut.height, wallThickness), wall);
                CreateVisualPrimitive("Lintel", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + doorHeight + lintelHeight * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(doorWidth, lintelHeight, wallThickness), wall);
                CreateVisualPrimitive("Door", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + doorHeight * 0.5f, hut.depth * 0.5f + 0.16f), new Vector3(doorWidth * 0.82f, doorHeight, 0.34f), materials.Door);
                CreateVisualPrimitive("WindowFrontLeft", PrimitiveType.Cube, item.transform, new Vector3(-hut.width * 0.28f, hut.platformY + Mathf.Min(hut.height - 2f, 6.1f), hut.depth * 0.5f + 0.18f), new Vector3(3.2f, 2.5f, 0.38f), materials.WindowFront);
                CreateVisualPrimitive("WindowFrontRight", PrimitiveType.Cube, item.transform, new Vector3(hut.width * 0.28f, hut.platformY + Mathf.Min(hut.height - 2f, 6.1f), hut.depth * 0.5f + 0.18f), new Vector3(3.2f, 2.5f, 0.38f), materials.WindowFront);
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateVisualPrimitive($"WindowSide_{side}", PrimitiveType.Cube, item.transform, new Vector3(side * (hut.width * 0.5f + 0.18f), hut.platformY + Mathf.Min(hut.height - 2.1f, 6.2f), -hut.depth * 0.12f), new Vector3(0.36f, 2.3f, 3.25f), materials.WindowSide);
                }
                var frontPlanks = Mathf.Max(3, Mathf.FloorToInt(hut.width / 4f));
                for (var index = 0; index < frontPlanks; index++)
                {
                    var x = -hut.width * 0.5f + (index + 0.5f) * hut.width / frontPlanks;
                    CreateVisualPrimitive($"FrontPlank_{index:00}", PrimitiveType.Cube, item.transform, new Vector3(x, hut.platformY + hut.height * 0.52f, hut.depth * 0.5f + 0.24f), new Vector3(0.18f, hut.height * 0.76f, 0.22f), materials.DarkWood);
                }
                foreach (var side in new[] { -1f, 1f })
                {
                    CreateVisualPrimitive($"WetBand_{side}", PrimitiveType.Cube, item.transform, new Vector3(side * (hut.width * 0.5f + 0.2f), hut.platformY + 1.55f, 0f), new Vector3(0.32f, 1.15f, hut.depth * 0.9f), materials.WetWood);
                }
                CreateVisualPrimitive("WetBandFront", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + 1.5f, hut.depth * 0.5f + 0.27f), new Vector3(hut.width * 0.9f, 1.1f, 0.26f), materials.WetWood);
                var roofRadius = Mathf.Max(hut.width, hut.depth) * 0.72f;
                var roofVisual = CreateMeshVisual("Roof", item.transform, new Vector3(0f, hut.platformY + hut.height + 2.4f, 0f), roofMesh, roof);
                roofVisual.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                roofVisual.transform.localScale = new Vector3(roofRadius, 1f, roofRadius);
                CreateVisualPrimitive("RoofBase", PrimitiveType.Cube, item.transform, new Vector3(0f, hut.platformY + hut.height + 0.45f, 0f), new Vector3(hut.width + 3.2f, 0.58f, hut.depth + 3.2f), materials.RoofBase);
                for (var index = 0; index < 6; index++)
                {
                    var side = index % 2 == 0 ? -1f : 1f;
                    var x = -hut.width * 0.38f + index * hut.width * 0.15f;
                    var vineLength = 2.4f + index % 3 * 0.9f;
                    CreateVisualPrimitive($"RoofVine_{index}", PrimitiveType.Cube, item.transform,
                        new Vector3(x, hut.platformY + hut.height + 0.08f - vineLength * 0.5f, side * (hut.depth * 0.5f + 1.15f)),
                        new Vector3(0.18f, vineLength, 0.18f), GetSwampMossMaterial(index, 1f));
                }
                CreateSwampHutColliders(item, hut, wallThickness, doorWidth, doorHeight, frontWallWidth, lintelHeight);
            }
        }

        private static void CreateSwampHutColliders(GameObject item, WofSwampHutRecord hut, float wallThickness, float doorWidth, float doorHeight, float frontWallWidth, float lintelHeight)
        {
            CreateSwampBoxCollider(item, new Vector3(0f, hut.platformY, 0f), new Vector3(hut.width + 4.4f, 1f, hut.depth + 4.4f));
            CreateSwampBoxCollider(item, new Vector3(-hut.width * 0.5f + wallThickness * 0.5f, hut.platformY + hut.height * 0.5f, 0f), new Vector3(wallThickness, hut.height, hut.depth));
            CreateSwampBoxCollider(item, new Vector3(hut.width * 0.5f - wallThickness * 0.5f, hut.platformY + hut.height * 0.5f, 0f), new Vector3(wallThickness, hut.height, hut.depth));
            CreateSwampBoxCollider(item, new Vector3(0f, hut.platformY + hut.height * 0.5f, -hut.depth * 0.5f + wallThickness * 0.5f), new Vector3(hut.width, hut.height, wallThickness));
            CreateSwampBoxCollider(item, new Vector3(-doorWidth * 0.5f - frontWallWidth * 0.5f, hut.platformY + hut.height * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, hut.height, wallThickness));
            CreateSwampBoxCollider(item, new Vector3(doorWidth * 0.5f + frontWallWidth * 0.5f, hut.platformY + hut.height * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, hut.height, wallThickness));
            CreateSwampBoxCollider(item, new Vector3(0f, hut.platformY + doorHeight + lintelHeight * 0.5f, hut.depth * 0.5f - wallThickness * 0.5f), new Vector3(doorWidth, lintelHeight, wallThickness));
        }

        private static void CreateSwampRopeLights(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var ropeMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/RopeSegmentUnit5.asset", () => CreateDarrelFrustumMesh(0.16f, 0.18f, 1f, 5));
            var cordMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/BulbCordUnit5.asset", () => CreateDarrelFrustumMesh(0.045f, 0.055f, 1f, 5));
            var bulbMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/BulbSphere8x6.asset", () => CreateUvSphereMesh(1f, 8, 6));
            var glowMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/BulbGlow10x8.asset", () => CreateUvSphereMesh(1f, 10, 8));
            var root = new GameObject("RopeLights");
            root.transform.SetParent(parent, false);
            foreach (var segment in document.ropeSegments)
            {
                var visual = CreateMeshVisual(segment.key, root.transform, ToSwampVector(segment.position), ropeMesh, materials.Rope);
                visual.transform.localRotation = ToSwampQuaternion(segment.quaternion);
                visual.transform.localScale = new Vector3(1f, segment.length, 1f);
            }
            foreach (var bulb in document.ropeBulbs)
            {
                var material = GetOrCreateDesertUnlit($"SwampBulb_{bulb.color.TrimStart('#')}", HexColor(bulb.color), null, true);
                var glowMaterial = GetOrCreateSwampGlowMaterial(bulb.color);
                var cord = CreateMeshVisual(bulb.key + "_Cord", root.transform, ToSwampVector(bulb.cordPosition), cordMesh, materials.BulbCord);
                cord.transform.localScale = new Vector3(1f, bulb.cordLength, 1f);
                var light = CreateMeshVisual(bulb.key + "_Bulb", root.transform, ToSwampVector(bulb.position), bulbMesh, material);
                light.transform.localScale = Vector3.one * 0.58f;
                var glow = CreateMeshVisual(bulb.key + "_Glow", root.transform, ToSwampVector(bulb.position), glowMesh, glowMaterial);
                glow.transform.localScale = Vector3.one * 1.45f;
                if (bulb.hasPointLight)
                {
                    var point = new GameObject(bulb.key + "_PointLight");
                    point.transform.SetParent(root.transform, false);
                    point.transform.localPosition = ToSwampVector(bulb.position);
                    var component = point.AddComponent<Light>();
                    component.type = LightType.Point;
                    component.color = HexColor(bulb.color);
                    component.intensity = 0.28f;
                    component.range = 18f;
                    component.shadows = LightShadows.None;
                }
            }
        }

        private static void CreateSwampToad(Transform parent, WofSwampVillageDocument document, SwampMaterialSet materials)
        {
            var root = new GameObject("SwampGiantToad");
            root.transform.SetParent(parent, false);
            var shadowMesh = GetOrCreateMeshAsset(SwampGeometryRoot + "/ToadShadow20.asset", () => CreateDarrelRingMesh(0f, 22f, 20));
            CreateMeshVisual("Shadow", root.transform, new Vector3(0f, document.layout.platformY + 0.58f, 0f), shadowMesh, materials.ToadShadow);
            var toad = new GameObject("ToadSprite");
            toad.transform.SetParent(root.transform, false);
            var toadRenderer = toad.AddComponent<SpriteRenderer>();
            toadRenderer.sharedMaterial = materials.Sprite;
            toadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            toadRenderer.receiveShadows = false;
            toadRenderer.sortingOrder = 4;
            var sleepZ = new GameObject("SleepZSprite");
            sleepZ.transform.SetParent(root.transform, false);
            var sleepZRenderer = sleepZ.AddComponent<SpriteRenderer>();
            sleepZRenderer.sharedMaterial = materials.Sprite;
            sleepZRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sleepZRenderer.receiveShadows = false;
            sleepZRenderer.sortingOrder = 5;
            var idle = LoadSwampSprites(document.toad.idle);
            var yawn = LoadSwampSprites(document.toad.yawn);
            var sleep = LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/" + document.toad.sleep);
            sleepZRenderer.sprite = LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/" + document.toad.sleepZ);
            var runtime = root.AddComponent<WofSwampToadRuntime>();
            runtime.Configure(toadRenderer, sleepZRenderer, idle, yawn, sleep, document.toad.idleFrameMs, document.toad.yawnFrameMs, document.layout.platformY);
            MarkDarrelDynamic(root);
            MarkDarrelDynamic(toad);
            MarkDarrelDynamic(sleepZ);
        }

        private static Sprite[] LoadSwampSprites(string[] paths)
        {
            var sprites = new Sprite[paths.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                sprites[index] = LoadRequiredAsset<Sprite>("Assets/WOF/Art/Generated/React/" + paths[index]);
            }
            return sprites;
        }

        private static void CreateSwampVillagers(Transform parent, WofSwampVillageDocument document, Material material)
        {
            var root = new GameObject("ReactSwampVillageVillagers");
            root.transform.SetParent(parent, false);
            var billboards = new WofVillagerBillboard[document.villagers.Length];
            for (var index = 0; index < document.villagers.Length; index++)
            {
                var record = document.villagers[index];
                if (record == null || string.IsNullOrWhiteSpace(record.id) || string.IsNullOrWhiteSpace(record.archiveFile) ||
                    string.IsNullOrWhiteSpace(record.displayName) || string.IsNullOrWhiteSpace(record.townId))
                {
                    throw new InvalidOperationException($"Invalid exact React swamp villager record at index {index}.");
                }
                var villager = new GameObject($"SwampVillager_{index:00}");
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
                billboard.Configure(record.id, record.archiveFile,
                    new Vector3(record.x, record.y + WofVillagerMath.AvatarGroundLift, record.z),
                    record.baseYaw, record.lookUpdateDesktopMs, record.lookUpdateMobileMs, record.hut,
                    visual.transform, renderer, record.displayName, record.townId);
                billboards[index] = billboard;
            }
            root.AddComponent<WofVillagerManager>().Configure(billboards);
        }

        private static Material GetSwampMossMaterial(int index, float alpha)
        {
            var color = SwampMossColors[index % SwampMossColors.Length];
            var suffix = Mathf.RoundToInt(alpha * 100f);
            return GetOrCreateDesertUnlit(
                $"SwampMoss_{color.TrimStart('#')}_{suffix}",
                SwampWithAlpha(HexColor(color), alpha),
                null,
                alpha < 0.999f);
        }

        private static Material GetOrCreateSwampGlowMaterial(string color)
        {
            var material = GetOrCreateDesertUnlit(
                $"SwampBulbGlow_{color.TrimStart('#')}",
                SwampWithAlpha(HexColor(color), 0.18f),
                null,
                true);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color SwampWithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Vector3 ToSwampVector(float[] values)
        {
            if (values == null || values.Length != 3) throw new InvalidDataException("Invalid swamp vector record.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ToSwampQuaternion(float[] values)
        {
            if (values == null || values.Length != 4) throw new InvalidDataException("Invalid swamp quaternion record.");
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        private static void CreateSwampBoxCollider(GameObject owner, Vector3 center, Vector3 size)
        {
            var collider = owner.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private sealed class SwampMaterialSet
        {
            public Material Terrain;
            public Material DeepWater;
            public Material Water;
            public Material Ripple;
            public Material Platform;
            public Material PlatformTop;
            public Material PlankA;
            public Material PlankB;
            public Material Walkway;
            public Material WalkwayPlankA;
            public Material WalkwayPlankB;
            public Material DarkWood;
            public Material WetWood;
            public Material Support;
            public Material HutStilt;
            public Material HutDeck;
            public Material HutDeckTop;
            public Material Door;
            public Material WindowFront;
            public Material WindowSide;
            public Material RoofBase;
            public Material Stump;
            public Material StumpTop;
            public Material ReedA;
            public Material ReedB;
            public Material ReedHead;
            public Material Rope;
            public Material BulbCord;
            public Material ToadShadow;
            public Material Sprite;
        }
    }
}
