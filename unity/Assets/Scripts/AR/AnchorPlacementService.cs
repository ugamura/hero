using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Hero.Detection;
using Hero.Game;

namespace Hero.ARLayer
{
    /// <summary>Goodになった単語を検出位置のAR平面へ固定し、しりとりの空間演出を描画する。</summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class AnchorPlacementService : MonoBehaviour
    {
        public GameObject committedLabelPrefab;
        [Min(0.25f)] public float fallbackDistance = 1f;
        [Range(1, 6)] public int maxAnchors = 6;
        [Tooltip("近いラベル同士を少し縦にずらす間隔")]
        [Range(0f, 0.08f)] public float labelStaggerMeters = 0.025f;

        [Header("Chain Trail")]
        [Tooltip("確定単語アンカー間を結ぶ、光るしりとりの道を描画する")]
        public bool drawChainTrail = true;
        [Range(0.002f, 0.04f)] public float chainWidth = 0.012f;
        [Tooltip("線をアンカーより少し上に浮かせてラベルへ寄せる")]
        public float chainHeightOffset = 0.02f;
        [Tooltip("古い側の色(薄)")]
        public Color chainColorOld = new Color(0f, 0.9f, 0.75f, 0.35f);
        [Tooltip("新しい側の色(濃)")]
        public Color chainColorNew = new Color(0.2f, 1f, 0.45f, 1f);
        [Tooltip("光の道の外側グロー倍率")]
        [Range(1.5f, 5f)] public float chainGlowWidthMultiplier = 3.2f;

        [Header("Chain Trail - Flow & Sparkles")]
        [Tooltip("光の縞が流れる速さ(マイナスで逆流)")]
        public float chainScrollSpeed = 1.2f;
        [Tooltip("流れる縞の密度")]
        [Range(0.5f, 12f)] public float chainTiling = 4f;
        [Tooltip("道沿いにキラキラを出す")]
        public bool chainSparkles = true;
        [Tooltip("1秒あたりのキラキラ数")]
        [Range(0f, 100f)] public float sparkleRate = 32f;
        [Range(0.005f, 0.08f)] public float sparkleSize = 0.025f;
        [Range(0.2f, 2f)] public float sparkleLifetime = 0.8f;

        [Header("Result FX")]
        public bool spawnResultFx = true;
        public Color successFxColor = new Color(0f, 1f, 0.55f, 1f);
        public Color failFxColor = new Color(1f, 0.16f, 0.18f, 1f);
        public Color usedFxColor = new Color(1f, 0.78f, 0.08f, 1f);
        [Range(0.05f, 0.35f)] public float successPopDistance = 0.16f;
        [Range(0.1f, 0.8f)] public float failMarkerSize = 0.34f;

        public Camera arCamera;
        public ARAnchorManager anchorManager;
        public WordChainGameManager game;

        private ARRaycastManager raycastManager;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private readonly Queue<GameObject> anchors = new Queue<GameObject>();
        private LineRenderer chainCoreLine;
        private LineRenderer chainGlowLine;
        private ParticleSystem chainParticleSystem;
        private Material chainCoreMaterial;
        private Material chainGlowMaterial;
        private Texture2D chainFlowTexture;
        private float sparkleAccumulator;

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            EnsureChainVisuals();
            EnsureSupportComponents();
        }

        // A案(候補3Dマーカー)をシーン無配線で有効化する。
        // D案(深度オクルージョン)は端末依存で落ちる場合があるため、自動有効化しない。
        private void EnsureSupportComponents()
        {
            if (FindObjectOfType<CandidateMarker3D>() == null)
            {
                var marker = gameObject.AddComponent<CandidateMarker3D>();
                if (arCamera != null) marker.arCamera = arCamera;
            }
        }

        void OnEnable() { if (game != null) { game.OnJudged += HandleJudged; game.OnReset += ClearAnchors; } }
        void OnDisable() { if (game != null) { game.OnJudged -= HandleJudged; game.OnReset -= ClearAnchors; } }

