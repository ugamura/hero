using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Hero.Detection;
using Hero.Game;

namespace Hero.Effects
{
    /// <summary>
    /// Good/Bad/AlreadyUsed のエフェクトを bbox 位置に再生する。
    /// Prefab を assign しない場合は、Canvas に簡易テキストで ✓ / ✗ を出す。
    /// </summary>
    public class FXSpawner : MonoBehaviour
    {
        [Header("Prefabs (optional)")]
        public GameObject goodFxPrefab;
        public GameObject badFxPrefab;
        public GameObject usedFxPrefab;

        [Header("Fallback canvas (simple text)")]
        public Canvas fallbackCanvas;
        public float flashDurationSec = 0.8f;
        public int flashFontSize = 80;

        public WordChainGameManager game;

        void OnEnable()
        {
            if (game != null) game.OnJudged += HandleJudged;
        }

        void OnDisable()
        {
            if (game != null) game.OnJudged -= HandleJudged;
        }

        private void HandleJudged(Candidate c, JudgeResult result)
        {
            if (c == null) return;

            // bbox 中心の正規化座標 → screen 座標
            var b = c.SmoothedBBox;
            float nx = b.x + b.w * 0.5f;
            float ny = b.y + b.h * 0.5f;
            var screenPos = new Vector2(nx * Screen.width, (1f - ny) * Screen.height);

            GameObject prefab = result switch
            {
                JudgeResult.Good => goodFxPrefab,
                JudgeResult.Bad => badFxPrefab,
                JudgeResult.AlreadyUsed => usedFxPrefab,
                _ => null,
            };

            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.transform.position = new Vector3(screenPos.x, screenPos.y, 0);
                Destroy(go, 2f);
            }
            else if (fallbackCanvas != null)
            {
                StartCoroutine(FlashText(screenPos, result));
            }
        }

        private IEnumerator FlashText(Vector2 screenPos, JudgeResult result)
        {
            var go = new GameObject("flash");
            go.transform.SetParent(fallbackCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 200);

            var canvasRect = fallbackCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, fallbackCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : fallbackCanvas.worldCamera,
                out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            var txt = go.AddComponent<Text>();
            txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = flashFontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            (txt.text, txt.color) = result switch
            {
                JudgeResult.Good => ("✓", new Color(0f, 0.9f, 0.47f)),
                JudgeResult.Bad => ("✗", new Color(0.95f, 0.3f, 0.3f)),
                JudgeResult.AlreadyUsed => ("used", new Color(1f, 0.85f, 0.2f)),
                _ => ("", Color.white),
            };

            float t = 0f;
            while (t < flashDurationSec)
            {
                t += Time.deltaTime;
                float a = 1f - (t / flashDurationSec);
                var c = txt.color; c.a = a; txt.color = c;
                rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.3f, t / flashDurationSec);
                yield return null;
            }
            Destroy(go);
        }
    }
}
