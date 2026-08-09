using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WOF
{
    public sealed class WofBushRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh bushMesh;
        [SerializeField] private Material[] bushMaterials;

        private Matrix4x4[][] _matricesByColor;
        private bool _fallbackLogged;

        public void Configure(Mesh mesh, Material[] materials)
        {
            bushMesh = mesh;
            bushMaterials = materials;
        }

        private void Awake()
        {
            var attempts = WofPerformanceModeRuntime.IsMobilePerformanceMode
                ? WofBushLayout.MobileAttempts
                : WofBushLayout.DesktopAttempts;
            var bushes = WofBushLayout.BuildBushes(attempts);
            var lobes = WofBushLayout.BuildLobes(bushes);
            var matrices = new[]
            {
                new List<Matrix4x4>(lobes.Count / 3 + 1),
                new List<Matrix4x4>(lobes.Count / 3 + 1),
                new List<Matrix4x4>(lobes.Count / 3 + 1)
            };
            foreach (var lobe in lobes)
            {
                matrices[lobe.ColorIndex].Add(WofBushLayout.ToThreeJsMatrix(lobe));
            }
            _matricesByColor = new[] { matrices[0].ToArray(), matrices[1].ToArray(), matrices[2].ToArray() };
            Debug.Log($"[WOF-AUTOMATION] BUSH_LAYOUT attempts={attempts} bushes={bushes.Count} lobes={lobes.Count}");
        }

        private void Update()
        {
            if (bushMesh == null || bushMaterials == null || bushMaterials.Length != 3 || _matricesByColor == null)
            {
                return;
            }

            for (var colorIndex = 0; colorIndex < 3; colorIndex++)
            {
                var matrices = _matricesByColor[colorIndex];
                if (bushMaterials[colorIndex] == null || matrices.Length == 0) continue;
                var material = bushMaterials[colorIndex];
                if (SystemInfo.supportsInstancing && material.enableInstancing)
                {
                    Graphics.DrawMeshInstanced(
                        bushMesh,
                        0,
                        material,
                        matrices,
                        matrices.Length,
                        null,
                        ShadowCastingMode.On,
                        true,
                        gameObject.layer,
                        null,
                        LightProbeUsage.BlendProbes,
                        null);
                    continue;
                }

                if (!_fallbackLogged)
                {
                    _fallbackLogged = true;
                    Debug.Log("[WOF-AUTOMATION] BUSH_INSTANCING_FALLBACK enabled=true");
                }
                for (var matrixIndex = 0; matrixIndex < matrices.Length; matrixIndex++)
                {
                    Graphics.DrawMesh(bushMesh, matrices[matrixIndex], material, gameObject.layer);
                }
            }
        }
    }
}
