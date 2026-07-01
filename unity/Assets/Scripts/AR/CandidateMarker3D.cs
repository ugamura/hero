using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Hero.Detection;
using Hero.Network;

namespace Hero.ARLayer
{
    /// <summary>
    /// A案: 検出候補を2Dオーバーレイではなくワールド空間の3Dマーカーで表示する。
    /// bbox中心を画面からワールドへ投影し、AR平面があればレイキャスト、なければ一定距離に置く。
    /// 物体方向にビルボードのマーカーを置いて毎フレーム追従させる。
    /// AnchorPlacementService が無配線で自動アタッチする。
    /// </summary>
    public class CandidateMarker3D : MonoBehaviour
    {
        public CandidateManager candidates;
        public Camera arCamera;
        public ARRaycastManager raycastManager;
        [Min(0.25f)] public float fallbackDistance = 1.2f;
        [Tooltip("マーカーが新位置へ追従する速さ")]
        public float followLerp = 12f;
        [Range(0.01f, 0.1f)] public float nodeScale = 0.03f;
        public Color matchColor = new Color(0f, 0.9f, 0.47f, 1f);
        public Color unmatchColor = new Color(0.62f, 0.66f, 0.7f, 0.9f);
        public Color usedColor = new Color(0.45f, 0.45f, 0.48f, 0.85f);

        private readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly List<int> stale = new List<int>();
        private int previewIdSeed = -1000;

        private class Marker
        {
            public GameObject Root;
            public TextMesh Text;
            public Renderer NodeRenderer;
            public bool Placed;
            public bool IsPreview;
        }

        void Awake()
        {
            if (candidates == null) candidates = FindObjectOfType<CandidateManager>();
            if (arCamera == null) arCamera = Camera.main;
            if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
        }

        void LateUpdate()
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return;

            if (candidates != null)
            {
                seen.Clear();
                foreach (Candidate candidate in candidates.Active)
                {
                    if (candidate.State == CandidateState.LowConf) continue;
                    seen.Add(candidate.TrackingId);
                    if (!markers.TryGetValue(candidate.TrackingId, out Marker marker))
                    {
                        marker = CreateMarker(candidate.TrackingId, candidate.Label);
                        markers[candidate.TrackingId] = marker;
                    }
                    Vector3 target = ProjectToWorld(candidate.SmoothedBBox);
                    ApplyMarker(marker, target, candidate.Label, ColorFor(candidate.State));
                }

                // 候補から消えたマーカーを破棄する(プレビュー用マーカーは残す)。
                stale.Clear();
                foreach (var pair in markers)
                    if (!pair.Value.IsPreview && !seen.Contains(pair.Key)) stale.Add(pair.Key);
                foreach (int id in stale)
                {
                    if (markers[id].Root != null) Destroy(markers[id].Root);
                    markers.Remove(id);
                }
            }

            // 全マーカーをカメラへ正対させる(ビルボード)。
            foreach (var pair in markers)
            {
                Marker marker = pair.Value;
                if (marker.Root == null) continue;
                Vector3 direction = marker.Root.transform.position - arCamera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                    marker.Root.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        private Vector3 ProjectToWorld(BBox box)
        {
            Vector2 screen = new Vector2(
                (box.x + box.w * 0.5f) * Screen.width,
                (1f - box.y - box.h * 0.5f) * Screen.height);

            if (raycastManager != null && raycastManager.enabled &&
                raycastManager.Raycast(screen, hits, TrackableType.PlaneWithinPolygon))
                return hits[0].pose.position;

            Ray ray = arCamera.ScreenPointToRay(screen);
            return ray.GetPoint(fallbackDistance);
        }

        private void ApplyMarker(Marker marker, Vector3 target, string label, Color color)
        {
            if (!marker.Placed)
            {
                marker.Root.transform.position = target;
                marker.Placed = true;
            }
            else
            {
                marker.Root.transform.position = Vector3.Lerp(
                    marker.Root.transform.position, target, Time.deltaTime * followLerp);
            }

            if (marker.Text != null)
            {
                marker.Text.text = label.ToUpperInvariant();
                marker.Text.color = color;
            }
            if (marker.NodeRenderer != null) marker.NodeRenderer.material.color = color;
        }

        private Color ColorFor(CandidateState state) =>
            state == CandidateState.Match ? matchColor :
            state == CandidateState.Used ? usedColor : unmatchColor;

        private Marker CreateMarker(int id, string label)
        {
            var root = new GameObject($"Marker_{id}_{label}");
            root.transform.SetParent(transform, false);

            var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = "Node";
            var collider = node.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            node.transform.SetParent(root.transform, false);
            node.transform.localScale = Vector3.one * nodeScale;
            var nodeRenderer = node.GetComponent<Renderer>();
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) nodeRenderer.material = new Material(shader);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, nodeScale * 1.6f, 0f);
            var text = textObject.AddComponent<TextMesh>();
            text.text = label.ToUpperInvariant();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = 0.01f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;

            return new Marker { Root = root, Text = text, NodeRenderer = nodeRenderer };
        }

#if UNITY_EDITOR
        // しりとりを動かさずに3Dマーカーの見た目だけ確認するデバッグ用。
        public void PreviewDummyMarkers()
        {
            if (!Application.isPlaying) return;
            if (arCamera == null) arCamera = Camera.main;

            Vector3 origin = arCamera != null
                ? arCamera.transform.position + arCamera.transform.forward * fallbackDistance
                : Vector3.forward;
            Vector3 right = arCamera != null ? arCamera.transform.right : Vector3.right;
            Vector3 up = arCamera != null ? arCamera.transform.up : Vector3.up;
            Vector3 forward = arCamera != null ? arCamera.transform.forward : Vector3.forward;

            (string label, CandidateState state)[] dummies =
            {
                ("apple", CandidateState.Match),
                ("chair", CandidateState.Unmatch),
                ("tv", CandidateState.Used),
            };
            for (int i = 0; i < dummies.Length; i++)
            {
                int id = previewIdSeed--;
                var marker = CreateMarker(id, dummies[i].label);
                marker.IsPreview = true;
                markers[id] = marker;
                // 奥行きもずらして立体配置にする(Scene viewで回すと分かる)。
                Vector3 position = origin + right * (i * 0.25f - 0.25f) - up * 0.1f + forward * (i * 0.4f);
                marker.Root.transform.position = position;
                marker.Placed = true;
                ApplyMarker(marker, position, dummies[i].label, ColorFor(dummies[i].state));
            }
        }
#endif
    }
}