        private void HandleJudged(Candidate candidate, JudgeResult result)
        {
            if (candidate == null) return;

            if (result == JudgeResult.Good)
            {
                GameObject anchor = PlaceAnchor(candidate);
                if (spawnResultFx && anchor != null) ARResultFx.SpawnSuccess(anchor.transform, arCamera, successFxColor, successPopDistance);
                return;
            }

            if (spawnResultFx)
            {
                Vector3 position = ProjectCandidateToWorld(candidate, out _);
                Color color = result == JudgeResult.AlreadyUsed ? usedFxColor : failFxColor;
                ARResultFx.SpawnFailure(position, arCamera, color, result == JudgeResult.AlreadyUsed ? "USED" : "X", failMarkerSize);
            }
        }

        private GameObject PlaceAnchor(Candidate candidate)
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return null;

            Vector3 position = ProjectCandidateToWorld(candidate, out bool onPlane);
            if (!onPlane)
                position += arCamera.transform.up * ((anchors.Count % 3) * labelStaggerMeters);

            var anchorObject = new GameObject($"Committed_{candidate.Label}");
            anchorObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            if (!Application.isEditor && ARSession.state == ARSessionState.SessionTracking)
                anchorObject.AddComponent<ARAnchor>();

            GameObject label = committedLabelPrefab != null
                ? Instantiate(committedLabelPrefab, anchorObject.transform)
                : CreateDefaultLabel(candidate.Label, anchorObject.transform);
            label.transform.localPosition = onPlane ? Vector3.up * 0.04f : Vector3.zero;

            var billboard = label.GetComponent<CommittedLabelBillboard>();
            if (billboard == null) billboard = label.AddComponent<CommittedLabelBillboard>();
            billboard.targetCamera = arCamera;
            billboard.popDistance = successPopDistance;
            billboard.PlaySuccessPop();

            anchors.Enqueue(anchorObject);
            int limit = Mathf.Clamp(maxAnchors, 1, 6);
            while (anchors.Count > limit)
            {
                GameObject oldest = anchors.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
            RefreshLabelPriorities();
            return anchorObject;
        }

        private Vector3 ProjectCandidateToWorld(Candidate candidate, out bool onPlane)
        {
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null)
            {
                onPlane = false;
                return transform.position + transform.forward * fallbackDistance;
            }

            var box = candidate.SmoothedBBox;
            var screenPosition = new Vector2(
                (box.x + box.w * 0.5f) * Screen.width,
                (1f - box.y - box.h * 0.5f) * Screen.height);

