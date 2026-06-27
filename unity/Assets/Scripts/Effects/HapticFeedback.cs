using System.Collections;
using UnityEngine;
using Hero.Detection;
using Hero.Game;

namespace Hero.Effects
{
    public class HapticFeedback : MonoBehaviour
    {
        public WordChainGameManager game;
        public bool enableVibration = true;
        void OnEnable()
        {
            if (game == null) return;
            game.OnJudged += HandleJudged;
            game.OnSkipped += VibrateOnce;
        }
        void OnDisable()
        {
            if (game == null) return;
            game.OnJudged -= HandleJudged;
            game.OnSkipped -= VibrateOnce;
        }
        private void HandleJudged(Candidate _, JudgeResult result)
        {
            if (!enableVibration) return;
            if (result == JudgeResult.Bad) StartCoroutine(DoubleVibrate());
            else VibrateOnce();
        }
        private void VibrateOnce()
        {
#if UNITY_IOS || UNITY_ANDROID
            if (enableVibration) Handheld.Vibrate();
#endif
        }
        private IEnumerator DoubleVibrate()
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            yield return new WaitForSecondsRealtime(0.15f);
            Handheld.Vibrate();
#else
            yield break;
#endif
        }
    }
}
