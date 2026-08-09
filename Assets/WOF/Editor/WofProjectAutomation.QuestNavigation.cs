using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private const string QuestBeaconSpritePath = "Assets/WOF/Art/Generated/React/Quest/quest-beacon.png";

        private static void CreateQuestNavigation(Transform parent)
        {
            var runtimeRoot = new GameObject("QuestNavigationRuntime");
            runtimeRoot.transform.SetParent(parent, false);
            var visualRoot = new GameObject("QuestNavigationBeacon");
            visualRoot.transform.SetParent(runtimeRoot.transform, false);

            var beamMaterial = CreateQuestBeaconMaterial("QuestBeaconBeam", 0.24f);
            var ringMaterial = CreateQuestBeaconMaterial("QuestBeaconRing", 0.72f);
            var diskMaterial = CreateQuestBeaconMaterial("QuestBeaconDisk", 0.18f);
            var iconMaterial = GetOrCreateDarrelUnlitMaterial(
                "QuestBeaconIcon",
                new Color(1f, 1f, 1f, 0.96f),
                LoadRequiredAsset<Texture2D>(QuestBeaconSpritePath),
                true,
                true);
            if (iconMaterial.HasProperty("_ZTest"))
            {
                iconMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
            }
            iconMaterial.renderQueue = (int)RenderQueue.Transparent + 20;
            EditorUtility.SetDirty(iconMaterial);

            var beamMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/QuestBeaconBeam.asset",
                () => CreateQuestBeaconOpenCylinderMesh(1.8f, 108f, 10));
            var beamObject = CreateMeshVisual(
                "Beam",
                visualRoot.transform,
                new Vector3(0f, 54f, 0f),
                beamMesh,
                beamMaterial);
            ConfigureQuestBeaconRenderer(beamObject.GetComponent<MeshRenderer>());
            MarkDarrelDynamic(beamObject);

            var ringMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/QuestBeaconRing.asset",
                () => CreateDarrelRingMesh(5.6f, 7.4f, 28));
            var ringObject = CreateMeshVisual(
                "Ring",
                visualRoot.transform,
                Vector3.zero,
                ringMesh,
                ringMaterial);
            ConfigureQuestBeaconRenderer(ringObject.GetComponent<MeshRenderer>());
            MarkDarrelDynamic(ringObject);

            var diskMesh = GetOrCreateMeshAsset(
                DarrelGeometryRoot + "/QuestBeaconDisk.asset",
                () => CreateDarrelDiskMesh(4.2f, 28));
            var diskObject = CreateMeshVisual(
                "Disk",
                visualRoot.transform,
                new Vector3(0f, 0.05f, 0f),
                diskMesh,
                diskMaterial);
            ConfigureQuestBeaconRenderer(diskObject.GetComponent<MeshRenderer>());
            MarkDarrelDynamic(diskObject);

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(visualRoot.transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 12.5f, 0f);
            var icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = LoadRequiredAsset<Sprite>(QuestBeaconSpritePath);
            icon.sharedMaterial = iconMaterial;
            icon.sortingOrder = 100;
            icon.shadowCastingMode = ShadowCastingMode.Off;
            icon.receiveShadows = false;
            var bounds = icon.sprite.bounds.size;
            iconObject.transform.localScale = new Vector3(
                11f / Mathf.Max(0.001f, bounds.x),
                11f / Mathf.Max(0.001f, bounds.y),
                1f);

            var runtime = runtimeRoot.AddComponent<WofQuestNavigationRuntime>();
            runtime.ConfigureGeneratedView(
                visualRoot.transform,
                beamObject.transform,
                ringObject.transform,
                icon,
                beamMaterial,
                ringMaterial,
                diskMaterial,
                iconMaterial);
        }

        private static Material CreateQuestBeaconMaterial(string name, float alpha)
        {
            var material = GetOrCreateUnlitMaterial(name, new Color(1f, 1f, 1f, alpha), true);
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureQuestBeaconRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CreateQuestBeaconOpenCylinderMesh(float radius, float height, int segments)
        {
            var vertices = new List<Vector3>((segments + 1) * 2);
            var uv = new List<Vector2>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);
            var halfHeight = height * 0.5f;
            for (var index = 0; index <= segments; index++)
            {
                var progress = index / (float)segments;
                var angle = progress * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                vertices.Add(radial + Vector3.down * halfHeight);
                vertices.Add(radial + Vector3.up * halfHeight);
                uv.Add(new Vector2(progress, 0f));
                uv.Add(new Vector2(progress, 1f));
                if (index >= segments)
                {
                    continue;
                }
                var current = index * 2;
                triangles.Add(current);
                triangles.Add(current + 2);
                triangles.Add(current + 1);
                triangles.Add(current + 1);
                triangles.Add(current + 2);
                triangles.Add(current + 3);
            }
            return BuildDarrelMesh("QuestBeaconOpenCylinder", vertices, uv, triangles);
        }
    }
}
