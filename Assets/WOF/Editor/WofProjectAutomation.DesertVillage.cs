using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string DesertArtRoot = "Assets/WOF/Art/Generated/React/DesertVillage";
        private const string DesertLayoutPath = DesertArtRoot + "/runtime-layout.json";
        private const string DesertTextureRoot = DesertArtRoot + "/Textures";
        private const string DesertGeometryRoot = GeometryRoot + "/DesertVillage";

        private static void CreateDesertVillage(Transform parent, Material villagerMaterial)
        {
            var document = LoadDesertVillageDocument();
            var materials = CreateDesertMaterials();
            var root = new GameObject("ReactSurvivalDesertVillage_4_-4");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofDesertVillageLayout.WorldOrigin;

            CreateDesertSurface(root.transform, document, materials);
            CreateDesertWallsAndGates(root.transform, document, materials);
            CreateDesertBuildings(root.transform, document, materials);
            CreateDesertWell(root.transform, document.baseHeight, materials);
            CreateDesertDressing(root.transform, document, materials);
            CreateDesertMarketStalls(root.transform, document, materials);
            CreateDesertPalms(root.transform, document, materials);
            CreateDesertVillagers(parent, document, villagerMaterial);
        }

        private static WofDesertVillageDocument LoadDesertVillageDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(DesertLayoutPath);
            var document = JsonUtility.FromJson<WofDesertVillageDocument>(source.text);
            if (document == null || document.schemaVersion != 1 ||
                document.chunk == null || document.chunk.cx != WofDesertVillageLayout.ChunkX ||
                document.chunk.cz != WofDesertVillageLayout.ChunkZ ||
                !string.Equals(document.chunk.biome, "desert", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.villageKind, "desert", StringComparison.Ordinal) ||
                !Mathf.Approximately(document.baseHeight, WofDesertVillageLayout.ReactBaseHeight) ||
                !WofDesertVillageLayout.HasExactCounts(document.counts) ||
                document.layout == null || document.layout.buildings?.Length != 55 ||
                document.layout.wallSegments?.Length != 52 || document.layout.marketStalls?.Length != 10 ||
                document.layout.palms?.Length != 22 || document.layout.ladders?.Length != 37 ||
                document.layout.fences?.Length != 41 || document.layout.clothesLines?.Length != 15 ||
                document.layout.streetProps?.Length != 94 || document.villagers?.Length != 55 ||
                !IsValidDesertMesh(document.padGeometry) || document.surfaceGeometries == null ||
                !IsValidDesertMesh(document.surfaceGeometries.northSouthRoad) ||
                !IsValidDesertMesh(document.surfaceGeometries.eastWestRoad) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalRoadA) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalRoadB) ||
                !IsValidDesertMesh(document.surfaceGeometries.northSouthLeft) ||
                !IsValidDesertMesh(document.surfaceGeometries.northSouthRight) ||
                !IsValidDesertMesh(document.surfaceGeometries.eastWestLeft) ||
                !IsValidDesertMesh(document.surfaceGeometries.eastWestRight) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalALeft) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalARight) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalBLeft) ||
                !IsValidDesertMesh(document.surfaceGeometries.diagonalBRight))
            {
                throw new InvalidOperationException($"Invalid exact React desert village layout at {DesertLayoutPath}.");
            }
            return document;
        }

        private static bool IsValidDesertMesh(WofSerializedMeshRecord record)
        {
            return record != null && record.vertexCount > 0 &&
                   record.positions?.Length == record.vertexCount * 3 &&
                   record.normals?.Length == record.vertexCount * 3 &&
                   record.uvs?.Length == record.vertexCount * 2 &&
                   record.indices != null && record.indices.Length > 0 &&
                   (record.colors == null || record.colors.Length == 0 || record.colors.Length == record.vertexCount * 3);
        }

        private static DesertMaterialSet CreateDesertMaterials()
        {
            var sandTexture = LoadRequiredAsset<Texture2D>($"{DesertTextureRoot}/desert-sand.png");
            var adobeTexture = LoadRequiredAsset<Texture2D>($"{DesertTextureRoot}/desert-adobe-wall.png");
            return new DesertMaterialSet
            {
                Sand = GetOrCreateDesertUnlit("DesertSand", Color.white, sandTexture, false, new Vector2(8f, 8f)),
                SandOverlay = GetOrCreateDesertUnlit("DesertSandOverlay", new Color(0.816f, 0.631f, 0.361f, 0.58f), null, true),
                CenterSand = GetOrCreateDesertUnlit("DesertCenterSand", HexColor("#d7a15c"), sandTexture, false, new Vector2(8f, 8f)),
                RingSandA = GetOrCreateDesertUnlit("DesertRingSandA", HexColor("#cb8f4c"), sandTexture, false, new Vector2(8f, 8f)),
                RingSandB = GetOrCreateDesertUnlit("DesertRingSandB", HexColor("#c28749"), sandTexture, false, new Vector2(8f, 8f)),
                Road = GetOrCreateDesertUnlit("DesertRoad", HexColor("#d49f5d"), sandTexture, false, new Vector2(8f, 8f)),
                DiagonalRoad = GetOrCreateDesertUnlit("DesertDiagonalRoad", new Color(0.788f, 0.533f, 0.294f, 0.78f), sandTexture, true, new Vector2(8f, 8f)),
                DarkRing66 = GetOrCreateDesertUnlit("DesertDarkRing66", new Color(0.294f, 0.188f, 0.125f, 0.66f), null, true),
                DarkRing68 = GetOrCreateDesertUnlit("DesertDarkRing68", new Color(0.294f, 0.188f, 0.125f, 0.68f), null, true),
                DarkRing64 = GetOrCreateDesertUnlit("DesertDarkRing64", new Color(0.294f, 0.188f, 0.125f, 0.64f), null, true),
                DarkRing60 = GetOrCreateDesertUnlit("DesertDarkRing60", new Color(0.294f, 0.188f, 0.125f, 0.60f), null, true),
                Sidewalk76 = GetOrCreateDesertUnlit("DesertSidewalk76", new Color(0.247f, 0.157f, 0.102f, 0.76f), null, true),
                Sidewalk62 = GetOrCreateDesertUnlit("DesertSidewalk62", new Color(0.247f, 0.157f, 0.102f, 0.62f), null, true),
                Adobe = GetOrCreateDesertUnlit("DesertAdobe", HexColor("#b68145"), adobeTexture, false, new Vector2(1.85f, 1.85f)),
                AdobeGate = GetOrCreateDesertUnlit("DesertAdobeGate", Color.white, adobeTexture, false, new Vector2(1.85f, 1.85f)),
                BuildingOutline = GetOrCreateDesertUnlit(
                    "DesertBuildingOutline",
                    HexColor("#160d08"),
                    null,
                    false,
                    null,
                    CullMode.Front),
                BuildingTrim = GetOrCreateDesertUnlit("DesertBuildingTrim", HexColor("#1c1009"), null, false),
                BuildingRoof = GetOrCreateDesertUnlit("DesertBuildingRoof", HexColor("#6f3e22"), null, false),
                BuildingDoor = GetOrCreateDesertUnlit("DesertBuildingDoor", HexColor("#3b2414"), null, false),
                BuildingWindow = GetOrCreateDesertUnlit("DesertBuildingWindow", HexColor("#6ac7d6"), null, false),
                BuildingFloor = GetOrCreateDesertUnlit("DesertBuildingFloor", HexColor("#70401f"), null, false),
                Dome = GetOrCreateDesertUnlit("DesertBuildingDome", HexColor("#b88345"), null, false),
                DarkWood = GetOrCreateDesertUnlit("DesertDarkWood", HexColor("#2d1b10"), null, false),
                Wood = GetOrCreateDesertUnlit("DesertWood", HexColor("#442917"), null, false),
                DeepWood = GetOrCreateDesertUnlit("DesertDeepWood", HexColor("#2f1d12"), null, false),
                Rope = GetOrCreateDesertUnlit("DesertRope", HexColor("#4a2d18"), null, false),
                WellStone = GetOrCreateDesertUnlit("DesertWellStone", HexColor("#9a6b3e"), null, false),
                WellWater = GetOrCreateDesertUnlit("DesertWellWater", new Color(0.227f, 0.627f, 0.722f, 0.86f), null, true),
                WellRim = GetOrCreateDesertUnlit("DesertWellRim", HexColor("#d5aa64"), null, false),
                GateTrim = GetOrCreateDesertUnlit("DesertGateTrim", HexColor("#e1bd78"), null, false),
                PalmTrunk = GetOrCreateDesertUnlit("DesertPalmTrunk", HexColor("#6b3f20"), null, false),
                PalmEdge = GetOrCreateDesertUnlit("DesertPalmEdge", new Color(0.027f, 0.118f, 0.063f, 0.42f), null, true),
                PalmLeafA = GetOrCreateDesertUnlit("DesertPalmLeafA", HexColor("#2f7a3f"), null, false),
                PalmLeafB = GetOrCreateDesertUnlit("DesertPalmLeafB", HexColor("#3e8f48"), null, false)
            };
        }

        private static Material GetOrCreateDesertUnlit(
            string name,
            Color color,
            Texture texture,
            bool transparent,
            Vector2? textureScale = null,
            CullMode cullMode = CullMode.Off)
        {
            var material = GetOrCreateUnlitMaterial(name, color, transparent);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", textureScale ?? Vector2.one);
                if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", textureScale ?? Vector2.one);
            }
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)cullMode);
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateDesertSurface(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertVillageSurface");
            root.transform.SetParent(parent, false);
            var padMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/VillagePad.asset",
                () => CreateDesertSerializedMesh("DesertVillagePad", document.padGeometry));
            var pad = CreateMeshVisual("ExactVillagePad", root.transform, Vector3.zero, padMesh, materials.Sand);
            pad.AddComponent<MeshCollider>().sharedMesh = padMesh;
            var overlay = CreateMeshVisual("ExactVillagePadOverlay", root.transform, Vector3.zero, padMesh, materials.SandOverlay);
            overlay.GetComponent<MeshRenderer>().sortingOrder = 1;

            CreateDesertDisk("CenterPlaza", root.transform, 66f, 36, document.baseHeight + 0.08f, materials.CenterSand);
            CreateDesertRing("CenterPlazaEdge", root.transform, 67f, 74f, 36, document.baseHeight + 0.16f, materials.DarkRing66);
            CreateDesertRing("MiddleRoad", root.transform, 118f, 128f, 56, document.baseHeight + 0.10f, materials.RingSandA);
            CreateDesertRing("MiddleRoadInner", root.transform, 110f, 115f, 56, document.baseHeight + 0.17f, materials.DarkRing68);
            CreateDesertRing("MiddleRoadOuter", root.transform, 131f, 137f, 56, document.baseHeight + 0.18f, materials.DarkRing64);
            CreateDesertRing("OuterRoad", root.transform, 188f, 198f, 72, document.baseHeight + 0.11f, materials.RingSandB);
            CreateDesertRing("OuterRoadInner", root.transform, 180f, 184f, 72, document.baseHeight + 0.19f, materials.DarkRing64);
            CreateDesertRing("OuterRoadOuter", root.transform, 202f, 207f, 72, document.baseHeight + 0.20f, materials.DarkRing60);

            CreateDesertSurfaceMesh(root.transform, "NorthSouthRoad", document.surfaceGeometries.northSouthRoad, materials.Road);
            CreateDesertSurfaceMesh(root.transform, "EastWestRoad", document.surfaceGeometries.eastWestRoad, materials.Road);
            CreateDesertSurfaceMesh(root.transform, "DiagonalRoadA", document.surfaceGeometries.diagonalRoadA, materials.DiagonalRoad);
            CreateDesertSurfaceMesh(root.transform, "DiagonalRoadB", document.surfaceGeometries.diagonalRoadB, materials.DiagonalRoad);
            CreateDesertSurfaceMesh(root.transform, "NorthSouthLeft", document.surfaceGeometries.northSouthLeft, materials.Sidewalk76);
            CreateDesertSurfaceMesh(root.transform, "NorthSouthRight", document.surfaceGeometries.northSouthRight, materials.Sidewalk76);
            CreateDesertSurfaceMesh(root.transform, "EastWestLeft", document.surfaceGeometries.eastWestLeft, materials.Sidewalk76);
            CreateDesertSurfaceMesh(root.transform, "EastWestRight", document.surfaceGeometries.eastWestRight, materials.Sidewalk76);
            CreateDesertSurfaceMesh(root.transform, "DiagonalALeft", document.surfaceGeometries.diagonalALeft, materials.Sidewalk62);
            CreateDesertSurfaceMesh(root.transform, "DiagonalARight", document.surfaceGeometries.diagonalARight, materials.Sidewalk62);
            CreateDesertSurfaceMesh(root.transform, "DiagonalBLeft", document.surfaceGeometries.diagonalBLeft, materials.Sidewalk62);
            CreateDesertSurfaceMesh(root.transform, "DiagonalBRight", document.surfaceGeometries.diagonalBRight, materials.Sidewalk62);
        }

        private static void CreateDesertSurfaceMesh(
            Transform parent,
            string name,
            WofSerializedMeshRecord record,
            Material material)
        {
            var mesh = GetOrCreateMeshAsset(
                $"{DesertGeometryRoot}/{name}.asset",
                () => CreateDesertSerializedMesh(name, record));
            CreateMeshVisual(name, parent, Vector3.zero, mesh, material);
        }

        private static Mesh CreateDesertSerializedMesh(string name, WofSerializedMeshRecord record)
        {
            var vertices = new Vector3[record.vertexCount];
            var hasNormals = record.normals != null && record.normals.Length == record.vertexCount * 3;
            var normals = hasNormals ? new Vector3[record.vertexCount] : null;
            var uv = new Vector2[record.vertexCount];
            var hasUv = record.uvs != null && record.uvs.Length == record.vertexCount * 2;
            var colors = record.colors != null && record.colors.Length == record.vertexCount * 3
                ? new Color[record.vertexCount]
                : null;
            for (var index = 0; index < record.vertexCount; index++)
            {
                vertices[index] = new Vector3(
                    record.positions[index * 3],
                    record.positions[index * 3 + 1],
                    record.positions[index * 3 + 2]);
                if (hasNormals)
                {
                    normals[index] = new Vector3(
                        record.normals[index * 3],
                        record.normals[index * 3 + 1],
                        record.normals[index * 3 + 2]);
                }
                if (hasUv) uv[index] = new Vector2(record.uvs[index * 2], record.uvs[index * 2 + 1]);
                if (colors != null)
                {
                    colors[index] = new Color(
                        record.colors[index * 3],
                        record.colors[index * 3 + 1],
                        record.colors[index * 3 + 2],
                        1f);
                }
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            if (hasNormals) mesh.normals = normals;
            mesh.uv = uv;
            if (colors != null) mesh.colors = colors;
            mesh.triangles = record.indices;
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateDesertDisk(
            string name,
            Transform parent,
            float radius,
            int segments,
            float y,
            Material material)
        {
            var mesh = GetOrCreateMeshAsset(
                $"{DesertGeometryRoot}/{name}.asset",
                () => CreateDarrelRingMesh(0f, radius, segments));
            CreateMeshVisual(name, parent, new Vector3(0f, y, 0f), mesh, material);
        }

        private static void CreateDesertRing(
            string name,
            Transform parent,
            float innerRadius,
            float outerRadius,
            int segments,
            float y,
            Material material)
        {
            var mesh = GetOrCreateMeshAsset(
                $"{DesertGeometryRoot}/{name}.asset",
                () => CreateDarrelRingMesh(innerRadius, outerRadius, segments));
            CreateMeshVisual(name, parent, new Vector3(0f, y, 0f), mesh, material);
        }

        private static void CreateDesertWallsAndGates(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertVillageWallsAndGates");
            root.transform.SetParent(parent, false);
            foreach (var segment in document.layout.wallSegments)
            {
                var wall = CreatePrimitive(
                    segment.key,
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(segment.localX, document.baseHeight + segment.height * 0.5f, segment.localZ),
                    new Vector3(segment.width, segment.height, segment.depth),
                    materials.AdobeGate);
                wall.transform.localRotation = Quaternion.Euler(0f, segment.rotation * Mathf.Rad2Deg, 0f);
                MarkStatic(wall);
            }
            CreateDesertGateArch(root.transform, document.baseHeight, "North", 0f, -250f, true, materials);
            CreateDesertGateArch(root.transform, document.baseHeight, "South", 0f, 250f, true, materials);
            CreateDesertGateArch(root.transform, document.baseHeight, "East", 250f, 0f, false, materials);
            CreateDesertGateArch(root.transform, document.baseHeight, "West", -250f, 0f, false, materials);
        }

        private static void CreateDesertGateArch(
            Transform parent,
            float baseHeight,
            string key,
            float x,
            float z,
            bool northSouth,
            DesertMaterialSet materials)
        {
            if (northSouth)
            {
                CreateVisualPrimitive($"{key}GateLeft", PrimitiveType.Cube, parent, new Vector3(-36f, baseHeight + 8.2f, z), new Vector3(10f, 16.4f, 12f), materials.AdobeGate);
                CreateVisualPrimitive($"{key}GateRight", PrimitiveType.Cube, parent, new Vector3(36f, baseHeight + 8.2f, z), new Vector3(10f, 16.4f, 12f), materials.AdobeGate);
                CreateVisualPrimitive($"{key}GateBeam", PrimitiveType.Cube, parent, new Vector3(0f, baseHeight + 18.4f, z), new Vector3(84f, 5.6f, 12f), materials.AdobeGate);
                CreateVisualPrimitive($"{key}GateTrim", PrimitiveType.Cube, parent, new Vector3(0f, baseHeight + 22.2f, z), new Vector3(34f, 4.8f, 12f), materials.GateTrim);
                return;
            }
            CreateVisualPrimitive($"{key}GateLeft", PrimitiveType.Cube, parent, new Vector3(x, baseHeight + 8.2f, -36f), new Vector3(12f, 16.4f, 10f), materials.AdobeGate);
            CreateVisualPrimitive($"{key}GateRight", PrimitiveType.Cube, parent, new Vector3(x, baseHeight + 8.2f, 36f), new Vector3(12f, 16.4f, 10f), materials.AdobeGate);
            CreateVisualPrimitive($"{key}GateBeam", PrimitiveType.Cube, parent, new Vector3(x, baseHeight + 18.4f, 0f), new Vector3(12f, 5.6f, 84f), materials.AdobeGate);
            CreateVisualPrimitive($"{key}GateTrim", PrimitiveType.Cube, parent, new Vector3(x, baseHeight + 22.2f, 0f), new Vector3(12f, 4.8f, 34f), materials.GateTrim);
        }

        private static void CreateDesertBuildings(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertVillageBuildings");
            root.transform.SetParent(parent, false);
            var domeMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/BuildingDome.asset",
                () => CreateUvSphereMesh(1f, 10, 6));
            foreach (var building in document.layout.buildings)
            {
                var buildingRoot = new GameObject(building.key);
                buildingRoot.transform.SetParent(root.transform, false);
                buildingRoot.transform.localPosition = new Vector3(building.localX, document.baseHeight, building.localZ);
                buildingRoot.transform.localRotation = Quaternion.Euler(0f, building.rotation * Mathf.Rad2Deg, 0f);
                var wallThickness = Mathf.Min(1.05f, building.width * 0.16f, building.depth * 0.16f);
                var doorWidth = Mathf.Min(5.4f, building.width - wallThickness * 4f);
                var doorHeight = Mathf.Min(7.25f, building.height - 1.2f);
                var frontWallWidth = Mathf.Max(1.2f, (building.width - doorWidth) * 0.5f);
                var lintelHeight = Mathf.Max(0.8f, building.height - doorHeight);
                var windowY = Mathf.Min(building.height - 2.3f, 6.2f);
                var sideWindowZ = building.depth * 0.22f;
                var sideWindowWidth = Mathf.Min(3.1f, building.depth * 0.24f);

                CreateVisualPrimitive("OutlineBody", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, building.height * 0.5f, 0f), new Vector3(building.width + 1.1f, building.height + 0.75f, building.depth + 1.1f), materials.BuildingOutline);
                CreateVisualPrimitive("OutlineRoof", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, building.height + 0.75f, 0f), new Vector3(building.width + 4.2f, 2.05f, building.depth + 4.2f), materials.BuildingOutline);
                var cornerPositions = new[]
                {
                    new Vector3(-building.width * 0.5f - 0.08f, building.height * 0.5f, building.depth * 0.5f + 0.08f),
                    new Vector3(building.width * 0.5f + 0.08f, building.height * 0.5f, building.depth * 0.5f + 0.08f),
                    new Vector3(-building.width * 0.5f - 0.08f, building.height * 0.5f, -building.depth * 0.5f - 0.08f),
                    new Vector3(building.width * 0.5f + 0.08f, building.height * 0.5f, -building.depth * 0.5f - 0.08f)
                };
                for (var index = 0; index < cornerPositions.Length; index++)
                {
                    CreateVisualPrimitive($"CornerOutline_{index}", PrimitiveType.Cube, buildingRoot.transform, cornerPositions[index], new Vector3(0.62f, building.height + 0.38f, 0.62f), materials.BuildingTrim);
                }
                CreateVisualPrimitive("FrontTopTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, building.height + 0.14f, building.depth * 0.5f + 0.08f), new Vector3(building.width + 0.65f, 0.36f, 0.5f), materials.BuildingTrim);
                CreateVisualPrimitive("BackTopTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, building.height + 0.14f, -building.depth * 0.5f - 0.08f), new Vector3(building.width + 0.65f, 0.36f, 0.5f), materials.BuildingTrim);
                CreateVisualPrimitive("LeftTopTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(-building.width * 0.5f - 0.08f, building.height + 0.14f, 0f), new Vector3(0.5f, 0.36f, building.depth + 0.65f), materials.BuildingTrim);
                CreateVisualPrimitive("RightTopTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(building.width * 0.5f + 0.08f, building.height + 0.14f, 0f), new Vector3(0.5f, 0.36f, building.depth + 0.65f), materials.BuildingTrim);
                CreateVisualPrimitive("Floor", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, 0.08f, 0f), new Vector3(building.width - wallThickness * 1.4f, 0.16f, building.depth - wallThickness * 1.4f), materials.BuildingFloor);
                CreateDesertBuildingWall("LeftWall", buildingRoot.transform, new Vector3(-building.width * 0.5f + wallThickness * 0.5f, building.height * 0.5f, 0f), new Vector3(wallThickness, building.height, building.depth), materials.Adobe);
                CreateDesertBuildingWall("RightWall", buildingRoot.transform, new Vector3(building.width * 0.5f - wallThickness * 0.5f, building.height * 0.5f, 0f), new Vector3(wallThickness, building.height, building.depth), materials.Adobe);
                CreateDesertBuildingWall("BackWall", buildingRoot.transform, new Vector3(0f, building.height * 0.5f, -building.depth * 0.5f + wallThickness * 0.5f), new Vector3(building.width, building.height, wallThickness), materials.Adobe);
                CreateDesertBuildingWall("FrontWallLeft", buildingRoot.transform, new Vector3(-doorWidth * 0.5f - frontWallWidth * 0.5f, building.height * 0.5f, building.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, building.height, wallThickness), materials.Adobe);
                CreateDesertBuildingWall("FrontWallRight", buildingRoot.transform, new Vector3(doorWidth * 0.5f + frontWallWidth * 0.5f, building.height * 0.5f, building.depth * 0.5f - wallThickness * 0.5f), new Vector3(frontWallWidth, building.height, wallThickness), materials.Adobe);
                CreateDesertBuildingWall("FrontLintel", buildingRoot.transform, new Vector3(0f, doorHeight + lintelHeight * 0.5f, building.depth * 0.5f - wallThickness * 0.5f), new Vector3(doorWidth, lintelHeight, wallThickness), materials.Adobe);
                CreateVisualPrimitive("Roof", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, building.height + 0.75f, 0f), new Vector3(building.width + 2.8f, 1.5f, building.depth + 2.8f), materials.BuildingRoof);
                CreateVisualPrimitive("Door", PrimitiveType.Cube, buildingRoot.transform, new Vector3(0f, doorHeight * 0.5f - 0.25f, building.depth * 0.5f + 0.16f), new Vector3(doorWidth * 0.82f, doorHeight - 0.5f, 0.34f), materials.BuildingDoor);
                CreateDesertWindowPair(buildingRoot.transform, -building.width * 0.28f, windowY, building.depth * 0.5f, materials);
                CreateDesertWindowPair(buildingRoot.transform, building.width * 0.28f, windowY, building.depth * 0.5f, materials);
                CreateVisualPrimitive("LeftSideWindowTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(-building.width * 0.5f - 0.16f, windowY, sideWindowZ), new Vector3(0.36f, 2.9f, sideWindowWidth + 0.62f), materials.BuildingTrim);
                CreateVisualPrimitive("LeftSideWindow", PrimitiveType.Cube, buildingRoot.transform, new Vector3(-building.width * 0.5f - 0.22f, windowY, sideWindowZ), new Vector3(0.32f, 2.15f, sideWindowWidth), materials.BuildingWindow);
                CreateVisualPrimitive("RightSideWindowTrim", PrimitiveType.Cube, buildingRoot.transform, new Vector3(building.width * 0.5f + 0.16f, windowY, -sideWindowZ), new Vector3(0.36f, 2.9f, sideWindowWidth + 0.62f), materials.BuildingTrim);
                CreateVisualPrimitive("RightSideWindow", PrimitiveType.Cube, buildingRoot.transform, new Vector3(building.width * 0.5f + 0.22f, windowY, -sideWindowZ), new Vector3(0.32f, 2.15f, sideWindowWidth), materials.BuildingWindow);
                if (building.variant > 0.72f)
                {
                    var radius = Mathf.Min(building.width, building.depth) * 0.32f;
                    var dome = CreateMeshVisual("Dome", buildingRoot.transform, new Vector3(0f, building.height + 3.2f, 0f), domeMesh, materials.Dome);
                    dome.transform.localScale = Vector3.one * radius;
                }
            }
        }

        private static void CreateDesertBuildingWall(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var wall = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material);
            MarkStatic(wall);
        }

        private static void CreateDesertWindowPair(Transform parent, float x, float y, float frontZ, DesertMaterialSet materials)
        {
            CreateVisualPrimitive($"FrontWindowTrim_{x:F2}", PrimitiveType.Cube, parent, new Vector3(x, y, frontZ + 0.14f), new Vector3(3.7f, 3f, 0.36f), materials.BuildingTrim);
            CreateVisualPrimitive($"FrontWindow_{x:F2}", PrimitiveType.Cube, parent, new Vector3(x, y, frontZ + 0.20f), new Vector3(3.1f, 2.4f, 0.32f), materials.BuildingWindow);
        }

        private static void CreateDesertWell(Transform parent, float baseHeight, DesertMaterialSet materials)
        {
            var root = new GameObject("DesertVillageWell");
            root.transform.SetParent(parent, false);
            var bodyMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/WellBody.asset",
                () => CreateDarrelFrustumMesh(28f, 31f, 7f, 20));
            var body = CreateMeshVisual("WellBody", root.transform, new Vector3(0f, baseHeight + 3.5f, 0f), bodyMesh, materials.WellStone);
            body.AddComponent<MeshCollider>().sharedMesh = bodyMesh;
            CreateDesertDisk("WellWater", root.transform, 22f, 20, baseHeight + 7.25f, materials.WellWater);
            CreateDesertRing("WellRim", root.transform, 27.5f, 32f, 20, baseHeight + 8.15f, materials.WellRim);
            var left = CreatePrimitive("WellPostLeft", PrimitiveType.Cube, root.transform, new Vector3(-18f, baseHeight + 16f, 0f), new Vector3(3.4f, 17f, 3.4f), materials.Wood);
            var right = CreatePrimitive("WellPostRight", PrimitiveType.Cube, root.transform, new Vector3(18f, baseHeight + 16f, 0f), new Vector3(3.4f, 17f, 3.4f), materials.Wood);
            var beam = CreatePrimitive("WellBeam", PrimitiveType.Cube, root.transform, new Vector3(0f, baseHeight + 25.2f, 0f), new Vector3(45f, 4.2f, 5f), materials.Wood);
            MarkStatic(left);
            MarkStatic(right);
            MarkStatic(beam);
        }

        private static void CreateDesertDressing(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertVillageDressing");
            root.transform.SetParent(parent, false);
            CreateDesertFences(root.transform, document, materials);
            CreateDesertLadders(root.transform, document, materials);
            CreateDesertClothesLines(root.transform, document, materials);
            CreateDesertStreetProps(root.transform, document, materials);
        }

        private static void CreateDesertLadders(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("Ladders");
            root.transform.SetParent(parent, false);
            foreach (var ladder in document.layout.ladders)
            {
                var ladderRoot = new GameObject(ladder.key);
                ladderRoot.transform.SetParent(root.transform, false);
                ladderRoot.transform.localPosition = new Vector3(ladder.localX, document.baseHeight, ladder.localZ);
                ladderRoot.transform.localRotation = Quaternion.Euler(0f, ladder.rotation * Mathf.Rad2Deg, 0f);
                CreateVisualPrimitive("LeftRail", PrimitiveType.Cube, ladderRoot.transform, new Vector3(-ladder.width * 0.5f, ladder.height * 0.5f, 0f), new Vector3(0.26f, ladder.height, 0.22f), materials.DarkWood);
                CreateVisualPrimitive("RightRail", PrimitiveType.Cube, ladderRoot.transform, new Vector3(ladder.width * 0.5f, ladder.height * 0.5f, 0f), new Vector3(0.26f, ladder.height, 0.22f), materials.DarkWood);
                var rungCount = Mathf.Max(4, Mathf.FloorToInt(ladder.height / 1.7f));
                for (var index = 0; index < rungCount; index++)
                {
                    var y = 1.15f + index * ((ladder.height - 2.3f) / Mathf.Max(1, rungCount - 1));
                    CreateVisualPrimitive($"Rung_{index:00}", PrimitiveType.Cube, ladderRoot.transform, new Vector3(0f, y, 0.08f), new Vector3(ladder.width + 0.36f, 0.22f, 0.28f), materials.Wood);
                }
            }
        }

        private static void CreateDesertFences(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("Fences");
            root.transform.SetParent(parent, false);
            foreach (var fence in document.layout.fences)
            {
                var fenceRoot = new GameObject(fence.key);
                fenceRoot.transform.SetParent(root.transform, false);
                fenceRoot.transform.localPosition = new Vector3(fence.localX, document.baseHeight, fence.localZ);
                fenceRoot.transform.localRotation = Quaternion.Euler(0f, fence.rotation * Mathf.Rad2Deg, 0f);
                foreach (var offset in new[] { -0.5f, 0f, 0.5f })
                {
                    var post = CreatePrimitive($"Post_{offset:F1}", PrimitiveType.Cube, fenceRoot.transform, new Vector3(offset * fence.length, 1.7f, 0f), new Vector3(0.66f, 3.4f, 0.58f), materials.DeepWood);
                    MarkStatic(post);
                }
                var lowerRail = CreatePrimitive("LowerRail", PrimitiveType.Cube, fenceRoot.transform, new Vector3(0f, 1.55f, 0f), new Vector3(fence.length, 0.38f, 0.38f), materials.Wood);
                var upperRail = CreatePrimitive("UpperRail", PrimitiveType.Cube, fenceRoot.transform, new Vector3(0f, 2.72f, 0f), new Vector3(fence.length, 0.34f, 0.34f), materials.DarkWood);
                MarkStatic(lowerRail);
                MarkStatic(upperRail);
            }
        }

        private static void CreateDesertClothesLines(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("ClothesLines");
            root.transform.SetParent(parent, false);
            foreach (var line in document.layout.clothesLines)
            {
                var lineRoot = new GameObject(line.key);
                lineRoot.transform.SetParent(root.transform, false);
                var start = new Vector3(line.startX, document.baseHeight + line.y, line.startZ);
                var end = new Vector3(line.endX, document.baseHeight + line.y - 0.6f, line.endZ);
                var direction = end - start;
                var length = Mathf.Max(0.1f, direction.magnitude);
                var rope = CreateVisualPrimitive("Rope", PrimitiveType.Cylinder, lineRoot.transform, (start + end) * 0.5f, new Vector3(0.16f, length * 0.5f, 0.16f), materials.Rope);
                rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up);
                var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                for (var index = 0; index < line.colors.Length; index++)
                {
                    var t = 0.26f + index * 0.24f;
                    var position = Vector3.Lerp(start, end, t) + Vector3.down * 1.45f;
                    var clothMaterial = GetOrCreateDesertUnlit($"DesertCloth_{line.colors[index].TrimStart('#')}", HexColor(line.colors[index]), null, false);
                    var cloth = CreateVisualPrimitive($"Cloth_{index}", PrimitiveType.Cube, lineRoot.transform, position, new Vector3(2.8f, 2.8f + (index % 2) * 0.65f, 0.12f), clothMaterial);
                    cloth.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
        }

        private static void CreateDesertStreetProps(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("StreetProps");
            root.transform.SetParent(parent, false);
            var barrelBody = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/BarrelBody.asset",
                () => CreateDarrelFrustumMesh(1.25f, 1.45f, 3.1f, 8));
            var barrelCollider = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/BarrelCollider.asset",
                () => CreateDarrelFrustumMesh(1.48f, 1.48f, 3.1f, 8));
            var barrelBand = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/BarrelBand.asset",
                () => CreateDarrelFrustumMesh(1.48f, 1.48f, 0.22f, 8));
            var sackBody = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/SackBody.asset",
                () => CreateUvSphereMesh(1.55f, 8, 5));
            var sackTop = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/SackTop.asset",
                () => CreateUvSphereMesh(0.9f, 7, 4));
            var barrelWood = GetOrCreateDesertUnlit("DesertBarrelWood", HexColor("#4a2a17"), null, false);
            var barrelMetal = GetOrCreateDesertUnlit("DesertBarrelBands", HexColor("#1d120b"), null, false);
            var crateWood = GetOrCreateDesertUnlit("DesertCrateWood", HexColor("#3d2414"), null, false);
            var crateBand = GetOrCreateDesertUnlit("DesertCrateBands", HexColor("#21140b"), null, false);
            var sackMaterial = GetOrCreateDesertUnlit("DesertSack", HexColor("#c7a46b"), null, false);
            var sackTopMaterial = GetOrCreateDesertUnlit("DesertSackTop", HexColor("#dfc18a"), null, false);
            foreach (var prop in document.layout.streetProps)
            {
                if (string.Equals(prop.kind, "barrel", StringComparison.Ordinal))
                {
                    var propRoot = new GameObject(prop.key);
                    propRoot.transform.SetParent(root.transform, false);
                    propRoot.transform.localPosition = new Vector3(prop.localX, document.baseHeight, prop.localZ);
                    propRoot.transform.localRotation = Quaternion.Euler(0f, prop.rotation * Mathf.Rad2Deg, 0f);
                    propRoot.transform.localScale = Vector3.one * prop.scale;
                    var body = CreateMeshVisual("Body", propRoot.transform, new Vector3(0f, 1.55f, 0f), barrelBody, barrelWood);
                    body.AddComponent<MeshCollider>().sharedMesh = barrelCollider;
                    CreateMeshVisual("TopBand", propRoot.transform, new Vector3(0f, 2.55f, 0f), barrelBand, barrelMetal);
                    CreateMeshVisual("BottomBand", propRoot.transform, new Vector3(0f, 0.62f, 0f), barrelBand, barrelMetal);
                    continue;
                }
                if (string.Equals(prop.kind, "crate", StringComparison.Ordinal))
                {
                    var propRoot = new GameObject(prop.key);
                    propRoot.transform.SetParent(root.transform, false);
                    propRoot.transform.localPosition = new Vector3(prop.localX, document.baseHeight, prop.localZ);
                    propRoot.transform.localRotation = Quaternion.Euler(0f, prop.rotation * Mathf.Rad2Deg, 0f);
                    propRoot.transform.localScale = Vector3.one * prop.scale;
                    CreateVisualPrimitive("Body", PrimitiveType.Cube, propRoot.transform, new Vector3(0f, 1.35f, 0f), new Vector3(2.85f, 2.7f, 2.85f), crateWood);
                    CreateVisualPrimitive("FrontBand", PrimitiveType.Cube, propRoot.transform, new Vector3(0f, 1.38f, 1.48f), new Vector3(3.05f, 0.28f, 0.26f), crateBand);
                    CreateVisualPrimitive("BackBand", PrimitiveType.Cube, propRoot.transform, new Vector3(0f, 1.38f, -1.48f), new Vector3(3.05f, 0.28f, 0.26f), crateBand);
                    CreateVisualPrimitive("SideBand", PrimitiveType.Cube, propRoot.transform, new Vector3(1.48f, 1.38f, 0f), new Vector3(0.26f, 0.28f, 3.05f), crateBand);
                    var collider = propRoot.AddComponent<BoxCollider>();
                    collider.center = new Vector3(0f, 1.35f, 0f);
                    collider.size = new Vector3(3.05f, 2.7f, 3.05f);
                    continue;
                }

                var sackRoot = new GameObject(prop.key);
                sackRoot.transform.SetParent(root.transform, false);
                sackRoot.transform.localPosition = new Vector3(prop.localX, document.baseHeight + 0.58f * prop.scale, prop.localZ);
                sackRoot.transform.localRotation = Quaternion.Euler(0f, prop.rotation * Mathf.Rad2Deg, 0f);
                var bodyVisual = CreateMeshVisual("Body", sackRoot.transform, Vector3.zero, sackBody, sackMaterial);
                bodyVisual.transform.localScale = new Vector3(prop.scale * 1.35f, prop.scale * 0.72f, prop.scale);
                var topVisual = CreateMeshVisual("Top", sackRoot.transform, new Vector3(0.2f * prop.scale * 1.35f, 1.05f * prop.scale * 0.72f, 0f), sackTop, sackTopMaterial);
                topVisual.transform.localScale = new Vector3(prop.scale * 1.35f * 0.78f, prop.scale * 0.72f * 0.22f, prop.scale * 0.55f);
                var sackCollider = sackRoot.AddComponent<BoxCollider>();
                sackCollider.center = Vector3.zero;
                sackCollider.size = new Vector3(3.5f * prop.scale, 1.16f * prop.scale, 2.4f * prop.scale);
            }
        }

        private static void CreateDesertMarketStalls(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertMarketStalls");
            root.transform.SetParent(parent, false);
            var stallBody = GetOrCreateDesertUnlit("DesertMarketBody", HexColor("#80512a"), null, false);
            foreach (var stall in document.layout.marketStalls)
            {
                var stallRoot = new GameObject(stall.key);
                stallRoot.transform.SetParent(root.transform, false);
                stallRoot.transform.localPosition = new Vector3(stall.localX, document.baseHeight, stall.localZ);
                stallRoot.transform.localRotation = Quaternion.Euler(0f, stall.rotation * Mathf.Rad2Deg, 0f);
                var body = CreatePrimitive("Body", PrimitiveType.Cube, stallRoot.transform, new Vector3(0f, 2.4f, 0f), new Vector3(10f, 4.8f, 5.5f), stallBody);
                MarkStatic(body);
                var canopyMaterial = GetOrCreateDesertUnlit($"DesertMarket_{stall.color.TrimStart('#')}", HexColor(stall.color), null, false);
                CreateVisualPrimitive("Canopy", PrimitiveType.Cube, stallRoot.transform, new Vector3(0f, 5.4f, 0f), new Vector3(12.5f, 1.1f, 7.2f), canopyMaterial);
                var awning = CreateVisualPrimitive("Awning", PrimitiveType.Cube, stallRoot.transform, new Vector3(0f, 5.95f, 0f), new Vector3(13.5f, 0.7f, 7.8f), canopyMaterial);
                awning.transform.localRotation = Quaternion.Euler(0f, 0f, 0.16f * Mathf.Rad2Deg);
                var canopyCollider = stallRoot.AddComponent<BoxCollider>();
                canopyCollider.center = new Vector3(0f, 5.95f, 0f);
                canopyCollider.size = new Vector3(13.5f, 1.1f, 7.8f);
            }
        }

        private static void CreateDesertPalms(
            Transform parent,
            WofDesertVillageDocument document,
            DesertMaterialSet materials)
        {
            var root = new GameObject("DesertDatePalms");
            root.transform.SetParent(parent, false);
            var trunkMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmTrunk.asset",
                () => CreateDarrelFrustumMesh(0.95f, 1.42f, 24.8f, 6));
            var trunkColliderMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmTrunkCollider.asset",
                () => CreateDarrelFrustumMesh(1.42f, 1.42f, 24.8f, 8));
            var dodecaMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmLeafDodeca.asset",
                CreateTreeHouseDodecaMesh);
            var dodecaEdgeMesh = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmLeafDodecaEdges.asset",
                CreateTreeHouseDodecaEdgeMesh);
            var fruitMeshA = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmFruit72.asset",
                () => CreateUvSphereMesh(0.72f, 6, 4));
            var fruitMeshB = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmFruit62.asset",
                () => CreateUvSphereMesh(0.62f, 6, 4));
            var fruitMeshC = GetOrCreateMeshAsset(
                DesertGeometryRoot + "/PalmFruit58.asset",
                () => CreateUvSphereMesh(0.58f, 6, 4));
            var fruitA = GetOrCreateDesertUnlit("DesertPalmFruitA", HexColor("#8b5c21"), null, false);
            var fruitB = GetOrCreateDesertUnlit("DesertPalmFruitB", HexColor("#a06a28"), null, false);
            var fruitC = GetOrCreateDesertUnlit("DesertPalmFruitC", HexColor("#7f4d1f"), null, false);
            foreach (var palm in document.layout.palms)
            {
                var palmRoot = new GameObject(palm.key);
                palmRoot.transform.SetParent(root.transform, false);
                palmRoot.transform.localPosition = new Vector3(palm.localX, palm.localY, palm.localZ);
                palmRoot.transform.localRotation = Quaternion.Euler(0f, palm.rotation * Mathf.Rad2Deg, 0f);
                palmRoot.transform.localScale = Vector3.one * palm.scale;
                var trunk = CreateMeshVisual("Trunk", palmRoot.transform, new Vector3(0f, 12.4f, 0f), trunkMesh, materials.PalmTrunk);
                trunk.transform.localRotation = Quaternion.Euler(0.12f * Mathf.Rad2Deg, 0f, 0.08f * Mathf.Rad2Deg);
                var colliderRoot = new GameObject("TrunkCollider");
                colliderRoot.transform.SetParent(palmRoot.transform, false);
                colliderRoot.transform.localPosition = new Vector3(0f, 12.4f, 0f);
                colliderRoot.AddComponent<MeshCollider>().sharedMesh = trunkColliderMesh;
                for (var index = 0; index < 9; index++)
                {
                    var angle = Mathf.PI * 2f * index / 9f;
                    var leafRoot = new GameObject($"Leaf_{index}");
                    leafRoot.transform.SetParent(palmRoot.transform, false);
                    leafRoot.transform.localPosition = new Vector3(Mathf.Sin(angle) * 4.2f, 25.2f, Mathf.Cos(angle) * 4.2f);
                    leafRoot.transform.localRotation = Quaternion.Euler(0.5f * Mathf.Rad2Deg, angle * Mathf.Rad2Deg, 0.18f * Mathf.Rad2Deg);
                    var edge = CreateMeshVisual("Edge", leafRoot.transform, Vector3.zero, dodecaEdgeMesh, materials.PalmEdge);
                    edge.transform.localScale = new Vector3(0.79f, 0.23f, 8.34f);
                    var leaf = CreateMeshVisual("Leaf", leafRoot.transform, Vector3.zero, dodecaMesh, index % 2 == 0 ? materials.PalmLeafA : materials.PalmLeafB);
                    leaf.transform.localScale = new Vector3(0.775f, 0.22f, 8.25f);
                }
                CreateMeshVisual("FruitA", palmRoot.transform, new Vector3(-1.15f, 22.6f, 0.95f), fruitMeshA, fruitA);
                CreateMeshVisual("FruitB", palmRoot.transform, new Vector3(0.25f, 22.1f, 1.15f), fruitMeshB, fruitB);
                CreateMeshVisual("FruitC", palmRoot.transform, new Vector3(1.25f, 22.8f, 0.5f), fruitMeshC, fruitC);
            }
        }

        private static void CreateDesertVillagers(
            Transform parent,
            WofDesertVillageDocument document,
            Material material)
        {
            var root = new GameObject("ReactDesertVillageVillagers");
            root.transform.SetParent(parent, false);
            var billboards = new WofVillagerBillboard[document.villagers.Length];
            for (var index = 0; index < document.villagers.Length; index++)
            {
                var record = document.villagers[index];
                if (record == null || string.IsNullOrWhiteSpace(record.id) ||
                    string.IsNullOrWhiteSpace(record.archiveFile) ||
                    string.IsNullOrWhiteSpace(record.displayName) ||
                    string.IsNullOrWhiteSpace(record.townId))
                {
                    throw new InvalidOperationException($"Invalid exact React desert villager record at index {index}.");
                }
                var villager = new GameObject($"DesertVillager_{index:00}");
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
                billboard.Configure(
                    record.id,
                    record.archiveFile,
                    new Vector3(record.x, record.y + WofVillagerMath.AvatarGroundLift, record.z),
                    record.baseYaw,
                    record.lookUpdateDesktopMs,
                    record.lookUpdateMobileMs,
                    record.hut,
                    visual.transform,
                    renderer,
                    record.displayName,
                    record.townId);
                billboards[index] = billboard;
            }
            root.AddComponent<WofVillagerManager>().Configure(billboards);
        }

        private sealed class DesertMaterialSet
        {
            public Material Sand;
            public Material SandOverlay;
            public Material CenterSand;
            public Material RingSandA;
            public Material RingSandB;
            public Material Road;
            public Material DiagonalRoad;
            public Material DarkRing66;
            public Material DarkRing68;
            public Material DarkRing64;
            public Material DarkRing60;
            public Material Sidewalk76;
            public Material Sidewalk62;
            public Material Adobe;
            public Material AdobeGate;
            public Material BuildingOutline;
            public Material BuildingTrim;
            public Material BuildingRoof;
            public Material BuildingDoor;
            public Material BuildingWindow;
            public Material BuildingFloor;
            public Material Dome;
            public Material DarkWood;
            public Material Wood;
            public Material DeepWood;
            public Material Rope;
            public Material WellStone;
            public Material WellWater;
            public Material WellRim;
            public Material GateTrim;
            public Material PalmTrunk;
            public Material PalmEdge;
            public Material PalmLeafA;
            public Material PalmLeafB;
        }
    }
}
