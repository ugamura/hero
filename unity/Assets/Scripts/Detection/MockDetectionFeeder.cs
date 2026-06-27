using System.Collections.Generic;
using UnityEngine;
using Hero.Network;
using NetworkDetection = Hero.Network.Detection;

namespace Hero.Detection
{
    /// <summary>
    /// 【開発用】サーバを使わずに偽の検出結果を流し込む。
    /// FrameCaptureService の代わりに使うことで、Editor 上でゲームロジックを試せる。
    ///
    /// 使い方:
    /// 1. このコンポーネントを GameSystems にアタッチ
    /// 2. mockDetections に試したい物体を登録
    /// 3. DetectionClient と CandidateManager を inspector で繋ぐ必要はなし。
    ///    代わりにこのスクリプトの client を CandidateManager にアサイン。
    /// </summary>
    public class MockDetectionFeeder : MonoBehaviour
    {
        [System.Serializable]
        public class MockEntry
        {
            public string label = "apple";
            public float confidence = 0.9f;
            [Tooltip("正規化座標 (0-1) 左上原点")]
            public Vector2 position = new Vector2(0.4f, 0.4f);
            public Vector2 size = new Vector2(0.2f, 0.2f);
            public int trackingId = 1;
        }

        public DetectionClient targetClient;     // 偽結果をここに OnDetected で流す
        public float emitIntervalSec = 0.5f;

        public List<MockEntry> mockDetections = new List<MockEntry>
        {
            new MockEntry { label = "apple",    trackingId = 1, position = new Vector2(0.3f, 0.3f) },
            new MockEntry { label = "eggplant", trackingId = 2, position = new Vector2(0.6f, 0.4f) },
            new MockEntry { label = "chair",    trackingId = 3, position = new Vector2(0.45f, 0.65f) },
            new MockEntry { label = "tv",       trackingId = 4, position = new Vector2(0.14f, 0.58f), size = new Vector2(0.18f, 0.15f) },
            new MockEntry { label = "vase",     trackingId = 5, position = new Vector2(0.70f, 0.62f), size = new Vector2(0.16f, 0.22f) },
            new MockEntry { label = "elephant", trackingId = 6, position = new Vector2(0.40f, 0.48f), size = new Vector2(0.24f, 0.18f) },
        };

        private float lastEmit;

        void Update()
        {
            if (targetClient == null) return;
            if (Time.time - lastEmit < emitIntervalSec) return;
            lastEmit = Time.time;
            Emit();
        }

        private void Emit()
        {
            var res = new DetectResponse
            {
                frame_id = (int)(Time.time * 10),
                elapsed_ms = 0,
                image_size = new ImageSize { w = Screen.width, h = Screen.height },
                detections = new List<NetworkDetection>(),
            };
            foreach (var m in mockDetections)
            {
                res.detections.Add(new NetworkDetection
                {
                    tracking_id = m.trackingId,
                    label = m.label,
                    confidence = m.confidence,
                    bbox = new BBox { x = m.position.x, y = m.position.y, w = m.size.x, h = m.size.y },
                });
            }
            // DetectionClient の OnDetected を直接呼ぶ手段はないので、
            // public な発火メソッドを足すか、CandidateManager 側に直接渡す。
            // ここではリフレクション風に events に渡すのを避け、専用の入口を使う。
            targetClient.InjectForTesting(res);
        }
    }
}
