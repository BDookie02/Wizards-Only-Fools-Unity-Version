using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WOF.Editor
{
    public static partial class WofProjectAutomation
    {
        private static void CreateChicagoStreetDetails(
            Transform parent,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials)
        {
            var root = new GameObject("ChicagoStreetDetails");
            root.transform.SetParent(parent, false);
            foreach (var plane in document.street.sidewalkPlanes)
            {
                ChicagoFlat("Sidewalk", root.transform, plane.x, document.baseHeight + 0.37f, plane.z, plane.width, plane.depth, materials.SidewalkDetail);
            }
            foreach (var plane in document.street.parkingLines)
            {
                ChicagoFlat("ParkingLine", root.transform, plane.x, document.baseHeight + 0.395f, plane.z, plane.width, plane.depth, materials.Parking);
            }
            foreach (var patch in document.street.grassPatches)
            {
                var material = ChicagoMaterial(
                    $"ChicagoGrass_{patch.color.TrimStart('#')}",
                    WithAlpha(HexColor(patch.color), 0.82f), null, true);
                ChicagoFlat(patch.key, root.transform, patch.x, document.baseHeight + 0.405f, patch.z, patch.width, patch.depth, material);
            }
            foreach (var stripe in document.street.crosswalks)
            {
                var material = Mathf.Approximately(stripe.opacity, 0.76f) ? materials.Crosswalk76 :
                    Mathf.Approximately(stripe.opacity, 0.66f) ? materials.Crosswalk66 : materials.Crosswalk82;
                ChicagoFlat(stripe.key, root.transform, stripe.x, document.baseHeight + 0.43f, stripe.z, stripe.width, stripe.depth, material);
            }
            CreateChicagoHydrants(root.transform, document, materials);
            CreateChicagoLamps(root.transform, document, materials);
            CreateChicagoTrashCans(root.transform, document, materials);
            CreateChicagoBenches(root.transform, document, materials);
            CreateChicagoStreetTrees(root.transform, document, materials);
            CreateChicagoTrafficLights(root.transform, document, materials);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void CreateChicagoHydrants(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("Hydrants");
            root.transform.SetParent(parent, false);
            var capMesh = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/HydrantSphere.asset", () => CreateUvSphereMesh(0.78f, 8, 6));
            foreach (var hydrant in document.street.hydrants)
            {
                var item = new GameObject(hydrant.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(hydrant.x, document.baseHeight + 0.56f, hydrant.z);
                ChicagoCylinder("Body", item.transform, new Vector3(0f, 1f, 0f), 0.7f, 0.74f, 2f, 8, materials.Hydrant);
                ChicagoMeshVisual("Cap", item.transform, new Vector3(0f, 2.25f, 0f), capMesh, materials.HydrantLight);
                var cross = ChicagoCylinder("Cross", item.transform, new Vector3(0f, 1.55f, 0f), 0.24f, 0.24f, 2.2f, 6, materials.HydrantDark);
                cross.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void CreateChicagoLamps(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("StreetLamps");
            root.transform.SetParent(parent, false);
            var glowMesh = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/LampGlowSphere.asset", () => CreateUvSphereMesh(2.2f, 10, 6));
            var colliderMesh = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/LampCollider.asset", () => CreateDarrelFrustumMesh(0.38f, 0.38f, 11.4f, 8));
            foreach (var lamp in document.street.lamps)
            {
                var item = new GameObject(lamp.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(lamp.x, document.baseHeight + 0.48f, lamp.z);
                item.transform.localRotation = Quaternion.Euler(0f, lamp.rotation * Mathf.Rad2Deg, 0f);
                ChicagoCylinder("Pole", item.transform, new Vector3(0f, 5.7f, 0f), 0.28f, 0.38f, 11.4f, 6, materials.DarkMetal);
                var arm = ChicagoCylinder("Arm", item.transform, new Vector3(0f, 11.25f, 1.4f), 0.18f, 0.22f, 3.1f, 6, materials.DarkMetal);
                arm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ChicagoCube("Lamp", item.transform, new Vector3(0f, 10.9f, 2.95f), new Vector3(2.35f, 1.22f, 2f), materials.Lamp);
                ChicagoMeshVisual("Glow", item.transform, new Vector3(0f, 10.9f, 2.95f), glowMesh, materials.LampGlow);
                var collider = new GameObject("PoleCollider");
                collider.transform.SetParent(item.transform, false);
                collider.transform.localPosition = new Vector3(0f, 5.7f, 0f);
                collider.AddComponent<MeshCollider>().sharedMesh = colliderMesh;
                AddChicagoBoxCollider(item.transform, "ArmCollider", new Vector3(0f, 11.25f, 1.4f), new Vector3(0.44f, 0.44f, 3.1f));
                AddChicagoBoxCollider(item.transform, "LampCollider", new Vector3(0f, 10.9f, 2.95f), new Vector3(2.35f, 1.22f, 2f));
            }
        }

        private static void CreateChicagoTrashCans(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("TrashCans");
            root.transform.SetParent(parent, false);
            foreach (var can in document.street.trashCans)
            {
                var item = new GameObject(can.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(can.x, document.baseHeight + 0.56f, can.z);
                ChicagoCylinder("Body", item.transform, new Vector3(0f, 1.05f, 0f), 1.05f, 0.92f, 2.1f, 8, materials.Trash);
                ChicagoCylinder("Top", item.transform, new Vector3(0f, 2.22f, 0f), 1.16f, 1.16f, 0.24f, 8, materials.BlackMetal);
                ChicagoCube("Handle", item.transform, new Vector3(0f, 1.25f, 1f), new Vector3(1.25f, 0.16f, 0.12f), materials.Steel);
            }
        }

        private static void CreateChicagoBenches(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("Benches");
            root.transform.SetParent(parent, false);
            foreach (var bench in document.street.benches)
            {
                var item = new GameObject(bench.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(bench.x, document.baseHeight + 0.58f, bench.z);
                item.transform.localRotation = Quaternion.Euler(0f, bench.rotation * Mathf.Rad2Deg, 0f);
                ChicagoCube("Seat", item.transform, new Vector3(0f, 1.04f, 0f), new Vector3(5.6f, 0.36f, 1.35f), materials.Bench);
                ChicagoCube("Back", item.transform, new Vector3(0f, 1.72f, -0.64f), new Vector3(5.7f, 1.1f, 0.32f), materials.BenchBack);
                ChicagoCube("LegLeft", item.transform, new Vector3(-2.2f, 0.52f, 0f), new Vector3(0.32f, 1f, 0.32f), materials.DarkMetal);
                ChicagoCube("LegRight", item.transform, new Vector3(2.2f, 0.52f, 0f), new Vector3(0.32f, 1f, 0.32f), materials.DarkMetal);
            }
        }

        private static void CreateChicagoStreetTrees(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("StreetTrees");
            root.transform.SetParent(parent, false);
            var dodeca = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/FoliageDodeca.asset", CreateTreeHouseDodecaMesh);
            var trunkCollider = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/TreeTrunkCollider.asset", () => CreateDarrelFrustumMesh(0.78f, 0.78f, 5.3f, 8));
            foreach (var tree in document.street.streetTrees)
            {
                var item = new GameObject(tree.key);
                item.transform.SetParent(root.transform, false);
                item.transform.localPosition = new Vector3(tree.x, document.baseHeight + 0.52f, tree.z);
                item.transform.localScale = Vector3.one * tree.scale;
                ChicagoCylinder("Trunk", item.transform, new Vector3(0f, 2.65f, 0f), 0.55f, 0.78f, 5.3f, 7, materials.TreeTrunk);
                var crown = ChicagoMeshVisual("Crown", item.transform, new Vector3(0f, 6.2f, 0f), dodeca, materials.TreeLeafA);
                crown.transform.localScale = Vector3.one * 2.85f;
                var sideCrown = ChicagoMeshVisual("SideCrown", item.transform, new Vector3(1.1f, 5.55f, -0.8f), dodeca, materials.TreeLeafB);
                sideCrown.transform.localScale = Vector3.one * 2.05f;
                var collider = new GameObject("TrunkCollider");
                collider.transform.SetParent(item.transform, false);
                collider.transform.localPosition = new Vector3(0f, 2.65f, 0f);
                collider.AddComponent<MeshCollider>().sharedMesh = trunkCollider;
            }
        }

        private static void CreateChicagoTrafficLights(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("TrafficLights");
            root.transform.SetParent(parent, false);
            var poleCollider = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/TrafficPoleCollider.asset", () => CreateDarrelFrustumMesh(0.34f, 0.34f, 8.2f, 8));
            var signalSphere = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/TrafficSignalSphere.asset", () => CreateUvSphereMesh(0.33f, 8, 6));
            var poles = new[]
            {
                new { Key = "northwest", X = -18f, Z = -18f, Yaw = 0f },
                new { Key = "southeast", X = 18f, Z = 18f, Yaw = Mathf.PI }
            };
            var signalColors = new[] { "#ef4444", "#facc15", "#22c55e" };
            var signalY = new[] { 8.42f, 7.55f, 6.68f };
            for (var intersectionIndex = 0; intersectionIndex < document.street.trafficLightIntersections.Length; intersectionIndex++)
            {
                var intersection = document.street.trafficLightIntersections[intersectionIndex];
                var intersectionRoot = new GameObject($"TrafficLight_{intersection.key}");
                intersectionRoot.transform.SetParent(root.transform, false);
                foreach (var pole in poles)
                {
                    var poleIndex = string.Equals(pole.Key, "northwest", StringComparison.Ordinal) ? 0 : 1;
                    var item = new GameObject(pole.Key);
                    item.transform.SetParent(intersectionRoot.transform, false);
                    item.transform.localPosition = new Vector3(intersection.x + pole.X, document.baseHeight + 0.36f, intersection.z + pole.Z);
                    item.transform.localRotation = Quaternion.Euler(0f, pole.Yaw * Mathf.Rad2Deg, 0f);
                    ChicagoCylinder("Pole", item.transform, new Vector3(0f, 4.1f, 0f), 0.28f, 0.34f, 8.2f, 6, materials.DarkMetal);
                    var arm = ChicagoCylinder("Arm", item.transform, new Vector3(4f, 8.1f, 0f), 0.18f, 0.18f, 8.1f, 6, materials.DarkMetal);
                    arm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    ChicagoCube("SignalBox", item.transform, new Vector3(8.3f, 7.55f, 0f), new Vector3(1.45f, 3.4f, 1f), materials.BlackMetal);
                    for (var lightIndex = 0; lightIndex < signalColors.Length; lightIndex++)
                    {
                        var alpha = (intersectionIndex + poleIndex + lightIndex) % 3 == 0 ? 1f : 0.42f;
                        var signalMaterial = ChicagoMaterial(
                            $"ChicagoSignal_{signalColors[lightIndex].TrimStart('#')}_{alpha:F2}",
                            WithAlpha(HexColor(signalColors[lightIndex]), alpha), null, alpha < 1f);
                        ChicagoMeshVisual($"Signal_{lightIndex}", item.transform, new Vector3(8.32f, signalY[lightIndex], 0.55f), signalSphere, signalMaterial);
                    }
                    var verticalCollider = new GameObject("PoleCollider");
                    verticalCollider.transform.SetParent(item.transform, false);
                    verticalCollider.transform.localPosition = new Vector3(0f, 4.1f, 0f);
                    verticalCollider.AddComponent<MeshCollider>().sharedMesh = poleCollider;
                    AddChicagoBoxCollider(item.transform, "ArmCollider", new Vector3(4f, 8.1f, 0f), new Vector3(8.1f, 0.36f, 0.36f));
                    AddChicagoBoxCollider(item.transform, "SignalCollider", new Vector3(8.3f, 7.55f, 0f), new Vector3(1.45f, 3.4f, 1f));
                }
            }
        }

        private static void CreateChicagoBeanPark(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("ChicagoBeanPark");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(document.constants.beanParkX, document.baseHeight, document.constants.beanParkZ);
            ChicagoDisk("BeanBase", root.transform, 15.5f, 42, new Vector3(0f, 0.505f, 0f), materials.BeanBase);
            var sphere48 = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/BeanSphere48x24.asset", () => CreateUvSphereMesh(1f, 48, 24));
            var sphere32 = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/BeanSphere32x16.asset", () => CreateUvSphereMesh(1f, 32, 16));
            var sphere32x12 = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/BeanSphere32x12.asset", () => CreateUvSphereMesh(1f, 32, 12));
            var sphere24x12 = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/BeanSphere24x12.asset", () => CreateUvSphereMesh(1f, 24, 12));
            var sphere24x10 = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/BeanSphere24x10.asset", () => CreateUvSphereMesh(1f, 24, 10));
            foreach (var side in new[] { -1f, 1f })
            {
                ChicagoScaledSphere($"BeanLobe_{side}", root.transform, sphere48, new Vector3(side * 5.8f, 8.55f, side * 0.25f), new Vector3(14.6f, 8.8f, 14.2f), new Vector3(-0.08f - side * 0.035f, 0.32f + side * 0.05f, 0f), materials.Bean);
                ChicagoScaledSphere($"BeanInner_{side}", root.transform, sphere32, new Vector3(side * 2.15f, 8.35f, side * 0.05f), new Vector3(2.85f, 6.35f, 12.35f), new Vector3(-0.08f - side * 0.025f, 0.32f + side * 0.035f, 0f), materials.BeanInner);
                ChicagoScaledSphere($"BeanLower_{side}", root.transform, sphere32x12, new Vector3(side * 5.65f, 5.72f, side * 0.22f), new Vector3(12.2f, 2.85f, 12.4f), new Vector3(-0.08f - side * 0.035f, 0.32f + side * 0.05f, 0f), materials.BeanLower);
                ChicagoScaledSphere($"BeanHighlight_{side}", root.transform, sphere24x12, new Vector3(side * 1.08f, 8.85f, side * 0.06f), new Vector3(0.42f, 6.7f, 11.6f), new Vector3(-0.08f - side * 0.018f, 0.32f + side * 0.018f, 0f), materials.BeanHighlight);
            }
            ChicagoScaledSphere("BeanCleft", root.transform, sphere32, new Vector3(0f, 8.25f, 0f), new Vector3(0.74f, 7.35f, 13.2f), new Vector3(-0.08f, 0.32f, 0f), materials.BeanCleft);
            ChicagoScaledSphere("BeanCleftDark", root.transform, sphere24x12, new Vector3(0f, 8.25f, -0.35f), new Vector3(0.34f, 7.8f, 10.8f), new Vector3(-0.08f, 0.32f, 0f), materials.BeanCleftDark);
            ChicagoScaledSphere("BeanLowerCleft", root.transform, sphere24x10, new Vector3(0f, 4.6f, 0f), new Vector3(0.46f, 2.1f, 9.8f), new Vector3(-0.08f, 0.32f, 0f), materials.BeanLowerCleft);
            ChicagoScaledSphere("BeanShineA", root.transform, sphere24x12, new Vector3(-7.6f, 12.4f, -4.8f), new Vector3(5.8f, 1.65f, 3.3f), new Vector3(-0.04f, 0.25f, 0f), materials.BeanShineA);
            ChicagoScaledSphere("BeanShineB", root.transform, sphere24x12, new Vector3(7.4f, 11.7f, 4.4f), new Vector3(4.6f, 1.15f, 2.4f), new Vector3(-0.11f, 0.36f, 0f), materials.BeanShineB);
            foreach (var side in new[] { -1f, 1f })
            {
                var bollards = new GameObject($"Bollards_{side}");
                bollards.transform.SetParent(root.transform, false);
                bollards.transform.localRotation = Quaternion.Euler(0f, side * 0.68f * Mathf.Rad2Deg, 0f);
                foreach (var x in new[] { -18f, -6f, 6f, 18f })
                {
                    ChicagoCylinder($"Bollard_{x}", bollards.transform, new Vector3(x, 0.98f, 31.5f), 0.42f, 0.5f, 1.9f, 8, materials.Bollard);
                }
            }
        }

        private static void ChicagoScaledSphere(
            string name,
            Transform parent,
            Mesh mesh,
            Vector3 position,
            Vector3 scale,
            Vector3 rotationRadians,
            Material material)
        {
            var sphere = ChicagoMeshVisual(name, parent, position, mesh, material);
            sphere.transform.localRotation = Quaternion.Euler(
                rotationRadians.x * Mathf.Rad2Deg,
                rotationRadians.y * Mathf.Rad2Deg,
                rotationRadians.z * Mathf.Rad2Deg);
            sphere.transform.localScale = scale;
        }

        private static void CreateChicagoWelcomeSign(
            Transform parent,
            WofChicagoCityDocument document,
            ChicagoMaterialSet materials,
            List<Transform> billboards)
        {
            var root = new GameObject("ChicagoWelcomeSign");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-206f, document.baseHeight + 15f, 214f);
            ChicagoCube("Support", root.transform, new Vector3(0f, -6f, 0f), new Vector3(4f, 12f, 3f), materials.BlackMetal);
            var sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sign.name = "CHICAGO";
            sign.transform.SetParent(root.transform, false);
            sign.transform.localScale = new Vector3(72f, 20f, 1f);
            sign.GetComponent<MeshRenderer>().sharedMaterial = materials.ChicagoSign;
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var collider = sign.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MarkDarrelDynamic(sign);
            billboards.Add(sign.transform);
        }

        private static void CreateChicagoTrafficRuntime(Transform parent, WofChicagoCityDocument document, ChicagoMaterialSet materials)
        {
            var root = new GameObject("ChicagoInstancedTrafficAndPedestrians");
            root.transform.SetParent(parent, false);
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var unitCube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            var unitCylinder = GetOrCreateMeshAsset(ChicagoGeometryRoot + "/TrafficUnitCylinder8.asset", () => CreateDarrelFrustumMesh(1f, 1f, 1f, 8));
            var bodyBindings = MakeChicagoBindings(
                document.layout.cars.Select(car => car.color),
                color => ChicagoMaterial($"ChicagoCarBody_{color.TrimStart('#')}", HexColor(color)));
            var sideBindings = MakeChicagoBindings(
                document.layout.cars.Select(GetChicagoSideMarkColor),
                color => ChicagoMaterial($"ChicagoCarSide_{color.TrimStart('#')}", HexColor(color)));
            var lightBindings = MakeChicagoBindings(
                document.layout.cars.Where(IsChicagoLightBarVehicle).Select(GetChicagoLightBarColor),
                color => ChicagoMaterial($"ChicagoCarLight_{color.TrimStart('#')}", HexColor(color)));
            var runtime = root.AddComponent<WofChicagoTrafficRuntime>();
            runtime.Configure(
                WofChicagoCityLayout.WorldOrigin,
                document.baseHeight,
                document.constants.trafficUpdateIntervalSeconds,
                document.constants.pedestrianUpdateIntervalSeconds,
                document.layout.cars,
                document.layout.pedestrians,
                unitCube,
                unitCylinder,
                bodyBindings,
                sideBindings,
                lightBindings,
                ChicagoMaterial("ChicagoCarCabin", new Color(0.875f, 0.976f, 1f, 0.92f), null, true),
                ChicagoMaterial("ChicagoTaxiSign", HexColor("#f8fafc")),
                materials.BlackMetal,
                ChicagoMaterial("ChicagoPedestrianBody", HexColor("#2563eb")),
                ChicagoMaterial("ChicagoPedestrianHead", HexColor("#e0ac69")),
                ChicagoMaterial("ChicagoPedestrianLegs", HexColor("#334155")),
                ChicagoMaterial("ChicagoPedestrianArms", HexColor("#e0ac69")));
        }

        private static WofChicagoColorMaterialBinding[] MakeChicagoBindings(
            IEnumerable<string> colors,
            Func<string, Material> materialFactory)
        {
            return colors.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(color => color, StringComparer.Ordinal)
                .Select(color => new WofChicagoColorMaterialBinding { color = color, material = materialFactory(color) })
                .ToArray();
        }

        private static string GetChicagoSideMarkColor(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "taxi", StringComparison.Ordinal) ? "#111827" :
                string.Equals(car.vehicleType, "bus", StringComparison.Ordinal) ? "#2563eb" :
                string.Equals(car.vehicleType, "firetruck", StringComparison.Ordinal) ? "#f8fafc" :
                string.Equals(car.vehicleType, "sedan", StringComparison.Ordinal) ? "#fef3c7" : "#ef4444";
        }

        private static string GetChicagoLightBarColor(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "police", StringComparison.Ordinal) ? "#2563eb" :
                string.Equals(car.vehicleType, "ambulance", StringComparison.Ordinal) ? "#ef4444" : "#facc15";
        }

        private static bool IsChicagoLightBarVehicle(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "police", StringComparison.Ordinal) ||
                   string.Equals(car.vehicleType, "ambulance", StringComparison.Ordinal) ||
                   string.Equals(car.vehicleType, "firetruck", StringComparison.Ordinal);
        }
    }
}
