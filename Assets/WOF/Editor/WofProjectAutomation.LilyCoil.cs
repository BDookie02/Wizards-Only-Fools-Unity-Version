using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string LilyCoilArtRoot = "Assets/WOF/Art/Generated/React/LilyCoil";
        private const string LilyCoilLayoutPath = LilyCoilArtRoot + "/runtime-layout.json";
        private const string LilyCoilTextureRoot = LilyCoilArtRoot + "/Textures";
        private const string LilyCoilEyeRoot = LilyCoilArtRoot + "/EyeFrames";
        private const string LilyCoilGeometryRoot = GeometryRoot + "/LilyCoil";

        private static void ConfigureLilyCoilTextureImports()
        {
            ConfigureRepeatingTextureFolder(LilyCoilTextureRoot);
            ConfigureClampedReactTexture(LilyCoilTextureRoot + "/calla-bloom.png", true);
            ConfigureClampedReactTexture(LilyCoilTextureRoot + "/meadow-overlay.png", true);
            ConfigureClampedReactTexture(LilyCoilTextureRoot + "/ground-blade-alpha.png", true);
            ConfigureClampedReactTexture(LilyCoilTextureRoot + "/tube-grass-alpha.png", true);
            ConfigureSpriteFolder(LilyCoilEyeRoot, 96f);
        }

        private static void CreateLilyCoilScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = WofLilyCoilSceneLoader.SceneName;
            var world = new GameObject("World");
            CreateLilyCoil(world.transform);
            EditorSceneManager.SaveScene(scene, LilyCoilScenePath);
        }

        private static void CreateLilyCoil(Transform parent)
        {
            var document = LoadLilyCoilDocument();
            var materials = CreateLilyCoilMaterials(document);
            var root = new GameObject("ReactSurvivalLilyCoil_48_-48");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = WofLilyCoilLayout.WorldOrigin;

            CreateLilyCoilRealm(root.transform, document, materials);
            CreateLilyCoilTunnel(root.transform, document, materials);
            CreateLilyCoilFlora(root.transform, document, materials);
            CreateLilyCoilLights(root.transform, document);
            root.AddComponent<WofLilyCoilAmbientEffectsRuntime>().Configure(
                document.flora.tubeFlowers,
                document.flora.smallTubeFlowers,
                document.flora.smallBloomParticles,
                document.flora.fireflies,
                document.flora.butterflies,
                materials.Particle,
                materials.PinkGlow,
                materials.Firefly,
                materials.FireflyGlow,
                materials.ButterflyLeft,
                materials.ButterflyRight,
                materials.ButterflyBody);
        }

        private static WofLilyCoilDocument LoadLilyCoilDocument()
        {
            var source = LoadRequiredAsset<TextAsset>(LilyCoilLayoutPath);
            var document = JsonUtility.FromJson<WofLilyCoilDocument>(source.text);
            if (document == null || document.schemaVersion != 1 ||
                document.sourceSignature != WofLilyCoilLayout.SourceSignature ||
                document.chunk == null || document.chunk.cx != WofLilyCoilLayout.ChunkX ||
                document.chunk.cz != WofLilyCoilLayout.ChunkZ || document.chunk.distance != 0 ||
                document.chunk.villageKind != "lily-coil" || document.chunk.lod != "near" ||
                document.constants == null || document.counts == null || document.flora == null ||
                document.geometries == null || !WofLilyCoilLayout.HasExactCounts(document.counts) ||
                document.flora.tubeGrass?.Length != WofLilyCoilLayout.TubeGrassTuftCount ||
                document.flora.tubeLilies?.Length != WofLilyCoilLayout.TubeLilyCount ||
                document.flora.tubeFlowers?.Length != WofLilyCoilLayout.TubeFlowerCount ||
                document.flora.smallTubeFlowers?.Length != WofLilyCoilLayout.SmallTubeFlowerCount ||
                document.flora.smallBloomParticles?.Length != WofLilyCoilLayout.SmallBloomParticleCount ||
                document.flora.fireflies?.Length != WofLilyCoilLayout.FireflyCount ||
                document.flora.butterflies?.Length != WofLilyCoilLayout.ButterflyCount ||
                document.flora.groundGrass?.Length != WofLilyCoilLayout.GroundGrassTuftCount ||
                document.flora.groundLilies?.Length != WofLilyCoilLayout.GroundLilyCount ||
                document.eyeFrames?.Length != WofLilyCoilLayout.EyeFrameCount ||
                !IsValidLilyMesh(document.geometries.tunnel) ||
                !IsValidLilyMesh(document.geometries.tunnelCollider))
            {
                throw new InvalidOperationException($"Invalid exact React Lily Coil layout at {LilyCoilLayoutPath}.");
            }
            return document;
        }

        private static bool IsValidLilyMesh(WofSerializedMeshRecord record)
        {
            return record != null && record.vertexCount > 0 &&
                   record.positions?.Length == record.vertexCount * 3 &&
                   record.normals?.Length == record.vertexCount * 3 &&
                   record.uvs?.Length == record.vertexCount * 2 &&
                   record.indices != null && record.indices.Length > 0 && record.indices.Length % 3 == 0;
        }

        private static LilyCoilMaterialSet CreateLilyCoilMaterials(WofLilyCoilDocument document)
        {
            var grass = LoadRequiredAsset<Texture2D>(LilyCoilArtRoot + "/" + document.textures.grass);
            var stone = LoadRequiredAsset<Texture2D>(LilyCoilArtRoot + "/" + document.textures.stone);
            var calla = LoadRequiredAsset<Texture2D>(LilyCoilArtRoot + "/" + document.textures.callaBloom);
            var groundBlade = LoadRequiredAsset<Texture2D>(LilyCoilArtRoot + "/" + document.textures.groundBladeAlpha);
            var tubeGrass = LoadRequiredAsset<Texture2D>(LilyCoilArtRoot + "/" + document.textures.tubeGrassAlpha);
            return new LilyCoilMaterialSet
            {
                Ground = LilyLit("Ground", HexColor("#a78bfa"), grass, HexColor("#3b0764") * 0.16f, 0.95f),
                GroundRing = LilyLit("GroundRing", new Color(0.298f, 0.114f, 0.584f, 0.54f), stone,
                    HexColor("#4c1d95") * 0.28f, 0.82f, true),
                TunnelInner = LilyLit("TunnelInner", HexColor("#7c3aed"), null, HexColor("#4c1d95") * 0.18f,
                    0.96f, false, CullMode.Front),
                TunnelOuter = LilyLit("TunnelOuter", HexColor("#07020d"), null, HexColor("#2e1065") * 0.2f,
                    0.22f, false, CullMode.Back, 0.5f),
                Seal = LilyLit("Seal", HexColor("#12051f"), null, HexColor("#2e1065") * 0.34f, 0.94f),
                CapDark = LilyUnlit("CapDark", HexColor("#0d0317")),
                CapRing = LilyUnlit("CapRing", new Color(0.847f, 0.706f, 0.996f, 0.9f), true),
                GroundGrass = LilyAlpha("GroundGrass", new Color(0.545f, 0.361f, 0.965f, 0.78f), groundBlade, 0.16f, true),
                TubeGrass = new[]
                {
                    LilyAlpha("TubeGrass0", HexColor("#4c1d95"), tubeGrass, 0.14f, false),
                    LilyAlpha("TubeGrass1", HexColor("#7c3aed"), tubeGrass, 0.14f, false),
                    LilyAlpha("TubeGrass2", HexColor("#a78bfa"), tubeGrass, 0.14f, false)
                },
                LilyPetal = LilyLit("LilyPetal", new Color(1f, 0.98f, 0.941f, 0.88f), null, Color.white * 1.9f,
                    0.34f, true),
                GroundLilyPetal = LilyLit("GroundLilyPetal", HexColor("#fff7ed"), null, Color.white * 1.55f, 0.38f),
                WhiteGlow = LilyUnlit("WhiteGlow", new Color(1f, 1f, 1f, 0.18f), true, true),
                FlowerStem = LilyLit("FlowerStem", HexColor("#a3c96a"), null, HexColor("#65a30d") * 0.28f, 0.72f),
                FlowerBloom = LilyAlpha("FlowerBloom", Color.white, calla, 0.06f, true),
                SmallStem = LilyLit("SmallFlowerStem", HexColor("#6f9a55"), null, HexColor("#365314") * 0.16f, 0.78f),
                SmallBloom = LilyUnlit("SmallBloom", new Color(0.941f, 0.671f, 0.988f, 0.9f), true),
                PinkGlow = LilyUnlit("PinkGlow", new Color(0.941f, 0.671f, 0.988f, 0.2f), true, true),
                Particle = LilyUnlit("Particle", new Color(1f, 0.969f, 0.929f, 0.88f), true, true),
                Firefly = LilyUnlit("Firefly", HexColor("#fef9c3"), true, true),
                FireflyGlow = LilyUnlit("FireflyGlow", new Color(0.992f, 0.902f, 0.541f, 0.42f), true, true),
                ButterflyLeft = LilyUnlit("ButterflyLeft", new Color(0.404f, 0.91f, 0.976f, 0.62f), true, true),
                ButterflyRight = LilyUnlit("ButterflyRight", new Color(0.941f, 0.671f, 0.988f, 0.58f), true, true),
                ButterflyBody = LilyUnlit("ButterflyBody", new Color(0.925f, 0.996f, 1f, 0.72f), true, true),
                Highlight = LilyUnlit("Highlight", new Color(0.953f, 0.91f, 1f, 0.36f), true, true)
            };
        }

        private static Material LilyLit(string name, Color color, Texture texture, Color emission, float roughness,
            bool transparent = false, CullMode cull = CullMode.Off, float metalness = 0f)
        {
            var material = GetOrCreateMaterial("LilyCoil_" + name, color, emission, texture, Vector2.one, roughness,
                cull == CullMode.Off, transparent);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)cull);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metalness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LilyUnlit(string name, Color color, bool transparent = false, bool additive = false)
        {
            var material = GetOrCreateUnlitMaterial("LilyCoil_" + name, color, transparent);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (transparent && additive && material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LilyAlpha(string name, Color color, Texture texture, float cutoff, bool transparent)
        {
            var material = LilyUnlit(name, color, transparent);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", cutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateLilyCoilRealm(Transform parent, WofLilyCoilDocument document,
            LilyCoilMaterialSet materials)
        {
            var disk = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/GroundDisk96.asset",
                () => CreateDarrelDiskMesh(WofLilyCoilLayout.RealmRadius - 5f, 96));
            var ring = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/GroundRing96.asset",
                () => CreateDarrelTorusMesh(WofLilyCoilLayout.RealmRadius - 5f, 1.8f, 96, 8));
            CreateMeshVisual("ExactLilyCoilGround", parent, new Vector3(0f, WofLilyCoilLayout.GroundY, 0f),
                disk, materials.Ground);
            CreateMeshVisual("ExactLilyCoilGroundRing", parent, new Vector3(0f, WofLilyCoilLayout.GroundY + 0.04f, 0f),
                ring, materials.GroundRing);

            var groundCollider = new GameObject("LilyCoilGroundCollider");
            groundCollider.transform.SetParent(parent, false);
            var box = groundCollider.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, WofLilyCoilLayout.GroundY - 1f, 0f);
            box.size = new Vector3(WofLilyCoilLayout.RealmRadius * 2f, 2f, WofLilyCoilLayout.RealmRadius * 2f);

            var wallRoot = new GameObject("LilyCoilCylinderWall_36");
            wallRoot.transform.SetParent(parent, false);
            var arcLength = Mathf.PI * 2f * WofLilyCoilLayout.RealmRadius / WofLilyCoilLayout.WallSegmentCount;
            for (var index = 0; index < WofLilyCoilLayout.WallSegmentCount; index++)
            {
                var angle = index / (float)WofLilyCoilLayout.WallSegmentCount * Mathf.PI * 2f;
                var segment = new GameObject($"WallCollider_{index:00}");
                segment.transform.SetParent(wallRoot.transform, false);
                segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * WofLilyCoilLayout.RealmRadius,
                    WofLilyCoilLayout.GroundY + WofLilyCoilLayout.WallHeight * 0.5f,
                    Mathf.Sin(angle) * WofLilyCoilLayout.RealmRadius);
                segment.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                segment.AddComponent<BoxCollider>().size = new Vector3(6.4f, WofLilyCoilLayout.WallHeight, arcLength);
            }
        }

        private static void CreateLilyCoilTunnel(Transform parent, WofLilyCoilDocument document,
            LilyCoilMaterialSet materials)
        {
            var tunnel = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/ExactTunnel.asset",
                () => CreateDesertSerializedMesh("ExactLilyCoilTunnel", document.geometries.tunnel));
            var colliderMesh = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/ExactTunnelCollider.asset",
                () => CreateDesertSerializedMesh("ExactLilyCoilTunnelCollider", document.geometries.tunnelCollider));
            CreateMeshVisual("LilyCoilTunnelInner", parent, Vector3.zero, tunnel, materials.TunnelInner);
            CreateMeshVisual("LilyCoilTunnelOuter", parent, Vector3.zero, tunnel, materials.TunnelOuter);
            var collider = new GameObject("LilyCoilTunnelExactCollider");
            collider.transform.SetParent(parent, false);
            collider.AddComponent<MeshCollider>().sharedMesh = colliderMesh;

            var sphere = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/HighlightSphere.asset",
                () => CreateUvSphereMesh(5.2f, 10, 6));
            for (var index = 0; index < 7; index++)
            {
                var t = (index + 0.35f) / 7.6f;
                var frame = LocalLilyFrame(t);
                var point = frame.Center;
                var highlight = CreateMeshVisual($"TunnelHighlight_{index}", parent,
                    new Vector3(point.x * 0.985f, point.y + WofLilyCoilLayout.TubeRadius * 0.46f, point.z * 0.985f),
                    sphere, materials.Highlight);
                highlight.transform.localRotation = Quaternion.Euler(0.2f * Mathf.Rad2Deg,
                    (-WofLilyCoilLayout.TubeStartAngle - Mathf.PI * 2f * WofLilyCoilLayout.TubeTurns * t) * Mathf.Rad2Deg,
                    -0.24f * Mathf.Rad2Deg);
                highlight.transform.localScale = new Vector3(3.4f, 0.48f, 1.05f);
            }

            CreateLilyCoilCap(parent, 0f, "#93c5fd", document, materials);
            CreateLilyCoilCap(parent, 1f, "#86efac", document, materials);
        }

        private static void CreateLilyCoilCap(Transform parent, float t, string glowColor,
            WofLilyCoilDocument document, LilyCoilMaterialSet materials)
        {
            var frame = LocalLilyFrame(t);
            var facing = frame.Tangent * (t <= 0f ? 1f : -1f);
            var rotation = Quaternion.LookRotation(facing.normalized, frame.Up);
            var root = new GameObject($"LilyCoilEndCap_{t:0}");
            root.transform.SetParent(parent, false);
            root.transform.SetLocalPositionAndRotation(frame.Center, rotation);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(WofLilyCoilLayout.EyeCapRadius * 2.16f,
                WofLilyCoilLayout.EyeCapRadius * 2.16f, 44f);

            var sphere = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/EndSealSphere.asset",
                () => CreateUvSphereMesh(1f, 32, 14));
            var seal = CreateMeshVisual("SolidSeal", root.transform, Vector3.zero, sphere, materials.Seal);
            seal.transform.localScale = new Vector3(WofLilyCoilLayout.EyeCapRadius * 1.08f,
                WofLilyCoilLayout.EyeCapRadius * 1.08f, 20f);
            var darkDisk = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/EyeDarkDisk72.asset",
                () => CreateRingMesh(0f, WofLilyCoilLayout.EyeCapRadius * 1.12f, 72));
            var ring = GetOrCreateMeshAsset(LilyCoilGeometryRoot + "/EyeRing72.asset",
                () => CreateRingMesh(WofLilyCoilLayout.EyeCapRadius * 0.97f,
                    WofLilyCoilLayout.EyeCapRadius * 1.1f, 72));
            CreateMeshVisual("EyeBackdrop", root.transform, new Vector3(0f, 0f, 22.05f), darkDisk, materials.CapDark);
            CreateMeshVisual("EyeRing", root.transform, new Vector3(0f, 0f, 22.12f), ring, materials.CapRing);

            var eye = new GameObject("AnimatedEye", typeof(SpriteRenderer), typeof(WofLilyCoilEyeAnimator));
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, 22.18f);
            var renderer = eye.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 20;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var frames = new Sprite[WofLilyCoilLayout.EyeFrameCount];
            for (var index = 0; index < frames.Length; index++)
                frames[index] = LoadRequiredAsset<Sprite>($"{LilyCoilEyeRoot}/eye_{index:000}.png");
            eye.GetComponent<WofLilyCoilEyeAnimator>().Configure(renderer, frames,
                WofLilyCoilLayout.EyeFrameFps, WofLilyCoilLayout.EyeCapRadius * 2.06f);
            CreatePointLight("EyeGlow", root.transform, new Vector3(0f, 0f, 28f), HexColor(glowColor), 1.35f, 82f);
        }

        private static void CreateLilyCoilFlora(Transform parent, WofLilyCoilDocument document,
            LilyCoilMaterialSet materials)
        {
            CreateLilyCoilGroundFlora(parent, document, materials);
            CreateLilyCoilTubeFlora(parent, document, materials);
        }

        private static void CreateLilyCoilGroundFlora(Transform parent, WofLilyCoilDocument document,
            LilyCoilMaterialSet materials)
        {
            var grassBuilder = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.groundGrass.Length; index++)
            for (var bladeIndex = 0; bladeIndex < 2; bladeIndex++)
            {
                var blade = document.flora.groundGrass[index];
                var yaw = blade.yaw + bladeIndex / 2f * Mathf.PI * 2f + index % 4 * 0.11f;
                var height = blade.height * (0.78f + LilyNoise(index, 58 + bladeIndex) * 0.44f);
                var width = blade.width * (0.62f + LilyNoise(index, 64 + bladeIndex) * 0.42f);
                var spread = 0.44f + LilyNoise(index, 70 + bladeIndex) * 1.24f;
                var position = new Vector3(blade.x + Mathf.Cos(yaw) * spread,
                    WofLilyCoilLayout.GroundY + height * 0.5f, blade.z + Mathf.Sin(yaw) * spread);
                var rotation = Quaternion.Euler(
                    (blade.lean + (LilyNoise(index, 76 + bladeIndex) - 0.5f) * 0.42f) * Mathf.Rad2Deg,
                    yaw * Mathf.Rad2Deg, Mathf.Sin(yaw) * 0.16f * Mathf.Rad2Deg);
                grassBuilder.AppendQuad(Matrix4x4.TRS(position, rotation, new Vector3(width, height, 1f)));
            }
            CreateCombinedLilyVisual("GroundGrass_10400", parent, grassBuilder,
                LilyCoilGeometryRoot + "/GroundGrass.asset", materials.GroundGrass);

            var petals = new LilyMeshBuilder();
            var glows = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.groundLilies.Length; index++)
            {
                var lily = document.flora.groundLilies[index];
                var y = WofLilyCoilLayout.GroundY + 0.18f + index % 5 * 0.012f;
                for (var petalIndex = 0; petalIndex < 5; petalIndex++)
                {
                    var angle = lily.yaw + petalIndex / 5f * Mathf.PI * 2f + index % 4 * 0.11f;
                    var offset = lily.scale * 0.42f;
                    var position = new Vector3(lily.x + Mathf.Cos(angle) * offset, y,
                        lily.z + Mathf.Sin(angle) * offset);
                    petals.AppendDisk(Matrix4x4.TRS(position,
                        Quaternion.Euler(-90f, 0f, angle * Mathf.Rad2Deg),
                        new Vector3(lily.scale * 0.26f, lily.scale * 0.5f, 1f)), 8);
                }
                glows.AppendDisk(Matrix4x4.TRS(new Vector3(lily.x, y + 0.02f, lily.z),
                    Quaternion.Euler(-90f, 0f, lily.yaw * Mathf.Rad2Deg),
                    new Vector3(lily.scale * 1.9f, lily.scale * 1.9f, 1f)), 10);
            }
            CreateCombinedLilyVisual("GroundLilyPetals_2800", parent, petals,
                LilyCoilGeometryRoot + "/GroundLilyPetals.asset", materials.GroundLilyPetal);
            CreateCombinedLilyVisual("GroundLilyGlow_560", parent, glows,
                LilyCoilGeometryRoot + "/GroundLilyGlow.asset", materials.WhiteGlow);
        }

        private static void CreateLilyCoilTubeFlora(Transform parent, WofLilyCoilDocument document,
            LilyCoilMaterialSet materials)
        {
            var grassBuilders = new[] { new LilyMeshBuilder(), new LilyMeshBuilder(), new LilyMeshBuilder() };
            for (var index = 0; index < document.flora.tubeGrass.Length; index++)
            {
                var tuft = document.flora.tubeGrass[index];
                var frame = LocalLilyFrame(tuft.t);
                var radial = (frame.Up * Mathf.Cos(tuft.angle) + frame.Side * Mathf.Sin(tuft.angle)).normalized;
                var inward = -radial;
                var around = (frame.Up * -Mathf.Sin(tuft.angle) + frame.Side * Mathf.Cos(tuft.angle)).normalized;
                var widthAxis = (frame.Tangent * Mathf.Cos(tuft.yaw) + around * Mathf.Sin(tuft.yaw)).normalized;
                var windLean = around * (Mathf.Sin(tuft.yaw) * tuft.lean) +
                               frame.Tangent * (Mathf.Cos(tuft.yaw) * tuft.lean * 0.62f);
                var growth = (inward + windLean).normalized;
                var normal = Vector3.Cross(widthAxis, growth).normalized;
                var height = tuft.height * (0.86f + LilyNoise(index, 211) * 0.34f);
                var width = tuft.width * (0.82f + LilyNoise(index, 221) * 0.36f);
                var basePosition = frame.Center + radial * tuft.radius;
                var position = basePosition + widthAxis * ((LilyNoise(index, 231) - 0.5f) * 2.6f) + growth * (height * 0.5f);
                var rotation = Quaternion.LookRotation(normal, growth);
                grassBuilders[Mathf.Clamp(tuft.group, 0, 2)].AppendQuad(
                    Matrix4x4.TRS(position, rotation, new Vector3(width, height, width * 0.28f)));
            }
            for (var group = 0; group < grassBuilders.Length; group++)
                CreateCombinedLilyVisual($"TubeGrassGroup_{group}", parent, grassBuilders[group],
                    $"{LilyCoilGeometryRoot}/TubeGrass{group}.asset", materials.TubeGrass[group]);

            var lilyPetals = new LilyMeshBuilder();
            var lilyGlows = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.tubeLilies.Length; index++)
            {
                var lily = document.flora.tubeLilies[index];
                var frame = LocalLilyFrame(lily.t);
                var radial = (frame.Up * Mathf.Cos(lily.angle) + frame.Side * Mathf.Sin(lily.angle)).normalized;
                var inward = -radial;
                var around = Vector3.Cross(frame.Tangent, inward).normalized;
                var basis = Quaternion.LookRotation(inward, around);
                var center = frame.Center + radial * (WofLilyCoilLayout.TubeRadius - 2.1f);
                for (var petalIndex = 0; petalIndex < 5; petalIndex++)
                {
                    var angle = lily.yaw + petalIndex / 5f * Mathf.PI * 2f + index % 3 * 0.13f;
                    var position = center + frame.Tangent * (Mathf.Cos(angle) * lily.scale * 0.42f) +
                                   around * (Mathf.Sin(angle) * lily.scale * 0.42f);
                    lilyPetals.AppendDisk(Matrix4x4.TRS(position,
                        basis * Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward),
                        new Vector3(lily.scale * 0.18f, lily.scale * 0.36f, 1f)), 8);
                }
                lilyGlows.AppendDisk(Matrix4x4.TRS(center,
                    basis * Quaternion.AngleAxis(lily.yaw * Mathf.Rad2Deg, Vector3.forward),
                    new Vector3(lily.scale * 2.1f, lily.scale * 2.1f, 1f)), 10);
            }
            CreateCombinedLilyVisual("TubeLilyPetals_6590", parent, lilyPetals,
                LilyCoilGeometryRoot + "/TubeLilyPetals.asset", materials.LilyPetal);
            CreateCombinedLilyVisual("TubeLilyGlow_1318", parent, lilyGlows,
                LilyCoilGeometryRoot + "/TubeLilyGlow.asset", materials.WhiteGlow);

            var flowerAnchors = MakeLilyAnchors(document.flora.tubeFlowers, 1.8f, 0.44f);
            var smallAnchors = MakeLilyAnchors(document.flora.smallTubeFlowers, 1.6f, 0.56f);
            CreateLilyFlowers(parent, document.flora.tubeFlowers, flowerAnchors, false, materials);
            CreateLilyFlowers(parent, document.flora.smallTubeFlowers, smallAnchors, true, materials);
            CreateLilyParticles(parent, document, flowerAnchors, smallAnchors, materials);
        }

        private static LilyAnchor[] MakeLilyAnchors(WofLilyCoilFlowerRecord[] flowers, float radiusInset,
            float glowHeightFactor)
        {
            var anchors = new LilyAnchor[flowers.Length];
            for (var index = 0; index < flowers.Length; index++)
            {
                var flower = flowers[index];
                var frame = LocalLilyFrame(flower.t);
                var radial = (frame.Up * Mathf.Cos(flower.angle) + frame.Side * Mathf.Sin(flower.angle)).normalized;
                var growth = -radial;
                var around = (frame.Up * -Mathf.Sin(flower.angle) + frame.Side * Mathf.Cos(flower.angle)).normalized;
                var width = (frame.Tangent * Mathf.Cos(flower.yaw) + around * Mathf.Sin(flower.yaw)).normalized;
                var normal = Vector3.Cross(width, growth).normalized;
                var basePosition = frame.Center + radial * (WofLilyCoilLayout.TubeRadius - radiusInset);
                anchors[index] = new LilyAnchor(basePosition, growth, width, normal,
                    basePosition + growth * (flower.stemHeight + flower.bloomHeight * glowHeightFactor));
            }
            return anchors;
        }

        private static void CreateLilyFlowers(Transform parent, WofLilyCoilFlowerRecord[] flowers,
            LilyAnchor[] anchors, bool small, LilyCoilMaterialSet materials)
        {
            var stems = new LilyMeshBuilder();
            var blooms = new LilyMeshBuilder();
            var glows = new LilyMeshBuilder();
            for (var index = 0; index < flowers.Length; index++)
            {
                var flower = flowers[index];
                var anchor = anchors[index];
                var basis = Quaternion.LookRotation(anchor.Normal, anchor.Growth);
                var stemWidth = (small ? 0.12f : 0.34f) * flower.scale;
                stems.AppendCylinder(Matrix4x4.TRS(anchor.Base + anchor.Growth * (flower.stemHeight * 0.5f), basis,
                    new Vector3(stemWidth, flower.stemHeight, stemWidth)), 5);
                if (small)
                {
                    for (var petalIndex = 0; petalIndex < 4; petalIndex++)
                    {
                        var angle = flower.yaw + petalIndex / 4f * Mathf.PI * 2f + flower.tilt;
                        var position = anchor.Glow + anchor.Width * (Mathf.Cos(angle) * flower.bloomWidth * 0.34f) +
                                       anchor.Growth * (Mathf.Sin(angle) * flower.bloomWidth * 0.34f * 0.7f);
                        blooms.AppendDisk(Matrix4x4.TRS(position,
                            basis * Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward),
                            new Vector3(flower.bloomWidth * 0.36f, flower.bloomHeight * 0.27f, 1f)), 10);
                    }
                    glows.AppendDisk(Matrix4x4.TRS(anchor.Glow, basis,
                        new Vector3(flower.bloomWidth * 0.48f, flower.bloomHeight * 0.38f, 1f)), 12);
                }
                else
                {
                    blooms.AppendQuad(Matrix4x4.TRS(
                        anchor.Base + anchor.Growth * (flower.stemHeight + flower.bloomHeight * 0.48f),
                        basis * Quaternion.AngleAxis(flower.tilt * Mathf.Rad2Deg, Vector3.forward),
                        new Vector3(flower.bloomWidth, flower.bloomHeight, 1f)));
                    glows.AppendDisk(Matrix4x4.TRS(anchor.Glow, basis,
                        new Vector3(flower.bloomWidth * 0.72f, flower.bloomHeight * 0.42f, 1f)), 12);
                }
            }
            var prefix = small ? "SmallTubeFlower" : "TubeFlower";
            CreateCombinedLilyVisual(prefix + "Stems", parent, stems,
                $"{LilyCoilGeometryRoot}/{prefix}Stems.asset", small ? materials.SmallStem : materials.FlowerStem);
            CreateCombinedLilyVisual(prefix + "Blooms", parent, blooms,
                $"{LilyCoilGeometryRoot}/{prefix}Blooms.asset", small ? materials.SmallBloom : materials.FlowerBloom);
            CreateCombinedLilyVisual(prefix + "Glow", parent, glows,
                $"{LilyCoilGeometryRoot}/{prefix}Glow.asset", small ? materials.PinkGlow : materials.WhiteGlow);
        }

        private static void CreateLilyParticles(Transform parent, WofLilyCoilDocument document,
            LilyAnchor[] flowers, LilyAnchor[] smallFlowers, LilyCoilMaterialSet materials)
        {
            var particles = new LilyMeshBuilder();
            var particleGlows = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.smallBloomParticles.Length; index++)
            {
                var particle = document.flora.smallBloomParticles[index];
                var anchor = smallFlowers[particle.flowerIndex % smallFlowers.Length];
                var orbit = particle.phase;
                var position = anchor.Glow + anchor.Width * (Mathf.Cos(orbit) * particle.radius) +
                               anchor.Normal * (Mathf.Sin(orbit * 0.86f) * particle.radius * 0.72f) +
                               anchor.Growth * (particle.height + Mathf.Sin(particle.phase) * 0.72f);
                var sparkle = 0.76f + Mathf.Sin(particle.phase) * 0.24f;
                particles.AppendSphere(Matrix4x4.TRS(position, Quaternion.identity,
                    Vector3.one * (particle.size * sparkle)), 6, 4);
                particleGlows.AppendSphere(Matrix4x4.TRS(position, Quaternion.identity,
                    Vector3.one * (particle.size * (2f + sparkle * 1.45f))), 7, 4);
            }
            CreateCombinedLilyVisual("SmallBloomParticles_750", parent, particles,
                LilyCoilGeometryRoot + "/SmallBloomParticles.asset", materials.Particle);
            CreateCombinedLilyVisual("SmallBloomParticleGlow_750", parent, particleGlows,
                LilyCoilGeometryRoot + "/SmallBloomParticleGlow.asset", materials.PinkGlow);

            var fireflies = new LilyMeshBuilder();
            var fireflyGlows = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.fireflies.Length; index++)
            {
                var fly = document.flora.fireflies[index];
                var route = fly.phase;
                var step = Mathf.FloorToInt(route);
                var amount = Mathf.SmoothStep(0f, 1f, route - step);
                var from = flowers[(fly.anchor + step * fly.hop) % flowers.Length];
                var to = flowers[(fly.anchor + (step + 1) * fly.hop) % flowers.Length];
                var position = Vector3.Lerp(from.Glow, to.Glow, amount) +
                               from.Growth * (Mathf.Sin(amount * Mathf.PI) * fly.arc) +
                               from.Width * (Mathf.Sin(fly.phase) * fly.wander) +
                               from.Normal * (Mathf.Cos(fly.phase * 1.7f) * fly.wander * 0.55f);
                fireflies.AppendSphere(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * (fly.size * 0.72f)), 6, 4);
                var blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(fly.phase * 2.1f)), 8f);
                var glowSize = blink > 0.035f ? fly.size * (3.2f + blink * 9.5f) : 0.001f;
                fireflyGlows.AppendSphere(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * glowSize), 8, 5);
            }
            CreateCombinedLilyVisual("Fireflies_160", parent, fireflies,
                LilyCoilGeometryRoot + "/Fireflies.asset", materials.Firefly);
            CreateCombinedLilyVisual("FireflyGlow_160", parent, fireflyGlows,
                LilyCoilGeometryRoot + "/FireflyGlow.asset", materials.FireflyGlow);

            var leftWings = new LilyMeshBuilder();
            var rightWings = new LilyMeshBuilder();
            var bodies = new LilyMeshBuilder();
            for (var index = 0; index < document.flora.butterflies.Length; index++)
            {
                var butterfly = document.flora.butterflies[index];
                var route = butterfly.phase;
                var step = Mathf.FloorToInt(route);
                var amount = Mathf.SmoothStep(0f, 1f, route - step);
                var from = flowers[(butterfly.anchor + step * butterfly.hop) % flowers.Length];
                var to = flowers[(butterfly.anchor + (step + 1) * butterfly.hop) % flowers.Length];
                var position = Vector3.Lerp(from.Glow, to.Glow, amount) +
                               from.Growth * (Mathf.Sin(amount * Mathf.PI) * butterfly.arc) +
                               from.Width * (Mathf.Sin(butterfly.phase) * butterfly.wander) +
                               from.Normal * (Mathf.Cos(butterfly.phase) * butterfly.wander * 0.75f);
                var flap = Mathf.Sin(butterfly.phase);
                var spread = butterfly.size * (0.82f + Mathf.Abs(flap) * 0.32f);
                leftWings.AppendDisk(Matrix4x4.TRS(position + Vector3.left * (spread * 1.15f),
                    Quaternion.AngleAxis((-0.35f - Mathf.Abs(flap) * 0.52f) * Mathf.Rad2Deg, Vector3.forward),
                    new Vector3(butterfly.size * 1.25f, butterfly.size * 1.85f, 1f)), 9);
                rightWings.AppendDisk(Matrix4x4.TRS(position + Vector3.right * (spread * 1.15f),
                    Quaternion.AngleAxis((0.35f + Mathf.Abs(flap) * 0.52f) * Mathf.Rad2Deg, Vector3.forward),
                    new Vector3(butterfly.size * 1.25f, butterfly.size * 1.85f, 1f)), 9);
                bodies.AppendSphere(Matrix4x4.TRS(position, Quaternion.identity,
                    new Vector3(butterfly.size * 0.2f, butterfly.size * 0.72f, butterfly.size * 0.2f)), 6, 4);
            }
            CreateCombinedLilyVisual("ButterflyLeftWings_10", parent, leftWings,
                LilyCoilGeometryRoot + "/ButterflyLeftWings.asset", materials.ButterflyLeft);
            CreateCombinedLilyVisual("ButterflyRightWings_10", parent, rightWings,
                LilyCoilGeometryRoot + "/ButterflyRightWings.asset", materials.ButterflyRight);
            CreateCombinedLilyVisual("ButterflyBodies_10", parent, bodies,
                LilyCoilGeometryRoot + "/ButterflyBodies.asset", materials.ButterflyBody);
        }

        private static void CreateLilyCoilLights(Transform parent, WofLilyCoilDocument document)
        {
            CreatePointLight("RealmPurpleLight", parent, new Vector3(0f, WofLilyCoilLayout.GroundY + 34f, 0f),
                HexColor("#c084fc"), 3.2f, 230f);
            CreatePointLight("RealmWhiteLight", parent, new Vector3(0f, WofLilyCoilLayout.GroundY + 118f, 0f),
                Color.white, 1.8f, 190f);
            for (var index = 0; index < document.flora.groundLilyLights.Length; index++)
            {
                var lily = document.flora.groundLilyLights[index];
                CreatePointLight($"GroundLilyLight_{index}", parent,
                    new Vector3(lily.x, WofLilyCoilLayout.GroundY + 2.4f, lily.z), Color.white, 0.85f, 34f);
            }
            var flowerAnchors = MakeLilyAnchors(document.flora.tubeFlowers, 1.8f, 0.44f);
            for (var index = 0; index < flowerAnchors.Length && index / 36 < 8; index += 36)
                CreatePointLight($"CallaLight_{index / 36}", parent, flowerAnchors[index].Glow,
                    index / 36 % 3 == 0 ? HexColor("#fde68a") : HexColor("#f0abfc"), 0.82f, 42f);
            for (var index = 0; index < document.flora.tubeLilies.Length && index / 360 < 5; index += 360)
            {
                var lily = document.flora.tubeLilies[index];
                var frame = LocalLilyFrame(lily.t);
                var radial = (frame.Up * Mathf.Cos(lily.angle) + frame.Side * Mathf.Sin(lily.angle)).normalized;
                CreatePointLight($"TubeLilyLight_{index / 360}", parent,
                    frame.Center + radial * (WofLilyCoilLayout.TubeRadius - 8f), Color.white, 1.18f, 68f);
            }
        }

        private static void CreatePointLight(string name, Transform parent, Vector3 position, Color color,
            float intensity, float range)
        {
            var item = new GameObject(name, typeof(Light));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            var light = item.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreateCombinedLilyVisual(string name, Transform parent, LilyMeshBuilder builder,
            string assetPath, Material material)
        {
            var mesh = GetOrCreateMeshAsset(assetPath, () => builder.Build(name));
            var visual = CreateMeshVisual(name, parent, Vector3.zero, mesh, material);
            visual.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            visual.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        private static WofLilyCoilFrame LocalLilyFrame(float t)
        {
            var frame = WofLilyCoilLayout.GetFrame(t);
            return new WofLilyCoilFrame(frame.Center - WofLilyCoilLayout.WorldOrigin,
                frame.Tangent, frame.Up, frame.Side);
        }

        private static float LilyNoise(int index, int salt)
        {
            var noise = Math.Sin(index * 91.731 + salt * 47.117) * 43758.5453123;
            return (float)(noise - Math.Floor(noise));
        }

        private readonly struct LilyAnchor
        {
            public LilyAnchor(Vector3 basePosition, Vector3 growth, Vector3 width, Vector3 normal, Vector3 glow)
            {
                Base = basePosition;
                Growth = growth;
                Width = width;
                Normal = normal;
                Glow = glow;
            }
            public Vector3 Base { get; }
            public Vector3 Growth { get; }
            public Vector3 Width { get; }
            public Vector3 Normal { get; }
            public Vector3 Glow { get; }
        }

        private sealed class LilyMeshBuilder
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _normals = new();
            private readonly List<Vector2> _uv = new();
            private readonly List<int> _triangles = new();

            public void AppendQuad(Matrix4x4 matrix)
            {
                var vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                var uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                Append(matrix, vertices, new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward },
                    uv, new[] { 0, 2, 1, 2, 3, 1 });
            }

            public void AppendDisk(Matrix4x4 matrix, int segments)
            {
                var vertices = new Vector3[segments + 1];
                var normals = new Vector3[segments + 1];
                var uv = new Vector2[segments + 1];
                var triangles = new int[segments * 3];
                vertices[0] = Vector3.zero;
                normals[0] = Vector3.forward;
                uv[0] = new Vector2(0.5f, 0.5f);
                for (var index = 0; index < segments; index++)
                {
                    var angle = index / (float)segments * Mathf.PI * 2f;
                    vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    normals[index + 1] = Vector3.forward;
                    uv[index + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
                    triangles[index * 3] = 0;
                    triangles[index * 3 + 1] = index + 1;
                    triangles[index * 3 + 2] = (index + 1) % segments + 1;
                }
                Append(matrix, vertices, normals, uv, triangles);
            }

            public void AppendCylinder(Matrix4x4 matrix, int segments)
            {
                var vertices = new Vector3[(segments + 1) * 2];
                var normals = new Vector3[vertices.Length];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[segments * 6];
                for (var index = 0; index <= segments; index++)
                {
                    var amount = index / (float)segments;
                    var angle = amount * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices[index * 2] = radial + Vector3.down * 0.5f;
                    vertices[index * 2 + 1] = radial + Vector3.up * 0.5f;
                    normals[index * 2] = normals[index * 2 + 1] = radial;
                    uv[index * 2] = new Vector2(amount, 0f);
                    uv[index * 2 + 1] = new Vector2(amount, 1f);
                    if (index == segments) continue;
                    var start = index * 2;
                    triangles[index * 6] = start;
                    triangles[index * 6 + 1] = start + 1;
                    triangles[index * 6 + 2] = start + 2;
                    triangles[index * 6 + 3] = start + 2;
                    triangles[index * 6 + 4] = start + 1;
                    triangles[index * 6 + 5] = start + 3;
                }
                Append(matrix, vertices, normals, uv, triangles);
            }

            public void AppendSphere(Matrix4x4 matrix, int widthSegments, int heightSegments)
            {
                var vertices = new List<Vector3>((widthSegments + 1) * (heightSegments + 1));
                var normals = new List<Vector3>(vertices.Capacity);
                var uv = new List<Vector2>(vertices.Capacity);
                var triangles = new List<int>(widthSegments * heightSegments * 6);
                for (var y = 0; y <= heightSegments; y++)
                {
                    var v = y / (float)heightSegments;
                    var theta = v * Mathf.PI;
                    for (var x = 0; x <= widthSegments; x++)
                    {
                        var u = x / (float)widthSegments;
                        var phi = u * Mathf.PI * 2f;
                        var vertex = new Vector3(-Mathf.Cos(phi) * Mathf.Sin(theta), Mathf.Cos(theta),
                            Mathf.Sin(phi) * Mathf.Sin(theta));
                        vertices.Add(vertex);
                        normals.Add(vertex.normalized);
                        uv.Add(new Vector2(u, 1f - v));
                    }
                }
                var stride = widthSegments + 1;
                for (var y = 0; y < heightSegments; y++)
                for (var x = 0; x < widthSegments; x++)
                {
                    var a = y * stride + x;
                    var b = a + stride;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
                Append(matrix, vertices.ToArray(), normals.ToArray(), uv.ToArray(), triangles.ToArray());
            }

            private void Append(Matrix4x4 matrix, Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles)
            {
                var start = _vertices.Count;
                for (var index = 0; index < vertices.Length; index++)
                {
                    _vertices.Add(matrix.MultiplyPoint3x4(vertices[index]));
                    _normals.Add(matrix.MultiplyVector(normals[index]).normalized);
                    _uv.Add(uv[index]);
                }
                for (var index = 0; index < triangles.Length; index++) _triangles.Add(start + triangles[index]);
            }

            public Mesh Build(string name)
            {
                if (_vertices.Count == 0) throw new InvalidOperationException($"Lily Coil mesh {name} has no vertices.");
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private sealed class LilyCoilMaterialSet
        {
            public Material Ground;
            public Material GroundRing;
            public Material TunnelInner;
            public Material TunnelOuter;
            public Material Seal;
            public Material CapDark;
            public Material CapRing;
            public Material GroundGrass;
            public Material[] TubeGrass;
            public Material LilyPetal;
            public Material GroundLilyPetal;
            public Material WhiteGlow;
            public Material FlowerStem;
            public Material FlowerBloom;
            public Material SmallStem;
            public Material SmallBloom;
            public Material PinkGlow;
            public Material Particle;
            public Material Firefly;
            public Material FireflyGlow;
            public Material ButterflyLeft;
            public Material ButterflyRight;
            public Material ButterflyBody;
            public Material Highlight;
        }
    }
}
