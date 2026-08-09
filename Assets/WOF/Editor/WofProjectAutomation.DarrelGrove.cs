using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string DarrelArtRoot = "Assets/WOF/Art/Generated/React/DarrelGrove";
        private const string DarrelLayoutPath = DarrelArtRoot + "/runtime-layout.json";
        private const string DarrelRepeatingTextureRoot = DarrelArtRoot + "/Textures/Repeating";
        private const string DarrelClampedTextureRoot = DarrelArtRoot + "/Textures/Clamped";
        private const string DarrelGeometryRoot = GeometryRoot + "/DarrelGrove";

        private static void CreateDarrelGrove(Transform parent)
        {
            var layout = LoadDarrelLayout();
            var materials = CreateDarrelMaterials();
            var root = new GameObject("ReactDarrelSacredGarden");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofDarrelGroveLayout.WorldOrigin;

            CreateDarrelGroundAndBoundary(root.transform, materials);
            CreateDarrelHouseHillAndMoat(root.transform, materials);
            CreateDarrelChineseHut(root.transform, materials);
            CreateDarrelBackyardRiver(root.transform, materials, layout);
            CreateDarrelWaterfall(
                root.transform,
                materials,
                layout,
                out var fallWaterMaterial,
                out var foamMaterial,
                out var poolWaterMaterials,
                out var runnelWaterMaterials,
                out var waterfallRunnels,
                out var waterfallSprays,
                out var waterfallSpraySeeds);
            CreateDarrelReturnGate(root.transform, materials);
            CreateDarrelTrees(root.transform, materials, layout);
            CreateDarrelPetals(root.transform, materials, layout, out var fallingPetals, out var fallingSeeds);
            CreateDarrelFuji(root.transform, materials);
            var dragon = CreateDarrelDragon(root.transform, out var dragonLight);

            var runtime = root.AddComponent<WofDarrelGroveRuntime>();
            runtime.ConfigureGeneratedView(
                dragon,
                dragonLight,
                LoadDarrelDragonFrames("sleep", WofDarrelGroveLayout.SleepFrameCount),
                LoadDarrelDragonFrames("wake", WofDarrelGroveLayout.WakeFrameCount),
                LoadDarrelDragonFrames("idle", WofDarrelGroveLayout.IdleFrameCount),
                LoadDarrelDragonFrames("attack", WofDarrelGroveLayout.AttackFrameCount),
                fallingPetals,
                fallingSeeds,
                fallWaterMaterial,
                foamMaterial,
                poolWaterMaterials,
                runnelWaterMaterials,
                waterfallRunnels,
                waterfallSprays,
                waterfallSpraySeeds,
                LoadRequiredAsset<Font>("Assets/WOF/Art/Fonts/VT323-Regular.ttf"));
        }

        private static DarrelLayoutDocument LoadDarrelLayout()
        {
            var source = LoadRequiredAsset<TextAsset>(DarrelLayoutPath);
            var layout = JsonUtility.FromJson<DarrelLayoutDocument>(source.text);
            if (layout == null || layout.schemaVersion != 1 ||
                !Mathf.Approximately(layout.groveGroundY, WofDarrelGroveLayout.GroundY) ||
                !Mathf.Approximately(layout.groveHalfSize, WofDarrelGroveLayout.HalfSize) ||
                layout.backyardRiverSegments == null || layout.backyardRiverSegments.Length != 3 ||
                layout.backyardRiverStones == null || layout.backyardRiverStones.Length != 9 ||
                layout.waterfallHillStones == null || layout.waterfallHillStones.Length != 6 ||
                layout.bonsaiBranches == null || layout.bonsaiBranches.Length != 14 ||
                layout.legacyBonsaiBranches == null || layout.legacyBonsaiBranches.Length != 15 ||
                layout.fallenPetals == null || layout.fallenPetals.Length != 347 ||
                layout.fallingPetals == null || layout.fallingPetals.Length != 68)
            {
                throw new InvalidOperationException($"Invalid exact React Darrel grove layout at {DarrelLayoutPath}.");
            }
            return layout;
        }

        private static DarrelMaterialSet CreateDarrelMaterials()
        {
            Texture2D Repeat(string name) => LoadRequiredAsset<Texture2D>($"{DarrelRepeatingTextureRoot}/{name}.png");
            Texture2D Clamp(string name) => LoadRequiredAsset<Texture2D>($"{DarrelClampedTextureRoot}/{name}.png");
            var materials = new DarrelMaterialSet
            {
                Ground = GetOrCreateMaterial("DarrelGround", HexColor("#80a963"), texture: Repeat("ground"), textureScale: new Vector2(12f, 12f), roughness: 1f),
                HillGround = GetOrCreateMaterial("DarrelHillGround", HexColor("#87b66a"), texture: Repeat("ground"), textureScale: new Vector2(12f, 12f), roughness: 1f),
                HillTop = GetOrCreateMaterial("DarrelHillTop", HexColor("#9ccf78"), texture: Repeat("ground"), textureScale: new Vector2(12f, 12f), roughness: 1f),
                Bark = GetOrCreateMaterial("DarrelBark", HexColor("#556064"), texture: Repeat("bark"), textureScale: new Vector2(2f, 2f), roughness: 0.95f),
                Leaf = GetOrCreateMaterial("DarrelLeaf", HexColor("#3f7a35"), texture: Repeat("leaf"), textureScale: new Vector2(3.4f, 3.4f), roughness: 0.92f),
                Wall = GetOrCreateMaterial("DarrelWall", HexColor("#d9b77f"), texture: Repeat("wall"), textureScale: new Vector2(2f, 2f), roughness: 1f),
                Roof = GetOrCreateMaterial("DarrelRoof", HexColor("#7f1d1d"), texture: Repeat("roof"), textureScale: new Vector2(2.4f, 2.4f), roughness: 0.86f),
                RoofLight = GetOrCreateMaterial("DarrelRoofLight", HexColor("#991b1b"), texture: Repeat("roof"), textureScale: new Vector2(2.4f, 2.4f), roughness: 0.86f),
                RoofDark = GetOrCreateMaterial("DarrelRoofDark", HexColor("#3f1115"), texture: Repeat("roof"), textureScale: new Vector2(2.4f, 2.4f), roughness: 0.9f),
                Tatami = GetOrCreateMaterial("DarrelTatami", HexColor("#f0df92"), texture: Repeat("tatami"), textureScale: new Vector2(2f, 2f), roughness: 1f, doubleSided: true),
                Wood = GetOrCreateMaterial("DarrelWood", HexColor("#7b4b2c"), texture: Repeat("wood"), textureScale: new Vector2(2f, 2f), roughness: 0.9f),
                DarkWood = GetOrCreateMaterial("DarrelDarkWood", HexColor("#4a2d1c"), texture: Repeat("wood"), textureScale: new Vector2(2f, 2f), roughness: 0.95f),
                MoatWater = GetOrCreateMaterial("DarrelMoatWater", new Color(0.455f, 0.843f, 0.878f, 0.88f), texture: Repeat("water"), textureScale: new Vector2(4f, 4f), roughness: 0.5f, transparent: true, doubleSided: true),
                Water = GetOrCreateMaterial("DarrelWater", new Color(0.286f, 0.749f, 0.816f, 0.88f), texture: Repeat("water"), textureScale: new Vector2(4f, 4f), roughness: 0.58f, transparent: true, doubleSided: true),
                PoolWater = GetOrCreateMaterial("DarrelPoolWater", new Color(0.396f, 0.804f, 0.867f, 0.90f), texture: Repeat("water"), textureScale: new Vector2(2.6f, 1.7f), roughness: 0.48f, transparent: true, doubleSided: true),
                MouthWater = GetOrCreateMaterial("DarrelMouthWater", new Color(0.447f, 0.859f, 0.894f, 0.72f), texture: Repeat("water"), textureScale: new Vector2(2.6f, 1.7f), roughness: 0.5f, transparent: true, doubleSided: true),
                RunnelVisibleWater = GetOrCreateMaterial("DarrelRunnelVisibleWater", new Color(0.365f, 0.765f, 0.839f, 0.64f), texture: Repeat("water"), textureScale: new Vector2(1.6f, 1.15f), roughness: 0.55f, transparent: true, doubleSided: true),
                RunnelWater = GetOrCreateMaterial("DarrelRunnelWater", new Color(0.365f, 0.765f, 0.839f, 0.56f), texture: Repeat("water"), textureScale: new Vector2(1.6f, 1.15f), roughness: 0.55f, transparent: true, doubleSided: true),
                FeedWater = GetOrCreateMaterial("DarrelFeedWater", new Color(0.345f, 0.780f, 0.847f, 0.58f), texture: Repeat("water"), textureScale: new Vector2(1.6f, 1.15f), roughness: 0.52f, transparent: true, doubleSided: true),
                FallWater = GetOrCreateDarrelUnlitMaterial("DarrelFallWater", new Color(0.557f, 0.918f, 1f, 0.62f), Repeat("water"), true, true, new Vector2(1.15f, 2.8f)),
                Stone = GetOrCreateMaterial("DarrelStone", HexColor("#777f78"), texture: Repeat("stone"), textureScale: new Vector2(2f, 2f), roughness: 1f),
                Moss = GetOrCreateMaterial("DarrelMoss", new Color(0.435f, 0.722f, 0.353f, 0.92f), texture: Repeat("leaf"), textureScale: new Vector2(3.4f, 3.4f), roughness: 1f, transparent: true, doubleSided: true),
                PetalCarpet = GetOrCreateDarrelUnlitMaterial("DarrelPetalCarpet", new Color(1f, 0.851f, 0.91f, 0.68f), Repeat("petal-carpet"), true, true),
                Petal = GetOrCreateDarrelUnlitMaterial("DarrelPetal", new Color(1f, 0.882f, 0.925f, 1f), Clamp("petal"), true, true),
                Blossom = GetOrCreateDarrelUnlitMaterial("DarrelBlossom", Color.white, Clamp("blossom"), true, true),
                Fuji = GetOrCreateDarrelUnlitMaterial("DarrelFuji", new Color(1f, 1f, 1f, 0.96f), Clamp("fuji"), true, true),
                Lantern = GetOrCreateDarrelUnlitMaterial("DarrelLantern", new Color(1f, 0.706f, 0.329f, 0.95f), null, true, true),
                GateGlow = GetOrCreateDarrelUnlitMaterial("DarrelGateGlow", new Color(0.976f, 0.659f, 0.831f, 0.78f), null, true, true)
            };
            SetDarrelMetallic(materials.MoatWater, 0.04f);
            SetDarrelMetallic(materials.Water, 0.05f);
            SetDarrelMetallic(materials.PoolWater, 0.04f);
            return materials;
        }

        private static Material GetOrCreateDarrelUnlitMaterial(
            string name,
            Color color,
            Texture texture,
            bool transparent,
            bool doubleSided,
            Vector2? textureScale = null)
        {
            var material = GetOrCreateUnlitMaterial(name, color, transparent);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", textureScale ?? Vector2.one);
                if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", textureScale ?? Vector2.one);
            }
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.doubleSidedGI = doubleSided;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetDarrelMetallic(Material material, float metallic)
        {
            if (material != null && material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(material);
            }
        }

        private static void CreateDarrelGroundAndBoundary(Transform parent, DarrelMaterialSet materials)
        {
            CreatePrimitive("DarrelGroveGround", PrimitiveType.Cube, parent,
                new Vector3(0f, WofDarrelGroveLayout.GroundY - 1f, 0f),
                new Vector3(512f, 2f, 512f), materials.Ground);

            var boundary = new GameObject("DarrelGroveBoundary");
            boundary.transform.SetParent(parent, false);
            var half = WofDarrelGroveLayout.HalfSize;
            CreatePrimitive("NorthFence", PrimitiveType.Cube, boundary.transform, new Vector3(0f, 22f, -half + 8f), new Vector3(504f, 8f, 6f), materials.DarkWood);
            CreatePrimitive("SouthFence", PrimitiveType.Cube, boundary.transform, new Vector3(0f, 22f, half - 8f), new Vector3(504f, 8f, 6f), materials.DarkWood);
            CreatePrimitive("WestFence", PrimitiveType.Cube, boundary.transform, new Vector3(-half + 8f, 22f, 0f), new Vector3(6f, 8f, 504f), materials.DarkWood);
            CreatePrimitive("EastFence", PrimitiveType.Cube, boundary.transform, new Vector3(half - 8f, 22f, 0f), new Vector3(6f, 8f, 504f), materials.DarkWood);
        }

        private static void CreateDarrelHouseHillAndMoat(Transform parent, DarrelMaterialSet materials)
        {
            var root = new GameObject("DarrelHouseHillAndMoat");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, WofDarrelGroveLayout.GroundY, 0f);

            var hillMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/HouseHillFrustum.asset",
                () => CreateDarrelFrustumMesh(72f, 108f, WofDarrelGroveLayout.HutHillSurfaceOffset, 56));
            var hill = CreateMeshVisual("HillSlope", root.transform,
                new Vector3(0f, WofDarrelGroveLayout.HutHillSurfaceOffset * 0.5f, 0f), hillMesh, materials.HillGround);
            var hillCollider = hill.AddComponent<MeshCollider>();
            hillCollider.sharedMesh = hillMesh;

            CreateVisualPrimitive("HillTop", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, WofDarrelGroveLayout.HutHillSurfaceOffset + 0.08f, 0f),
                new Vector3(146f, 0.08f, 146f), materials.HillTop);

            var moatMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/MoatRing.asset",
                () => CreateDarrelRingMesh(84f, 116f, 96));
            CreateMeshVisual("MoatWater", root.transform, new Vector3(0f, 0.12f, 0f), moatMesh, materials.MoatWater);
            CreateDarrelTorus("InnerMoatStone", root.transform, new Vector3(0f, 0.48f, 0f), 84f, 1.35f, materials.Stone);
            CreateDarrelTorus("OuterMoatStone", root.transform, new Vector3(0f, 0.56f, 0f), 116f, 1.6f, materials.Stone);

            CreateDarrelBridge(root.transform, materials, "Front", -101f, 38f, 58f, new[] { -76f }, HexColor("#7a4a2b"));
            CreateDarrelBridge(root.transform, materials, "Back", 101f, 26f, 52f, new[] { 78f, 124f }, HexColor("#6b4228"));

            const int stepCount = 16;
            for (var index = 0; index < stepCount; index++)
            {
                var progress = (index + 1f) / stepCount;
                var height = WofDarrelGroveLayout.HutEntrySurfaceOffset * progress;
                var width = 34f - Mathf.Min(index, 6) * 1.2f;
                var material = GetOrCreateMaterial($"DarrelHillStep{index % 2}", HexColor(index % 2 == 0 ? "#8f968d" : "#77806f"), texture: materials.Stone.mainTexture, textureScale: new Vector2(2f, 2f), roughness: 1f);
                CreatePrimitive($"HillStep_{index:00}", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, height * 0.5f, -116f + index * 4.8f),
                    new Vector3(width, height, 9f), material);
            }
            foreach (var x in new[] { -54f, 54f })
            {
                var stone = CreateVisualPrimitive($"HillSideStone_{x}", PrimitiveType.Cube, root.transform,
                    new Vector3(x, WofDarrelGroveLayout.HutHillSurfaceOffset - 1.8f, -38f),
                    new Vector3(12f, 4f, 18f), materials.Stone);
                stone.transform.localRotation = Quaternion.Euler(0f, (x > 0f ? -0.2f : 0.2f) * Mathf.Rad2Deg, 0f);
            }
        }

        private static void CreateDarrelBridge(
            Transform parent,
            DarrelMaterialSet materials,
            string key,
            float z,
            float width,
            float depth,
            float[] railZ,
            Color deckColor)
        {
            var deckMaterial = GetOrCreateMaterial($"Darrel{key}BridgeDeck", deckColor, texture: materials.Wood.mainTexture, textureScale: new Vector2(2f, 2f), roughness: 0.92f);
            CreatePrimitive($"{key}BridgeDeck", PrimitiveType.Cube, parent, new Vector3(0f, 1.04f, z), new Vector3(width, 1.4f, depth), deckMaterial);
            foreach (var side in new[] { -1f, 1f })
            {
                CreateVisualPrimitive($"{key}BridgeRail_{side}", PrimitiveType.Cube, parent,
                    new Vector3(side * (width * 0.5f - 3f), 3.2f, z), new Vector3(2.2f, 4.2f, depth), materials.DarkWood);
            }
            foreach (var endZ in railZ)
            {
                CreateVisualPrimitive($"{key}BridgeEnd_{endZ}", PrimitiveType.Cube, parent,
                    new Vector3(0f, 2.25f, endZ), new Vector3(width + 4f, 2.1f, 2.2f), materials.DarkWood);
            }
        }

        private static void CreateDarrelChineseHut(Transform parent, DarrelMaterialSet materials)
        {
            const float width = 78f;
            const float depth = 62f;
            const float wallHeight = 28f;
            const float wallCenterY = 15f;
            const float doorWidth = 20f;
            const float foundationWidth = 98f;
            const float foundationDepth = 110f;
            const float foundationZ = -10f;
            const float foundationFrontSupportDepth = 34f;
            const float foundationRearDepth = 76f;
            const float foundationFrontZ = -65f;
            const float foundationFrontSupportZ = -48f;
            const float foundationRearZ = 10f;
            const float foundationOpeningHalfWidth = 22f;
            const float foundationFrontSupportWidth = 27f;
            const float foundationSideX = 35.5f;
            const float porchDepth = 30f;
            const float porchZ = -50f;
            const float porchTopY = 2.12f;
            const float porchHalfWidth = 23f;
            const float sideStairRun = 17f;
            const int sideStairCount = 4;
            var foundationCenterY = -WofDarrelGroveLayout.HutFoundationHeight * 0.5f;
            var foundationTrimHeight = Mathf.Min(0.7f, WofDarrelGroveLayout.HutFoundationHeight * 0.56f);
            var foundationTrimY = foundationCenterY + WofDarrelGroveLayout.HutFoundationHeight * 0.34f;

            var root = new GameObject("DarrelChineseHut");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, WofDarrelGroveLayout.HutBaseY, 0f);

            CreatePrimitive("FoundationRear", PrimitiveType.Cube, root.transform,
                new Vector3(0f, foundationCenterY, foundationRearZ),
                new Vector3(foundationWidth, WofDarrelGroveLayout.HutFoundationHeight, foundationRearDepth), materials.Stone);
            foreach (var side in new[] { -1f, 1f })
            {
                CreatePrimitive($"FoundationFrontSupport_{side}", PrimitiveType.Cube, root.transform,
                    new Vector3(side * foundationSideX, foundationCenterY, foundationFrontSupportZ),
                    new Vector3(foundationFrontSupportWidth, WofDarrelGroveLayout.HutFoundationHeight, foundationFrontSupportDepth), materials.Stone);
                CreateVisualPrimitive($"FoundationFrontTrim_{side}", PrimitiveType.Cube, root.transform,
                    new Vector3(side * foundationSideX, foundationTrimY, foundationFrontZ - 0.35f),
                    new Vector3(foundationFrontSupportWidth + 4f, foundationTrimHeight, 2.4f), materials.Stone);
            }
            foreach (var x in new[] { -42f, -28f, 28f, 42f })
            {
                CreateVisualPrimitive($"FoundationFrontStone_{x}", PrimitiveType.Cube, root.transform,
                    new Vector3(x, foundationCenterY - 0.2f, foundationFrontZ - 0.8f),
                    new Vector3(8f, WofDarrelGroveLayout.HutFoundationHeight * 0.72f, 1.2f), materials.Stone);
            }

            CreatePrimitive("HutFloor", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(width + 8f, 2.1f, depth + 8f), materials.Wood);
            CreatePrimitive("HutPorch", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.05f, porchZ), new Vector3(46f, 2.1f, porchDepth), materials.Wood);
            var stepWidth = sideStairRun / sideStairCount;
            for (var sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                var side = sideIndex == 0 ? -1f : 1f;
                for (var index = 0; index < sideStairCount; index++)
                {
                    var stepHeight = porchTopY * ((index + 1f) / sideStairCount);
                    var innerOffset = index * stepWidth + stepWidth * 0.5f;
                    var x = side * (porchHalfWidth + sideStairRun - innerOffset);
                    var step = CreatePrimitive($"SideStep_{sideIndex}_{index}", PrimitiveType.Cube, root.transform,
                        new Vector3(x, stepHeight * 0.5f, porchZ),
                        new Vector3(stepWidth + 0.18f, stepHeight, porchDepth - 2.4f), materials.Stone);
                    CreateVisualPrimitive("WoodCap", PrimitiveType.Cube, step.transform,
                        new Vector3(0f, 0.5f + 0.065f / Mathf.Max(0.001f, stepHeight), 0f),
                        new Vector3((stepWidth + 0.44f) / (stepWidth + 0.18f), 0.13f / stepHeight, (porchDepth - 1.98f) / (porchDepth - 2.4f)), materials.Wood);
                }
            }

            CreatePrimitive("WestWall", PrimitiveType.Cube, root.transform, new Vector3(-width * 0.5f, wallCenterY, 0f), new Vector3(2.2f, wallHeight, depth), materials.Wall);
            CreatePrimitive("EastWall", PrimitiveType.Cube, root.transform, new Vector3(width * 0.5f, wallCenterY, 0f), new Vector3(2.2f, wallHeight, depth), materials.Wall);
            CreatePrimitive("RearWall", PrimitiveType.Cube, root.transform, new Vector3(0f, wallCenterY, depth * 0.5f), new Vector3(width, wallHeight, 2.2f), materials.Wall);
            CreatePrimitive("FrontWallLeft", PrimitiveType.Cube, root.transform, new Vector3(-(doorWidth * 0.5f + 14f), wallCenterY, -depth * 0.5f), new Vector3(28f, wallHeight, 2.2f), materials.Wall);
            CreatePrimitive("FrontWallRight", PrimitiveType.Cube, root.transform, new Vector3(doorWidth * 0.5f + 14f, wallCenterY, -depth * 0.5f), new Vector3(28f, wallHeight, 2.2f), materials.Wall);
            foreach (var x in new[] { -34f, -14f, 14f, 34f })
            {
                CreateVisualPrimitive($"FrontPost_{x}", PrimitiveType.Cylinder, root.transform,
                    new Vector3(x, 15.5f, -35.2f), new Vector3(4f, 15.5f, 4f),
                    GetOrCreateMaterial("DarrelRedPost", HexColor("#8b1f24"), texture: materials.Wood.mainTexture, textureScale: new Vector2(2f, 2f), roughness: 0.8f));
            }

            CreateDarrelHutFurniture(root.transform, materials);
            CreateVisualPrimitive("RoofLower", PrimitiveType.Cube, root.transform, new Vector3(0f, 31.5f, 0f), new Vector3(94f, 4f, 78f), materials.Roof);
            CreateVisualPrimitive("RoofMiddle", PrimitiveType.Cube, root.transform, new Vector3(0f, 35.2f, 0f), new Vector3(70f, 5f, 54f), materials.RoofLight);
            CreateVisualPrimitive("RoofUpper", PrimitiveType.Cube, root.transform, new Vector3(0f, 38.5f, 0f), new Vector3(42f, 3.4f, 28f),
                GetOrCreateMaterial("DarrelRoofUpper", HexColor("#5c1117"), texture: materials.Roof.mainTexture, textureScale: new Vector2(2.4f, 2.4f), roughness: 0.9f));
            var westEdge = CreateVisualPrimitive("RoofWestEdge", PrimitiveType.Cube, root.transform, new Vector3(-49f, 30.4f, 0f), new Vector3(4f, 3.2f, 80f), materials.RoofDark);
            westEdge.transform.localRotation = Quaternion.Euler(0f, 0f, -0.17f * Mathf.Rad2Deg);
            var eastEdge = CreateVisualPrimitive("RoofEastEdge", PrimitiveType.Cube, root.transform, new Vector3(49f, 30.4f, 0f), new Vector3(4f, 3.2f, 80f), materials.RoofDark);
            eastEdge.transform.localRotation = Quaternion.Euler(0f, 0f, 0.17f * Mathf.Rad2Deg);
            var frontEdge = CreateVisualPrimitive("RoofFrontEdge", PrimitiveType.Cube, root.transform, new Vector3(0f, 30.4f, -41f), new Vector3(4f, 3.2f, 80f), materials.RoofDark);
            frontEdge.transform.localRotation = Quaternion.Euler(0f, 90f, -0.17f * Mathf.Rad2Deg);
            var rearEdge = CreateVisualPrimitive("RoofRearEdge", PrimitiveType.Cube, root.transform, new Vector3(0f, 30.4f, 41f), new Vector3(4f, 3.2f, 80f), materials.RoofDark);
            rearEdge.transform.localRotation = Quaternion.Euler(0f, 90f, 0.17f * Mathf.Rad2Deg);
        }

        private static void CreateDarrelHutFurniture(Transform parent, DarrelMaterialSet materials)
        {
            foreach (var x in new[] { -20f, 0f, 20f })
            {
                CreateDarrelQuad($"TatamiA_{x}", parent, new Vector3(x, 1.08f, -6f), new Vector2(18f, 28f), 0f, materials.Tatami, true);
                CreateDarrelQuad($"TatamiB_{x}", parent, new Vector3(x, 1.09f, 17f), new Vector2(16f, 28f), 90f, materials.Tatami, true);
            }
            var table = new GameObject("TeaTable");
            table.transform.SetParent(parent, false);
            table.transform.localPosition = new Vector3(0f, 2.4f, -4f);
            CreateVisualPrimitive("Top", PrimitiveType.Cube, table.transform, Vector3.zero, new Vector3(18f, 1.4f, 10f), materials.Wood);
            foreach (var x in new[] { -7f, 7f })
            foreach (var z in new[] { -3f, 3f })
            {
                CreateVisualPrimitive($"Leg_{x}_{z}", PrimitiveType.Cube, table.transform, new Vector3(x, -2.6f, z), new Vector3(1.4f, 4.2f, 1.4f), materials.DarkWood);
            }
            CreateVisualPrimitive("Teapot", PrimitiveType.Cylinder, table.transform, new Vector3(0f, 1.25f, 0f), new Vector3(4.4f, 0.55f, 4.4f), GetOrCreateMaterial("DarrelTeapot", HexColor("#d9a441"), roughness: 0.7f));
            foreach (var x in new[] { -16f, 16f })
            {
                CreateVisualPrimitive($"Cushion_{x}", PrimitiveType.Cube, parent, new Vector3(x, 1.45f, -5f), new Vector3(8f, 0.8f, 7f), GetOrCreateMaterial(x < 0f ? "DarrelRedCushion" : "DarrelBlueCushion", HexColor(x < 0f ? "#b91c1c" : "#1d4ed8"), roughness: 0.9f));
            }
            var shelf = new GameObject("Shelf");
            shelf.transform.SetParent(parent, false);
            shelf.transform.localPosition = new Vector3(-31.5f, 7f, 8f);
            foreach (var y in new[] { 0f, 5.5f, 11f })
            {
                CreateVisualPrimitive($"Shelf_{y}", PrimitiveType.Cube, shelf.transform, new Vector3(0f, y, 0f), new Vector3(2f, 1.2f, 26f), materials.DarkWood);
            }
            var jarZ = new[] { -9f, 0f, 9f };
            for (var index = 0; index < jarZ.Length; index++)
            {
                CreateVisualPrimitive($"Jar_{index}", PrimitiveType.Cylinder, shelf.transform, new Vector3(0.4f, 12.2f, jarZ[index]), new Vector3(3.8f, 1.7f, 3.8f), GetOrCreateMaterial($"DarrelJar{index % 2}", HexColor(index % 2 == 1 ? "#94a3b8" : "#d97706"), roughness: 0.8f));
            }
            CreateDarrelQuad("EastWallArt", parent, new Vector3(31.1f, 9.2f, 2f), new Vector2(14f, 18f), -90f, materials.Wall, false);
            CreateDarrelQuad("RearScroll", parent, new Vector3(0f, 9.5f, 28.8f), new Vector2(16f, 18f), 180f, GetOrCreateMaterial("DarrelRearScroll", HexColor("#fef3c7"), roughness: 1f, doubleSided: true), false);
            foreach (var x in new[] { -28f, 28f })
            {
                var lantern = new GameObject($"Lantern_{x}");
                lantern.transform.SetParent(parent, false);
                lantern.transform.localPosition = new Vector3(x, 11f, -18f);
                CreateVisualPrimitive("Glow", PrimitiveType.Sphere, lantern.transform, Vector3.zero, new Vector3(10.4f, 10.4f, 10.4f), materials.Lantern);
                CreateVisualPrimitive("Body", PrimitiveType.Cube, lantern.transform, Vector3.zero, new Vector3(3.8f, 4.5f, 3.8f), materials.Lantern);
                var light = lantern.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = HexColor("#ffb454");
                light.intensity = 3.2f;
                light.range = 30f;
            }
        }

        private static void CreateDarrelBackyardRiver(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelLayoutDocument layout)
        {
            var root = new GameObject("DarrelBackyardRiver");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, WofDarrelGroveLayout.GroundY + 0.08f, 0f);
            for (var index = 0; index < layout.backyardRiverSegments.Length; index++)
            {
                var segment = layout.backyardRiverSegments[index];
                CreateDarrelQuad($"River_{index}", root.transform,
                    new Vector3(segment.x, 0.05f, segment.z),
                    new Vector2(segment.width, segment.depth),
                    segment.rotation * Mathf.Rad2Deg,
                    materials.Water,
                    true);
            }
            for (var index = 0; index < layout.backyardRiverStones.Length; index++)
            {
                var stone = layout.backyardRiverStones[index];
                var visual = CreateVisualPrimitive($"RiverStone_{index}", PrimitiveType.Cube, root.transform,
                    new Vector3(stone.x, 0.42f, stone.z),
                    new Vector3(stone.width, stone.height, stone.depth), materials.Stone);
                visual.transform.localRotation = Quaternion.Euler(0f, stone.rotation * Mathf.Rad2Deg, 0f);
            }
            var bridge = new GameObject("BackyardBridge");
            bridge.transform.SetParent(root.transform, false);
            bridge.transform.localPosition = new Vector3(0f, 2.1f, 114f);
            CreatePrimitive("Deck", PrimitiveType.Cube, bridge.transform, Vector3.zero, new Vector3(58f, 2.4f, 9f), materials.Wood);
            foreach (var x in new[] { -25f, 25f })
            {
                CreateVisualPrimitive($"Rail_{x}", PrimitiveType.Cube, bridge.transform, new Vector3(x, 3.2f, 0f), new Vector3(2.2f, 5.2f, 11f), materials.DarkWood);
            }
        }

        private static void CreateDarrelWaterfall(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelLayoutDocument layout,
            out Material fallWaterMaterial,
            out Material foamMaterial,
            out Material[] poolWaterMaterials,
            out Material[] runnelWaterMaterials,
            out Transform[] waterfallRunnels,
            out Transform[] waterfallSprays,
            out WofDarrelWaterfallSpraySeed[] waterfallSpraySeeds)
        {
            fallWaterMaterial = materials.FallWater;
            foamMaterial = GetOrCreateDarrelUnlitMaterial(
                "DarrelWaterfallFoam",
                new Color(0.945f, 0.996f, 1f, 0.36f),
                null,
                true,
                true);
            poolWaterMaterials = new[] { materials.PoolWater, materials.MouthWater };
            runnelWaterMaterials = new[]
            {
                materials.RunnelVisibleWater,
                materials.RunnelWater,
                materials.FeedWater
            };
            var runtimeRunnels = new List<Transform>(3 + layout.waterfallRunnels.Length);
            var root = new GameObject("DarrelWaterfallHill");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, WofDarrelGroveLayout.GroundY, -145f);
            var hillMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/WaterfallHillFrustum.asset",
                () => CreateDarrelFrustumMesh(44f, 90f, 24.4f, 56));
            var hill = CreateMeshVisual("WaterfallHillSlope", root.transform, new Vector3(0f, 12.2f, -4f), hillMesh, materials.HillGround);
            var hillCollider = hill.AddComponent<MeshCollider>();
            hillCollider.sharedMesh = hillMesh;
            CreatePrimitive("UpperTerraceCollider", PrimitiveType.Cube, root.transform, new Vector3(0f, 24.35f, -12f), new Vector3(86f, 1.6f, 44f), materials.HillTop).GetComponent<MeshRenderer>().enabled = false;
            CreatePrimitive("MiddleTerraceCollider", PrimitiveType.Cube, root.transform, new Vector3(0f, 15.1f, 8f), new Vector3(114f, 1.3f, 30f), materials.HillTop).GetComponent<MeshRenderer>().enabled = false;
            CreatePrimitive("LowerTerraceCollider", PrimitiveType.Cube, root.transform, new Vector3(0f, 7.4f, 36f), new Vector3(48f, 1f, 24f), materials.HillTop).GetComponent<MeshRenderer>().enabled = false;
            CreateVisualPrimitive("UpperTerrace", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 24.72f, -12f), new Vector3(76f, 0.08f, 76f), materials.HillTop);
            CreateVisualPrimitive("WaterfallStoneFace", PrimitiveType.Cube, root.transform, new Vector3(0f, 13.7f, 22f), new Vector3(47f, 27f, 7f), materials.Stone);

            var fall = CreateDarrelQuad("Waterfall", root.transform, new Vector3(0f, 13.1f, 67f), new Vector2(27f, 63f), 0f, materials.FallWater, false);
            fall.transform.localRotation = Quaternion.Euler(-1.08f * Mathf.Rad2Deg, 0f, 0f);
            var foam = CreateDarrelQuad("WaterfallFoam", root.transform, new Vector3(0f, 13.25f, 67.24f), new Vector2(8f, 60f), 0f, foamMaterial, false);
            foam.transform.localRotation = Quaternion.Euler(-1.08f * Mathf.Rad2Deg, 0f, 0f);
            foreach (var side in new[] { -1f, 1f })
            {
                var edge = CreateDarrelQuad($"WaterfallEdge_{side}", root.transform, new Vector3(side * 14.8f, 12.75f, 67.32f), new Vector2(2.5f, 58f), 0f,
                    GetOrCreateDarrelUnlitMaterial("DarrelWaterfallEdge", new Color(0.18f, 0.561f, 0.659f, 0.25f), null, true, true), false);
                edge.transform.localRotation = Quaternion.Euler(-1.08f * Mathf.Rad2Deg, 0f, 0f);
            }
            for (var index = 0; index < 3; index++)
            {
                var cascade = CreateDarrelQuad($"VisibleCascade_{index}", root.transform,
                    new Vector3((index - 1) * 7f, 0.55f + index * 0.08f, 102f + index * 16f),
                    new Vector2(22f - index * 3f, 20f),
                    (index == 1 ? 0f : index == 0 ? 0.16f : -0.14f) * Mathf.Rad2Deg,
                    materials.RunnelVisibleWater, true);
                MarkDarrelDynamic(cascade);
                runtimeRunnels.Add(cascade.transform);
            }
            var poolMesh = GetOrCreateMeshAsset(DarrelGeometryRoot + "/WaterfallPool.asset", () => CreateDarrelDiskMesh(1f, 32));
            var pool = CreateMeshVisual("WaterfallPool", root.transform, new Vector3(0f, 0.34f, 94f), poolMesh, materials.PoolWater);
            pool.transform.localScale = new Vector3(52f, 1f, 32f);
            var poolOverlay = CreateMeshVisual(
                "WaterfallPoolHighlight",
                root.transform,
                new Vector3(0f, 0.38f, 94f),
                poolMesh,
                GetOrCreateDarrelUnlitMaterial(
                    "DarrelWaterfallPoolHighlight",
                    new Color(0.875f, 0.984f, 1f, 0.14f),
                    null,
                    true,
                    true));
            poolOverlay.transform.localScale = new Vector3(52f, 1f, 32f);

            for (var index = 0; index < layout.waterfallRunnels.Length; index++)
            {
                var runnel = layout.waterfallRunnels[index];
                var runnelObject = CreateDarrelQuad($"WaterfallRunnel_{index}", root.transform, new Vector3(runnel.x, 0.2f, runnel.z), new Vector2(runnel.width, runnel.depth), runnel.yaw * Mathf.Rad2Deg, materials.RunnelWater, true);
                MarkDarrelDynamic(runnelObject);
                runtimeRunnels.Add(runnelObject.transform);
            }
            CreateDarrelWaterPatches(root.transform, materials.FeedWater, "Feed", layout.waterfallRiverFeedChannels, 0.24f, 0.003f);
            CreateDarrelWaterPatches(root.transform, materials.MouthWater, "Mouth", layout.waterfallRiverMouths, 0.27f, 0.004f);
            for (var index = 0; index < layout.waterfallHillStones.Length; index++)
            {
                var stone = layout.waterfallHillStones[index];
                var visual = CreateVisualPrimitive($"WaterfallStone_{index}", PrimitiveType.Cube, root.transform, new Vector3(stone.x, stone.y, stone.z), new Vector3(stone.width, stone.height, stone.depth), materials.Stone);
                visual.transform.localRotation = Quaternion.Euler(0f, stone.yaw * Mathf.Rad2Deg, 0f);
            }
            for (var index = 0; index < layout.waterfallMossPads.Length; index++)
            {
                var moss = layout.waterfallMossPads[index];
                CreateDarrelQuad($"WaterfallMoss_{index}", root.transform, new Vector3(moss.x, moss.y, moss.z), new Vector2(moss.width, moss.depth), moss.yaw * Mathf.Rad2Deg, materials.Moss, true);
            }
            var sprayMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/WaterfallSpraySphere.asset",
                () => CreateUvSphereMesh(1f, 7, 4));
            waterfallSprays = new Transform[layout.waterfallSprayPuffs.Length];
            waterfallSpraySeeds = new WofDarrelWaterfallSpraySeed[layout.waterfallSprayPuffs.Length];
            for (var index = 0; index < layout.waterfallSprayPuffs.Length; index++)
            {
                var spray = layout.waterfallSprayPuffs[index];
                var visual = CreateMeshVisual($"WaterfallSpray_{index}", root.transform,
                    new Vector3(spray.x, spray.y, spray.z),
                    sprayMesh,
                    GetOrCreateDarrelUnlitMaterial("DarrelWaterfallSpray", new Color(0.918f, 1f, 1f, 0.32f), null, true, true));
                MarkDarrelDynamic(visual);
                visual.transform.localScale = new Vector3(spray.scale, spray.scale * 0.45f, spray.scale);
                waterfallSprays[index] = visual.transform;
                waterfallSpraySeeds[index] = new WofDarrelWaterfallSpraySeed
                {
                    baseY = spray.y,
                    baseScale = spray.scale
                };
            }
            waterfallRunnels = runtimeRunnels.ToArray();
            var lightObject = new GameObject("WaterfallLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 11f, 47f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HexColor("#a7f3ff");
            light.intensity = 1.7f;
            light.range = 62f;
        }

        private static void CreateDarrelWaterPatches(
            Transform parent,
            Material material,
            string key,
            DarrelWaterPatch[] patches,
            float baseY,
            float incrementY)
        {
            for (var index = 0; index < patches.Length; index++)
            {
                var patch = patches[index];
                CreateDarrelQuad($"Waterfall{key}_{index}", parent,
                    new Vector3(patch.x, baseY + index * incrementY, patch.z),
                    new Vector2(patch.width, patch.depth), patch.yaw * Mathf.Rad2Deg, material, true);
            }
        }

        private static void CreateDarrelReturnGate(Transform parent, DarrelMaterialSet materials)
        {
            var root = new GameObject("DarrelQuestReturnGate");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofDarrelGroveLayout.ReturnGateLocalPosition;
            foreach (var x in new[] { -8f, 8f })
            {
                CreateVisualPrimitive($"ReturnPost_{x}", PrimitiveType.Cylinder, root.transform, new Vector3(x, 8f, 0f), new Vector3(3.6f, 8f, 3.6f), GetOrCreateMaterial("DarrelReturnPost", HexColor("#7f1d1d"), roughness: 0.7f));
            }
            CreateVisualPrimitive("ReturnLintel", PrimitiveType.Cube, root.transform, new Vector3(0f, 16.5f, 0f), new Vector3(22f, 2.6f, 4f), GetOrCreateMaterial("DarrelReturnLintel", HexColor("#991b1b"), roughness: 0.8f));
            CreateDarrelTorus("ReturnPortal", root.transform, new Vector3(0f, 8f, 0f), 6.2f, 0.38f, materials.GateGlow);
            var lightObject = new GameObject("ReturnGateLight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 9f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = HexColor("#f9a8d4");
            light.intensity = 2.4f;
            light.range = 26f;
        }

        private static void CreateDarrelTrees(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelLayoutDocument layout)
        {
            var root = new GameObject("DarrelBonsaiTrees");
            root.transform.SetParent(parent, false);
            var modern = new[]
            {
                new DarrelTreePlacement(-176f, WofDarrelGroveLayout.GroundY, -176f, -2.35619449f, 2.45f),
                new DarrelTreePlacement(176f, WofDarrelGroveLayout.GroundY, -176f, 2.35619449f, 2.45f),
                new DarrelTreePlacement(-176f, WofDarrelGroveLayout.GroundY, 176f, -0.78539816f, 2.45f),
                new DarrelTreePlacement(176f, WofDarrelGroveLayout.GroundY, 176f, 0.78539816f, 2.45f)
            };
            for (var index = 0; index < modern.Length; index++)
            {
                CreateDarrelTree(root.transform, materials, layout, modern[index], index, false);
            }
            var legacy = new[]
            {
                new DarrelTreePlacement(-92f, WofDarrelGroveLayout.GroundY, -184f, 0.42f, 2.18f),
                new DarrelTreePlacement(92f, WofDarrelGroveLayout.GroundY, -184f, -0.42f, 2.18f),
                new DarrelTreePlacement(176f, WofDarrelGroveLayout.GroundY, 0f, -1.32f, 2.28f),
                new DarrelTreePlacement(0f, WofDarrelGroveLayout.GroundY, 176f, Mathf.PI + 0.18f, 2.35f),
                new DarrelTreePlacement(-176f, WofDarrelGroveLayout.GroundY, 0f, 1.42f, 2.28f)
            };
            for (var index = 0; index < legacy.Length; index++)
            {
                CreateDarrelTree(root.transform, materials, layout, legacy[index], index, true);
            }
        }

        private static void CreateDarrelTree(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelLayoutDocument layout,
            DarrelTreePlacement placement,
            int treeIndex,
            bool legacy)
        {
            var tree = new GameObject($"{(legacy ? "Legacy" : "Modern")}Bonsai_{treeIndex}");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = new Vector3(placement.x, placement.y, placement.z);
            tree.transform.localRotation = Quaternion.Euler(0f, placement.rotation * Mathf.Rad2Deg, 0f);
            tree.transform.localScale = Vector3.one * placement.scale;
            var branchMesh = GetOrCreateMeshAsset(DarrelGeometryRoot + "/BranchFrustum.asset", () => CreateDarrelFrustumMesh(0.72f, 1f, 1f, 8));
            var branches = legacy ? layout.legacyBonsaiBranches : layout.bonsaiBranches;
            for (var index = 0; index < branches.Length; index++)
            {
                var branch = branches[index];
                var start = ToVector3(branch.start);
                var end = ToVector3(branch.end);
                var direction = end - start;
                var visual = CreateMeshVisual($"Branch_{index:00}", tree.transform, (start + end) * 0.5f, branchMesh, materials.Bark);
                visual.transform.localRotation = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.FromToRotation(Vector3.up, direction.normalized)
                    : Quaternion.identity;
                visual.transform.localScale = new Vector3(branch.radius, direction.magnitude, branch.radius);
            }
            if (!legacy)
            {
                var dodeca = LoadRequiredAsset<Mesh>(BushMeshPath);
                for (var index = 0; index < layout.bonsaiCanopyPads.Length; index++)
                {
                    var pad = layout.bonsaiCanopyPads[index];
                    var canopy = CreateMeshVisual($"Canopy_{index:00}", tree.transform, ToVector3(pad.position), dodeca, materials.Leaf);
                    canopy.transform.localRotation = Quaternion.Euler(0f, pad.rotation * Mathf.Rad2Deg, 0f);
                    canopy.transform.localScale = ToVector3(pad.scale);
                }
            }
            var baseMesh = GetOrCreateMeshAsset(DarrelGeometryRoot + "/TreeBaseFrustum.asset", () => CreateDarrelFrustumMesh(8f, 10f, 1.6f, 8));
            CreateMeshVisual("TreeBase", tree.transform, new Vector3(0f, 0.8f, 0f), baseMesh, materials.Bark);
            var clusters = legacy ? layout.legacyBonsaiBlossomClusters : layout.bonsaiBlossomClusters;
            for (var index = 0; index < clusters.Length; index++)
            {
                CreateDarrelBlossomCluster(tree.transform, materials, clusters[index], index);
            }
        }

        private static void CreateDarrelBlossomCluster(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelBlossomCluster cluster,
            int clusterIndex)
        {
            var sprite = LoadRequiredAsset<Sprite>(DarrelClampedTextureRoot + "/blossom.png");
            var root = new GameObject($"BlossomCluster_{clusterIndex:00}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = ToVector3(cluster.position);
            for (var index = 0; index < cluster.count; index++)
            {
                var angle = index * 2.399f;
                var radiusX = 1.2f + index % 3 * 0.8f;
                var radiusZ = 1.2f + index % 2 * 0.7f;
                var size = cluster.size * (0.72f + index % 3 * 0.13f);
                var blossom = new GameObject($"Blossom_{index:00}");
                blossom.transform.SetParent(root.transform, false);
                blossom.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radiusX,
                    (index % 4 - 1.5f) * 1.15f,
                    Mathf.Sin(angle) * radiusZ);
                blossom.transform.localScale = new Vector3(
                    size / sprite.bounds.size.x,
                    size / sprite.bounds.size.y,
                    1f);
                var renderer = blossom.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = materials.Blossom;
                renderer.sortingOrder = 4;
            }
        }

        private static void CreateDarrelPetals(
            Transform parent,
            DarrelMaterialSet materials,
            DarrelLayoutDocument layout,
            out Transform[] fallingPetals,
            out WofDarrelFallingPetalSeed[] fallingSeeds)
        {
            var root = new GameObject("DarrelPetals");
            root.transform.SetParent(parent, false);
            for (var index = 0; index < layout.petalDriftPatches.Length; index++)
            {
                var patch = layout.petalDriftPatches[index];
                CreateDarrelQuad($"PetalDrift_{index}", root.transform,
                    new Vector3(patch.x, WofDarrelGroveLayout.GroundY + 0.105f + index * 0.002f, patch.z),
                    new Vector2(patch.width, patch.depth), patch.yaw * Mathf.Rad2Deg, materials.PetalCarpet, true);
            }

            var fallenMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/FallenPetals.asset",
                () => CreateDarrelFallenPetalMesh(layout.fallenPetals));
            CreateMeshVisual("FallenPetalField", root.transform, Vector3.zero, fallenMesh, materials.Petal);

            var sprite = LoadRequiredAsset<Sprite>(DarrelClampedTextureRoot + "/petal.png");
            fallingPetals = new Transform[layout.fallingPetals.Length];
            fallingSeeds = new WofDarrelFallingPetalSeed[layout.fallingPetals.Length];
            for (var index = 0; index < layout.fallingPetals.Length; index++)
            {
                var source = layout.fallingPetals[index];
                var petal = new GameObject($"FallingPetal_{index:00}");
                petal.transform.SetParent(root.transform, false);
                var renderer = petal.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = materials.Petal;
                renderer.sortingOrder = 4;
                fallingPetals[index] = petal.transform;
                fallingSeeds[index] = new WofDarrelFallingPetalSeed
                {
                    x = source.x,
                    z = source.z,
                    phase = source.phase,
                    speed = source.speed,
                    sway = source.sway,
                    drift = source.drift,
                    scale = source.scale,
                    spin = source.spin
                };
            }

            foreach (var x in new[] { -118f, -74f, 72f, 126f })
            {
                var index = x == -118f ? 0 : x == -74f ? 1 : x == 72f ? 2 : 3;
                var cluster = new DarrelBlossomCluster
                {
                    position = new[] { x, WofDarrelGroveLayout.GroundY + 2.4f, 64f + Mathf.Sin(index) * 42f },
                    size = 3.6f,
                    count = 4
                };
                CreateDarrelBlossomCluster(root.transform, materials, cluster, 100 + index);
            }
        }

        private static void CreateDarrelFuji(Transform parent, DarrelMaterialSet materials)
        {
            CreateDarrelQuad("DarrelMountFuji", parent,
                new Vector3(0f, WofDarrelGroveLayout.GroundY + 146f, -WofDarrelGroveLayout.HalfSize - 34f),
                new Vector2(560f, 310f), 0f, materials.Fuji, false);
        }

        private static SpriteRenderer CreateDarrelDragon(Transform parent, out Light dragonLight)
        {
            var dragon = new GameObject("DarrelSpiritDragon");
            dragon.transform.SetParent(parent, false);
            dragon.transform.localPosition = WofDarrelGroveLayout.DragonLocalPosition;
            var renderer = dragon.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadDarrelDragonFrames("sleep", WofDarrelGroveLayout.SleepFrameCount)[0];
            renderer.sortingOrder = 12;
            renderer.color = new Color(0.796f, 0.835f, 0.882f, 0.78f);
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(43f / size.x, 27f / size.y, 1f);
            var lightObject = new GameObject("DragonLight");
            lightObject.transform.SetParent(dragon.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3f / renderer.transform.localScale.y, 0f);
            dragonLight = lightObject.AddComponent<Light>();
            dragonLight.type = LightType.Point;
            dragonLight.color = HexColor("#bfdbfe");
            dragonLight.intensity = 1.2f;
            dragonLight.range = 44f;
            return renderer;
        }

        private static Sprite[] LoadDarrelDragonFrames(string mode, int expectedCount)
        {
            var frames = LoadSprites(DarrelArtRoot + "/Dragon", mode + "_");
            if (frames.Length != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} exact {mode} dragon frames; found {frames.Length}.");
            }
            return frames;
        }

        private static GameObject CreateDarrelQuad(
            string name,
            Transform parent,
            Vector3 position,
            Vector2 size,
            float yawDegrees,
            Material material,
            bool horizontal)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localRotation = horizontal
                ? Quaternion.Euler(90f, yawDegrees, 0f)
                : Quaternion.Euler(0f, yawDegrees, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            MarkStatic(quad);
            return quad;
        }

        private static void MarkDarrelDynamic(GameObject target)
        {
            target.isStatic = false;
            GameObjectUtility.SetStaticEditorFlags(target, (StaticEditorFlags)0);
        }

        private static void CreateDarrelTorus(
            string name,
            Transform parent,
            Vector3 position,
            float majorRadius,
            float minorRadius,
            Material material)
        {
            var safeName = name.Replace(" ", string.Empty);
            var mesh = GetOrCreateMeshAsset(
                $"{DarrelGeometryRoot}/{safeName}.asset",
                () => CreateDarrelTorusMesh(majorRadius, minorRadius, 96, 8));
            CreateMeshVisual(name, parent, position, mesh, material);
        }

        private static Mesh CreateDarrelFrustumMesh(float topRadius, float bottomRadius, float height, int segments)
        {
            var vertices = new List<Vector3>((segments + 1) * 2 + 2);
            var uv = new List<Vector2>((segments + 1) * 2 + 2);
            var triangles = new List<int>(segments * 12);
            var halfHeight = height * 0.5f;
            for (var index = 0; index <= segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * bottomRadius + Vector3.down * halfHeight);
                vertices.Add(direction * topRadius + Vector3.up * halfHeight);
                var u = index / (float)segments;
                uv.Add(new Vector2(u, 0f));
                uv.Add(new Vector2(u, 1f));
                if (index >= segments)
                {
                    continue;
                }
                var current = index * 2;
                triangles.Add(current);
                triangles.Add(current + 1);
                triangles.Add(current + 2);
                triangles.Add(current + 2);
                triangles.Add(current + 1);
                triangles.Add(current + 3);
            }
            var bottomCenter = vertices.Count;
            vertices.Add(Vector3.down * halfHeight);
            uv.Add(new Vector2(0.5f, 0.5f));
            var topCenter = vertices.Count;
            vertices.Add(Vector3.up * halfHeight);
            uv.Add(new Vector2(0.5f, 0.5f));
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) * 2;
                var current = index * 2;
                triangles.Add(bottomCenter);
                triangles.Add(next);
                triangles.Add(current);
                triangles.Add(topCenter);
                triangles.Add(current + 1);
                triangles.Add(next + 1);
            }
            return BuildDarrelMesh("DarrelFrustum", vertices, uv, triangles);
        }

        private static Mesh CreateDarrelRingMesh(float innerRadius, float outerRadius, int segments)
        {
            var vertices = new List<Vector3>((segments + 1) * 2);
            var uv = new List<Vector2>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);
            for (var index = 0; index <= segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * innerRadius);
                vertices.Add(direction * outerRadius);
                var u = index / (float)segments;
                uv.Add(new Vector2(u, 0f));
                uv.Add(new Vector2(u, 1f));
                if (index >= segments)
                {
                    continue;
                }
                var current = index * 2;
                triangles.Add(current);
                triangles.Add(current + 1);
                triangles.Add(current + 2);
                triangles.Add(current + 2);
                triangles.Add(current + 1);
                triangles.Add(current + 3);
            }
            return BuildDarrelMesh("DarrelRing", vertices, uv, triangles);
        }

        private static Mesh CreateDarrelDiskMesh(float radius, int segments)
        {
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var uv = new List<Vector2>(segments + 1) { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>(segments * 3);
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                uv.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (var index = 0; index < segments; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add((index + 1) % segments + 1);
            }
            return BuildDarrelMesh("DarrelDisk", vertices, uv, triangles);
        }

        private static Mesh CreateDarrelTorusMesh(
            float majorRadius,
            float minorRadius,
            int majorSegments,
            int minorSegments)
        {
            var vertices = new List<Vector3>((majorSegments + 1) * (minorSegments + 1));
            var uv = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(majorSegments * minorSegments * 6);
            for (var major = 0; major <= majorSegments; major++)
            {
                var majorAngle = major / (float)majorSegments * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(majorAngle), 0f, Mathf.Sin(majorAngle));
                for (var minor = 0; minor <= minorSegments; minor++)
                {
                    var minorAngle = minor / (float)minorSegments * Mathf.PI * 2f;
                    vertices.Add(radial * (majorRadius + Mathf.Cos(minorAngle) * minorRadius) + Vector3.up * (Mathf.Sin(minorAngle) * minorRadius));
                    uv.Add(new Vector2(major / (float)majorSegments, minor / (float)minorSegments));
                }
            }
            var stride = minorSegments + 1;
            for (var major = 0; major < majorSegments; major++)
            for (var minor = 0; minor < minorSegments; minor++)
            {
                var current = major * stride + minor;
                triangles.Add(current);
                triangles.Add(current + stride);
                triangles.Add(current + 1);
                triangles.Add(current + 1);
                triangles.Add(current + stride);
                triangles.Add(current + stride + 1);
            }
            return BuildDarrelMesh("DarrelTorus", vertices, uv, triangles);
        }

        private static Mesh CreateDarrelFallenPetalMesh(DarrelFallenPetal[] petals)
        {
            var vertices = new List<Vector3>(petals.Length * 4);
            var uv = new List<Vector2>(petals.Length * 4);
            var triangles = new List<int>(petals.Length * 6);
            for (var index = 0; index < petals.Length; index++)
            {
                var petal = petals[index];
                var right = new Vector3(Mathf.Cos(petal.yaw), 0f, -Mathf.Sin(petal.yaw)) * (petal.sx * 0.5f);
                var forward = new Vector3(Mathf.Sin(petal.yaw), 0f, Mathf.Cos(petal.yaw)) * (petal.sz * 0.5f);
                var center = new Vector3(petal.x, petal.y, petal.z);
                var start = vertices.Count;
                vertices.Add(center - right - forward);
                vertices.Add(center + right - forward);
                vertices.Add(center + right + forward);
                vertices.Add(center - right + forward);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
            return BuildDarrelMesh("FallenPetals", vertices, uv, triangles);
        }

        private static Mesh BuildDarrelMesh(
            string name,
            List<Vector3> vertices,
            List<Vector2> uv,
            List<int> triangles)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToVector3(float[] value)
        {
            if (value == null || value.Length != 3)
            {
                throw new InvalidOperationException("Darrel grove vector must contain exactly three values.");
            }
            return new Vector3(value[0], value[1], value[2]);
        }

        private readonly struct DarrelTreePlacement
        {
            public DarrelTreePlacement(float x, float y, float z, float rotation, float scale)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.rotation = rotation;
                this.scale = scale;
            }

            public readonly float x;
            public readonly float y;
            public readonly float z;
            public readonly float rotation;
            public readonly float scale;
        }

        private sealed class DarrelMaterialSet
        {
            public Material Ground;
            public Material HillGround;
            public Material HillTop;
            public Material Bark;
            public Material Leaf;
            public Material Wall;
            public Material Roof;
            public Material RoofLight;
            public Material RoofDark;
            public Material Tatami;
            public Material Wood;
            public Material DarkWood;
            public Material MoatWater;
            public Material Water;
            public Material PoolWater;
            public Material MouthWater;
            public Material RunnelVisibleWater;
            public Material RunnelWater;
            public Material FeedWater;
            public Material FallWater;
            public Material Stone;
            public Material Moss;
            public Material PetalCarpet;
            public Material Petal;
            public Material Blossom;
            public Material Fuji;
            public Material Lantern;
            public Material GateGlow;
        }

        [Serializable]
        private sealed class DarrelLayoutDocument
        {
            public int schemaVersion;
            public float groveGroundY;
            public float groveHalfSize;
            public DarrelRiverSegment[] backyardRiverSegments;
            public DarrelRiverStone[] backyardRiverStones;
            public DarrelWaterfallStone[] waterfallHillStones;
            public DarrelWaterfallMoss[] waterfallMossPads;
            public DarrelWaterPatch[] waterfallRiverFeedChannels;
            public DarrelWaterPatch[] waterfallRiverMouths;
            public DarrelWaterfallSpray[] waterfallSprayPuffs;
            public DarrelWaterPatch[] waterfallRunnels;
            public DarrelPetalPatch[] petalDriftPatches;
            public DarrelBranch[] bonsaiBranches;
            public DarrelCanopy[] bonsaiCanopyPads;
            public DarrelBlossomCluster[] bonsaiBlossomClusters;
            public DarrelBranch[] legacyBonsaiBranches;
            public DarrelBlossomCluster[] legacyBonsaiBlossomClusters;
            public DarrelFallenPetal[] fallenPetals;
            public DarrelFallingPetal[] fallingPetals;
        }

        [Serializable] private sealed class DarrelRiverSegment { public float x; public float z; public float width; public float depth; public float rotation; }
        [Serializable] private sealed class DarrelRiverStone { public float x; public float z; public float rotation; public float width; public float height; public float depth; }
        [Serializable] private sealed class DarrelWaterfallStone { public float x; public float y; public float z; public float width; public float height; public float depth; public float yaw; }
        [Serializable] private sealed class DarrelWaterfallMoss { public float x; public float y; public float z; public float width; public float depth; public float yaw; }
        [Serializable] private sealed class DarrelWaterPatch { public float x; public float z; public float width; public float depth; public float yaw; }
        [Serializable] private sealed class DarrelWaterfallSpray { public float x; public float y; public float z; public float scale; }
        [Serializable] private sealed class DarrelPetalPatch { public float x; public float z; public float width; public float depth; public float yaw; }
        [Serializable] private sealed class DarrelBranch { public float[] start; public float[] end; public float radius; }
        [Serializable] private sealed class DarrelCanopy { public float[] position; public float[] scale; public float rotation; }
        [Serializable] private sealed class DarrelBlossomCluster { public float[] position; public float size; public int count; }
        [Serializable] private sealed class DarrelFallenPetal { public float x; public float z; public float y; public float yaw; public float sx; public float sz; }
        [Serializable] private sealed class DarrelFallingPetal { public float x; public float z; public float phase; public float speed; public float sway; public float drift; public float scale; public float spin; }
    }
}
