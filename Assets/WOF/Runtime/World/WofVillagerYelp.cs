using UnityEngine;

namespace WOF
{
    public static class WofVillagerYelp
    {
        public const float DurationSeconds = 0.31f;
        public const float StartDelaySeconds = 0.01f;
        public const float AttackSeconds = 0.025f;
        public const float FirstPitchRampSeconds = 0.08f;
        public const float SecondPitchRampEndSeconds = 0.24f;
        public const float ReleaseEndSeconds = 0.28f;
        public const float MinimumVolume = 0.12f;
        public const float MaximumVolume = 0.8f;
        public const float PeakGainScale = 0.18f;
        public const float GainFloor = 0.0001f;

        public static float ClampVolume(float volume)
        {
            return Mathf.Clamp(volume, MinimumVolume, MaximumVolume);
        }

        public static float ResolveFrequency(float elapsedSeconds)
        {
            var elapsed = Mathf.Max(0f, elapsedSeconds);
            if (elapsed <= FirstPitchRampSeconds)
            {
                return ExponentialRamp(760f, 1320f, elapsed / FirstPitchRampSeconds);
            }
            if (elapsed <= SecondPitchRampEndSeconds)
            {
                return ExponentialRamp(
                    1320f,
                    520f,
                    (elapsed - FirstPitchRampSeconds) /
                    (SecondPitchRampEndSeconds - FirstPitchRampSeconds));
            }
            return 520f;
        }

        public static float ResolveGain(float elapsedSeconds, float volume)
        {
            var elapsed = Mathf.Max(0f, elapsedSeconds);
            var peak = PeakGainScale * ClampVolume(volume);
            if (elapsed <= AttackSeconds)
            {
                return ExponentialRamp(GainFloor, peak, elapsed / AttackSeconds);
            }
            if (elapsed <= ReleaseEndSeconds)
            {
                return ExponentialRamp(
                    peak,
                    GainFloor,
                    (elapsed - AttackSeconds) / (ReleaseEndSeconds - AttackSeconds));
            }
            return GainFloor;
        }

        public static AudioClip CreateClip(float volume, int sampleRate)
        {
            sampleRate = Mathf.Max(8000, sampleRate);
            var sampleCount = Mathf.CeilToInt(DurationSeconds * sampleRate);
            var samples = new float[sampleCount];
            var phase = 0d;
            var phaseScale = Mathf.PI * 2d / sampleRate;
            for (var index = 0; index < sampleCount; index++)
            {
                var elapsed = index / (float)sampleRate;
                phase += ResolveFrequency(elapsed) * phaseScale;
                if (phase >= Mathf.PI * 2d)
                {
                    phase %= Mathf.PI * 2d;
                }
                samples[index] = (phase < Mathf.PI ? 1f : -1f) * ResolveGain(elapsed, volume);
            }

            var clip = AudioClip.Create("ReactVillagerYelp", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float ExponentialRamp(float from, float to, float progress)
        {
            return from * Mathf.Pow(to / from, Mathf.Clamp01(progress));
        }
    }
}