            onPlane = raycastManager != null &&
                raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon);
            if (onPlane) return hits[0].pose.position;

            Ray screenRay = arCamera.ScreenPointToRay(screenPosition);
            return screenRay.GetPoint(fallbackDistance);
        }

        private static GameObject CreateDefaultLabel(string word, Transform parent)
        {
            var label = new GameObject("WordLabel");
            label.transform.SetParent(parent, false);

            var back = CreateTextLayer("WordDepth", label.transform, word.ToUpperInvariant(), new Color(0f, 0.22f, 0.16f, 0.95f), 0.014f, new Vector3(0.006f, -0.006f, 0.012f));
            back.fontStyle = FontStyle.Bold;

            var text = CreateTextLayer("WordFace", label.transform, word.ToUpperInvariant(), new Color(0.55f, 1f, 0.82f, 1f), 0.014f, Vector3.zero);
            text.fontStyle = FontStyle.Bold;

            var shine = CreateTextLayer("WordShine", label.transform, word.ToUpperInvariant(), new Color(1f, 1f, 1f, 0.45f), 0.014f, new Vector3(-0.003f, 0.004f, -0.002f));
            shine.fontStyle = FontStyle.Bold;

            label.AddComponent<CommittedLabelBillboard>();
            return label;
        }

        private static TextMesh CreateTextLayer(string name, Transform parent, string value, Color color, float size, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var text = go.AddComponent<TextMesh>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            return text;
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
            if (chainCoreLine != null) chainCoreLine.positionCount = 0;
            if (chainGlowLine != null) chainGlowLine.positionCount = 0;
        }

        void LateUpdate()
        {
            EnsureChainVisuals();
            UpdateChainLines();
            UpdateChainFlow();
            EmitChainSparkles();
        }

        private void UpdateChainLines()
        {
            if (chainCoreLine == null || chainGlowLine == null) return;
            if (!drawChainTrail)
            {
                chainCoreLine.positionCount = 0;
                chainGlowLine.positionCount = 0;
                return;
            }

            int count = 0;
            foreach (GameObject anchor in anchors) if (anchor != null) count++;
            if (count < 2)
            {
                chainCoreLine.positionCount = 0;
                chainGlowLine.positionCount = 0;
                return;
            }

            chainCoreLine.widthMultiplier = chainWidth;
            chainGlowLine.widthMultiplier = chainWidth * chainGlowWidthMultiplier;
            chainCoreLine.startColor = chainColorOld;
            chainCoreLine.endColor = chainColorNew;
            chainGlowLine.startColor = WithAlpha(chainColorOld, chainColorOld.a * 0.35f);
            chainGlowLine.endColor = WithAlpha(chainColorNew, chainColorNew.a * 0.4f);
            chainCoreLine.positionCount = count;
            chainGlowLine.positionCount = count;

            int index = 0;
            foreach (GameObject anchor in anchors)
            {
                if (anchor == null) continue;
                Vector3 p = anchor.transform.position + Vector3.up * chainHeightOffset;
                chainCoreLine.SetPosition(index, p);
                chainGlowLine.SetPosition(index, p);
                index++;
            }
        }

        private void UpdateChainFlow()
        {
            if (chainCoreMaterial == null) return;
            Vector2 scale = new Vector2(Mathf.Max(0.1f, chainTiling), 1f);
            Vector2 offset = new Vector2(Time.time * chainScrollSpeed, 0f);
            chainCoreMaterial.mainTextureScale = scale;
            chainCoreMaterial.mainTextureOffset = offset;
            if (chainGlowMaterial != null)
            {
                chainGlowMaterial.mainTextureScale = scale * 0.5f;
                chainGlowMaterial.mainTextureOffset = -offset * 0.45f;
            }
        }

        private void EmitChainSparkles()
        {
            if (!chainSparkles || chainParticleSystem == null || chainCoreLine == null || chainCoreLine.positionCount < 2) return;

            sparkleAccumulator += Time.deltaTime * sparkleRate;
            int emitCount = Mathf.FloorToInt(sparkleAccumulator);
            if (emitCount <= 0) return;
            sparkleAccumulator -= emitCount;

            for (int i = 0; i < emitCount; i++)
            {
                if (!TryGetRandomPointOnChain(out Vector3 point, out Vector3 tangent)) return;
                var ep = new ParticleSystem.EmitParams
                {
                    position = point + Random.insideUnitSphere * chainWidth * 1.4f,
                    velocity = tangent * Random.Range(0.02f, 0.08f) + Vector3.up * Random.Range(0.015f, 0.06f),
                    startColor = Color.Lerp(chainColorOld, chainColorNew, Random.value),
                    startSize = Random.Range(sparkleSize * 0.45f, sparkleSize),
                    startLifetime = Random.Range(sparkleLifetime * 0.55f, sparkleLifetime),
                };
                chainParticleSystem.Emit(ep, 1);
            }
        }

        private bool TryGetRandomPointOnChain(out Vector3 point, out Vector3 tangent)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            int count = chainCoreLine.positionCount;
            if (count < 2) return false;

            int segment = Random.Range(0, count - 1);
            Vector3 a = chainCoreLine.GetPosition(segment);
            Vector3 b = chainCoreLine.GetPosition(segment + 1);
            tangent = (b - a).normalized;
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.forward;
            point = Vector3.Lerp(a, b, Random.value);
            return true;
        }

        private void EnsureChainVisuals()
        {
            if (chainCoreLine != null && chainGlowLine != null && chainParticleSystem != null) return;

            chainFlowTexture = chainFlowTexture != null ? chainFlowTexture : CreateFlowTexture();
            if (chainGlowLine == null) chainGlowLine = CreateChainLine("WordChainGlow", true);
            if (chainCoreLine == null) chainCoreLine = CreateChainLine("WordChainCore", false);
            if (chainParticleSystem == null) chainParticleSystem = CreateChainParticles();
        }

        private LineRenderer CreateChainLine(string name, bool glow)
        {
            var trail = new GameObject(name);
            trail.transform.SetParent(transform, false);
            var line = trail.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = glow ? 8 : 5;
            line.numCapVertices = glow ? 8 : 5;
            line.textureMode = LineTextureMode.Tile;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = glow ? chainWidth * chainGlowWidthMultiplier : chainWidth;
            line.positionCount = 0;

            var shader = Shader.Find("Sprites/Default");
            var material = shader != null ? new Material(shader) : new Material(Shader.Find("Unlit/Transparent"));
            material.mainTexture = chainFlowTexture;
            if (glow)
            {
                chainGlowMaterial = material;
                line.startColor = WithAlpha(chainColorOld, chainColorOld.a * 0.35f);
                line.endColor = WithAlpha(chainColorNew, chainColorNew.a * 0.4f);
            }
            else
            {
                chainCoreMaterial = material;
                line.startColor = chainColorOld;
                line.endColor = chainColorNew;
            }
            line.material = material;
            return line;
        }

        private ParticleSystem CreateChainParticles()
        {
            var go = new GameObject("WordChainSparkles");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = sparkleLifetime;
            main.startSize = sparkleSize;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) renderer.material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            ps.Play();
            return ps;
        }

        private static Texture2D CreateFlowTexture()
        {
            var texture = new Texture2D(64, 1, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < texture.width; x++)
            {
                float t = x / (float)(texture.width - 1);
                float stripe = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 4f) * 0.5f + 0.5f), 5f);
                float glow = Mathf.Lerp(0.18f, 1f, stripe);
                texture.SetPixel(x, 0, new Color(glow, glow, glow, Mathf.Lerp(0.22f, 1f, stripe)));
            }
            texture.Apply();
            return texture;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Chain Trail")]
        private void PreviewChainTrail()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AnchorPlacementService] 再生(Play)中に実行してください。");
                return;
            }
            if (arCamera == null) arCamera = Camera.main;

            Vector3 origin = arCamera != null
                ? arCamera.transform.position + arCamera.transform.forward * fallbackDistance
                : Vector3.forward;
            Vector3 right = arCamera != null ? arCamera.transform.right : Vector3.right;
            Vector3 up = arCamera != null ? arCamera.transform.up : Vector3.up;
            Vector3 forward = arCamera != null ? arCamera.transform.forward : Vector3.forward;

            string[] words = { "apple", "eggplant", "tv", "vase" };
            for (int i = 0; i < words.Length; i++)
            {
                Vector3 position = origin
                    + right * (i * 0.2f - 0.3f)
                    + up * ((i % 2) * 0.08f)
                    + forward * (i * 0.35f);
                var obj = new GameObject($"PreviewAnchor_{words[i]}");
                obj.transform.position = position;
                CreateDefaultLabel(words[i], obj.transform);
                anchors.Enqueue(obj);
            }
        }

        [ContextMenu("Clear Preview Chain")]
        private void ClearPreviewChain() => ClearAnchors();

        [ContextMenu("Preview All AR (A+B+C)")]
        private void PreviewAllAR()
        {
            PreviewChainTrail();
            var marker = GetComponent<CandidateMarker3D>();
            if (marker == null) marker = FindObjectOfType<CandidateMarker3D>();
            if (marker != null) marker.PreviewDummyMarkers();
            PreviewResultFx();
        }

        [ContextMenu("Preview Result FX (Good/Bad/Used)")]
        private void PreviewResultFx()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AnchorPlacementService] 再生(Play)中に実行してください。");
                return;
            }
            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null) return;

            Vector3 origin = arCamera.transform.position + arCamera.transform.forward * fallbackDistance;
            Vector3 right = arCamera.transform.right;
            Vector3 up = arCamera.transform.up;

            if (anchors.Count > 0)
            {
                ARResultFx.SpawnSuccess(anchors.Peek().transform, arCamera, successFxColor, successPopDistance);
            }
            else
            {
                var anchor = new GameObject("PreviewSuccessAnchor");
                anchor.transform.position = origin + right * -0.25f + up * 0.08f;
                CreateDefaultLabel("apple", anchor.transform);
                anchors.Enqueue(anchor);
                ARResultFx.SpawnSuccess(anchor.transform, arCamera, successFxColor, successPopDistance);
            }

            ARResultFx.SpawnFailure(origin + right * 0.22f + up * 0.05f, arCamera, failFxColor, "X", failMarkerSize);
            ARResultFx.SpawnFailure(origin + right * 0.45f - up * 0.06f, arCamera, usedFxColor, "USED", failMarkerSize * 0.9f);
        }
