using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Hero.Detection;
using Hero.Game;

namespace Hero.ARLayer
{
    /// <summary>Goodになった単語を検出位置のAR平面へ固定する。</summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class AnchorPlacementService : MonoBehaviour
    {
        public GameObject committedLabelPrefab;
        [Min(0.25f)] public float fallbackDistance = 1f;
        [Range(1, 6)] public int maxAnchors = 6;
        [Tooltip("近いラベル同士を少し縦にずらす間隔")]
        [Range(0f, 0.08f)] public float labelStaggerMeters = 0.025f;
        public Camera arCamera;
        public ARAnchorManager anchorManager;
        public WordChainGameManager game;

        private ARRaycastManager raycastManager;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly Queue<GameObject> anchors = new Queue<GameObject>();

        void Awake() => raycastManager = GetComponent<ARRaycastManager>();
        void OnEnable() { if (game != null) { game.OnJudged += HandleJudged; game.OnReset += ClearAnchors; } }
        void OnDisable() { if (game != null) { game.OnJudged -= HandleJudged; game.OnReset -= ClearAnchors; } }

        private void HandleJudged(Candidate candidate, JudgeResult result)
        {
            if (result == JudgeResult.Good && candidate != null) PlaceAnchor(candidate);
        }

        private void PlaceAnchor(Candidate candidate)
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return;

            var box = candidate.SmoothedBBox;
            var screenPosition = new Vector2(
                (box.x + box.w * 0.5f) * Screen.width,
                (1f - box.y - box.h * 0.5f) * Screen.height);

            Pose pose;
            bool onPlane = raycastManager != null &&
                raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon);
            if (onPlane)
            {
                pose = hits[0].pose;
            }
            else
            {
                // AR平面がないEditorデモでも、画面中央ではなく選択したbboxの方向へ置く。
                Ray screenRay = arCamera.ScreenPointToRay(screenPosition);
                Vector3 position = screenRay.GetPoint(fallbackDistance);
                position += arCamera.transform.up * ((anchors.Count % 3) * labelStaggerMeters);
                pose = new Pose(position, Quaternion.identity);
            }

            var anchorObject = new GameObject($"Committed_{candidate.Label}");
            anchorObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (!Application.isEditor && ARSession.state == ARSessionState.SessionTracking)
                anchorObject.AddComponent<ARAnchor>();

            GameObject label = committedLabelPrefab != null
                ? Instantiate(committedLabelPrefab, anchorObject.transform)
                : CreateDefaultLabel(candidate.Label, anchorObject.transform);
            label.transform.localPosition = onPlane ? Vector3.up * 0.04f : Vector3.zero;

            var billboard = label.GetComponent<CommittedLabelBillboard>();
            if (billboard == null) billboard = label.AddComponent<CommittedLabelBillboard>();
            billboard.targetCamera = arCamera;

            anchors.Enqueue(anchorObject);
            int limit = Mathf.Clamp(maxAnchors, 1, 6);
            while (anchors.Count > limit)
            {
                GameObject oldest = anchors.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
            RefreshLabelPriorities();
        }

        private static GameObject CreateDefaultLabel(string word, Transform parent)
        {
            var label = new GameObject("WordLabel");
            label.transform.SetParent(parent, false);
            var text = label.AddComponent<TextMesh>();
            text.text = word.ToUpperInvariant() + "  ✓";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 54;
            text.characterSize = 0.012f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0f, 0.95f, 0.55f);
            return label;
        }

        private void RefreshLabelPriorities()
        {
            int count = anchors.Count;
            int index = 0;
            foreach (GameObject anchor in anchors)
            {
                if (anchor != null)
                {
                    var label = anchor.GetComponentInChildren<CommittedLabelBillboard>();
                    if (label != null)
                    {
                        float priority = count <= 1 ? 1f : index / (float)(count - 1);
                        label.SetVisualPriority(priority);
                    }
                }
                index++;
            }
        }

        private void ClearAnchors()
        {
            while (anchors.Count > 0)
            {
                GameObject anchor = anchors.Dequeue();
                if (anchor != null) Destroy(anchor);
            }
        }
    }

    public class CommittedLabelBillboard : MonoBehaviour
    {
        public Camera targetCamera;
        private TextMesh[] textMeshes;
        private Color[] baseColors;
        private float targetAlpha = 1f;
        private float targetScale = 1f;

        void Awake()
        {
            textMeshes = GetComponentsInChildren<TextMesh>(true);
            baseColors = new Color[textMeshes.Length];
            for (int i = 0; i < textMeshes.Length; i++) baseColors[i] = textMeshes[i].color;
        }

        public void SetVisualPriority(float priority)
        {
            targetAlpha = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(priority));
            targetScale = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(priority));
        }

        void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 direction = transform.position - targetCamera.transform.position;
                if (direction.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 8f);
            if (textMeshes == null) return;
            for (int i = 0; i < textMeshes.Length; i++)
            {
                Color color = textMeshes[i].color;
                Color baseColor = baseColors[i];
                color.r = baseColor.r;
                color.g = baseColor.g;
                color.b = baseColor.b;
                color.a = Mathf.Lerp(color.a, baseColor.a * targetAlpha, Time.unscaledDeltaTime * 8f);
                textMeshes[i].color = color;
            }
        }
    }
}
