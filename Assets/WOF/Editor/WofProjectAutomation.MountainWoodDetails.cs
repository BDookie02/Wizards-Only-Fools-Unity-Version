using UnityEngine;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static readonly string[] MountainRetroWoodDarkColors = { "#0a0604", "#1a100a", "#2f1d11", "#4d301b" };
        private static readonly string[] MountainRetroWoodLightColors = { "#1b1009", "#3c2415", "#704627", "#b47a3f" };

        private static void CreateMountainVerticalTimberDetails(
            Transform parent,
            float height,
            float width,
            float depth,
            string bandColor = "#a67642",
            string darkColor = "#1d130d",
            string lightColor = "#6f4b2b")
        {
            var z = depth * 0.5f + 0.035f;
            var bandCount = Mathf.Max(2, Mathf.Min(6, Mathf.FloorToInt(height / 5.2f)));
            for (var index = 0; index < bandCount; index++)
            {
                var y = -height * 0.5f + (index + 1f) * height / (bandCount + 1f);
                MountainBox($"TimberBand_{index:00}", parent, new Vector3(0f, y, z),
                    new Vector3(width + 0.28f, 0.28f, 0.12f), MountainMaterial(bandColor));
                foreach (var side in new[] { -1f, 1f })
                {
                    MountainBox($"TimberBolt_{index:00}_{side}", parent,
                        new Vector3(side * width * 0.32f, y + 0.01f, z + 0.07f),
                        new Vector3(0.18f, 0.18f, 0.12f), MountainMaterial("#d7a85e"));
                }
            }
            var offsets = new[] { -0.27f, 0.26f };
            for (var index = 0; index < offsets.Length; index++)
            {
                MountainBox($"TimberGrain_{index}", parent, new Vector3(offsets[index] * width, 0f, z + 0.04f),
                    new Vector3(0.08f, height * 0.86f, 0.08f),
                    MountainMaterial(index == 0 ? darkColor : lightColor, 0.82f));
            }
            CreateMountainPixelWoodTexture(parent, width * 0.82f, height * 0.86f, z + 0.12f,
                Mathf.Max(5, Mathf.Min(14, Mathf.FloorToInt(height / 2.8f))),
                Mathf.FloorToInt(height + width * 5f), true);
            foreach (var side in new[] { -1f, 1f })
            {
                MountainBox($"TimberDarkEdge_{side}", parent, new Vector3(side * (width * 0.5f + 0.03f), 0f, z + 0.02f),
                    new Vector3(0.12f, height * 0.92f, 0.1f), MountainMaterial("#080504", 0.62f));
            }
            MountainBox("TimberBottomShadow", parent, new Vector3(0f, -height * 0.5f + 0.2f, z + 0.05f),
                new Vector3(width + 0.22f, 0.18f, 0.12f), MountainMaterial("#090604", 0.72f));
        }

        private static void CreateMountainHorizontalTimberDetails(
            Transform parent,
            float length,
            float height,
            float depth,
            string bandColor = "#a67642",
            string darkColor = "#21150d")
        {
            var z = depth * 0.5f + 0.035f;
            var bandCount = Mathf.Max(2, Mathf.Min(7, Mathf.FloorToInt(length / 5.8f)));
            for (var index = 0; index < bandCount; index++)
            {
                var x = -length * 0.5f + (index + 1f) * length / (bandCount + 1f);
                MountainBox($"HorizontalBand_{index:00}", parent, new Vector3(x, 0f, z),
                    new Vector3(0.28f, height + 0.22f, 0.13f), MountainMaterial(bandColor));
                MountainBox($"HorizontalBolt_{index:00}", parent, new Vector3(x, height * 0.18f, z + 0.08f),
                    new Vector3(0.18f, 0.18f, 0.12f), MountainMaterial("#d7a85e"));
            }
            var offsets = new[] { -0.2f, 0.22f };
            for (var index = 0; index < offsets.Length; index++)
            {
                MountainBox($"HorizontalGrain_{index}", parent, new Vector3(0f, offsets[index] * height, z + 0.04f),
                    new Vector3(length * 0.86f, 0.08f, 0.08f), MountainMaterial(darkColor, index == 0 ? 0.72f : 0.46f));
            }
            CreateMountainPixelWoodTexture(parent, length * 0.86f, height * 0.86f, z + 0.12f,
                Mathf.Max(6, Mathf.Min(16, Mathf.FloorToInt(length / 2.8f))),
                Mathf.FloorToInt(length + height * 9f), true);
            foreach (var side in new[] { -1f, 1f })
            {
                MountainBox($"HorizontalEndShadow_{side}", parent,
                    new Vector3(side * (length * 0.5f + 0.02f), 0f, z + 0.04f),
                    new Vector3(0.16f, height + 0.16f, 0.12f), MountainMaterial("#080504", 0.68f));
            }
            MountainBox("HorizontalBottomShadow", parent, new Vector3(0f, -height * 0.5f - 0.02f, z + 0.04f),
                new Vector3(length * 0.96f, 0.14f, 0.12f), MountainMaterial("#090604", 0.58f));
        }

        private static void CreateMountainPixelWoodTexture(
            Transform parent,
            float width,
            float height,
            float z,
            int count,
            int seed,
            bool dark)
        {
            var colors = dark ? MountainRetroWoodDarkColors : MountainRetroWoodLightColors;
            for (var index = 0; index < count; index++)
            {
                var t = ((index * 37 + seed * 19) % 100) / 100f;
                var u = ((index * 53 + seed * 11) % 100) / 100f;
                var x = -width * 0.42f + t * width * 0.84f;
                var y = -height * 0.38f + u * height * 0.76f;
                var pieceWidth = width * (0.09f + (index + seed) % 3 * 0.045f);
                var pieceHeight = Mathf.Max(0.08f, height * (0.025f + index % 2 * 0.012f));
                MountainBox($"PixelWood_{index:00}", parent, new Vector3(x, y, z),
                    new Vector3(pieceWidth, pieceHeight, 0.08f),
                    MountainMaterial(colors[(index + seed) % colors.Length], dark ? 0.76f : 0.68f));
            }
            var knotCount = Mathf.Max(2, Mathf.FloorToInt(count / 4f));
            for (var index = 0; index < knotCount; index++)
            {
                var t = ((index * 29 + seed * 7) % 100) / 100f;
                var u = ((index * 41 + seed * 13) % 100) / 100f;
                MountainBox($"PixelKnot_{index:00}", parent,
                    new Vector3(-width * 0.36f + t * width * 0.72f, -height * 0.32f + u * height * 0.64f, z + 0.02f),
                    new Vector3(Mathf.Max(0.28f, width * 0.08f), Mathf.Max(0.18f, height * 0.035f), 0.1f),
                    MountainMaterial("#090604", 0.72f));
            }
        }

        private static void CreateMountainHutWallDetails(
            Transform parent,
            WofMountainCabinMetricsRecord cabin,
            float floorY,
            float frontZ,
            float backZ,
            float doorWidth,
            float doorHeight,
            bool compact)
        {
            var frontPlankCount = compact ? 5 : 7;
            var sidePlankCount = compact ? 4 : 5;
            for (var index = 0; index < frontPlankCount; index++)
            {
                var x = -cabin.width * 0.5f + (index + 1f) * cabin.width / (frontPlankCount + 1f);
                if (Mathf.Abs(x) < doorWidth * 0.5f + 0.55f) continue;
                MountainBox($"FrontPlankSeam_{index:00}", parent,
                    new Vector3(x, floorY + cabin.height * 0.5f, frontZ + 0.2f),
                    new Vector3(0.12f, cabin.height * 0.78f, 0.14f), MountainMaterial("#21160f", 0.72f));
            }
            foreach (var side in new[] { -1f, 1f })
            {
                for (var index = 0; index < sidePlankCount; index++)
                {
                    var z = -cabin.depth * 0.5f + (index + 1f) * cabin.depth / (sidePlankCount + 1f);
                    MountainBox($"SidePlank_{side}_{index:00}", parent,
                        new Vector3(side * (cabin.width * 0.5f + 0.08f), floorY + cabin.height * 0.5f, z),
                        new Vector3(0.12f, cabin.height * 0.72f, 0.1f),
                        MountainMaterial(index % 2 == 0 ? "#241810" : "#7b5332", 0.62f));
                }
            }
            foreach (var band in new[] { floorY + 1.2f, floorY + cabin.height - 1.2f })
            {
                MountainBox($"FrontBand_{band}", parent, new Vector3(0f, band, frontZ + 0.24f),
                    new Vector3(cabin.width + 0.58f, 0.32f, 0.2f),
                    MountainMaterial(band > floorY + cabin.height * 0.5f ? "#805832" : "#2b1c12"));
                MountainBox($"BackBand_{band}", parent, new Vector3(0f, band, backZ - 0.18f),
                    new Vector3(cabin.width + 0.28f, 0.24f, 0.18f), MountainMaterial("#2b1c12"));
            }
            foreach (var side in new[] { -1f, 1f })
            {
                MountainBox($"FrontCornerShadow_{side}", parent,
                    new Vector3(side * (cabin.width * 0.5f + 0.18f), floorY + cabin.height * 0.5f, frontZ + 0.18f),
                    new Vector3(0.34f, cabin.height + 0.44f, 0.22f), MountainMaterial("#0b0705", 0.76f));
                MountainBox($"BackCornerShadow_{side}", parent,
                    new Vector3(side * (cabin.width * 0.5f + 0.12f), floorY + cabin.height * 0.5f, backZ - 0.1f),
                    new Vector3(0.24f, cabin.height * 0.9f, 0.2f), MountainMaterial("#0b0705", 0.58f));
            }
            MountainBox("WallBottomShadow", parent, new Vector3(0f, floorY + 0.34f, frontZ + 0.3f),
                new Vector3(cabin.width + 0.86f, 0.42f, 0.22f), MountainMaterial("#0c0805", 0.8f));
            MountainBox("WallTopShadow", parent, new Vector3(0f, floorY + cabin.height + 0.14f, frontZ + 0.26f),
                new Vector3(cabin.width + 1.1f, 0.3f, 0.2f), MountainMaterial("#100b07", 0.68f));
            var frontPanelWidth = Mathf.Max(1.2f, (cabin.width - doorWidth) * 0.5f);
            foreach (var side in new[] { -1f, 1f })
            {
                var pixelRoot = new GameObject($"FrontWallPixelWood_{side}");
                pixelRoot.transform.SetParent(parent, false);
                pixelRoot.transform.localPosition = new Vector3(
                    side * (doorWidth * 0.5f + frontPanelWidth * 0.5f),
                    floorY + cabin.height * 0.5f,
                    frontZ + 0.36f);
                CreateMountainPixelWoodTexture(pixelRoot.transform, frontPanelWidth * 0.82f, cabin.height * 0.78f, 0f,
                    compact ? 7 : 10, side > 0 ? 4 : 9, true);
            }
            CreateMountainDoorPanel(parent, doorWidth, doorHeight, floorY, frontZ, compact);
        }

        private static void CreateMountainDoorPanel(
            Transform parent,
            float doorWidth,
            float doorHeight,
            float floorY,
            float frontZ,
            bool compact)
        {
            var panelWidth = doorWidth * 0.86f;
            var panelHeight = doorHeight * 0.84f;
            var boardCount = compact ? 3 : 4;
            var boardWidth = panelWidth / boardCount;
            var panelY = floorY + doorHeight * 0.46f;
            var panelZ = frontZ + 0.42f;
            MountainBox("DoorPanelBack", parent, new Vector3(0f, panelY, panelZ - 0.04f),
                new Vector3(panelWidth + 0.28f, panelHeight + 0.22f, 0.32f), MountainMaterial("#1b1009"));
            for (var index = 0; index < boardCount; index++)
            {
                var x = -panelWidth * 0.5f + boardWidth * (index + 0.5f);
                var board = new GameObject($"DoorBoard_{index:00}");
                board.transform.SetParent(parent, false);
                board.transform.localPosition = new Vector3(x, panelY, panelZ);
                MountainBox("Board", board.transform, Vector3.zero, new Vector3(boardWidth + 0.04f, panelHeight, 0.24f),
                    MountainMaterial(index % 2 == 0 ? "#6f4528" : "#4c2e1a"));
                CreateMountainPixelWoodTexture(board.transform, boardWidth * 0.88f, panelHeight * 0.92f, 0.16f,
                    compact ? 5 : 7, index + (compact ? 8 : 2), false);
            }
            for (var index = 0; index <= boardCount; index++)
            {
                var x = -panelWidth * 0.5f + index * boardWidth;
                MountainBox($"DoorGap_{index:00}", parent, new Vector3(x, panelY, panelZ + 0.18f),
                    new Vector3(0.1f, panelHeight * 0.96f, 0.1f), MountainMaterial("#080504"));
            }
            var straps = new[] { 0.31f, 0.64f };
            for (var index = 0; index < straps.Length; index++)
            {
                MountainBox($"DoorStrap_{index}", parent,
                    new Vector3(0f, floorY + doorHeight * straps[index], panelZ + 0.24f),
                    new Vector3(panelWidth + 0.42f, 0.42f, 0.2f),
                    MountainMaterial(index == 0 ? "#2a180d" : "#9a6333"));
            }
            MountainBox("DoorHandle", parent, new Vector3(panelWidth * 0.24f, floorY + doorHeight * 0.5f, panelZ + 0.34f),
                new Vector3(0.36f, 0.36f, 0.22f), MountainMaterial("#d0a05d"));
        }

        private static void CreateMountainHutRoofDetails(
            Transform parent,
            WofMountainCabinMetricsRecord cabin,
            float roofBaseY,
            float roofHeight,
            bool compact)
        {
            var rowCount = compact ? 3 : 4;
            var frontZ = cabin.depth * 0.44f;
            var sideX = cabin.width * 0.44f;
            for (var index = 0; index < rowCount; index++)
            {
                var t = (index + 1f) / (rowCount + 1f);
                var y = roofBaseY + t * roofHeight;
                var widthScale = Mathf.Lerp(cabin.width * 0.84f, cabin.width * 0.32f, t);
                var depthScale = Mathf.Lerp(cabin.depth * 0.84f, cabin.depth * 0.32f, t);
                MountainBox($"FrontRoofShingle_{index:00}", parent,
                    new Vector3(0f, y, frontZ - t * cabin.depth * 0.2f),
                    new Vector3(widthScale, 0.16f, 0.24f), MountainMaterial(index % 2 == 0 ? "#311f15" : "#8f6338"));
                MountainBox($"BackRoofShingle_{index:00}", parent,
                    new Vector3(0f, y + 0.06f, -frontZ + t * cabin.depth * 0.2f),
                    new Vector3(widthScale * 0.86f, 0.14f, 0.2f), MountainMaterial("#2a1b12"));
                MountainBox($"RightRoofShingle_{index:00}", parent,
                    new Vector3(sideX - t * cabin.width * 0.22f, y + 0.02f, 0f),
                    new Vector3(0.2f, 0.14f, depthScale), MountainMaterial("#7b5332"));
                MountainBox($"LeftRoofShingle_{index:00}", parent,
                    new Vector3(-sideX + t * cabin.width * 0.22f, y + 0.02f, 0f),
                    new Vector3(0.2f, 0.14f, depthScale), MountainMaterial("#2a1b12"));
            }
            MountainBox("FrontRoofShadow", parent, new Vector3(0f, roofBaseY + 0.28f, frontZ + 0.22f),
                new Vector3(cabin.width * 1.06f, 0.26f, 0.32f), MountainMaterial("#080504", 0.78f));
            MountainBox("BackRoofShadow", parent, new Vector3(0f, roofBaseY + 0.24f, -frontZ - 0.18f),
                new Vector3(cabin.width * 0.92f, 0.22f, 0.28f), MountainMaterial("#080504", 0.62f));
            MountainBox("RoofTopShadow", parent, new Vector3(0f, roofBaseY + roofHeight * 0.86f, 0f),
                new Vector3(cabin.width * 0.3f, 0.22f, cabin.depth * 0.3f), MountainMaterial("#090605", 0.72f));
            MountainBox("RoofSnowPatchA", parent,
                new Vector3(-cabin.width * 0.24f, roofBaseY + roofHeight * 0.66f, cabin.depth * 0.2f),
                new Vector3(cabin.width * 0.28f, 0.2f, 0.42f), MountainMaterial("#f7fcff", 0.82f));
            MountainBox("RoofSnowPatchB", parent,
                new Vector3(cabin.width * 0.18f, roofBaseY + roofHeight * 0.5f, -cabin.depth * 0.28f),
                new Vector3(cabin.width * 0.22f, 0.18f, 0.36f), MountainMaterial("#cdeafa", 0.7f));
        }

        private static void CreateMountainWindowDetails(
            Transform parent,
            float x,
            float y,
            float z,
            float width,
            float height)
        {
            MountainBox($"WindowFrame_{x}", parent, new Vector3(x, y, z),
                new Vector3(width + 0.32f, height + 0.32f, 0.12f), MountainMaterial("#18100a", 0.54f));
            MountainBox($"WindowSill_{x}", parent, new Vector3(x, y - height * 0.5f - 0.15f, z + 0.16f),
                new Vector3(width + 0.62f, 0.24f, 0.14f), MountainMaterial("#050403", 0.82f));
            MountainBox($"WindowMullionV_{x}", parent, new Vector3(x, y, z + 0.1f),
                new Vector3(0.18f, height + 0.42f, 0.14f), MountainMaterial("#2b1c12"));
            MountainBox($"WindowMullionH_{x}", parent, new Vector3(x, y, z + 0.12f),
                new Vector3(width + 0.42f, 0.18f, 0.14f), MountainMaterial("#2b1c12"));
            foreach (var side in new[] { -1f, 1f })
            {
                MountainBox($"WindowGlint_{x}_{side}", parent,
                    new Vector3(x + side * width * 0.24f, y + height * 0.18f, z + 0.16f),
                    new Vector3(0.28f, 0.34f, 0.1f), MountainMaterial("#fff1a9", 0.58f));
            }
        }
    }
}
