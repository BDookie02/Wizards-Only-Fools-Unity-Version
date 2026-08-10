using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WOF.Tests
{
    public sealed class WofReactVisualAssetTests
    {
        [Test]
        public void ReactVisualManifestTracksEveryBakedOutput()
        {
            var manifestPath = ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "react-visual-assets.json");
            var text = File.ReadAllText(manifestPath);

            StringAssert.Contains("\"outputCount\": 1266", text);
            StringAssert.Contains("Avatar/Default/launch-preview.png", text);
            StringAssert.Contains("HUD/Hands/idle_1.png", text);
            StringAssert.Contains("Huts/mushroom_cap_0.png", text);
            StringAssert.Contains("TreeHouse/bark.png", text);
            StringAssert.Contains("TreeHouse/plank.png", text);
            StringAssert.Contains("Vegetation/botw-grass.png", text);
            StringAssert.Contains("Launch/press-background.png", text);
            StringAssert.Contains("HUD/Hands/Equipped/right_idle_1.png", text);
            StringAssert.Contains("HUD/Hands/Firing/left_idle_1.png", text);
            StringAssert.Contains("HUD/Hands/Firing/right_idle_1.png", text);
            StringAssert.Contains("HUD/Fireball/Equipped/right_fireballidle_1.png", text);
            StringAssert.Contains("HUD/SpellMenu/spellbook_icon.png", text);
            StringAssert.Contains("HUD/SpellMenu/speedboost.png", text);
            StringAssert.Contains("HUD/SpellMenu/jumpboost.png", text);
            StringAssert.Contains("HUD/SpellMenu/magicglassorb.png", text);
            StringAssert.Contains("Geometry/bush-dodecahedron.json", text);
            StringAssert.Contains("Villagers/base-village.json", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/-224--224.wofavatar", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/-64--48-darrel.wofavatar", text);
            StringAssert.Contains("DarrelGrove/Textures/Repeating/ground.png", text);
            StringAssert.Contains("DarrelGrove/Textures/Clamped/fuji.png", text);
            StringAssert.Contains("DarrelGrove/Dragon/attack_15.png", text);
            StringAssert.Contains("DarrelGrove/runtime-layout.json", text);
            StringAssert.Contains("DesertVillage/runtime-layout.json", text);
            StringAssert.Contains("DesertVillage/Textures/desert-sand.png", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/desert-54.wofavatar", text);
            StringAssert.Contains("ChicagoCity/runtime-layout.json", text);
            StringAssert.Contains("ChicagoCity/Textures/facade-5.png", text);
            StringAssert.Contains("ChicagoCity/Textures/led-sign.png", text);
            StringAssert.Contains("ChicagoCity/Operators/operator-34.png", text);
            StringAssert.Contains("SwampVillage/runtime-layout.json", text);
            StringAssert.Contains("SwampVillage/Textures/terrain-detail.png", text);
            StringAssert.Contains("SwampVillage/Toad/toad_idle_27.png", text);
            StringAssert.Contains("SwampVillage/Toad/toad_yawn_11.png", text);
            StringAssert.Contains("SwampVillage/Toad/toad_sleep.png", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/swamp-12.wofavatar", text);
            StringAssert.Contains("MountainVillage/runtime-layout.json", text);
            StringAssert.Contains("MountainVillage/Textures/terrain-detail.png", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/mountain-00.wofavatar", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/mountain-10.wofavatar", text);
            StringAssert.Contains("GraveyardVillage/runtime-layout.json", text);
            StringAssert.Contains("GraveyardVillage/Tombs/00-body.png", text);
            StringAssert.Contains("GraveyardVillage/Tombs/00-inscription.png", text);
            StringAssert.Contains("GraveyardVillage/Textures/chapel-stone.png", text);
            StringAssert.Contains("GraveyardVillage/Textures/chapel-dark-stone.png", text);
            StringAssert.Contains("GraveyardVillage/Textures/chapel-pope-miter.png", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/graveyard-chapel-npc-00.wofavatar", text);
            StringAssert.Contains("StreamingAssets/WOF/Villagers/Base/graveyard-chapel-pope.wofavatar", text);
        }

        [Test]
        public void BakedBushGeometryComesFromTheReactThreeGeometry()
        {
            var path = ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", "Geometry", "bush-dodecahedron.json");
            var text = File.ReadAllText(path);

            StringAssert.Contains("new THREE.DodecahedronGeometry(0.5, 0).toNonIndexed()", text);
            StringAssert.Contains("\"vertexCount\": 108", text);
        }

        [TestCase("Avatar/Default/launch-preview.png", 360, 360)]
        [TestCase("Launch/press-background.png", 1920, 1080)]
        [TestCase("Avatar/Default/idle/d0_f0.png", 512, 512)]
        [TestCase("HUD/Hands/idle_1.png", 859, 484)]
        [TestCase("HUD/Hands/Equipped/left_idle_1.png", 859, 484)]
        [TestCase("HUD/Hands/Equipped/right_idle_1.png", 859, 484)]
        [TestCase("HUD/Hands/Firing/left_idle_1.png", 859, 484)]
        [TestCase("HUD/Hands/Firing/right_idle_1.png", 859, 484)]
        [TestCase("HUD/Fireball/fireballidle_1.png", 48, 48)]
        [TestCase("HUD/Fireball/Equipped/left_fireballidle_1.png", 859, 495)]
        [TestCase("HUD/Fireball/Equipped/right_fireballidle_1.png", 859, 495)]
        [TestCase("Huts/mushroom_cap_0.png", 128, 128)]
        [TestCase("Huts/grass.png", 128, 128)]
        [TestCase("TreeHouse/bark.png", 64, 64)]
        [TestCase("TreeHouse/plank.png", 64, 64)]
        [TestCase("Vegetation/botw-grass.png", 128, 128)]
        [TestCase("DarrelGrove/Textures/Repeating/ground.png", 128, 128)]
        [TestCase("DarrelGrove/Textures/Clamped/blossom.png", 64, 64)]
        [TestCase("DarrelGrove/Textures/Clamped/petal.png", 32, 32)]
        [TestCase("DarrelGrove/Textures/Clamped/fuji.png", 256, 144)]
        [TestCase("DarrelGrove/Dragon/attack_00.png", 512, 320)]
        [TestCase("DesertVillage/Textures/desert-sand.png", 128, 128)]
        [TestCase("DesertVillage/Textures/desert-adobe-wall.png", 128, 128)]
        [TestCase("ChicagoCity/Textures/facade-0.png", 128, 192)]
        [TestCase("ChicagoCity/Textures/facade-5.png", 128, 192)]
        [TestCase("ChicagoCity/Textures/chicago-sign.png", 256, 72)]
        [TestCase("ChicagoCity/Textures/led-sign.png", 1024, 192)]
        [TestCase("ChicagoCity/Textures/store-sign-0.png", 256, 96)]
        [TestCase("ChicagoCity/Textures/ad-0.png", 192, 256)]
        [TestCase("ChicagoCity/Operators/operator-00.png", 512, 512)]
        [TestCase("SwampVillage/Textures/terrain-detail.png", 128, 128)]
        [TestCase("SwampVillage/Toad/toad_idle_00.png", 288, 187)]
        [TestCase("SwampVillage/Toad/toad_yawn_11.png", 288, 187)]
        [TestCase("SwampVillage/Toad/toad_sleep.png", 288, 187)]
        [TestCase("MountainVillage/Textures/terrain-detail.png", 256, 256)]
        [TestCase("GraveyardVillage/Tombs/00-body.png", 128, 128)]
        [TestCase("GraveyardVillage/Tombs/00-inscription.png", 256, 160)]
        [TestCase("GraveyardVillage/Textures/chapel-stone.png", 256, 256)]
        [TestCase("GraveyardVillage/Textures/chapel-dark-stone.png", 256, 256)]
        [TestCase("GraveyardVillage/Textures/chapel-pope-miter.png", 96, 64)]
        [TestCase("GraveyardVillage/Textures/terrain-detail.png", 128, 128)]
        public void BakedReactPngHasExpectedDimensions(string relativePath, int width, int height)
        {
            var bytes = File.ReadAllBytes(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", relativePath));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(bytes, false), Is.True);
                Assert.That(texture.width, Is.EqualTo(width));
                Assert.That(texture.height, Is.EqualTo(height));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase("TreeHouse/bark.png")]
        [TestCase("TreeHouse/plank.png")]
        [TestCase("DarrelGrove/Textures/Repeating/ground.png")]
        [TestCase("DarrelGrove/Textures/Repeating/petal-carpet.png")]
        [TestCase("DesertVillage/Textures/desert-sand.png")]
        [TestCase("DesertVillage/Textures/desert-adobe-wall.png")]
        [TestCase("SwampVillage/Textures/terrain-detail.png")]
        [TestCase("GraveyardVillage/Tombs/00-body.png")]
        [TestCase("GraveyardVillage/Textures/chapel-stone.png")]
        [TestCase("GraveyardVillage/Textures/chapel-dark-stone.png")]
        public void TreeHouseTextureImportMatchesReactNearestRepeatSettings(string relativePath)
        {
            var assetPath = "Assets/WOF/Art/Generated/React/" + relativePath;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void GraveyardTerrainImportRetainsSharedReactRepeatAndMipmapSettings()
        {
            var importer = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/GraveyardVillage/Textures/terrain-detail.png") as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void BotwGrassTextureImportMatchesReactLinearClampSettings()
        {
            var importer = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/Vegetation/botw-grass.png") as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void FullWorldMapRetainsTheReactAtlasAspectAndUiSamplingSettings()
        {
            var importer = AssetImporter.GetAtPath(
                "Assets/WOF/Resources/Maps/dagamemap.png") as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(Resources.Load<Shader>("Shaders/WofUiMapColorGrade"), Is.Not.Null);
        }

        [TestCase("GraveyardVillage/Tombs/00-inscription.png")]
        [TestCase("GraveyardVillage/Tombs/08-inscription.png")]
        [TestCase("GraveyardVillage/Tombs/16-inscription.png")]
        [TestCase("GraveyardVillage/Textures/chapel-pope-miter.png")]
        public void GraveyardAlphaTextureImportMatchesReactNearestClampSettings(string relativePath)
        {
            var importer = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/" + relativePath) as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void MountainTextureImportRetainsReactRepeatAndMipmapSettings()
        {
            var importer = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/MountainVillage/Textures/terrain-detail.png") as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void ChicagoTextureImportsRetainTheirPerAxisReactWrapModes()
        {
            var facade = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/ChicagoCity/Textures/facade-0.png") as TextureImporter;
            var led = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/ChicagoCity/Textures/led-sign.png") as TextureImporter;
            var store = AssetImporter.GetAtPath(
                "Assets/WOF/Art/Generated/React/ChicagoCity/Textures/store-sign-0.png") as TextureImporter;

            Assert.That(facade, Is.Not.Null);
            Assert.That(facade.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(facade.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(led, Is.Not.Null);
            Assert.That(led.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(led.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(store, Is.Not.Null);
            Assert.That(store.wrapModeU, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(store.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [TestCase("HUD/Hands/idle_1.png")]
        [TestCase("HUD/Hands/Equipped/right_idle_1.png")]
        [TestCase("HUD/Hands/Firing/left_idle_1.png")]
        [TestCase("HUD/Hands/Firing/right_idle_1.png")]
        [TestCase("HUD/Fireball/fireballidle_1.png")]
        [TestCase("HUD/Fireball/Equipped/right_fireballidle_1.png")]
        public void ProcessedHudArtRemovesOpaqueBlackBackground(string relativePath)
        {
            var bytes = File.ReadAllBytes(ResolveProjectPath(
                "Assets", "WOF", "Art", "Generated", "React", relativePath));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(bytes, false), Is.True);
                Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static string ResolveProjectPath(params string[] segments)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            Assert.That(Path.GetPathRoot(projectRoot), Is.EqualTo(@"D:\"));

            var path = projectRoot;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }
            return path;
        }
    }
}
