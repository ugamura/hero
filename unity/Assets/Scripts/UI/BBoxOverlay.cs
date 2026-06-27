using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hero.Network;
using NetworkDetection = Hero.Network.Detection;

namespace Hero.UI
{
    /// <summary>
    /// 【土台確認用】DetectionClient の結果をそのまま画面に矩形で描画。
    /// ゲームロジック実装後は BBoxRenderer に置き換える。
    /// </summary>
    public class BBoxOverlay : MonoBehaviour
    {
        public DetectionClient client;
        public Color boxColor = new(0f, 0.9f, 0.47f, 1f);
        public int fontSize = 22;

        private RectTransform canvasRect;
        private readonly List<GameObject> active = new();

        void Awake() => canvasRect = GetComponent<RectTransform>();
        void OnEnable()  { if (client != null) client.OnDetected += HandleDetected; }
        void OnDisable() { if (client != null) client.OnDetected -= HandleDetected; }

        private void HandleDetected(DetectResponse res)
        {
            ClearActive();
            if (res?.detections == null) return;

            float cw = canvasRect.rect.width;
            float ch = canvasRect.rect.height;

            foreach (var d in res.detections)
            {
                float x = d.bbox.x * cw - cw * 0.5f;
                float y = -(d.bbox.y * ch) + ch * 0.5f;
                float w = d.bbox.w * cw;
                float h = d.bbox.h * ch;
                active.Add(CreateBoxUI(d, x, y - h, w, h));
            }
        }

        private GameObject CreateBoxUI(NetworkDetection d, float x, float y, float w, float h)
        {
            var root = new GameObject($"bbox_{d.label}");
            root.transform.SetParent(transform, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            CreateEdge(rt, new Vector2(0, 0), new Vector2(1, 0), 3f);
            CreateEdge(rt, new Vector2(0, 1), new Vector2(1, 0), 3f);
            CreateEdge(rt, new Vector2(0, 0), new Vector2(0, 1), 3f);
            CreateEdge(rt, new Vector2(1, 0), new Vector2(0, 1), 3f);

            var labelGO = new GameObject("label");
            labelGO.transform.SetParent(rt, false);
            var lt = labelGO.AddComponent<RectTransform>();
            lt.anchorMin = new Vector2(0, 1);
            lt.anchorMax = new Vector2(1, 1);
            lt.pivot = new Vector2(0, 0);
            lt.anchoredPosition = new Vector2(0, 4);
            lt.sizeDelta = new Vector2(0, 30);
            var txt = labelGO.AddComponent<Text>();
            txt.text = $"{d.label} {d.confidence:0.00}" + (d.tracking_id > 0 ? $" #{d.tracking_id}" : "");
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.color = boxColor;
            return root;
        }

        private void CreateEdge(RectTransform parent, Vector2 anchor, Vector2 stretch, float thickness)
        {
            var go = new GameObject("edge");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            bool horizontal = stretch.x > 0;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor + stretch;
            rt.pivot = anchor;
            rt.sizeDelta = horizontal ? new Vector2(0, thickness) : new Vector2(thickness, 0);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = boxColor;
        }

        private void ClearActive()
        {
            foreach (var g in active) if (g) Destroy(g);
            active.Clear();
        }
    }
}
