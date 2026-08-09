using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static void CreateGraveyardChapel(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("giant-catholic-chapel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, document.baseHeight, 0f);
            var stone = GraveyardTextureMaterial("ChapelStone", "GraveyardVillage/Textures/chapel-stone.png",
                new Vector2(3f, 2f));
            var darkStone = GraveyardTextureMaterial("ChapelDarkStone",
                "GraveyardVillage/Textures/chapel-dark-stone.png", new Vector2(2f, 3f));

            GraveyardBox("CenterFoundation", root.transform, new Vector3(0f, 0.48f, 0f),
                new Vector3(118f, 0.96f, 172f), GraveyardMaterial("ChapelCenterFoundation", HexColor("#17141a")));
            foreach (var side in new[] { -1f, 1f })
                GraveyardBox($"WingFoundation_{side}", root.transform, new Vector3(side * 88f, 0.44f, 0f),
                    new Vector3(78f, 0.88f, 122f), GraveyardMaterial("ChapelWingFoundation", HexColor("#161319")));

            CreateGraveyardChapelInterior(root.transform, document, materials);
            foreach (var wall in document.chapel.wallSegments)
                GraveyardBox("chapel-wall-" + wall.key, root.transform,
                    ToGraveyardVector(wall.position), ToGraveyardVector(wall.size), stone);
            CreateGraveyardChapelCeiling(root.transform, document, darkStone);
            CreateGraveyardChapelRoofsAndTower(root.transform, darkStone);
            CreateGraveyardWatchTowers(root.transform, document, stone, darkStone);
            CreateGraveyardExitShadows(root.transform, document);
            CreateGraveyardDoors(root.transform);
            CreateGraveyardEntryRamps(root.transform, document);
            CreateGraveyardChapelWindows(root.transform);
            CreateGraveyardChapelButtresses(root.transform, darkStone);
            CreateGraveyardGargoyles(root.transform, document);
            CreateGraveyardCrack(root.transform, "CrackInterior", new Vector3(-32f, 24f, 78.2f),
                Vector3.zero, 1.12f);
            CreateGraveyardCrack(root.transform, "CrackExterior", new Vector3(28f, 18f, 83.92f),
                Vector3.zero, 0.86f);
            CreateGraveyardChapelLight(root.transform, "EntranceLight", new Vector3(0f, 18f, 78f),
                HexColor("#f8d477"), 2.6f, 52f);
            CreateGraveyardChapelLight(root.transform, "NaveLight", new Vector3(0f, 18f, -36f),
                HexColor("#f9cf71"), 2.4f, 64f);
        }

        private static void CreateGraveyardChapelCeiling(
            Transform parent,
            WofGraveyardVillageDocument document,
            Material darkStone)
        {
            var root = new GameObject("chapel-roof-and-ceiling-fill");
            root.transform.SetParent(parent, false);
            GraveyardBox("NaveCeiling", root.transform, new Vector3(0f, 35.4f, 0f),
                new Vector3(103f, 2.4f, 158f), darkStone);
            foreach (var side in new[] { -1f, 1f })
                GraveyardBox($"WingCeiling_{side}", root.transform, new Vector3(side * 88f, 35.1f, 0f),
                    new Vector3(64f, 2.1f, 109f), darkStone);
            var rows = new[] { -66f, -44f, -22f, 0f, 22f, 44f, 66f };
            for (var index = 0; index < rows.Length; index++)
                GraveyardBox($"NaveCeilingBeam_{index}", root.transform, new Vector3(0f, 33.65f, rows[index]),
                    new Vector3(100f, 2.1f, 1.8f),
                    GraveyardMaterial(index % 2 == 0 ? "CeilingBeamA" : "CeilingBeamB",
                        HexColor(index % 2 == 0 ? "#171017" : "#241821")));
            foreach (var beam in document.chapel.wingCeilingBeams)
                GraveyardBox(beam.key, root.transform, new Vector3(beam.side * 88f, 33.45f, beam.z),
                    new Vector3(60f, 1.7f, 1.55f), GraveyardMaterial("WingBeam_" + beam.color.TrimStart('#'),
                        HexColor(beam.color)));
            GraveyardBox("NaveSpine", root.transform, new Vector3(0f, 34.6f, 0f),
                new Vector3(3.4f, 3f, 154f), GraveyardMaterial("CeilingSpine", HexColor("#100b10")));
            GraveyardBox("CrossSpine", root.transform, new Vector3(0f, 34.45f, 0f),
                new Vector3(226f, 2.6f, 3.2f), GraveyardMaterial("CeilingSpine", HexColor("#100b10")));
        }

        private static void CreateGraveyardChapelRoofsAndTower(Transform parent, Material darkStone)
        {
            var cone4 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/ChapelCone4.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            var roof = GraveyardMaterial("ChapelRoof", HexColor("#171319"));
            var main = CreateMeshVisual("NaveRoof", parent, new Vector3(0f, 39.2f, 0f), cone4, roof);
            main.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            main.transform.localScale = new Vector3(82f, 23f, 82f);
            foreach (var side in new[] { -1f, 1f })
            {
                var wing = CreateMeshVisual($"WingRoof_{side}", parent, new Vector3(side * 88f, 31.8f, 0f),
                    cone4, GraveyardMaterial("ChapelWingRoof", HexColor("#141016")));
                wing.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                wing.transform.localScale = new Vector3(49f, 16f, 49f);
                GraveyardBox($"TowerLongPier_{side}", parent, new Vector3(side * 17.3f, 33.2f, 62f),
                    new Vector3(2.6f, 66.4f, 42f), darkStone);
                GraveyardBox($"TowerFrontPier_{side}", parent, new Vector3(side * 13.7f, 33.2f, 78.5f),
                    new Vector3(5.4f, 66.4f, 2.6f), darkStone);
                GraveyardBox($"TowerRearPier_{side}", parent, new Vector3(side * 13.7f, 33.2f, 41.5f),
                    new Vector3(5.4f, 66.4f, 2.4f), darkStone);
            }
            GraveyardBox("TowerFrontCenter", parent, new Vector3(0f, 46.4f, 78.5f),
                new Vector3(22f, 40f, 2.6f), darkStone);
            GraveyardBox("TowerRearCenter", parent, new Vector3(0f, 46.4f, 41.5f),
                new Vector3(22f, 40f, 2.4f), darkStone);
            var spire = CreateMeshVisual("BellTowerSpire", parent, new Vector3(0f, 72.5f, 62f), cone4,
                GraveyardMaterial("BellTowerSpire", HexColor("#0e0b10")));
            spire.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            spire.transform.localScale = new Vector3(22f, 34f, 22f);
            GraveyardBox("BellCrossVertical", parent, new Vector3(0f, 96.2f, 62f),
                new Vector3(2.4f, 22f, 2.4f), GraveyardMaterial("BellCross", HexColor("#050505")));
            GraveyardBox("BellCrossHorizontal", parent, new Vector3(0f, 100.8f, 62f),
                new Vector3(13.5f, 2.4f, 2.4f), GraveyardMaterial("BellCross", HexColor("#050505")));
        }

        private static void CreateGraveyardWatchTowers(
            Transform parent,
            WofGraveyardVillageDocument document,
            Material stone,
            Material darkStone)
        {
            var bodyMesh = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/WatchTowerBody8.asset",
                () => CreateDarrelFrustumMesh(8.8f, 10.2f, 42f, 8));
            var capMesh = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/WatchTowerCap8.asset",
                () => CreateDarrelFrustumMesh(11f, 11f, 3.3f, 8));
            var roofMesh = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/WatchTowerRoof4.asset",
                () => CreateDarrelFrustumMesh(0f, 10f, 12.8f, 4));
            for (var towerIndex = 0; towerIndex < document.chapel.watchTowerPositions.Length; towerIndex++)
            {
                var position = document.chapel.watchTowerPositions[towerIndex];
                var root = new GameObject($"chapel-corner-watch-tower-{towerIndex}");
                root.transform.SetParent(parent, false);
                root.transform.localPosition = new Vector3(position.x, position.y, position.z);
                CreateMeshVisual("Body", root.transform, Vector3.zero, bodyMesh, stone);
                CreateMeshVisual("Cap", root.transform, new Vector3(0f, 22.65f, 0f), capMesh, darkStone);
                for (var index = 0; index < 8; index++)
                {
                    var angle = index / 8f * Mathf.PI * 2f;
                    var crenel = GraveyardBox($"Crenel_{index}", root.transform,
                        new Vector3(Mathf.Sin(angle) * 10.7f, 26.4f, Mathf.Cos(angle) * 10.7f),
                        new Vector3(3.1f, 4.7f, 2.4f), darkStone);
                    crenel.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                }
                var roof = CreateMeshVisual("Roof", root.transform, new Vector3(0f, 33f, 0f), roofMesh,
                    GraveyardMaterial("WatchTowerRoof", HexColor("#0d0a0f")));
                roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                foreach (var side in new[] { -1f, 1f })
                {
                    GraveyardBox($"FrontArrowSlit_{side}", root.transform, new Vector3(side * 5.2f, 4.8f, 8.88f),
                        new Vector3(1.2f, 9.8f, 0.22f), GraveyardMaterial("ArrowSlit", HexColor("#09070a")));
                    GraveyardBox($"SideArrowSlit_{side}", root.transform, new Vector3(8.88f, 4.8f, side * 5.2f),
                        new Vector3(0.22f, 9.8f, 1.2f), GraveyardMaterial("ArrowSlit", HexColor("#09070a")));
                }
            }
        }

        private static void CreateGraveyardExitShadows(Transform parent, WofGraveyardVillageDocument document)
        {
            var material = GraveyardMaterial("ExitShadow", new Color(0.02f, 0.016f, 0.012f, 0.16f), true);
            foreach (var shadow in document.chapel.exitShadows)
            {
                var visual = GraveyardBox("chapel-exit-shadow-" + shadow.key, parent,
                    ToGraveyardVector(shadow.position), ToGraveyardVector(shadow.size), material);
                visual.transform.localRotation = ToGraveyardEuler(shadow.rotation);
            }
        }

        private static void CreateGraveyardDoors(Transform parent)
        {
            CreateGraveyardDoubleDoor(parent, "SouthDoor", new Vector3(0f, 0f, 83.35f), 0f, 26f, 23f);
            CreateGraveyardDoubleDoor(parent, "NorthWestDoor", new Vector3(-33f, 0f, -83.35f), Mathf.PI, 19f, 21f);
            CreateGraveyardDoubleDoor(parent, "NorthEastDoor", new Vector3(33f, 0f, -83.35f), Mathf.PI, 19f, 21f);
            CreateGraveyardDoubleDoor(parent, "EastDoor", new Vector3(123.35f, 0f, 0f), Mathf.PI * 0.5f, 23f, 21f);
            CreateGraveyardDoubleDoor(parent, "WestDoor", new Vector3(-123.35f, 0f, 0f), -Mathf.PI * 0.5f, 23f, 21f);
        }

        private static void CreateGraveyardDoubleDoor(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float width,
            float height)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
            var panelWidth = width * 0.48f;
            var hingeInset = panelWidth * 0.5f;
            foreach (var side in new[] { -1f, 1f })
            {
                var panelRoot = new GameObject($"DoorPanel_{side}");
                panelRoot.transform.SetParent(root.transform, false);
                panelRoot.transform.localPosition = new Vector3(side * (width * 0.5f - 0.7f), 1.2f + height * 0.5f, 0.65f);
                panelRoot.transform.localRotation = Quaternion.Euler(0f, side * -0.44f * Mathf.Rad2Deg, 0f);
                GraveyardBox("Panel", panelRoot.transform, new Vector3(side * -hingeInset * 0.45f, 0f, 0f),
                    new Vector3(panelWidth, height, 1.05f), GraveyardMaterial("DoorPanel", HexColor("#5b351f")));
                foreach (var offset in new[] { -0.24f, 0.24f })
                    GraveyardBox($"Plank_{offset}", panelRoot.transform,
                        new Vector3(side * (-hingeInset * 0.45f + offset * panelWidth), 0f, 0.58f),
                        new Vector3(0.28f, height - 1.8f, 0.18f),
                        GraveyardMaterial("DoorPlank", new Color(0.478f, 0.286f, 0.157f, 0.76f), true));
                foreach (var yOffset in new[] { -0.34f, 0.34f })
                    GraveyardBox($"Strap_{yOffset}", panelRoot.transform,
                        new Vector3(side * -hingeInset * 0.45f, yOffset * height, 0.7f),
                        new Vector3(panelWidth - 1.5f, 0.52f, 0.24f),
                        GraveyardMaterial("DoorStrap", HexColor("#1c1410")));
                GraveyardBox("Hinge", panelRoot.transform, new Vector3(side * -hingeInset * 0.9f, 0f, 0.78f),
                    new Vector3(0.5f, height + 1.2f, 0.34f), GraveyardMaterial("DoorHinge", HexColor("#24150d")));
                CreateGraveyardDoorKnocker(panelRoot.transform, side * -hingeInset * 0.42f);
            }
        }

        private static void CreateGraveyardDoorKnocker(Transform parent, float x)
        {
            var root = new GameObject("LionDoorKnocker");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, 1.4f, 0.68f);
            var sphere = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/KnockerSphere.asset",
                () => CreateUvSphereMesh(1f, 12, 8));
            CreateScaledMesh("LionHead", root.transform, new Vector3(0f, 1.34f, 0f),
                new Vector3(0.819f, 0.6396f, 0.156f), sphere, GraveyardMaterial("KnockerBronze", HexColor("#a66f2f")));
            CreateScaledMesh("LionMuzzle", root.transform, new Vector3(0f, 1.36f, 0.16f),
                new Vector3(0.406f, 0.252f, 0.112f), sphere, GraveyardMaterial("KnockerGold", HexColor("#d19a45")));
            CreateScaledMesh("LionJaw", root.transform, new Vector3(0f, 0.9f, 0.22f),
                new Vector3(0.31f, 0.1364f, 0.0744f), sphere, GraveyardMaterial("KnockerDark", HexColor("#6b421e")));
            var cone = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/KnockerEar4.asset",
                () => CreateDarrelFrustumMesh(0f, 0.24f, 0.7f, 4));
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = CreateMeshVisual($"Ear_{side}", root.transform, new Vector3(side * 0.52f, 1.88f, 0.08f),
                    cone, GraveyardMaterial("KnockerEar", HexColor("#7e5228")));
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, side * 0.46f * Mathf.Rad2Deg);
                GraveyardBox($"Eye_{side}", root.transform, new Vector3(side * 0.22f, 1.48f, 0.34f),
                    new Vector3(0.12f, 0.12f, 0.08f), GraveyardMaterial("KnockerEye", HexColor("#1b1008")));
            }
            var halfTorus = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/KnockerHalfTorus.asset",
                () => CreateGraveyardTorusArcMesh(1.02f, 0.12f, 18, 8, Mathf.PI));
            var ring = CreateMeshVisual("Ring", root.transform, new Vector3(0f, -0.36f, 0.14f), halfTorus,
                GraveyardMaterial("KnockerRing", HexColor("#c68a35")));
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            GraveyardBox("RingMount", root.transform, new Vector3(0f, 0.2f, 0.17f),
                new Vector3(0.42f, 0.34f, 0.16f), GraveyardMaterial("KnockerMount", HexColor("#7a4b21")));
        }

        private static void CreateGraveyardEntryRamps(Transform parent, WofGraveyardVillageDocument document)
        {
            foreach (var exit in document.chapel.exitRamps)
            {
                var root = new GameObject("chapel-entry-ramp-" + exit.key);
                root.transform.SetParent(parent, false);
                root.transform.localPosition = ToGraveyardVector(exit.position);
                root.transform.localRotation = Quaternion.Euler(0f, exit.rotation * Mathf.Rad2Deg, 0f);
                var slope = Mathf.Atan2(exit.top - 0.02f, 44f);
                var centerY = (exit.top + 0.02f) * 0.5f - 0.41f * Mathf.Cos(slope);
                var centerZ = exit.distance + exit.outset + 22f - 0.25f;
                var ramp = GraveyardBox("SmoothRamp", root.transform, new Vector3(0f, centerY, centerZ),
                    new Vector3(exit.width, 0.82f, 44f), GraveyardMaterial("EntryRamp", HexColor("#3a3530")));
                ramp.transform.localRotation = Quaternion.Euler(slope * Mathf.Rad2Deg, 0f, 0f);
                GraveyardBox("RampLanding", root.transform, new Vector3(0f, 0.01f, centerZ + 23.2f),
                    new Vector3(exit.width + 4f, 0.02f, 5.2f), GraveyardMaterial("EntryLanding", HexColor("#302b26")));
            }
        }

        private static void CreateGraveyardChapelWindows(Transform parent)
        {
            CreateGraveyardGothicWindow(parent, "FrontCenterWindow", new Vector3(0f, 42.2f, 83.55f),
                Vector3.zero, 1.2f, 0);
            foreach (var side in new[] { -1f, 1f })
                CreateGraveyardGothicWindow(parent, $"FrontWindow_{side}", new Vector3(side * 35.5f, 23.4f, 83.5f),
                    Vector3.zero, 0.74f, side > 0 ? 1 : 2);
            var disk16 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/VerticalDisk16.asset",
                () => CreateGraveyardVerticalDiskMesh(1f, 16));
            CreateScaledMesh("RoseOuter", parent, new Vector3(0f, 54.8f, 83.36f), new Vector3(7.4f, 7.4f, 1f),
                disk16, GraveyardMaterial("RoseOuter", HexColor("#0f172a")));
            CreateScaledMesh("RoseGlass", parent, new Vector3(0f, 54.8f, 83.18f), new Vector3(5.6f, 5.6f, 1f),
                disk16, GraveyardMaterial("RoseGlass", new Color(0.659f, 0.333f, 0.969f, 0.84f), true));
            GraveyardBox("RoseCrossH", parent, new Vector3(0f, 54.8f, 82.94f),
                new Vector3(10.2f, 0.62f, 0.14f), GraveyardMaterial("RoseCross", HexColor("#fde68a")));
            GraveyardBox("RoseCrossV", parent, new Vector3(0f, 54.8f, 82.9f),
                new Vector3(0.62f, 10.2f, 0.14f), GraveyardMaterial("RoseCross", HexColor("#fde68a")));
            CreateGraveyardGothicWindow(parent, "RearWindow", new Vector3(0f, 25.8f, -83.5f),
                new Vector3(0f, Mathf.PI, 0f), 1.05f, 2);
            foreach (var side in new[] { -1f, 1f })
            {
                var wingZ = new[] { -34f, 34f };
                for (var index = 0; index < wingZ.Length; index++)
                    CreateGraveyardGothicWindow(parent, $"WingWindow_{side}_{index}",
                        new Vector3(side * 123.55f, 24.2f, wingZ[index]),
                        new Vector3(0f, side * Mathf.PI * 0.5f, 0f), 0.98f, index + (side > 0 ? 1 : 0));
                var naveZ = new[] { -68f, 68f };
                for (var index = 0; index < naveZ.Length; index++)
                    CreateGraveyardGothicWindow(parent, $"NaveWindow_{side}_{index}",
                        new Vector3(side * 55.48f, 24.6f, naveZ[index]),
                        new Vector3(0f, side * Mathf.PI * 0.5f, 0f), 0.9f, index + 2);
            }
        }

        private static void CreateGraveyardGothicWindow(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 rotationRadians,
            float scale,
            int variant)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(rotationRadians * Mathf.Rad2Deg);
            root.transform.localScale = Vector3.one * scale;
            var outer = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/GothicArchOuter.asset",
                () => CreateGraveyardGothicArchMesh(13.4f, 26.8f));
            var inner = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/GothicArchInner.asset",
                () => CreateGraveyardGothicArchMesh(10.4f, 23.2f));
            CreateMeshVisual("OuterStone", root.transform, Vector3.zero, outer,
                GraveyardMaterial("GothicOuter", HexColor("#121019")));
            CreateMeshVisual("InnerBlue", root.transform, new Vector3(0f, -0.35f, 0.08f), inner,
                GraveyardMaterial("GothicBlue", new Color(0.114f, 0.306f, 0.722f, 0.74f), true));
            var glowHex = variant % 3 == 0 ? "#38bdf8" : variant % 3 == 1 ? "#a78bfa" : "#f472b6";
            CreateMeshVisual("Glow", root.transform, new Vector3(0f, -0.35f, 0.12f), inner,
                GraveyardMaterial("GothicGlow_" + glowHex.TrimStart('#'),
                    GraveyardWithAlpha(HexColor(glowHex), 0.48f), true));
            foreach (var x in new[] { -3.15f, 0f, 3.15f })
                GraveyardBox($"Mullion_{x}", root.transform, new Vector3(x, -1.95f, 0.2f),
                    new Vector3(0.42f, 17.4f, 0.22f), GraveyardMaterial("GothicMullion", HexColor("#efe5c7")));
            foreach (var x in new[] { -4.8f, 4.8f })
                GraveyardBox($"SideRib_{x}", root.transform, new Vector3(x, -1.1f, 0.18f),
                    new Vector3(0.36f, 19.2f, 0.22f), GraveyardMaterial("GothicSideRib", HexColor("#b8ad99")));
            foreach (var x in new[] { -6.15f, 6.15f })
                GraveyardBox($"OuterPier_{x}", root.transform, new Vector3(x, -1.4f, 0.16f),
                    new Vector3(0.62f, 21.8f, 0.28f), GraveyardMaterial("GothicOuterPier", HexColor("#8f897f")));
            GraveyardBox("Sill", root.transform, new Vector3(0f, -8.95f, 0.22f),
                new Vector3(11.8f, 0.62f, 0.26f), GraveyardMaterial("GothicSill", HexColor("#d8cbb1")));
            GraveyardBox("CrossRib", root.transform, new Vector3(0f, -2.25f, 0.24f),
                new Vector3(10.2f, 0.46f, 0.22f), GraveyardMaterial("GothicSideRib", HexColor("#b8ad99")));
            var accentHex = variant % 2 == 0 ? "#fde68a" : "#e9d5ff";
            foreach (var x in new[] { -2.6f, 2.6f })
            {
                var first = GraveyardBox($"LancetA_{x}", root.transform, new Vector3(x, 5.25f, 0.28f),
                    new Vector3(0.36f, 8.2f, 0.2f), GraveyardMaterial("GothicAccent_" + accentHex.TrimStart('#'),
                        GraveyardWithAlpha(HexColor(accentHex), 0.88f), true));
                first.transform.localRotation = Quaternion.Euler(0f, 0f, (x > 0f ? -0.44f : 0.44f) * Mathf.Rad2Deg);
                var second = GraveyardBox($"LancetB_{x}", root.transform, new Vector3(x * 0.58f, 7.7f, 0.3f),
                    new Vector3(0.28f, 5.6f, 0.18f), GraveyardMaterial("GothicLancetLight",
                        new Color(0.843f, 0.816f, 0.741f, 0.9f), true));
                second.transform.localRotation = Quaternion.Euler(0f, 0f, (x > 0f ? -0.78f : 0.78f) * Mathf.Rad2Deg);
            }
            var disk14 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/VerticalDisk14.asset",
                () => CreateGraveyardVerticalDiskMesh(1f, 14));
            var disk10 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/VerticalDisk10.asset",
                () => CreateGraveyardVerticalDiskMesh(1f, 10));
            CreateScaledMesh("RoseAccent", root.transform, new Vector3(0f, 7.25f, 0.32f),
                new Vector3(1.55f, 1.55f, 1f), disk14,
                GraveyardMaterial("GothicAccent_" + accentHex.TrimStart('#'),
                    GraveyardWithAlpha(HexColor(accentHex), 0.72f), true));
            CreateScaledMesh("RoseCore", root.transform, new Vector3(0f, 7.25f, 0.36f),
                new Vector3(0.72f, 0.72f, 1f), disk10,
                GraveyardMaterial("GothicRoseCore", new Color(0.027f, 0.031f, 0.063f, 0.82f), true));
            var strips = new[] { -4.2f, -1.4f, 1.4f, 4.2f };
            for (var index = 0; index < strips.Length; index++)
                GraveyardBox($"GlassStrip_{index}", root.transform, new Vector3(strips[index], -4.85f, 0.34f),
                    new Vector3(1.15f, 6.8f, 0.12f),
                    GraveyardMaterial(index % 2 == 0 ? "GothicStripBlue" : "GothicStripPurple",
                        GraveyardWithAlpha(HexColor(index % 2 == 0 ? "#38bdf8" : "#a78bfa"), 0.76f), true));
        }

        private static void CreateGraveyardChapelButtresses(Transform parent, Material darkStone)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var z in new[] { -46f, -18f, 18f, 46f })
                    GraveyardBox($"WingButtress_{side}_{z}", parent, new Vector3(side * 126f, 12.8f, z),
                        new Vector3(4.2f, 25.6f, 6.2f), darkStone);
                foreach (var z in new[] { -72f, 72f })
                    GraveyardBox($"CentralButtress_{side}_{z}", parent, new Vector3(side * 58f, 12.8f, z),
                        new Vector3(4.2f, 25.6f, 6.2f), darkStone);
            }
        }

        private static void CreateGraveyardGargoyles(Transform parent, WofGraveyardVillageDocument document)
        {
            var cone4 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/GargoyleCone4.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 4));
            var cone5 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/GargoyleCone5.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 5));
            var cone6 = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/GargoyleCone6.asset",
                () => CreateDarrelFrustumMesh(0f, 1f, 1f, 6));
            foreach (var record in document.chapel.gargoyles)
            {
                var root = new GameObject("chapel-roof-gargoyle-" + record.key);
                root.transform.SetParent(parent, false);
                root.transform.localPosition = ToGraveyardVector(record.position);
                root.transform.localRotation = Quaternion.Euler(0f, record.yaw * Mathf.Rad2Deg, 0f);
                root.transform.localScale = Vector3.one * record.scale;
                var body = GraveyardBox("Body", root.transform, new Vector3(0f, 0.7f, 0f),
                    new Vector3(2.3f, 1.65f, 3f), GraveyardMaterial("GargoyleBody", HexColor("#313039")));
                body.transform.localRotation = Quaternion.Euler(-0.18f * Mathf.Rad2Deg, 0f, 0f);
                GraveyardBox("Head", root.transform, new Vector3(0f, 1.72f, 1.26f),
                    new Vector3(1.55f, 1.25f, 1.45f), GraveyardMaterial("GargoyleHead", HexColor("#3c3a43")));
                GraveyardBox("Muzzle", root.transform, new Vector3(0f, 1.58f, 2.1f),
                    new Vector3(0.9f, 0.55f, 1f), GraveyardMaterial("GargoyleMuzzle", HexColor("#232129")));
                foreach (var side in new[] { -1f, 1f })
                {
                    var wing = GraveyardBox($"Wing_{side}", root.transform,
                        new Vector3(side * 1.38f, 1.05f, -0.24f), new Vector3(0.42f, 2.5f, 2.6f),
                        GraveyardMaterial("GargoyleWing", HexColor("#27262e")));
                    wing.transform.localRotation = Quaternion.Euler(0.18f * Mathf.Rad2Deg, 0f, side * 0.72f * Mathf.Rad2Deg);
                    var horn = CreateMeshVisual($"Horn_{side}", root.transform,
                        new Vector3(side * 0.54f, 2.42f, 1.45f), cone4,
                        GraveyardMaterial("GargoyleHorn", HexColor("#191820")));
                    horn.transform.localScale = new Vector3(0.34f, 1.15f, 0.34f);
                    horn.transform.localRotation = Quaternion.Euler(0f, 0f, side * 0.58f * Mathf.Rad2Deg);
                    var foot = GraveyardBox($"Foot_{side}", root.transform,
                        new Vector3(side * 0.66f, -0.16f, 0.96f), new Vector3(0.52f, 1.2f, 0.5f),
                        GraveyardMaterial("GargoyleFoot", HexColor("#1f1e25")));
                    foot.transform.localRotation = Quaternion.Euler(0.28f * Mathf.Rad2Deg, 0f, side * 0.2f * Mathf.Rad2Deg);
                }
                var tail = CreateMeshVisual("Tail", root.transform, new Vector3(0f, 0.38f, -1.96f), cone5,
                    GraveyardMaterial("GargoyleTail", HexColor("#24232b")));
                tail.transform.localScale = new Vector3(0.42f, 2.4f, 0.42f);
                tail.transform.localRotation = Quaternion.Euler(-0.55f * Mathf.Rad2Deg, 0f, 0f);
                var fang = CreateMeshVisual("Fang", root.transform, new Vector3(0f, 2.04f, 2.85f), cone6,
                    GraveyardMaterial("GargoyleFang", HexColor("#18171d")));
                fang.transform.localScale = new Vector3(0.28f, 1.5f, 0.28f);
                fang.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static void CreateGraveyardCrack(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 rotationRadians,
            float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(rotationRadians * Mathf.Rad2Deg);
            root.transform.localScale = Vector3.one * scale;
            var first = GraveyardBox("CrackA", root.transform, Vector3.zero, new Vector3(0.34f, 5.6f, 0.18f),
                GraveyardMaterial("ChapelCrackA", HexColor("#151319")));
            first.transform.localRotation = Quaternion.Euler(0f, 0f, 0.62f * Mathf.Rad2Deg);
            var second = GraveyardBox("CrackB", root.transform, new Vector3(1.1f, -1.7f, -0.04f),
                new Vector3(0.28f, 3.2f, 0.16f), GraveyardMaterial("ChapelCrackB", HexColor("#17151b")));
            second.transform.localRotation = Quaternion.Euler(0f, 0f, -0.9f * Mathf.Rad2Deg);
            var third = GraveyardBox("CrackC", root.transform, new Vector3(-0.9f, 1.9f, -0.04f),
                new Vector3(0.26f, 2.7f, 0.16f), GraveyardMaterial("ChapelCrackB", HexColor("#17151b")));
            third.transform.localRotation = Quaternion.Euler(0f, 0f, -0.72f * Mathf.Rad2Deg);
        }

        private static void CreateGraveyardChapelLight(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static GameObject CreateScaledMesh(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Mesh mesh,
            Material material)
        {
            var visual = CreateMeshVisual(name, parent, position, mesh, material);
            visual.transform.localScale = scale;
            return visual;
        }

        private static Vector3 ToGraveyardVector(float[] values)
        {
            if (values == null || values.Length != 3)
                throw new InvalidOperationException("Invalid exact graveyard vector record.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Vector3 ToGraveyardVector(WofGraveyardVectorRecord value)
        {
            if (value == null) throw new InvalidOperationException("Invalid exact graveyard vector record.");
            return new Vector3(value.x, value.y, value.z);
        }

        private static Quaternion ToGraveyardEuler(float[] values)
        {
            return Quaternion.Euler(ToGraveyardVector(values) * Mathf.Rad2Deg);
        }

        private static Color GraveyardWithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Mesh CreateGraveyardVerticalDiskMesh(float radius, int segments)
        {
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var uv = new List<Vector2>(segments + 1) { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>(segments * 3);
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                uv.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));
            }
            for (var index = 0; index < segments; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add((index + 1) % segments + 1);
            }
            return BuildDarrelMesh("GraveyardVerticalDisk", vertices, uv, triangles);
        }

        private static Mesh CreateGraveyardGothicArchMesh(float width, float height)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            var springY = halfHeight * 0.2f;
            var apexY = halfHeight;
            var outline = new List<Vector2> { new(-halfWidth, -halfHeight), new(halfWidth, -halfHeight), new(halfWidth, springY) };
            const int curveSegments = 12;
            var start = new Vector2(halfWidth, springY);
            var control = new Vector2(halfWidth * 0.84f, apexY * 0.74f);
            var end = new Vector2(0f, apexY);
            for (var index = 1; index <= curveSegments; index++)
            {
                var t = index / (float)curveSegments;
                outline.Add((1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end);
            }
            start = end;
            control = new Vector2(-halfWidth * 0.84f, apexY * 0.74f);
            end = new Vector2(-halfWidth, springY);
            for (var index = 1; index <= curveSegments; index++)
            {
                var t = index / (float)curveSegments;
                outline.Add((1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end);
            }
            var vertices = new List<Vector3>(outline.Count + 1) { Vector3.zero };
            var uv = new List<Vector2>(outline.Count + 1) { new(0.5f, 0.5f) };
            foreach (var point in outline)
            {
                vertices.Add(new Vector3(point.x, point.y, 0f));
                uv.Add(new Vector2(point.x / width + 0.5f, point.y / height + 0.5f));
            }
            var triangles = new List<int>(outline.Count * 3);
            for (var index = 0; index < outline.Count; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add((index + 1) % outline.Count + 1);
            }
            return BuildDarrelMesh("GraveyardGothicArch", vertices, uv, triangles);
        }

        private static Mesh CreateGraveyardTorusArcMesh(
            float majorRadius,
            float minorRadius,
            int radialSegments,
            int tubularSegments,
            float arc)
        {
            var vertices = new List<Vector3>((radialSegments + 1) * (tubularSegments + 1));
            var uv = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(radialSegments * tubularSegments * 6);
            for (var radial = 0; radial <= radialSegments; radial++)
            {
                var angle = radial / (float)radialSegments * arc;
                for (var tubular = 0; tubular <= tubularSegments; tubular++)
                {
                    var tubeAngle = tubular / (float)tubularSegments * Mathf.PI * 2f;
                    var radius = majorRadius + Mathf.Cos(tubeAngle) * minorRadius;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                        Mathf.Sin(tubeAngle) * minorRadius));
                    uv.Add(new Vector2(radial / (float)radialSegments, tubular / (float)tubularSegments));
                }
            }
            var stride = tubularSegments + 1;
            for (var radial = 0; radial < radialSegments; radial++)
            for (var tubular = 0; tubular < tubularSegments; tubular++)
            {
                var current = radial * stride + tubular;
                triangles.Add(current);
                triangles.Add(current + stride);
                triangles.Add(current + 1);
                triangles.Add(current + 1);
                triangles.Add(current + stride);
                triangles.Add(current + stride + 1);
            }
            return BuildDarrelMesh("GraveyardTorusArc", vertices, uv, triangles);
        }
    }
}
