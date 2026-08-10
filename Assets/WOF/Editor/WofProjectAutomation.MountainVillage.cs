using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string MountainArtRoot = "Assets/WOF/Art/Generated/React/MountainVillage";
        private const string MountainLayoutPath = MountainArtRoot + "/runtime-layout.json";
        private const string MountainGeometryRoot = GeometryRoot + "/MountainVillage";

        private static void CreateMountainVillageScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = WofMountainVillageSceneLoader.SceneName;
            var world = new GameObject("World");
            CreateMountainVillage(world.transform, GetOrCreateVillagerMaterial());
            EditorSceneManager.SaveScene(scene, MountainScenePath);
        }

        private static void CreateMountainVillage(Transform parent, Material villagerMaterial)
        {
            var document = LoadMountainVillageDocument();
            var materials = CreateMountainMaterials();
            var root = new GameObject("ReactSurvivalMountainVillage_3_0");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofMountainVillageLayout.WorldOrigin;

            CreateMountainSurface(root.transform, document, materials);
            CreateMountainTrail(root.transform, document, materials);
            CreateMountainCliffsAndSnow(root.transform, document, materials);
            root.AddComponent<WofMountainSnowRuntime>();
            CreateMountainWaterfall(root.transform, document, materials);
            CreateMountainCabins(root.transform, document, materials);
            CreateMountainMineshaft(root.transform, document, materials);
            CreateMountainVillagers(parent, document, villagerMaterial);
        }

        private static WofMountainVillageDocument LoadMountainVillageDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(MountainLayoutPath);
            var document = JsonUtility.FromJson<WofMountainVillageDocument>(source.text);
            if (document == null || document.schemaVersion != 1 || document.chunk == null ||
                document.chunk.cx != WofMountainVillageLayout.ChunkX ||
                document.chunk.cz != WofMountainVillageLayout.ChunkZ || document.chunk.distance != 0 ||
                !string.Equals(document.chunk.biome, "mushroom", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.villageKind, "mountain", StringComparison.Ordinal) ||
                !string.Equals(document.chunk.lod, "near", StringComparison.Ordinal) ||
                !document.chunk.hasVillage || document.chunk.hasRiver ||
                !Mathf.Approximately(document.baseHeight, WofMountainVillageLayout.ReactBaseHeight) ||
                !Mathf.Approximately(document.summitY, WofMountainVillageLayout.ReactSummitY) ||
                !WofMountainVillageLayout.HasExactCounts(document.counts) || document.constants == null ||
                !Mathf.Approximately(document.constants.radius, WofMountainVillageLayout.MountainRadius) ||
                document.constants.slopeGrassNearCount != 2200 || document.layout == null ||
                document.layout.trailPoints?.Length != 25 || document.layout.trailSegments?.Length != 24 ||
                document.layout.cliffPatches?.Length != 48 || document.layout.cabins?.Length != 8 ||
                document.layout.interiorHuts?.Length != 3 || document.layout.interiorLadders?.Length != 4 ||
                document.opening?.summitSnowDrifts?.Length != 28 || document.opening.rimBeams?.Length != 12 ||
                document.opening.supportFrames?.Length != 4 || document.opening.bottomRocks?.Length != 14 ||
                document.wallDecor?.lanterns?.Length != 9 || document.wallDecor.paintings?.Length != 6 ||
                document.wallDecor.ropeLights?.Length != 20 || document.banquet?.bottomLights?.Length != 8 ||
                document.banquet.chairs?.Length != 7 || document.interiorPlatforms?.Length != 3 ||
                document.ladderDetails?.Length != 4 || document.exitBridge == null ||
                document.waterfallVisuals == null || document.villagers?.Length != 11 ||
                document.geometries == null || !IsValidMountainMesh(document.geometries.terrain) ||
                !IsValidMountainMesh(document.geometries.terrainCollider) ||
                !IsValidMountainMesh(document.geometries.slopeGrass) ||
                !IsValidMountainMesh(document.geometries.trailDeck) ||
                !IsValidMountainMesh(document.geometries.trailTop) ||
                !IsValidMountainMesh(document.geometries.trailCollider) ||
                !IsValidMountainMesh(document.geometries.summitCollider))
            {
                throw new InvalidOperationException($"Invalid exact React mountain village layout at {MountainLayoutPath}.");
            }
            return document;
        }

        private static bool IsValidMountainMesh(WofSerializedMeshRecord record)
        {
            return record != null && record.vertexCount > 0 &&
                   record.positions?.Length == record.vertexCount * 3 &&
                   record.normals?.Length == record.vertexCount * 3 &&
                   (record.uvs == null || record.uvs.Length == 0 ||
                    record.uvs.Length == record.vertexCount * 2) &&
                   record.indices != null && record.indices.Length > 0 &&
                   (record.colors == null || record.colors.Length == 0 ||
                    record.colors.Length == record.vertexCount * 3);
        }

        private static MountainMaterialSet CreateMountainMaterials()
        {
            return new MountainMaterialSet
            {
                Terrain = GetOrCreateMountainVertexMaterial("MountainTerrainBanded", Color.white, null),
                Grass = GetOrCreateMountainVertexMaterial("MountainSlopeGrass", Color.white, null),
                TrailDeck = MountainMaterial("#4b3827"),
                TrailTop = MountainMaterial("#74613f"),
                TrailDark = MountainMaterial("#2f2117"),
                DarkWood = MountainMaterial("#21150d"),
                DeepDarkWood = MountainMaterial("#080504"),
                MidWood = MountainMaterial("#5b4029"),
                LightWood = MountainMaterial("#8d6238"),
                Snow = MountainMaterial("#eef8ff"),
                Ice = MountainMaterial("#a7d8ef"),
                Shaft = MountainMaterial("#0b0908"),
                Waterfall = MountainMaterial("#89e9ff", 0.48f),
                WaterfallBright = MountainMaterial("#effdff", 0.28f),
                WaterfallDark = MountainMaterial("#16596d", 0.2f),
                FoamTop = MountainMaterial("#b9f1ff", 0.48f),
                FoamBottom = MountainMaterial("#5bbbd4", 0.56f),
                Spray = MountainMaterial("#dffaff", 0.26f)
            };
        }

        private static Material GetOrCreateMountainVertexMaterial(string name, Color color, Texture texture)
        {
            var shader = Shader.Find("WOF/Vertex Color Texture");
            if (shader == null) throw new InvalidOperationException("Required mountain vertex-color shader was not imported.");
            var path = $"{MaterialsRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", new Vector2(7f, 9f));
            }
            else
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetTextureScale("_BaseMap", Vector2.one);
            }
            material.enableInstancing = true;
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MountainMaterial(string hex, float alpha = 1f)
        {
            var color = HexColor(hex);
            color.a = alpha;
            return GetOrCreateDesertUnlit(
                $"Mountain_{hex.TrimStart('#')}_{Mathf.RoundToInt(alpha * 100f):000}",
                color,
                null,
                alpha < 0.999f);
        }

        private static void CreateMountainSurface(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainSurface");
            root.transform.SetParent(parent, false);
            var terrain = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/TerrainBanded.asset",
                () => CreateMountainBandedTerrainMesh(document));
            var terrainCollider = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/TerrainColliderBiomeBlended.asset",
                () => CreateMountainBiomeBlendedColliderMesh(document));
            var grass = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/SlopeGrassBaseFringe.asset",
                () => CreateMountainBaseFringeGrassMesh(document));
            CreateMeshVisual("BandedMountainTerrain_DirtStoneSnow", root.transform, Vector3.zero, terrain, materials.Terrain);
            var colliderOwner = new GameObject("ExactMountainTerrainCollider");
            colliderOwner.transform.SetParent(root.transform, false);
            colliderOwner.AddComponent<MeshCollider>().sharedMesh = terrainCollider;
            CreateMeshVisual("MountainBaseFringeGrass", root.transform, Vector3.zero, grass, materials.Grass);
        }

        private static void CreateLegacyMountainTrail(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainWrappingTrail");
            root.transform.SetParent(parent, false);
            var deck = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/TrailDeck.asset",
                () => CreateDesertSerializedMesh("ExactMountainTrailDeck", document.geometries.trailDeck));
            var top = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/TrailTop.asset",
                () => CreateDesertSerializedMesh("ExactMountainTrailTop", document.geometries.trailTop));
            var colliderMesh = GetOrCreateMeshAsset(
                MountainGeometryRoot + "/TrailCollider.asset",
                () => CreateDesertSerializedMesh("ExactMountainTrailCollider", document.geometries.trailCollider));
            CreateMeshVisual("ExactTrailDeck", root.transform, Vector3.zero, deck, materials.TrailDeck);
            CreateMeshVisual("ExactTrailTop", root.transform, Vector3.zero, top, materials.TrailTop);
            var colliderOwner = new GameObject("ExactTrailCollider");
            colliderOwner.transform.SetParent(root.transform, false);
            colliderOwner.AddComponent<MeshCollider>().sharedMesh = colliderMesh;

            foreach (var segment in document.layout.trailSegments)
            {
                var item = new GameObject(segment.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(segment.localX, segment.y, segment.localZ);
                item.transform.localRotation = Quaternion.Euler(0f, segment.yaw * Mathf.Rad2Deg, 0f);
                var slopeRoot = new GameObject("SlopeFrame");
                slopeRoot.transform.SetParent(item.transform, false);
                slopeRoot.transform.localRotation = Quaternion.Euler(segment.slope * Mathf.Rad2Deg, 0f, 0f);
                MountainBox("LeftBeam", slopeRoot.transform,
                    new Vector3(-segment.width * 0.5f + 0.94f, -0.72f, 0f),
                    new Vector3(1.24f, 0.54f, segment.length * 1.02f), materials.TrailDark);
                MountainBox("RightBeam", slopeRoot.transform,
                    new Vector3(segment.width * 0.5f - 0.94f, -0.72f, 0f),
                    new Vector3(1.24f, 0.54f, segment.length * 1.02f), materials.TrailDark);
                if (segment.index == 0 || segment.index == document.layout.trailSegments.Length - 1)
                {
                    var landingLength = Mathf.Min(22f, segment.length * 0.72f);
                    var landingZ = segment.index == 0 ? -segment.length * 0.28f : segment.length * 0.28f;
                    MountainBox("Landing", slopeRoot.transform, new Vector3(0f, -0.96f, landingZ),
                        new Vector3(segment.width * 1.08f, 0.42f, landingLength * 0.84f), MountainMaterial("#3a2719"));
                }
                foreach (var side in new[] { -1f, 1f })
                {
                    MountainBox($"TopShadow_{side}", slopeRoot.transform,
                        new Vector3(side * (segment.width * 0.5f - 0.52f), 0.08f, 0f),
                        new Vector3(0.42f, 0.08f, segment.length * 0.92f), MountainMaterial("#120c08", 0.5f));
                }
                for (var plank = 0; plank < 4; plank++)
                {
                    var z = -segment.length * 0.38f + plank * segment.length * 0.76f / 3f;
                    MountainBox($"CrossShadow_{plank}", slopeRoot.transform, new Vector3(0f, 0.1f, z),
                        new Vector3(segment.width * 0.86f, 0.07f, 0.18f),
                        MountainMaterial(plank % 2 == 0 ? "#16100b" : "#5b3b22", 0.58f));
                }
                MountainBox("UnderShadow", slopeRoot.transform, new Vector3(0f, -0.32f, 0f),
                    new Vector3(segment.width * 1.02f, 0.16f, segment.length * 0.96f), MountainMaterial("#090604", 0.3f));
                foreach (var zSign in new[] { -1f, 1f })
                {
                    MountainBox($"EndBrace_{zSign}", slopeRoot.transform,
                        new Vector3(0f, -1.02f, zSign * segment.length * 0.34f),
                        new Vector3(segment.width + 1.6f, 0.5f, 0.9f), MountainMaterial("#3a2719"));
                }
                foreach (var roll in new[] { -0.24f, 0.24f })
                {
                    var brace = MountainBox($"Diagonal_{roll}", slopeRoot.transform, new Vector3(0f, -1.3f, 0f),
                        new Vector3(segment.width * 0.72f, 0.42f, 0.72f), materials.MidWood);
                    brace.transform.localRotation = Quaternion.Euler(0f, 0f, roll * Mathf.Rad2Deg);
                }

                foreach (var support in segment.supports ?? Array.Empty<WofMountainTrailSupportRecord>())
                {
                    var supportRoot = new GameObject(support.key);
                    supportRoot.transform.SetParent(root.transform, false);
                    supportRoot.transform.localPosition = new Vector3(
                        support.localX,
                        support.topY - support.height * 0.5f,
                        support.localZ);
                    supportRoot.transform.localRotation = Quaternion.Euler(0f, support.yaw * Mathf.Rad2Deg, 0f);
                    MountainBox("MainPost", supportRoot.transform, Vector3.zero,
                        new Vector3(2.15f, support.height, 2.15f), materials.TrailDark);
                    CreateMountainVerticalTimberDetails(supportRoot.transform, support.height, 2.15f, 2.15f, "#8e6137");
                    MountainBox("Foot", supportRoot.transform, new Vector3(0f, -support.height * 0.5f - 0.08f, 0f),
                        new Vector3(5.6f, 0.62f, 5.6f), MountainMaterial("#4b3524"));
                    if (support.height > 4.2f)
                    {
                        foreach (var braceSide in new[] { -1f, 1f })
                        {
                            var brace = MountainBox($"SideBrace_{braceSide}", supportRoot.transform,
                                new Vector3(braceSide * 0.98f, -support.height * 0.08f, 0f),
                                new Vector3(0.9f, support.height * 0.86f, 0.9f), MountainMaterial("#3f2d1f"));
                            brace.transform.localRotation = Quaternion.Euler(0f, 0f, -braceSide * 0.24f * Mathf.Rad2Deg);
                        }
                    }
                }
            }
        }

        private static void CreateMountainCliffsAndSnow(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var cliffs = new GameObject("MountainCliffBreakup");
            cliffs.transform.SetParent(parent, false);
            foreach (var patch in document.layout.cliffPatches)
            {
                var item = MountainBox(patch.key, cliffs.transform,
                    new Vector3(patch.localX, patch.y, patch.localZ),
                    new Vector3(patch.width, patch.thickness, patch.depth),
                    MountainMaterial(patch.color, patch.opacity));
                item.transform.localRotation = Quaternion.Euler(0f, patch.yaw * Mathf.Rad2Deg, patch.roll * Mathf.Rad2Deg);
            }

            var snow = new GameObject("MountainSnowCap");
            snow.transform.SetParent(parent, false);
            var disk = GetOrCreateMeshAsset(MountainGeometryRoot + "/SnowDriftDisk12.asset", () => CreateDarrelRingMesh(0f, 1f, 12));
            foreach (var drift in document.opening.summitSnowDrifts)
            {
                var visual = CreateMeshVisual($"SummitSnowDrift_{drift.index:00}", snow.transform,
                    new Vector3(drift.positionXZ[0], document.summitY + drift.yOffset, drift.positionXZ[1]),
                    disk,
                    MountainMaterial(drift.color, 0.46f));
                visual.transform.localRotation = Quaternion.Euler(0f, drift.rotation[2] * Mathf.Rad2Deg, 0f);
                visual.transform.localScale = new Vector3(drift.scale[0], 1f, drift.scale[1]);
            }
        }

        private static void CreateMountainWaterfall(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainWaterfall");
            root.transform.SetParent(parent, false);
            CreateMountainWaterfallPlane("MainFall", root.transform, document.waterfallVisuals.mainFall, materials.Waterfall);
            CreateMountainWaterfallPlane("BrightFall", root.transform, document.waterfallVisuals.brightFall, materials.WaterfallBright);
            foreach (var edge in document.waterfallVisuals.darkEdges)
                CreateMountainWaterfallPlane($"DarkEdge_{edge.side}", root.transform, edge, materials.WaterfallDark);
            var disk18 = GetOrCreateMeshAsset(MountainGeometryRoot + "/FoamDisk18.asset", () => CreateDarrelRingMesh(0f, 1f, 18));
            var disk24 = GetOrCreateMeshAsset(MountainGeometryRoot + "/FoamDisk24.asset", () => CreateDarrelRingMesh(0f, 1f, 24));
            CreateMountainFoam("TopFoam", root.transform, document.waterfallVisuals.topFoam, disk18, materials.FoamTop);
            CreateMountainFoam("BottomFoam", root.transform, document.waterfallVisuals.bottomFoam, disk24, materials.FoamBottom);
            var sphere = GetOrCreateMeshAsset(MountainGeometryRoot + "/SpraySphere6x4.asset", () => CreateUvSphereMesh(1f, 6, 4));
            foreach (var spray in document.waterfallVisuals.sprayPuffs)
            {
                var visual = CreateMeshVisual($"Spray_{spray.index:00}", root.transform,
                    ToMountainVector(spray.position), sphere, materials.Spray);
                visual.transform.localScale = ToMountainVector(spray.scale);
            }
        }

        private static void CreateMountainWaterfallPlane(
            string name,
            Transform parent,
            WofMountainWaterfallPlaneRecord plane,
            Material material)
        {
            var visual = MountainBox(name, parent, ToMountainVector(plane.position),
                new Vector3(plane.width, plane.height, 0.035f), material);
            visual.transform.localRotation = Quaternion.Euler(0f, plane.rotation[1] * Mathf.Rad2Deg, 0f);
        }

        private static void CreateMountainFoam(
            string name,
            Transform parent,
            WofMountainWaterfallFoamRecord record,
            Mesh mesh,
            Material material)
        {
            var visual = CreateMeshVisual(name, parent, ToMountainVector(record.position), mesh, material);
            visual.transform.localScale = new Vector3(record.scale[0], 1f, record.scale[1]);
        }

        private static void CreateMountainCabins(
            Transform parent,
            WofMountainVillageDocument document,
            MountainMaterialSet materials)
        {
            var root = new GameObject("MountainSummitCabins");
            root.transform.SetParent(parent, false);
            foreach (var cabin in document.layout.cabins)
                CreateMountainCabin(root.transform, cabin, document.summitY, false, null, materials);
        }

        private static void CreateMountainCabin(
            Transform parent,
            WofMountainCabinMetricsRecord cabin,
            float floorY,
            bool compact,
            WofMountainInteriorPlatformRecord platform,
            MountainMaterialSet materials)
        {
            var item = new GameObject(cabin.key);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(cabin.localX, floorY, cabin.localZ);
            item.transform.localRotation = Quaternion.Euler(0f, cabin.rotation * Mathf.Rad2Deg, 0f);
            var wallThickness = Mathf.Min(1.05f, cabin.width * 0.12f, cabin.depth * 0.12f);
            var doorWidth = Mathf.Min(6.2f, cabin.width - wallThickness * 4f);
            var doorHeight = Mathf.Min(7.4f, cabin.height - 1.15f);
            var frontWallWidth = Mathf.Max(1.05f, (cabin.width - doorWidth) * 0.5f);
            var lintelHeight = Mathf.Max(0.75f, cabin.height - doorHeight);
            var frontZ = cabin.depth * 0.5f - wallThickness * 0.5f;
            var backZ = -cabin.depth * 0.5f + wallThickness * 0.5f;
            var localFloor = compact ? 0.48f : 0f;
            var body = MountainMaterial(cabin.bodyColor);
            var roof = MountainMaterial(cabin.roofColor);
            var accent = MountainMaterial(cabin.accentColor, compact ? 0.9f : 0.88f);

            if (compact)
            {
                MountainBox("BackShadow", item.transform,
                    new Vector3(0f, cabin.height * 0.5f + localFloor, backZ - 0.42f),
                    new Vector3(cabin.width + 1.6f, cabin.height + 1.1f, 0.7f),
                    MountainMaterial("#16100c", 0.88f));
                MountainBox("FrontFloorShadow", item.transform,
                    new Vector3(0f, localFloor + 0.42f, cabin.depth * 0.5f + 0.28f),
                    new Vector3(cabin.width + 1f, 0.18f, 0.2f), MountainMaterial("#060403", 0.86f));
            }
            else
            {
                MountainBox("Foundation", item.transform, new Vector3(0f, 0.18f, 0f),
                    new Vector3(cabin.width + 0.8f, 0.36f, cabin.depth + 0.8f), MountainMaterial("#4b3826"));
                MountainBox("FrontFoundationShadow", item.transform,
                    new Vector3(0f, 0.42f, cabin.depth * 0.5f + 0.52f),
                    new Vector3(cabin.width + 1.15f, 0.18f, 0.24f), MountainMaterial("#080504", 0.78f));
                MountainBox("BackFoundationShadow", item.transform,
                    new Vector3(0f, 0.38f, -cabin.depth * 0.5f - 0.44f),
                    new Vector3(cabin.width + 0.7f, 0.14f, 0.22f), MountainMaterial("#080504", 0.56f));
            }
            MountainBox("WallLeft", item.transform,
                new Vector3(-cabin.width * 0.5f + wallThickness * 0.5f, cabin.height * 0.5f + localFloor, 0f),
                new Vector3(wallThickness, cabin.height, cabin.depth), body);
            MountainBox("WallRight", item.transform,
                new Vector3(cabin.width * 0.5f - wallThickness * 0.5f, cabin.height * 0.5f + localFloor, 0f),
                new Vector3(wallThickness, cabin.height, cabin.depth), body);
            MountainBox("WallBack", item.transform, new Vector3(0f, cabin.height * 0.5f + localFloor, backZ),
                new Vector3(cabin.width, cabin.height, wallThickness), body);
            MountainBox("WallFrontLeft", item.transform,
                new Vector3(-doorWidth * 0.5f - frontWallWidth * 0.5f, cabin.height * 0.5f + localFloor, frontZ),
                new Vector3(frontWallWidth, cabin.height, wallThickness), body);
            MountainBox("WallFrontRight", item.transform,
                new Vector3(doorWidth * 0.5f + frontWallWidth * 0.5f, cabin.height * 0.5f + localFloor, frontZ),
                new Vector3(frontWallWidth, cabin.height, wallThickness), body);
            MountainBox("Lintel", item.transform,
                new Vector3(0f, doorHeight + lintelHeight * 0.5f + localFloor, frontZ),
                new Vector3(doorWidth, lintelHeight, wallThickness), body);
            MountainBox("Door", item.transform,
                new Vector3(
                    0f,
                    doorHeight * (compact ? 0.5f : 0.46f) + localFloor,
                    compact ? frontZ + 0.16f : cabin.depth * 0.5f + 0.16f),
                new Vector3(doorWidth * (compact ? 0.76f : 0.84f), doorHeight * (compact ? 0.78f : 0.86f), compact ? 0.24f : 0.32f),
                MountainMaterial(compact ? "#1c130d" : "#4c2e1a"));

            if (!compact)
            {
                MountainBox("DoorFrameLeft", item.transform,
                    new Vector3(-doorWidth * 0.5f - 0.28f, doorHeight * 0.5f, cabin.depth * 0.5f + 0.12f),
                    new Vector3(0.56f, doorHeight, 0.62f), MountainMaterial("#251a12"));
                MountainBox("DoorFrameRight", item.transform,
                    new Vector3(doorWidth * 0.5f + 0.28f, doorHeight * 0.5f, cabin.depth * 0.5f + 0.12f),
                    new Vector3(0.56f, doorHeight, 0.62f), MountainMaterial("#251a12"));
                MountainBox("DoorFrameTop", item.transform,
                    new Vector3(0f, doorHeight + 0.28f, cabin.depth * 0.5f + 0.12f),
                    new Vector3(doorWidth + 1.1f, 0.56f, 0.62f), MountainMaterial("#251a12"));
                MountainBox("BackPanel", item.transform, new Vector3(0f, 3.25f, backZ + 0.08f),
                    new Vector3(doorWidth * 0.86f, 5.1f, 0.22f), MountainMaterial("#251a12"));
            }

            var cone = GetOrCreateMeshAsset(MountainGeometryRoot + "/CabinRoofCone4.asset", () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            var roofHeight = compact ? 6.2f : 9.2f;
            var roofRadius = Mathf.Max(cabin.width, cabin.depth) * (compact ? 0.76f : 0.78f);
            var roofVisual = CreateMeshVisual("Roof", item.transform,
                new Vector3(0f, cabin.height + (compact ? 2.8f : 4.2f) + localFloor, 0f), cone, roof);
            roofVisual.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roofVisual.transform.localScale = new Vector3(roofRadius, roofHeight, roofRadius);
            var snowHeight = compact ? 2.3f : 3.4f;
            var snowRadius = Mathf.Max(cabin.width, cabin.depth) * (compact ? 0.3f : 0.34f);
            var snow = CreateMeshVisual("RoofSnow", item.transform,
                new Vector3(0f, cabin.height + (compact ? 6f : 8.9f) + localFloor, 0f), cone, compact ? MountainMaterial("#cfe6f3") : MountainMaterial("#f8fdff"));
            snow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            snow.transform.localScale = new Vector3(snowRadius, snowHeight, snowRadius);

            CreateMountainHutWallDetails(item.transform, cabin, localFloor, frontZ, backZ, doorWidth, doorHeight, compact);
            CreateMountainHutRoofDetails(item.transform, cabin,
                cabin.height + localFloor + (compact ? 0.25f : 0.35f), compact ? 5.2f : 7.8f, compact);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * cabin.width * (compact ? 0.28f : 0.27f);
                var y = (compact ? 4.6f : 5.9f) + localFloor;
                MountainBox($"Window_{side}", item.transform, new Vector3(x, y, frontZ + 0.18f),
                    compact ? new Vector3(2f, 1.8f, 0.26f) : new Vector3(3.4f, 2.8f, 0.36f), accent);
                CreateMountainWindowDetails(item.transform, x, y, compact ? frontZ + 0.34f : cabin.depth * 0.5f + 0.4f,
                    compact ? 1.75f : 3.1f, compact ? 1.55f : 2.5f);
            }
            if (!compact)
            {
                MountainBox("Chimney", item.transform,
                    new Vector3(0f, cabin.height + 2.4f, cabin.depth * 0.18f),
                    new Vector3(2.2f, 5.4f, 2.2f), MountainMaterial("#3b2b1d"));
                MountainBox("ChimneySnow", item.transform,
                    new Vector3(0f, cabin.height + 5.4f, cabin.depth * 0.18f),
                    new Vector3(3.2f, 1.2f, 3.2f), MountainMaterial("#d8edf8"));
                MountainBox("ChimneyFrontShadow", item.transform,
                    new Vector3(0f, cabin.height + 4.82f, cabin.depth * 0.18f + 1.72f),
                    new Vector3(3.55f, 0.18f, 0.2f), MountainMaterial("#080504", 0.62f));
            }
            else if (platform != null && cabin is WofMountainInteriorHutRecord interiorHut)
            {
                CreateMountainLantern(item.transform,
                    new Vector3(doorWidth * 0.5f + 1.35f, 3.55f + localFloor, frontZ + 0.34f),
                    0.62f, false);
                var sphere = GetOrCreateMeshAsset(
                    MountainGeometryRoot + "/BanquetSphere10x6.asset",
                    () => CreateUvSphereMesh(1f, 10, 6));
                var orb = CreateMeshVisual("PlatformOrb", item.transform,
                    new Vector3(0f, 2.8f, platform.platformZ + interiorHut.platformDepth * 0.28f),
                    sphere, MountainMaterial("#ffd47a", 0.86f));
                orb.transform.localScale = Vector3.one * 0.78f;
                MountainBox("PlatformOrbBase", item.transform,
                    new Vector3(0f, 2.08f, platform.platformZ + interiorHut.platformDepth * 0.28f),
                    new Vector3(1.16f, 0.14f, 1.16f), MountainMaterial("#080504", 0.72f));
            }
            CreateMountainCabinColliders(item, cabin, localFloor, wallThickness, doorWidth, doorHeight, frontWallWidth, lintelHeight, frontZ, backZ);
        }

        private static void CreateMountainCabinColliders(
            GameObject owner,
            WofMountainCabinMetricsRecord cabin,
            float floorY,
            float wallThickness,
            float doorWidth,
            float doorHeight,
            float frontWallWidth,
            float lintelHeight,
            float frontZ,
            float backZ)
        {
            CreateMountainBoxCollider(owner,
                new Vector3(-cabin.width * 0.5f + wallThickness * 0.5f, cabin.height * 0.5f + floorY, 0f),
                new Vector3(wallThickness, cabin.height, cabin.depth));
            CreateMountainBoxCollider(owner,
                new Vector3(cabin.width * 0.5f - wallThickness * 0.5f, cabin.height * 0.5f + floorY, 0f),
                new Vector3(wallThickness, cabin.height, cabin.depth));
            CreateMountainBoxCollider(owner, new Vector3(0f, cabin.height * 0.5f + floorY, backZ),
                new Vector3(cabin.width, cabin.height, wallThickness));
            CreateMountainBoxCollider(owner,
                new Vector3(-doorWidth * 0.5f - frontWallWidth * 0.5f, cabin.height * 0.5f + floorY, frontZ),
                new Vector3(frontWallWidth, cabin.height, wallThickness));
            CreateMountainBoxCollider(owner,
                new Vector3(doorWidth * 0.5f + frontWallWidth * 0.5f, cabin.height * 0.5f + floorY, frontZ),
                new Vector3(frontWallWidth, cabin.height, wallThickness));
            CreateMountainBoxCollider(owner,
                new Vector3(0f, doorHeight + lintelHeight * 0.5f + floorY, frontZ),
                new Vector3(doorWidth, lintelHeight, wallThickness));
        }

        private static void CreateMountainVillagers(
            Transform parent,
            WofMountainVillageDocument document,
            Material material)
        {
            var root = new GameObject("ReactMountainVillageVillagers");
            root.transform.SetParent(parent, false);
            var billboards = new WofVillagerBillboard[document.villagers.Length];
            for (var index = 0; index < document.villagers.Length; index++)
            {
                var record = document.villagers[index];
                if (record == null || string.IsNullOrWhiteSpace(record.id) ||
                    string.IsNullOrWhiteSpace(record.archiveFile) || string.IsNullOrWhiteSpace(record.displayName) ||
                    string.IsNullOrWhiteSpace(record.townId))
                {
                    throw new InvalidOperationException($"Invalid exact React mountain villager record at index {index}.");
                }
                var villager = new GameObject($"MountainVillager_{index:00}");
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

        private static GameObject MountainBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            return CreateVisualPrimitive(name, PrimitiveType.Cube, parent, position, scale, material);
        }

        private static Vector3 ToMountainVector(float[] values)
        {
            if (values == null || values.Length != 3) throw new InvalidOperationException("Invalid mountain vector record.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ToMountainEuler(float[] values)
        {
            if (values == null || values.Length != 3) throw new InvalidOperationException("Invalid mountain Euler record.");
            return Quaternion.Euler(values[0] * Mathf.Rad2Deg, values[1] * Mathf.Rad2Deg, values[2] * Mathf.Rad2Deg);
        }

        private static void CreateMountainBoxCollider(GameObject owner, Vector3 center, Vector3 size)
        {
            var collider = owner.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private sealed class MountainMaterialSet
        {
            public Material Terrain;
            public Material Grass;
            public Material TrailDeck;
            public Material TrailTop;
            public Material TrailDark;
            public Material DarkWood;
            public Material DeepDarkWood;
            public Material MidWood;
            public Material LightWood;
            public Material Snow;
            public Material Ice;
            public Material Shaft;
            public Material Waterfall;
            public Material WaterfallBright;
            public Material WaterfallDark;
            public Material FoamTop;
            public Material FoamBottom;
            public Material Spray;
        }
    }
}
