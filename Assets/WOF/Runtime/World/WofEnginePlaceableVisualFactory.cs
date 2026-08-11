using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    public static class WofEnginePlaceableVisualFactory
    {
        private static readonly Dictionary<string, Material> OpaqueMaterials = new(StringComparer.Ordinal);

        public static GameObject Create(
            WofEnginePlaceableDefinition definition,
            Transform parent,
            string name,
            float opacity = 1f,
            bool addCollider = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            if (definition.Id == "training-spell-dummy") CreateTrainingDummy(definition, root.transform, opacity);
            else if (definition.Id == "campfire-small") CreateCampfire(definition, root.transform, opacity);
            else if (definition.Category == WofEnginePlaceableCategory.Nature) CreateNature(definition, root.transform, opacity);
            else if (definition.Category == WofEnginePlaceableCategory.Magic) CreateMagic(definition, root.transform, opacity);
            else CreateBuilding(definition, root.transform, opacity);
            Collider placementCollider = null;
            if (addCollider) placementCollider = AddCollider(definition, root.transform);
            if (definition.Id == "training-spell-dummy" && addCollider)
            {
                var runtime = root.AddComponent<WofTrainingDummyRuntime>();
                runtime.Initialize(name, root.transform.Find("Model"), placementCollider);
            }
            return root;
        }

        public static void ClearMaterialCache()
        {
            foreach (var material in OpaqueMaterials.Values)
            {
                if (material != null) UnityEngine.Object.Destroy(material);
            }
            OpaqueMaterials.Clear();
        }

        private static void CreateTrainingDummy(WofEnginePlaceableDefinition definition, Transform parent, float opacity)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            CreatePrimitive("Base", PrimitiveType.Cylinder, model, new Vector3(0f, -1.58f, 0f),
                new Vector3(2.75f, 0.18f, 2.75f), Vector3.zero, new Color32(51, 65, 85, 255), opacity);
            CreatePrimitive("Body", PrimitiveType.Cube, model, new Vector3(0f, -0.25f, 0f),
                new Vector3(1.72f, 2.72f, 1.08f), Vector3.zero, definition.AccentColor, opacity);
            CreatePrimitive("Head", PrimitiveType.Cube, model, new Vector3(0f, 1.15f, 0f),
                new Vector3(1.22f, 0.86f, 0.86f), Vector3.zero, new Color32(253, 230, 138, 255), opacity);
            CreatePrimitive("Target", PrimitiveType.Cube, model, new Vector3(0f, -0.25f, -0.55f),
                new Vector3(1.32f, 1.86f, 0.05f), Vector3.zero, new Color32(17, 24, 39, 255), opacity * 0.86f);
            CreatePrimitive("CrossX", PrimitiveType.Cube, model, new Vector3(0f, 1.78f, 0f),
                new Vector3(2.45f, 0.18f, 0.18f), Vector3.zero, definition.AccentColor, opacity);
            CreatePrimitive("CrossZ", PrimitiveType.Cube, model, new Vector3(0f, 1.78f, 0f),
                new Vector3(0.18f, 0.18f, 2.45f), Vector3.zero, definition.AccentColor, opacity);
        }

        private static void CreateCampfire(WofEnginePlaceableDefinition definition, Transform parent, float opacity)
        {
            CreatePrimitive("LogA", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.18f, 0f),
                new Vector3(0.48f, 2.1f, 0.48f), new Vector3(90f, 0f, 45f), definition.BaseColor, opacity);
            CreatePrimitive("LogB", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.2f, 0f),
                new Vector3(0.44f, 2f, 0.44f), new Vector3(90f, 0f, -45f), new Color32(58, 37, 21, 255), opacity);
            var flame = CreateMesh("Flame", parent, CreateConeMesh(0.82f, 1.8f, 6),
                new Vector3(0f, 1.12f, 0f), Vector3.one, Vector3.zero, definition.AccentColor, opacity * 0.82f);
            flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (opacity >= 0.99f)
            {
                var lightObject = new GameObject("FireLight");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = definition.HighlightColor;
                light.intensity = 1.4f;
                light.range = 16f;
                light.shadows = LightShadows.None;
            }
        }

        private static void CreateNature(WofEnginePlaceableDefinition definition, Transform parent, float opacity)
        {
            CreatePrimitive("BushMain", PrimitiveType.Sphere, parent, new Vector3(0f, 1.05f, 0f),
                Vector3.one * (definition.FootprintRadius * 1.44f), Vector3.zero, definition.AccentColor, opacity);
            CreatePrimitive("BushHighlight", PrimitiveType.Sphere, parent, new Vector3(0.9f, 1.25f, -0.35f),
                Vector3.one * (definition.FootprintRadius * 0.84f), Vector3.zero, definition.HighlightColor, opacity);
        }

        private static void CreateMagic(WofEnginePlaceableDefinition definition, Transform parent, float opacity)
        {
            CreatePrimitive("Base", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.2f, 0f),
                new Vector3(definition.FootprintRadius * 1.46f, 0.175f, definition.FootprintRadius * 1.46f),
                Vector3.zero, definition.BaseColor, opacity);
            CreateMesh("Ring", parent, CreateTorusMesh(definition.FootprintRadius * 0.48f, 0.12f, 24, 8),
                new Vector3(0f, 2.4f, 0f), Vector3.one, new Vector3(90f, 0f, 0f), definition.AccentColor, opacity * 0.86f);
            CreatePrimitive("Orb", PrimitiveType.Sphere, parent, new Vector3(0f, 2.4f, 0f),
                Vector3.one * 0.76f, Vector3.zero, definition.HighlightColor, opacity * 0.72f);
            if (opacity >= 0.99f)
            {
                var lightObject = new GameObject("MagicLight");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = definition.AccentColor;
                light.intensity = 0.75f;
                light.range = 18f;
                light.shadows = LightShadows.None;
            }
        }

        private static void CreateBuilding(WofEnginePlaceableDefinition definition, Transform parent, float opacity)
        {
            WofEnginePlaceableCatalog.GetBuildingMetrics(definition, out var width, out var depth,
                out var height, out var roofHeight, out var roofSegments);
            CreatePrimitive("Body", PrimitiveType.Cube, parent, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width, height, depth), Vector3.zero, definition.BaseColor, opacity);
            CreateMesh("Roof", parent, CreateConeMesh(Mathf.Max(width, depth) * 0.76f, roofHeight, roofSegments),
                new Vector3(0f, height + roofHeight * 0.46f, 0f), Vector3.one, new Vector3(0f, 45f, 0f),
                definition.AccentColor, opacity);
            CreatePrimitive("Door", PrimitiveType.Cube, parent, new Vector3(0f, 1.8f, depth * 0.5f + 0.04f),
                new Vector3(1.8f, 3.2f, 0.18f), Vector3.zero, new Color32(29, 18, 11, 255), opacity);
        }

        private static Collider AddCollider(WofEnginePlaceableDefinition definition, Transform root)
        {
            if (definition.Id == "campfire-small")
            {
                return AddCylinderCollider(root, 1.8f, 0.9f, new Vector3(0f, 0.35f, 0f));
            }
            if (definition.Id == "training-spell-dummy")
            {
                var collider = root.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(2.16f, 3.44f, 2.16f);
                return collider;
            }
            if (definition.Category == WofEnginePlaceableCategory.Nature)
            {
                return AddCylinderCollider(root, definition.FootprintRadius * 0.75f, 1.6f, new Vector3(0f, 0.8f, 0f));
            }
            if (definition.Category == WofEnginePlaceableCategory.Magic)
            {
                return AddCylinderCollider(root, definition.FootprintRadius * 0.62f, 2.4f, new Vector3(0f, 0.65f, 0f));
            }
            WofEnginePlaceableCatalog.GetBuildingMetrics(definition, out var width, out var depth, out var height, out _, out _);
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(width, height, depth);
            return box;
        }

        private static Collider AddCylinderCollider(Transform root, float radius, float height, Vector3 position)
        {
            var colliderRoot = new GameObject("PlacementCollider");
            colliderRoot.transform.SetParent(root, false);
            colliderRoot.transform.localPosition = position;
            var filter = colliderRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateCylinderMesh(radius, height, 18);
            var collider = colliderRoot.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            return collider;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Color color,
            float opacity)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localEulerAngles = rotation;
            item.transform.localScale = scale;
            var collider = item.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            item.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, opacity);
            return item;
        }

        private static GameObject CreateMesh(
            string name,
            Transform parent,
            Mesh mesh,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Color color,
            float opacity)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localEulerAngles = rotation;
            item.transform.localScale = scale;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(color, opacity);
            return item;
        }

        private static Material GetMaterial(Color color, float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            var key = ColorUtility.ToHtmlStringRGBA(new Color(color.r, color.g, color.b, opacity));
            if (OpaqueMaterials.TryGetValue(key, out var cached)) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = $"EnginePlaceable_{key}",
                color = new Color(color.r, color.g, color.b, opacity)
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color);
            if (opacity < 0.99f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            OpaqueMaterials[key] = material;
            return material;
        }

        private static Mesh CreateConeMesh(float radius, float height, int segments)
        {
            segments = Mathf.Max(3, segments);
            var vertices = new Vector3[segments + 2];
            vertices[0] = new Vector3(0f, height * 0.5f, 0f);
            vertices[1] = new Vector3(0f, -height * 0.5f, 0f);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius);
            }
            var triangles = new int[segments * 6];
            var cursor = 0;
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                triangles[cursor++] = 0;
                triangles[cursor++] = index + 2;
                triangles[cursor++] = next + 2;
                triangles[cursor++] = 1;
                triangles[cursor++] = next + 2;
                triangles[cursor++] = index + 2;
            }
            return FinishMesh("EngineCone", vertices, triangles);
        }

        private static Mesh CreateCylinderMesh(float radius, float height, int segments)
        {
            segments = Mathf.Max(6, segments);
            var vertices = new Vector3[segments * 2 + 2];
            vertices[segments * 2] = new Vector3(0f, height * 0.5f, 0f);
            vertices[segments * 2 + 1] = new Vector3(0f, -height * 0.5f, 0f);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var x = Mathf.Cos(angle) * radius;
                var z = Mathf.Sin(angle) * radius;
                vertices[index * 2] = new Vector3(x, height * 0.5f, z);
                vertices[index * 2 + 1] = new Vector3(x, -height * 0.5f, z);
            }
            var triangles = new int[segments * 12];
            var cursor = 0;
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var top = index * 2;
                var bottom = top + 1;
                var nextTop = next * 2;
                var nextBottom = nextTop + 1;
                triangles[cursor++] = top;
                triangles[cursor++] = bottom;
                triangles[cursor++] = nextTop;
                triangles[cursor++] = nextTop;
                triangles[cursor++] = bottom;
                triangles[cursor++] = nextBottom;
                triangles[cursor++] = segments * 2;
                triangles[cursor++] = nextTop;
                triangles[cursor++] = top;
                triangles[cursor++] = segments * 2 + 1;
                triangles[cursor++] = bottom;
                triangles[cursor++] = nextBottom;
            }
            return FinishMesh("EngineCylinder", vertices, triangles);
        }

        private static Mesh CreateTorusMesh(float radius, float tube, int radialSegments, int tubeSegments)
        {
            var vertices = new Vector3[radialSegments * tubeSegments];
            var triangles = new int[radialSegments * tubeSegments * 6];
            for (var radial = 0; radial < radialSegments; radial++)
            for (var ring = 0; ring < tubeSegments; ring++)
            {
                var u = radial * Mathf.PI * 2f / radialSegments;
                var v = ring * Mathf.PI * 2f / tubeSegments;
                var distance = radius + tube * Mathf.Cos(v);
                vertices[radial * tubeSegments + ring] = new Vector3(
                    Mathf.Cos(u) * distance,
                    tube * Mathf.Sin(v),
                    Mathf.Sin(u) * distance);
            }
            var cursor = 0;
            for (var radial = 0; radial < radialSegments; radial++)
            for (var ring = 0; ring < tubeSegments; ring++)
            {
                var nextRadial = (radial + 1) % radialSegments;
                var nextRing = (ring + 1) % tubeSegments;
                var a = radial * tubeSegments + ring;
                var b = nextRadial * tubeSegments + ring;
                var c = radial * tubeSegments + nextRing;
                var d = nextRadial * tubeSegments + nextRing;
                triangles[cursor++] = a;
                triangles[cursor++] = b;
                triangles[cursor++] = c;
                triangles[cursor++] = c;
                triangles[cursor++] = b;
                triangles[cursor++] = d;
            }
            return FinishMesh("EngineTorus", vertices, triangles);
        }

        private static Mesh FinishMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
