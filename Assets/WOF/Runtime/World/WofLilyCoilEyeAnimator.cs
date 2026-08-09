using UnityEngine;

namespace WOF
{
    [DisallowMultipleComponent]
    public sealed class WofLilyCoilEyeAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = WofLilyCoilLayout.EyeFrameFps;
        [SerializeField] private float worldDiameter = WofLilyCoilLayout.EyeCapRadius * 2.06f;

        public void Configure(SpriteRenderer spriteRenderer, Sprite[] sourceFrames, float fps, float diameter)
        {
            target = spriteRenderer;
            frames = sourceFrames;
            framesPerSecond = fps;
            worldDiameter = diameter;
            ApplyFrame(0);
        }

        private void OnEnable()
        {
            ApplyFrame(0);
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || framesPerSecond <= 0f) return;
            var frame = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
            ApplyFrame(frame);
        }

        private void ApplyFrame(int index)
        {
            if (target == null || frames == null || frames.Length == 0) return;
            var sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            if (sprite == null) return;
            target.sprite = sprite;
            var width = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var scale = worldDiameter / width;
            target.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
