using UnityEngine;
using Hero.Detection;
using Hero.Game;

namespace Hero.Effects
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundManager : MonoBehaviour
    {
        [Header("Clips (optional)")]
        public AudioClip goodClip;
        public AudioClip badClip;
        public AudioClip usedClip;
        public AudioClip skipClip;
        public AudioClip hintClip;

        [Header("Playback")]
        public WordChainGameManager game;
        [Range(0f, 1f)] public float volume = 0.65f;
        [Range(0f, 0.2f)] public float randomPitchRange = 0.035f;
        public bool generateFallbackClips = true;

        private AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;

            if (!generateFallbackClips) return;
            if (goodClip == null) goodClip = CreateSuccessClip();
            if (badClip == null) badClip = CreateFailureClip();
            if (usedClip == null) usedClip = CreateUsedClip();
            if (skipClip == null) skipClip = CreateSweepClip("Skip", 460f, 250f, 0.18f, Wave.Sine, 0.32f);
            if (hintClip == null) hintClip = CreateSuccessClip("Hint", new[] { 780f, 980f, 1180f }, 0.24f, 0.24f);
        }

        void OnEnable()
        {
            if (game == null) return;
            game.OnJudged += HandleJudged;
            game.OnSkipped += HandleSkipped;
            game.OnHintRequested += HandleHint;
        }

        void OnDisable()
        {
            if (game == null) return;
            game.OnJudged -= HandleJudged;
            game.OnSkipped -= HandleSkipped;
            game.OnHintRequested -= HandleHint;
        }

        private void HandleJudged(Candidate _, JudgeResult result)
        {
            Play(result == JudgeResult.Good ? goodClip : result == JudgeResult.AlreadyUsed ? usedClip : badClip);
        }

        private void HandleSkipped() => Play(skipClip);
        private void HandleHint() => Play(hintClip);

        private void Play(AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.pitch = 1f + Random.Range(-randomPitchRange, randomPitchRange);
            source.PlayOneShot(clip, volume);
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Sound - Good")]
        private void PreviewGood() => Play(goodClip != null ? goodClip : CreateSuccessClip());

        [ContextMenu("Preview Sound - Bad")]
        private void PreviewBad() => Play(badClip != null ? badClip : CreateFailureClip());

        [ContextMenu("Preview Sound - Used")]
        private void PreviewUsed() => Play(usedClip != null ? usedClip : CreateUsedClip());
#endif

        private static AudioClip CreateSuccessClip() =>
            CreateSuccessClip("Good", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.42f, 0.34f);

        private static AudioClip CreateSuccessClip(string name, float[] notes, float duration, float gain)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
                float value = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * duration / (notes.Length + 1f);
                    float local = Mathf.Clamp01((t - start) / (duration * 0.34f));
                    if (t < start || local >= 1f) continue;

                    float envelope = AttackRelease(local, 0.08f, 1.6f);
                    float frequency = notes[n] * (1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 8f * t));
                    value += Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
                    value += Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * envelope * 0.22f;
                }

                float sparkle = Mathf.Sin(2f * Mathf.PI * 2450f * t) * Mathf.Pow(Mathf.Clamp01(normalized), 2f) * Mathf.Pow(1f - normalized, 2f) * 0.12f;
                samples[i] = SoftClip((value * 0.42f + sparkle) * gain);
            }

            return ToClip(name, samples, sampleRate);
        }

        private static AudioClip CreateFailureClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.38f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(240f, 82f, normalized) + Mathf.Sin(t * 110f) * 10f;
                float envelope = Mathf.Pow(1f - normalized, 0.75f) * Mathf.Clamp01(normalized / 0.03f);
                float buzz = Saw(frequency, t) * 0.55f + Square(frequency * 0.5f, t) * 0.32f;
                samples[i] = SoftClip(buzz * envelope * 0.34f);
            }

            return ToClip("Bad", samples, sampleRate);
        }

        private static AudioClip CreateUsedClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.24f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
                float pulseA = Mathf.Sin(2f * Mathf.PI * 330f * t) * AttackRelease(Mathf.Clamp01(normalized / 0.46f), 0.05f, 1.2f);
                float pulseB = Mathf.Sin(2f * Mathf.PI * 250f * t) * AttackRelease(Mathf.Clamp01((normalized - 0.42f) / 0.58f), 0.04f, 1.2f);
                samples[i] = SoftClip((pulseA + pulseB) * 0.28f);
            }

            return ToClip("Used", samples, sampleRate);
        }

        private static AudioClip CreateSweepClip(string name, float startFrequency, float endFrequency, float duration, Wave wave, float gain)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float seconds = i / (float)sampleRate;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                float envelope = Mathf.Sin(Mathf.PI * t);
                float value = wave == Wave.Square ? Square(frequency, seconds) : Mathf.Sin(2f * Mathf.PI * frequency * seconds);
                samples[i] = SoftClip(value * envelope * gain);
            }
            return ToClip(name, samples, sampleRate);
        }

        private static float AttackRelease(float t, float attack, float releasePower)
        {
            float attackEnvelope = Mathf.Clamp01(t / Mathf.Max(0.001f, attack));
            float releaseEnvelope = Mathf.Pow(Mathf.Clamp01(1f - t), releasePower);
            return attackEnvelope * releaseEnvelope;
        }

        private static float Saw(float frequency, float time)
        {
            float phase = Mathf.Repeat(frequency * time, 1f);
            return phase * 2f - 1f;
        }

        private static float Square(float frequency, float time) =>
            Mathf.Sin(2f * Mathf.PI * frequency * time) >= 0f ? 1f : -1f;

        private static float SoftClip(float value) => (float)System.Math.Tanh(value * 1.35f);

        private static AudioClip ToClip(string name, float[] samples, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private enum Wave { Sine, Square }
    }
}

