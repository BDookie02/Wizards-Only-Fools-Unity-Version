using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static void CreateGraveyardChapelInterior(
            Transform parent,
            WofGraveyardVillageDocument document,
            GraveyardMaterialSet materials)
        {
            var root = new GameObject("graveyard-chapel-interior");
            root.transform.SetParent(parent, false);
            GraveyardBox("CenterFloor", root.transform, new Vector3(0f, 1.02f, 0f),
                new Vector3(116f, 0.18f, 152f), GraveyardMaterial("ChapelCenterFloor", HexColor("#242019")));
            foreach (var side in new[] { -1f, 1f })
                GraveyardBox($"WingFloor_{side}", root.transform, new Vector3(side * 88f, 1.02f, 0f),
                    new Vector3(74f, 0.18f, 108f), GraveyardMaterial("ChapelWingFloor", HexColor("#211d17")));
            CreateGraveyardCenterPews(root.transform, document);
            CreateGraveyardSidePews(root.transform, document);
            CreateGraveyardAltar(root.transform);
            CreateGraveyardWallCross(root.transform);
            CreateGraveyardPulpit(root.transform);
            CreateGraveyardChapelAvatars(root.transform, document);
            for (var index = 0; index < document.chapel.interiorCandles.Length; index++)
                CreateGraveyardCandle(root.transform, $"InteriorCandle_{index}",
                    ToGraveyardVector(document.chapel.interiorCandles[index]), index > 7 ? 1.28f : 1f, false);
            CreateGraveyardChandelier(root.transform, document, -48f, false);
            CreateGraveyardChandelier(root.transform, document, 0f, true);
            CreateGraveyardChandelier(root.transform, document, 42f, false);
        }

        private static void CreateGraveyardCenterPews(Transform parent, WofGraveyardVillageDocument document)
        {
            foreach (var z in document.chapel.centerPewRows)
            foreach (var side in new[] { -1f, 1f })
            {
                var root = new GameObject($"CenterPew_{side}_{z}");
                root.transform.SetParent(parent, false);
                root.transform.localPosition = new Vector3(side * 17.2f, 0.96f, z);
                GraveyardBox("Seat", root.transform, new Vector3(0f, 1.15f, 0f),
                    new Vector3(18f, 1.2f, 3.8f), GraveyardMaterial("PewSeat", HexColor("#5a321c")));
                var seatGrain = new[] { -5.6f, 0f, 5.6f };
                for (var index = 0; index < seatGrain.Length; index++)
                    GraveyardBox($"SeatGrain_{index}", root.transform,
                        new Vector3(seatGrain[index], 1.78f, 0.2f - index * 0.28f),
                        new Vector3(3.7f, 0.12f, 0.18f),
                        GraveyardMaterial(index % 2 == 0 ? "PewGrainLight" : "PewGrainDark",
                            GraveyardWithAlpha(HexColor(index % 2 == 0 ? "#9a6132" : "#2b160b"), 0.68f), true));
                var back = GraveyardBox("Back", root.transform, new Vector3(0f, 2.32f, 1.35f),
                    new Vector3(18.4f, 2.05f, 0.9f), GraveyardMaterial("PewBack", HexColor("#3a2115")));
                back.transform.localRotation = Quaternion.Euler(-0.14f * Mathf.Rad2Deg, 0f, 0f);
                foreach (var grainX in new[] { -6.5f, 0f, 6.5f })
                {
                    var grain = GraveyardBox($"BackGrain_{grainX}", root.transform,
                        new Vector3(grainX, 2.64f, 1.95f), new Vector3(4.2f, 0.18f, 0.16f),
                        GraveyardMaterial("PewBackGrain", GraveyardWithAlpha(HexColor("#8d552c"), 0.58f), true));
                    grain.transform.localRotation = Quaternion.Euler(-0.14f * Mathf.Rad2Deg, 0f, 0f);
                }
                foreach (var legX in new[] { -7.2f, 7.2f })
                {
                    GraveyardBox($"FrontLeg_{legX}", root.transform, new Vector3(legX, 0.55f, -1.1f),
                        new Vector3(0.78f, 1.1f, 0.78f), GraveyardMaterial("PewLeg", HexColor("#2a160d")));
                    GraveyardBox($"RearLeg_{legX}", root.transform, new Vector3(legX, 0.55f, 1.1f),
                        new Vector3(0.78f, 1.1f, 0.78f), GraveyardMaterial("PewLeg", HexColor("#2a160d")));
                }
            }
        }

        private static void CreateGraveyardSidePews(Transform parent, WofGraveyardVillageDocument document)
        {
            var group = new GameObject("chapel-side-wing-diagonal-pews");
            group.transform.SetParent(parent, false);
            foreach (var pew in document.chapel.sideWingPews)
            {
                var yaw = Mathf.Atan2(pew.x, 68.6f + pew.z);
                var root = new GameObject("SidePew_" + pew.key);
                root.transform.SetParent(group.transform, false);
                root.transform.localPosition = new Vector3(pew.x, 0.96f, pew.z);
                root.transform.localRotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
                GraveyardBox("Seat", root.transform, new Vector3(0f, 1.08f, 0f),
                    new Vector3(pew.width, 1.08f, 3.5f), GraveyardMaterial("PewSeat", HexColor("#5a321c")));
                var back = GraveyardBox("Back", root.transform, new Vector3(0f, 2.12f, 1.22f),
                    new Vector3(pew.width + 0.5f, 1.8f, 0.78f), GraveyardMaterial("DiagonalPewBack", HexColor("#321d12")));
                back.transform.localRotation = Quaternion.Euler(-0.14f * Mathf.Rad2Deg, 0f, 0f);
                var plankOffsets = new[] { -0.28f, 0.28f };
                for (var index = 0; index < plankOffsets.Length; index++)
                    GraveyardBox($"Plank_{index}", root.transform, new Vector3(0f, 1.62f, plankOffsets[index]),
                        new Vector3(pew.width - 1.7f, 0.11f, 0.16f),
                        GraveyardMaterial(index % 2 == 0 ? "PewGrainLight" : "PewGrainDark",
                            GraveyardWithAlpha(HexColor(index % 2 == 0 ? "#9a6132" : "#2b160b"), 0.62f), true));
                foreach (var offset in new[] { -0.38f, 0f, 0.38f })
                {
                    var grain = GraveyardBox($"BackGrain_{offset}", root.transform,
                        new Vector3(offset * pew.width, 2.44f, 1.74f),
                        new Vector3(pew.width * 0.22f, 0.15f, 0.14f),
                        GraveyardMaterial("DiagonalPewBackGrain",
                            GraveyardWithAlpha(HexColor("#8d552c"), 0.56f), true));
                    grain.transform.localRotation = Quaternion.Euler(-0.14f * Mathf.Rad2Deg, 0f, 0f);
                }
                foreach (var offset in new[] { -0.42f, 0.42f })
                {
                    GraveyardBox($"FrontLeg_{offset}", root.transform,
                        new Vector3(offset * pew.width, 0.48f, -0.95f), new Vector3(0.7f, 0.96f, 0.7f),
                        GraveyardMaterial("PewLeg", HexColor("#2a160d")));
                    GraveyardBox($"RearLeg_{offset}", root.transform,
                        new Vector3(offset * pew.width, 0.48f, 0.95f), new Vector3(0.7f, 0.96f, 0.7f),
                        GraveyardMaterial("PewLeg", HexColor("#2a160d")));
                }
            }
        }

        private static void CreateGraveyardAltar(Transform parent)
        {
            var root = new GameObject("ChapelAltar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 1f, -68f);
            GraveyardBox("Body", root.transform, new Vector3(0f, 2.1f, 0f), new Vector3(18f, 3.2f, 7.5f),
                GraveyardMaterial("AltarBody", HexColor("#5b351f")));
            GraveyardBox("Top", root.transform, new Vector3(0f, 4f, -0.2f), new Vector3(21f, 1.2f, 8.6f),
                GraveyardMaterial("AltarTop", HexColor("#7a4928")));
            var xs = new[] { -7.2f, 0f, 7.2f };
            for (var index = 0; index < xs.Length; index++)
                GraveyardBox($"Grain_{index}", root.transform, new Vector3(xs[index], 4.7f, 4.18f),
                    new Vector3(4.2f, 0.22f, 0.2f),
                    GraveyardMaterial(index % 2 == 0 ? "AltarGrainLight" : "AltarGrainDark",
                        GraveyardWithAlpha(HexColor(index % 2 == 0 ? "#b06d36" : "#2a170e"), 0.66f), true));
            var book = GraveyardBox("Book", root.transform, new Vector3(0f, 5.15f, 2.2f),
                new Vector3(11.5f, 0.7f, 5.2f), GraveyardMaterial("AltarBook", HexColor("#3c2416")));
            book.transform.localRotation = Quaternion.Euler(-0.28f * Mathf.Rad2Deg, 0f, 0f);
        }

        private static void CreateGraveyardWallCross(Transform parent)
        {
            var root = new GameObject("chapel-wall-cross-behind-pope");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 18.2f, -80.6f);
            var shadow = GraveyardMaterial("WallCrossShadow", new Color(0.071f, 0.051f, 0.031f, 0.72f), true);
            GraveyardBox("ShadowVertical", root.transform, new Vector3(0.35f, -0.35f, -0.06f),
                new Vector3(2.7f, 19.8f, 0.34f), shadow);
            GraveyardBox("ShadowHorizontal", root.transform, new Vector3(0.35f, 2.8f, -0.08f),
                new Vector3(13.6f, 2.7f, 0.34f), shadow);
            GraveyardBox("GoldVertical", root.transform, Vector3.zero, new Vector3(2.25f, 19.2f, 0.46f),
                GraveyardMaterial("WallCrossGold", HexColor("#d7b46a")));
            GraveyardBox("GoldHorizontal", root.transform, new Vector3(0f, 3.05f, 0.03f),
                new Vector3(13.2f, 2.25f, 0.52f), GraveyardMaterial("WallCrossGold", HexColor("#d7b46a")));
            GraveyardBox("HighlightVertical", root.transform, new Vector3(-0.34f, 0.6f, 0.08f),
                new Vector3(0.36f, 16.8f, 0.12f),
                GraveyardMaterial("WallCrossHighlight", GraveyardWithAlpha(HexColor("#f5d990"), 0.72f), true));
            GraveyardBox("HighlightHorizontal", root.transform, new Vector3(-0.55f, 3.55f, 0.1f),
                new Vector3(10.6f, 0.34f, 0.12f),
                GraveyardMaterial("WallCrossHighlight", GraveyardWithAlpha(HexColor("#f5d990"), 0.7f), true));
            GraveyardBox("ShadeVertical", root.transform, new Vector3(0.62f, -0.5f, 0.09f),
                new Vector3(0.34f, 15.6f, 0.12f),
                GraveyardMaterial("WallCrossShade", GraveyardWithAlpha(HexColor("#7a5328"), 0.62f), true));
            GraveyardBox("ShadeHorizontal", root.transform, new Vector3(0.66f, 2.35f, 0.11f),
                new Vector3(10.8f, 0.34f, 0.12f),
                GraveyardMaterial("WallCrossShade", GraveyardWithAlpha(HexColor("#7a5328"), 0.56f), true));
        }

        private static void CreateGraveyardPulpit(Transform parent)
        {
            var root = new GameObject("ChapelPulpit");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(30f, 1f, -54f);
            GraveyardBox("Body", root.transform, new Vector3(0f, 2f, 0f), new Vector3(7.4f, 4f, 6.4f),
                GraveyardMaterial("PulpitBody", HexColor("#4b2c1a")));
            var top = GraveyardBox("Top", root.transform, new Vector3(0f, 4.5f, -0.6f),
                new Vector3(8.2f, 1f, 5.6f), GraveyardMaterial("PulpitTop", HexColor("#724526")));
            top.transform.localRotation = Quaternion.Euler(-0.35f * Mathf.Rad2Deg, 0f, 0f);
            foreach (var x in new[] { -2.4f, 0f, 2.4f })
                GraveyardBox($"Grain_{x}", root.transform, new Vector3(x, 4.96f, 2.02f),
                    new Vector3(1.6f, 0.18f, 0.18f),
                    GraveyardMaterial("PulpitGrain", GraveyardWithAlpha(HexColor("#af7038"), 0.64f), true));
            GraveyardBox("Page", root.transform, new Vector3(0f, 5.25f, 1.9f), new Vector3(4.2f, 0.42f, 2.4f),
                GraveyardMaterial("PulpitPage", HexColor("#d6c28a")));
        }

        private static void CreateGraveyardChapelAvatars(Transform parent, WofGraveyardVillageDocument document)
        {
            var material = GetOrCreateVillagerMaterial();
            var centerRoot = new GameObject("chapel-pew-npcs");
            centerRoot.transform.SetParent(parent, false);
            foreach (var placement in document.chapel.centerNpcPlacements)
                CreateGraveyardChapelAvatar(centerRoot.transform, placement.key, placement.position, placement.yaw,
                    placement.characterIndex, -1, document, material, false);
            var sideRoot = new GameObject("chapel-side-wing-pew-npcs");
            sideRoot.transform.SetParent(parent, false);
            foreach (var placement in document.chapel.sideWingNpcPlacements)
                CreateGraveyardChapelAvatar(sideRoot.transform, placement.key, placement.position, placement.yaw,
                    placement.characterIndex, -1, document, material, false);
            CreateGraveyardChapelAvatar(parent, "chapel-pope-at-pulpit", document.chapel.pope.position,
                document.chapel.pope.yaw, document.chapel.pope.characterIndex, 0, document, material, true);
        }

        private static void CreateGraveyardChapelAvatar(
            Transform parent,
            string id,
            float[] position,
            float yaw,
            int characterIndex,
            int fixedDirection,
            WofGraveyardVillageDocument document,
            Material material,
            bool pope)
        {
            if (characterIndex < 0 || characterIndex >= document.chapel.characters.Length)
                throw new InvalidOperationException($"Invalid chapel character index {characterIndex} for {id}.");
            var character = document.chapel.characters[characterIndex];
            var avatar = new GameObject(id);
            avatar.transform.SetParent(parent, false);
            var visual = new GameObject("AvatarBillboard");
            visual.transform.SetParent(avatar.transform, false);
            visual.transform.localPosition = new Vector3(0f, WofVillagerMath.AvatarWorldCenterY, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            var billboard = avatar.AddComponent<WofStaticAvatarBillboard>();
            billboard.Configure(id, character.archiveFile, ToGraveyardVector(position), yaw, fixedDirection,
                visual.transform, renderer);
            if (pope) CreateGraveyardPopeMiter(visual.transform);
            MarkDarrelDynamic(avatar);
            MarkDarrelDynamic(visual);
        }

        private static void CreateGraveyardPopeMiter(Transform billboardVisual)
        {
            var quad = GetOrCreateMeshAsset(GraveyardGeometryRoot + "/PopeMiterQuad.asset",
                CreateGraveyardVerticalQuadMesh);
            var material = GraveyardTextureMaterial("PopeMiter", "GraveyardVillage/Textures/chapel-pope-miter.png",
                Vector2.one, true);
            var miter = new GameObject("PopeMiter", typeof(MeshFilter), typeof(MeshRenderer));
            miter.transform.SetParent(billboardVisual, false);
            miter.transform.localPosition = new Vector3(0f, 2.08f - WofVillagerMath.AvatarWorldCenterY, 0.08f);
            miter.transform.localScale = new Vector3(1.36f, 1.36f, 1f);
            miter.GetComponent<MeshFilter>().sharedMesh = quad;
            miter.GetComponent<MeshRenderer>().sharedMaterial = material;
            MarkDarrelDynamic(miter);
        }

        private static void CreateGraveyardCandle(
            Transform parent,
            string name,
            Vector3 position,
            float scale,
            bool light)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * scale;
            CreateScaledMesh("Base", root.transform, new Vector3(0f, 0.12f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleBase10.asset",
                    () => CreateDarrelFrustumMesh(0.7f, 0.82f, 0.24f, 10)),
                GraveyardMaterial("CandleBase", HexColor("#3a2a1b")));
            CreateScaledMesh("Cup", root.transform, new Vector3(0f, 0.28f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleCup10.asset",
                    () => CreateDarrelFrustumMesh(0.44f, 0.56f, 0.3f, 10)),
                GraveyardMaterial("CandleCup", HexColor("#7f6035")));
            CreateScaledMesh("Wax", root.transform, new Vector3(0f, 0.98f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleWax12.asset",
                    () => CreateDarrelFrustumMesh(0.33f, 0.38f, 1.38f, 12)),
                GraveyardMaterial("CandleWax", HexColor("#f0e2bd")));
            CreateScaledMesh("WaxTop", root.transform, new Vector3(0f, 1.69f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleTop12.asset",
                    () => CreateDarrelFrustumMesh(0.32f, 0.34f, 0.12f, 12)),
                GraveyardMaterial("CandleTop", HexColor("#fff1ca")));
            var angles = new[] { 0.2f, 2.45f, 4.1f };
            for (var index = 0; index < angles.Length; index++)
            {
                var angle = angles[index];
                var drip = GraveyardBox($"WaxDrip_{index}", root.transform,
                    new Vector3(Mathf.Cos(angle) * 0.3f, 1.24f - index * 0.14f, Mathf.Sin(angle) * 0.3f),
                    new Vector3(0.1f, 0.44f + index * 0.08f, 0.08f),
                    GraveyardMaterial("CandleDrip", HexColor("#fff3cf")));
                drip.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
            CreateScaledMesh("Wick", root.transform, new Vector3(0f, 1.9f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleWick5.asset",
                    () => CreateDarrelFrustumMesh(0.035f, 0.045f, 0.44f, 5)),
                GraveyardMaterial("CandleWick", HexColor("#18110a")));
            CreateScaledMesh("OuterFlame", root.transform, new Vector3(0f, 2.24f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleOuterFlame8.asset",
                    () => CreateDarrelFrustumMesh(0f, 0.38f, 0.92f, 8)),
                GraveyardMaterial("CandleOuterFlame", GraveyardWithAlpha(HexColor("#ff9d1f"), 0.94f), true));
            CreateScaledMesh("InnerFlame", root.transform, new Vector3(0f, 2.32f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleInnerFlame8.asset",
                    () => CreateDarrelFrustumMesh(0f, 0.18f, 0.52f, 8)),
                GraveyardMaterial("CandleInnerFlame", GraveyardWithAlpha(HexColor("#fff4a8"), 0.96f), true));
            CreateScaledMesh("Glow", root.transform, new Vector3(0f, 2.28f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/CandleGlowSphere.asset",
                    () => CreateUvSphereMesh(0.72f, 10, 8)),
                GraveyardMaterial("CandleGlow", GraveyardWithAlpha(HexColor("#ffb347"), 0.18f), true));
            if (light)
                CreateGraveyardChapelLight(root.transform, "CandleLight", new Vector3(0f, 2.22f, 0f),
                    HexColor("#ffd27a"), 4.4f, 32f);
        }

        private static void CreateGraveyardChandelier(
            Transform parent,
            WofGraveyardVillageDocument document,
            float z,
            bool light)
        {
            var root = new GameObject($"ChapelChandelier_{z}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 23.4f, z);
            CreateScaledMesh("Chain", root.transform, new Vector3(0f, 7.3f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/ChandelierChain5.asset",
                    () => CreateDarrelFrustumMesh(0.16f, 0.16f, 14.6f, 5)),
                GraveyardMaterial("ChandelierChain", HexColor("#15110c")));
            CreateMeshVisual("Ring", root.transform, Vector3.zero,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/ChandelierTorus.asset",
                    () => CreateDarrelTorusMesh(6.2f, 0.24f, 24, 6)),
                GraveyardMaterial("ChandelierRing", HexColor("#24180c")));
            for (var index = 0; index < document.chapel.chandelierCandles.Length; index++)
            {
                var candle = document.chapel.chandelierCandles[index];
                var spoke = GraveyardBox($"Spoke_{index}", root.transform,
                    new Vector3(candle.x * 0.5f, 0f, candle.z * 0.5f), new Vector3(6.2f, 0.16f, 0.16f),
                    GraveyardMaterial("ChandelierSpoke", HexColor("#2f1e0f")));
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(candle.z, candle.x) * Mathf.Rad2Deg);
                CreateGraveyardCandle(root.transform, $"Candle_{index}",
                    new Vector3(candle.x, candle.y - 0.15f, candle.z), 0.86f, false);
            }
            CreateScaledMesh("Glow", root.transform, new Vector3(0f, 0.9f, 0f), Vector3.one,
                GetOrCreateMeshAsset(GraveyardGeometryRoot + "/ChandelierGlowSphere.asset",
                    () => CreateUvSphereMesh(8.6f, 14, 10)),
                GraveyardMaterial("ChandelierGlow", GraveyardWithAlpha(HexColor("#ffbf5a"), 0.14f), true));
            if (light)
                CreateGraveyardChapelLight(root.transform, "ChandelierLight", new Vector3(0f, 1.4f, 0f),
                    HexColor("#ffd27a"), 4.2f, 58f);
        }

        private static Mesh CreateGraveyardVerticalQuadMesh()
        {
            var vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            var mesh = new Mesh { name = "GraveyardVerticalQuad" };
            mesh.vertices = vertices;
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
