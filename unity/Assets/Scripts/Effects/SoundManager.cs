using UnityEngine;
using Hero.Detection;
using Hero.Game;

namespace Hero.Effects
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundManager : MonoBehaviour
    {
        public AudioClip goodClip;
        public AudioClip badClip;
        public AudioClip usedClip;
        public AudioClip skipClip;
        public AudioClip hintClip;
        public WordChainGameManager game;
        [Range(0f, 1f)] public float volume = 0.65f;
        private AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            if (goodClip == null) goodClip = CreateTone("Good", 620f, 980f, 0.24f);
            if (badClip == null) badClip = CreateTone("Bad", 340f, 150f, 0.28f);
            if (usedClip == null) usedClip = CreateTone("Used", 230f, 230f, 0.16f);
            if (skipClip == null) skipClip = CreateTone("Skip", 460f, 250f, 0.20f);
            if (hintClip == null) hintClip = CreateTone("Hint", 780f, 1180f, 0.30f);
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
        private void Play(AudioClip clip) { if (clip != null && source != null) source.PlayOneShot(clip, volume); }

        private static AudioClip CreateTone(string name, float startFrequency, float endFrequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(phase) * envelope * 0.35f;
            }
            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