#endif
    }

    public class CommittedLabelBillboard : MonoBehaviour
    {
        public Camera targetCamera;
        public float popDistance = 0.16f;
        private TextMesh[] textMeshes;
        private Color[] baseColors;
        private Vector3 baseLocalPosition;
        private float targetAlpha = 1f;
        private float targetScale = 1f;
        private float popTime = 1f;

        void Awake()
        {
            textMeshes = GetComponentsInChildren<TextMesh>(true);
            baseColors = new Color[textMeshes.Length];
            for (int i = 0; i < textMeshes.Length; i++) baseColors[i] = textMeshes[i].color;
            baseLocalPosition = transform.localPosition;
            transform.localScale = Vector3.one * 0.12f;
        }

        public void PlaySuccessPop() => popTime = 0f;

        public void SetVisualPriority(float priority)
        {
            targetAlpha = Mathf.Lerp(0.28f, 1f, Mathf.Clamp01(priority));
            targetScale = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(priority));
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

            popTime += Time.deltaTime;
            float pop = Mathf.Clamp01(popTime / 0.65f);
            float overshoot = Mathf.Sin(pop * Mathf.PI) * 0.22f;
            float scale = targetScale * (Mathf.SmoothStep(0.2f, 1f, pop) + overshoot);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * scale, Time.unscaledDeltaTime * 12f);
            transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(pop * Mathf.PI) * popDistance * (1f - pop * 0.35f));

            if (textMeshes == null) return;
            float shine = 1f + Mathf.Sin(Time.time * 5f) * 0.08f;
            for (int i = 0; i < textMeshes.Length; i++)
            {
                Color color = textMeshes[i].color;
                Color baseColor = baseColors[i] * shine;
                color.r = Mathf.Clamp01(baseColor.r);
                color.g = Mathf.Clamp01(baseColor.g);
                color.b = Mathf.Clamp01(baseColor.b);
                color.a = Mathf.Lerp(color.a, baseColors[i].a * targetAlpha, Time.unscaledDeltaTime * 8f);
                textMeshes[i].color = color;
            }
        }
    }

    public class ARResultFx : MonoBehaviour
    {
        private Camera targetCamera;
        private TextMesh[] texts;
        private LineRenderer[] rings;
        private Renderer[] renderers;
        private Color color;
        private float duration;
        private float age;
        private float baseScale;
        private bool success;

        public static void SpawnSuccess(Transform anchor, Camera camera, Color color, float popDistance)
        {
            var root = new GameObject("ARSuccessBurst");
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = Vector3.up * (0.08f + popDistance * 0.25f);
            root.transform.localRotation = Quaternion.identity;

            AddRing(root.transform, "SuccessRingOuter", color, 0.23f, 48, 0.012f);
            AddRing(root.transform, "SuccessRingInner", new Color(1f, 1f, 1f, 0.9f), 0.14f, 48, 0.006f);
            AddBurstDots(root.transform, color, 10, 0.11f, 0.018f);

            var fx = root.AddComponent<ARResultFx>();
            fx.Initialize(camera, color, 1.1f, 1f, true);
        }

        public static void SpawnFailure(Vector3 position, Camera camera, Color color, string label, float size)
        {
            var root = new GameObject("ARFailureBurst");
            root.transform.position = position;

            var textObject = new GameObject("FailureText");
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.AddComponent<TextMesh>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = label == "X" ? 96 : 58;
            text.characterSize = label == "X" ? size * 0.012f : size * 0.007f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;

            AddRing(root.transform, "FailRing", color, size * 0.38f, 40, size * 0.035f);
            AddCrossSlash(root.transform, color, size);
            AddBurstDots(root.transform, color, 8, size * 0.2f, size * 0.055f);

            var fx = root.AddComponent<ARResultFx>();
            fx.Initialize(camera, color, 0.9f, size, false);
        }

        private void Initialize(Camera camera, Color fxColor, float life, float scale, bool isSuccess)
        {
            targetCamera = camera;
            color = fxColor;
            duration = life;
            baseScale = scale;
            success = isSuccess;
            texts = GetComponentsInChildren<TextMesh>(true);
            rings = GetComponentsInChildren<LineRenderer>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            transform.localScale = Vector3.one * 0.25f;
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 direction = transform.position - targetCamera.transform.position;
                if (direction.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            float pop = success
                ? Mathf.SmoothStep(0.4f, 1.35f, Mathf.Sin(t * Mathf.PI * 0.85f))
                : Mathf.Lerp(1.25f, 0.95f, t) + Mathf.Sin(t * Mathf.PI * 8f) * 0.06f * (1f - t);
            transform.localScale = Vector3.one * baseScale * pop;

            float alpha = 1f - Mathf.SmoothStep(0.55f, 1f, t);
            foreach (TextMesh text in texts)
            {
                Color c = text.color;
                c.a = alpha;
                text.color = c;
            }
            foreach (LineRenderer ring in rings)
            {
                Color start = ring.startColor;
                Color end = ring.endColor;
                start.a = alpha * color.a;
                end.a = alpha * color.a;
                ring.startColor = start;
                ring.endColor = end;
                ring.widthMultiplier *= 1f + Time.deltaTime * 0.45f;
            }
            foreach (Renderer renderer in renderers)
            {
                if (renderer is LineRenderer) continue;
                if (renderer.material != null)
                {
                    Color c = renderer.material.color;
                    c.a = alpha;
                    renderer.material.color = c;
                }
            }

            if (age >= duration) Destroy(gameObject);
        }

        private static void AddRing(Transform parent, string name, Color color, float radius, int segments, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.numCornerVertices = 5;
            line.numCapVertices = 5;
            line.alignment = LineAlignment.View;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.startColor = color;
            line.endColor = color;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        private static void AddCrossSlash(Transform parent, Color color, float size)
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "FailSlashA" : "FailSlashB");
                go.transform.SetParent(parent, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.widthMultiplier = size * 0.04f;
                line.alignment = LineAlignment.View;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) line.material = new Material(shader);
                line.startColor = color;
                line.endColor = color;
                float r = size * 0.26f;
                if (i == 0)
                {
                    line.SetPosition(0, new Vector3(-r, -r, 0f));
                    line.SetPosition(1, new Vector3(r, r, 0f));
                }
                else
                {
                    line.SetPosition(0, new Vector3(-r, r, 0f));
                    line.SetPosition(1, new Vector3(r, -r, 0f));
                }
            }
        }

        private static void AddBurstDots(Transform parent, Color color, int count, float radius, float size)
        {
            var shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "BurstDot";
                dot.transform.SetParent(parent, false);
                dot.transform.localPosition = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, Random.Range(-0.015f, 0.015f));
                dot.transform.localScale = Vector3.one * size;
                var collider = dot.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var renderer = dot.GetComponent<Renderer>();
                if (shader != null) renderer.material = new Material(shader);
                renderer.material.color = Color.Lerp(color, Color.white, 0.35f);
            }
        }
    }
}

