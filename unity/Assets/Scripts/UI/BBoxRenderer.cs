using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hero.Detection;

namespace Hero.UI
{
    /// <summary>検出候補を状態別の色と動きで画面へ重畳する。</summary>
    [RequireComponent(typeof(RectTransform))]
    public class BBoxRenderer : MonoBehaviour
    {
        public CandidateManager candidates;
        [Header("Colors")]
        public Color matchColor = new Color(0f, 0.9f, 0.47f, 1f);
        public Color unmatchColor = new Color(0.62f, 0.66f, 0.7f, 0.65f);
        public Color usedColor = new Color(0.38f, 0.38f, 0.4f, 0.55f);
        public Color lowConfColor = new Color(1f, 1f, 1f, 0.25f);
        [Header("Style")]
        public int fontSize = 22;
        public float edgeThickness = 3f;
        public float pulseSpeed = 2f;
        public float pulseMinAlpha = 0.62f;
        public float pulseMaxAlpha = 1f;

        private RectTransform canvasRect;
        private readonly Dictionary<int, BoxUI> boxes = new Dictionary<int, BoxUI>();

        private class BoxUI
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image[] Edges;
            public Image Slash;
            public Text Label;
        }

        void Awake() => canvasRect = GetComponent<RectTransform>();

        void Update()
        {
            if (candidates == null || canvasRect == null) return;
            float width = canvasRect.rect.width;
            float height = canvasRect.rect.height;
            var activeIds = new HashSet<int>();
            foreach (Candidate candidate in candidates.Active)
            {
                activeIds.Add(candidate.TrackingId);
                if (!boxes.TryGetValue(candidate.TrackingId, out BoxUI ui))
                {
                    ui = CreateBox();
                    boxes[candidate.TrackingId] = ui;
                }
                UpdateBox(ui, candidate, width, height);
            }

            var stale = new List<int>();
            foreach (var pair in boxes)
                if (!activeIds.Contains(pair.Key)) { Destroy(pair.Value.Root); stale.Add(pair.Key); }
            foreach (int key in stale) boxes.Remove(key);
        }

        private void UpdateBox(BoxUI ui, Candidate candidate, float canvasWidth, float canvasHeight)
        {
            var box = candidate.SmoothedBBox;
            float x = box.x * canvasWidth - canvasWidth * 0.5f;
            float y = -(box.y * canvasHeight) + canvasHeight * 0.5f;
            float width = box.w * canvasWidth;
            float height = box.h * canvasHeight;
            ui.Rect.anchoredPosition = new Vector2(x, y - height);
            ui.Rect.sizeDelta = new Vector2(width, height);

            Color color = candidate.State == CandidateState.Match ? matchColor :
                candidate.State == CandidateState.Used ? usedColor :
                candidate.State == CandidateState.LowConf ? lowConfColor : unmatchColor;
            if (candidate.State == CandidateState.Match)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
                color.a = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse);
                ui.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.025f, pulse);
            }
            else ui.Rect.localScale = Vector3.one;

            foreach (Image edge in ui.Edges) edge.color = color;
            ui.Label.color = color;
            ui.Slash.color = color;
            ui.Slash.gameObject.SetActive(candidate.State == CandidateState.Used);
            string suffix = candidate.State == CandidateState.Used ? "  USED" :
                candidate.State == CandidateState.Match ? "  TAP" : string.Empty;
            ui.Label.text = $"{candidate.Label.ToUpperInvariant()}  {candidate.Confidence:P0}{suffix}";
        }

        private BoxUI CreateBox()
        {
            var root = new GameObject("CandidateBox");
            root.transform.SetParent(transform, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            var ui = new BoxUI { Root = root, Rect = rect, Edges = new Image[4] };
            ui.Edges[0] = CreateEdge(rect, new Vector2(0, 0), new Vector2(1, 0));
            ui.Edges[1] = CreateEdge(rect, new Vector2(0, 1), new Vector2(1, 0));
            ui.Edges[2] = CreateEdge(rect, new Vector2(0, 0), new Vector2(0, 1));
            ui.Edges[3] = CreateEdge(rect, new Vector2(1, 0), new Vector2(0, 1));

            var slashObject = new GameObject("UsedSlash");
            slashObject.transform.SetParent(rect, false);
            var slashRect = slashObject.AddComponent<RectTransform>();
            slashRect.anchorMin = new Vector2(0f, 0.5f);
            slashRect.anchorMax = new Vector2(1f, 0.5f);
            slashRect.sizeDelta = new Vector2(0f, edgeThickness);
            slashRect.localRotation = Quaternion.Euler(0f, 0f, 25f);
            ui.Slash = slashObject.AddComponent<Image>();
            ui.Slash.raycastTarget = false;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(rect, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 5f);
            labelRect.sizeDelta = new Vector2(0f, 32f);
            ui.Label = labelObject.AddComponent<Text>();
            ui.Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.Label.fontSize = fontSize;
            ui.Label.fontStyle = FontStyle.Bold;
            ui.Label.raycastTarget = false;
            return ui;
        }

        private Image CreateEdge(RectTransform parent, Vector2 anchor, Vector2 stretch)
        {
            var edgeObject = new GameObject("Edge");
            edgeObject.transform.SetParent(parent, false);
            var rect = edgeObject.AddComponent<RectTransform>();
            bool horizontal = stretch.x > 0f;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor + stretch;
            rect.pivot = anchor;
            rect.sizeDelta = horizontal ? new Vector2(0f, edgeThickness) : new Vector2(edgeThickness, 0f);
            var image = edgeObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
