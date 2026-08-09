using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofChicagoOperatorBillboards : MonoBehaviour
    {
        [SerializeField] private Transform[] spriteTransforms;
        private Camera _camera;

        public void Configure(Transform[] transforms)
        {
            spriteTransforms = transforms;
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (_camera == null || spriteTransforms == null) return;
            foreach (var spriteTransform in spriteTransforms)
            {
                if (spriteTransform == null) continue;
                var direction = spriteTransform.position - _camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    spriteTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }
    }
}
