using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    [Serializable]
    public sealed class WofChicagoColorMaterialBinding
    {
        public string color;
        public Material material;
    }

    [DisallowMultipleComponent]
    public sealed class WofChicagoTrafficRuntime : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOrigin;
        [SerializeField] private float baseHeight;
        [SerializeField] private float trafficUpdateInterval = 1f / 30f;
        [SerializeField] private float pedestrianUpdateInterval = 1f / 24f;
        [SerializeField] private WofChicagoCarRecord[] cars;
        [SerializeField] private WofChicagoPedestrianRecord[] pedestrians;
        [SerializeField] private Mesh unitCube;
        [SerializeField] private Mesh unitCylinder;
        [SerializeField] private WofChicagoColorMaterialBinding[] carBodyMaterials;
        [SerializeField] private WofChicagoColorMaterialBinding[] carSideMarkMaterials;
        [SerializeField] private WofChicagoColorMaterialBinding[] carLightBarMaterials;
        [SerializeField] private Material carCabinMaterial;
        [SerializeField] private Material taxiSignMaterial;
        [SerializeField] private Material wheelMaterial;
        [SerializeField] private Material pedestrianBodyMaterial;
        [SerializeField] private Material pedestrianHeadMaterial;
        [SerializeField] private Material pedestrianLegMaterial;
        [SerializeField] private Material pedestrianArmMaterial;

        private RuntimeCarGroup[] _bodyGroups;
        private RuntimeCarGroup[] _sideMarkGroups;
        private RuntimeCarGroup[] _lightBarGroups;
        private Matrix4x4[] _cabinMatrices;
        private Matrix4x4[] _taxiMatrices;
        private int[] _taxiCarIndexes;
        private Matrix4x4[] _wheelMatrices;
        private Matrix4x4[] _pedestrianBodies;
        private Matrix4x4[] _pedestrianHeads;
        private Matrix4x4[] _pedestrianLeftLegs;
        private Matrix4x4[] _pedestrianRightLegs;
        private Matrix4x4[] _pedestrianLeftArms;
        private Matrix4x4[] _pedestrianRightArms;
        private float _lastTrafficUpdate = float.NegativeInfinity;
        private float _lastPedestrianUpdate = float.NegativeInfinity;

        public int CarCount => cars?.Length ?? 0;
        public int PedestrianCount => pedestrians?.Length ?? 0;

        public void Configure(
            Vector3 origin,
            float reactBaseHeight,
            float carUpdateInterval,
            float pedestrianInterval,
            WofChicagoCarRecord[] carRecords,
            WofChicagoPedestrianRecord[] pedestrianRecords,
            Mesh cube,
            Mesh cylinder,
            WofChicagoColorMaterialBinding[] bodyBindings,
            WofChicagoColorMaterialBinding[] sideBindings,
            WofChicagoColorMaterialBinding[] lightBindings,
            Material cabin,
            Material taxi,
            Material wheels,
            Material pedestrianBody,
            Material pedestrianHead,
            Material pedestrianLegs,
            Material pedestrianArms)
        {
            worldOrigin = origin;
            baseHeight = reactBaseHeight;
            trafficUpdateInterval = carUpdateInterval;
            pedestrianUpdateInterval = pedestrianInterval;
            cars = carRecords;
            pedestrians = pedestrianRecords;
            unitCube = cube;
            unitCylinder = cylinder;
            carBodyMaterials = bodyBindings;
            carSideMarkMaterials = sideBindings;
            carLightBarMaterials = lightBindings;
            carCabinMaterial = cabin;
            taxiSignMaterial = taxi;
            wheelMaterial = wheels;
            pedestrianBodyMaterial = pedestrianBody;
            pedestrianHeadMaterial = pedestrianHead;
            pedestrianLegMaterial = pedestrianLegs;
            pedestrianArmMaterial = pedestrianArms;
        }

        private void Awake()
        {
            cars ??= Array.Empty<WofChicagoCarRecord>();
            pedestrians ??= Array.Empty<WofChicagoPedestrianRecord>();
            _bodyGroups = BuildCarGroups(carBodyMaterials, car => car.color, _ => true);
            _sideMarkGroups = BuildCarGroups(carSideMarkMaterials, GetSideMarkColor, _ => true);
            _lightBarGroups = BuildCarGroups(carLightBarMaterials, GetLightBarColor, IsLightBarVehicle);
            _cabinMatrices = new Matrix4x4[cars.Length];
            var taxis = new List<int>();
            for (var index = 0; index < cars.Length; index++)
            {
                if (string.Equals(cars[index].vehicleType, "taxi", StringComparison.Ordinal))
                {
                    taxis.Add(index);
                }
            }
            _taxiCarIndexes = taxis.ToArray();
            _taxiMatrices = new Matrix4x4[_taxiCarIndexes.Length];
            _wheelMatrices = new Matrix4x4[cars.Length * 4];
            _pedestrianBodies = new Matrix4x4[pedestrians.Length];
            _pedestrianHeads = new Matrix4x4[pedestrians.Length];
            _pedestrianLeftLegs = new Matrix4x4[pedestrians.Length];
            _pedestrianRightLegs = new Matrix4x4[pedestrians.Length];
            _pedestrianLeftArms = new Matrix4x4[pedestrians.Length];
            _pedestrianRightArms = new Matrix4x4[pedestrians.Length];
            UpdateTrafficMatrices(0f);
            UpdatePedestrianMatrices(0f);
        }

        private void LateUpdate()
        {
            var elapsed = Time.timeSinceLevelLoad;
            if (elapsed - _lastTrafficUpdate >= trafficUpdateInterval)
            {
                _lastTrafficUpdate = elapsed;
                UpdateTrafficMatrices(elapsed);
            }
            if (elapsed - _lastPedestrianUpdate >= pedestrianUpdateInterval)
            {
                _lastPedestrianUpdate = elapsed;
                UpdatePedestrianMatrices(elapsed);
            }
            DrawTraffic();
            DrawPedestrians();
        }

        private RuntimeCarGroup[] BuildCarGroups(
            WofChicagoColorMaterialBinding[] bindings,
            Func<WofChicagoCarRecord, string> colorSelector,
            Func<WofChicagoCarRecord, bool> include)
        {
            var result = new List<RuntimeCarGroup>();
            if (bindings == null) return result.ToArray();
            foreach (var binding in bindings)
            {
                if (binding == null || binding.material == null) continue;
                var indexes = new List<int>();
                for (var index = 0; index < cars.Length; index++)
                {
                    if (include(cars[index]) && string.Equals(colorSelector(cars[index]), binding.color, StringComparison.OrdinalIgnoreCase))
                    {
                        indexes.Add(index);
                    }
                }
                if (indexes.Count > 0)
                {
                    result.Add(new RuntimeCarGroup(binding.material, indexes.ToArray()));
                }
            }
            return result.ToArray();
        }

        private void UpdateTrafficMatrices(float elapsed)
        {
            var transforms = new VehicleTransform[cars.Length];
            for (var index = 0; index < cars.Length; index++)
            {
                transforms[index] = ResolveVehicleTransform(cars[index], elapsed);
            }

            foreach (var group in _bodyGroups)
            {
                for (var matrixIndex = 0; matrixIndex < group.CarIndexes.Length; matrixIndex++)
                {
                    var carIndex = group.CarIndexes[matrixIndex];
                    var car = cars[carIndex];
                    var lengthScale = GetLengthScale(car);
                    group.Matrices[matrixIndex] = PartMatrix(
                        transforms[carIndex], car, Vector3.up * (0.85f * car.scale),
                        new Vector3(5.2f, 1.35f, 8.7f * lengthScale) * car.scale);
                }
            }

            for (var index = 0; index < cars.Length; index++)
            {
                var car = cars[index];
                var bus = string.Equals(car.vehicleType, "bus", StringComparison.Ordinal);
                _cabinMatrices[index] = PartMatrix(
                    transforms[index], car,
                    new Vector3(0f, 1.72f * car.scale, (bus ? 0.2f : -0.65f) * car.scale),
                    new Vector3(bus ? 4.3f : 3.8f, bus ? 1.02f : 1.04f, bus ? 8.4f : 3.7f) * car.scale);
            }

            foreach (var group in _sideMarkGroups)
            {
                for (var matrixIndex = 0; matrixIndex < group.CarIndexes.Length; matrixIndex++)
                {
                    var carIndex = group.CarIndexes[matrixIndex];
                    var car = cars[carIndex];
                    var sedan = string.Equals(car.vehicleType, "sedan", StringComparison.Ordinal);
                    group.Matrices[matrixIndex] = PartMatrix(
                        transforms[carIndex], car,
                        new Vector3(0f, 1.34f * car.scale, 4.42f * car.scale * GetLengthScale(car)),
                        new Vector3(sedan ? 3.6f : 4.2f, sedan ? 0.26f : 0.34f, sedan ? 0.26f : 0.38f) * car.scale);
                }
            }

            for (var index = 0; index < _taxiCarIndexes.Length; index++)
            {
                var carIndex = _taxiCarIndexes[index];
                var car = cars[carIndex];
                _taxiMatrices[index] = PartMatrix(
                    transforms[carIndex], car,
                    new Vector3(0f, 2.58f * car.scale, -0.75f * car.scale),
                    new Vector3(2.3f, 0.6f, 1.2f) * car.scale);
            }

            foreach (var group in _lightBarGroups)
            {
                for (var matrixIndex = 0; matrixIndex < group.CarIndexes.Length; matrixIndex++)
                {
                    var carIndex = group.CarIndexes[matrixIndex];
                    var car = cars[carIndex];
                    group.Matrices[matrixIndex] = PartMatrix(
                        transforms[carIndex], car,
                        new Vector3(0f, 2.45f * car.scale, -0.92f * car.scale),
                        new Vector3(2.2f, 0.32f, 0.65f) * car.scale);
                }
            }

            for (var index = 0; index < cars.Length; index++)
            {
                var car = cars[index];
                var lengthScale = GetLengthScale(car);
                var wheelIndex = index * 4;
                foreach (var xSide in new[] { -1f, 1f })
                {
                    foreach (var zSide in new[] { -1f, 1f })
                    {
                        _wheelMatrices[wheelIndex++] = PartMatrix(
                            transforms[index], car,
                            new Vector3(
                                xSide * 2.65f * car.scale,
                                0.42f * car.scale,
                                zSide * 3.15f * car.scale * lengthScale),
                            Vector3.one * (0.48f * car.scale) + Vector3.up * (-0.06f * car.scale),
                            90f);
                    }
                }
            }
        }

        private void UpdatePedestrianMatrices(float elapsed)
        {
            for (var index = 0; index < pedestrians.Length; index++)
            {
                var pedestrian = pedestrians[index];
                var transform = ResolvePedestrianTransform(pedestrian, elapsed);
                var step = Mathf.Sin(elapsed * 9f + index * 0.73f) * 0.16f * pedestrian.direction;
                _pedestrianBodies[index] = PedestrianPartMatrix(transform, new Vector3(0f, 1.85f, 0f), new Vector3(0.78f, 1.55f, 0.48f));
                _pedestrianHeads[index] = PedestrianPartMatrix(transform, new Vector3(0f, 3f, -0.02f), new Vector3(0.94f, 0.9f, 0.72f));
                _pedestrianLeftLegs[index] = PedestrianPartMatrix(transform, new Vector3(-0.22f, 0.74f, step), new Vector3(0.26f, 1.05f, 0.26f));
                _pedestrianRightLegs[index] = PedestrianPartMatrix(transform, new Vector3(0.22f, 0.74f, -step), new Vector3(0.26f, 1.05f, 0.26f));
                _pedestrianLeftArms[index] = PedestrianPartMatrix(transform, new Vector3(-0.58f, 1.82f, -step), new Vector3(0.2f, 1.05f, 0.22f));
                _pedestrianRightArms[index] = PedestrianPartMatrix(transform, new Vector3(0.58f, 1.82f, step), new Vector3(0.2f, 1.05f, 0.22f));
            }
        }

        private VehicleTransform ResolveVehicleTransform(WofChicagoCarRecord car, float elapsed)
        {
            var t = (car.offset + elapsed * car.speed) % 1f;
            if (car.direction < 0) t = 1f - t;
            var position = Mathf.Lerp(-218f, 190f, t);
            if (string.Equals(car.route, "vertical", StringComparison.Ordinal) ||
                string.Equals(car.route, "lakeshore", StringComparison.Ordinal))
            {
                return new VehicleTransform(
                    string.Equals(car.route, "lakeshore", StringComparison.Ordinal) ? 190f : car.lane + car.direction * 4.2f,
                    position,
                    car.direction > 0 ? 0f : Mathf.PI);
            }
            return new VehicleTransform(position, car.lane - car.direction * 4.2f, car.direction > 0 ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f);
        }

        private VehicleTransform ResolvePedestrianTransform(WofChicagoPedestrianRecord pedestrian, float elapsed)
        {
            var t = (pedestrian.offset + elapsed * pedestrian.speed) % 1f;
            if (pedestrian.direction < 0) t = 1f - t;
            var position = Mathf.Lerp(-214f, 174f, t);
            if (string.Equals(pedestrian.route, "vertical", StringComparison.Ordinal))
            {
                return new VehicleTransform(pedestrian.lane + pedestrian.sideOffset, position, pedestrian.direction > 0 ? 0f : Mathf.PI);
            }
            return new VehicleTransform(position, pedestrian.lane + pedestrian.sideOffset, pedestrian.direction > 0 ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f);
        }

        private Matrix4x4 PartMatrix(VehicleTransform transform, WofChicagoCarRecord car, Vector3 localOffset, Vector3 scale, float rotationX = 0f)
        {
            var position = TransformOffset(transform, localOffset);
            position += worldOrigin + Vector3.up * (baseHeight + 0.1f);
            var rotation = Quaternion.Euler(0f, transform.Yaw * Mathf.Rad2Deg, 0f) * Quaternion.Euler(rotationX, 0f, 0f);
            return Matrix4x4.TRS(position, rotation, scale);
        }

        private Matrix4x4 PedestrianPartMatrix(VehicleTransform transform, Vector3 localOffset, Vector3 scale)
        {
            var position = TransformOffset(transform, localOffset);
            position += worldOrigin + Vector3.up * (baseHeight + 0.08f);
            return Matrix4x4.TRS(position, Quaternion.Euler(0f, transform.Yaw * Mathf.Rad2Deg, 0f), scale);
        }

        private static Vector3 TransformOffset(VehicleTransform transform, Vector3 offset)
        {
            var cos = Mathf.Cos(transform.Yaw);
            var sin = Mathf.Sin(transform.Yaw);
            return new Vector3(
                transform.X + cos * offset.x + sin * offset.z,
                offset.y,
                transform.Z - sin * offset.x + cos * offset.z);
        }

        private void DrawTraffic()
        {
            foreach (var group in _bodyGroups) Draw(unitCube, group.Material, group.Matrices);
            Draw(unitCube, carCabinMaterial, _cabinMatrices);
            foreach (var group in _sideMarkGroups) Draw(unitCube, group.Material, group.Matrices);
            Draw(unitCube, taxiSignMaterial, _taxiMatrices);
            foreach (var group in _lightBarGroups) Draw(unitCube, group.Material, group.Matrices);
            Draw(unitCylinder, wheelMaterial, _wheelMatrices);
        }

        private void DrawPedestrians()
        {
            Draw(unitCube, pedestrianBodyMaterial, _pedestrianBodies);
            Draw(unitCube, pedestrianHeadMaterial, _pedestrianHeads);
            Draw(unitCube, pedestrianLegMaterial, _pedestrianLeftLegs);
            Draw(unitCube, pedestrianLegMaterial, _pedestrianRightLegs);
            Draw(unitCube, pedestrianArmMaterial, _pedestrianLeftArms);
            Draw(unitCube, pedestrianArmMaterial, _pedestrianRightArms);
        }

        private void Draw(Mesh mesh, Material material, Matrix4x4[] matrices)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                mesh == null || material == null || matrices == null || matrices.Length == 0) return;
            Graphics.DrawMeshInstanced(
                mesh, 0, material, matrices, matrices.Length, null,
                ShadowCastingMode.Off, false, gameObject.layer, null,
                LightProbeUsage.Off, null);
        }

        private static float GetLengthScale(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "bus", StringComparison.Ordinal) ? 1.85f :
                string.Equals(car.vehicleType, "firetruck", StringComparison.Ordinal) ? 1.22f :
                string.Equals(car.vehicleType, "ambulance", StringComparison.Ordinal) ? 1.12f : 1f;
        }

        private static string GetSideMarkColor(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "taxi", StringComparison.Ordinal) ? "#111827" :
                string.Equals(car.vehicleType, "bus", StringComparison.Ordinal) ? "#2563eb" :
                string.Equals(car.vehicleType, "firetruck", StringComparison.Ordinal) ? "#f8fafc" :
                string.Equals(car.vehicleType, "sedan", StringComparison.Ordinal) ? "#fef3c7" : "#ef4444";
        }

        private static string GetLightBarColor(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "police", StringComparison.Ordinal) ? "#2563eb" :
                string.Equals(car.vehicleType, "ambulance", StringComparison.Ordinal) ? "#ef4444" : "#facc15";
        }

        private static bool IsLightBarVehicle(WofChicagoCarRecord car)
        {
            return string.Equals(car.vehicleType, "police", StringComparison.Ordinal) ||
                   string.Equals(car.vehicleType, "ambulance", StringComparison.Ordinal) ||
                   string.Equals(car.vehicleType, "firetruck", StringComparison.Ordinal);
        }

        private readonly struct VehicleTransform
        {
            public readonly float X;
            public readonly float Z;
            public readonly float Yaw;

            public VehicleTransform(float x, float z, float yaw)
            {
                X = x;
                Z = z;
                Yaw = yaw;
            }
        }

        private sealed class RuntimeCarGroup
        {
            public readonly Material Material;
            public readonly int[] CarIndexes;
            public readonly Matrix4x4[] Matrices;

            public RuntimeCarGroup(Material material, int[] carIndexes)
            {
                Material = material;
                CarIndexes = carIndexes;
                Matrices = new Matrix4x4[carIndexes.Length];
            }
        }
    }

}
