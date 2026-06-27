using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Hero.Network;

namespace Hero.Detection
{
    /// <summary>
    /// ARCameraManager から CPU image を取得 → JPEG → DetectionClient に渡す。
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class FrameCaptureService : MonoBehaviour
    {
        [Tooltip("検出サーバへの送信レート (fps)")]
        public float sendFps = 5f;

        [Range(30, 95)] public int jpegQuality = 70;

        public DetectionClient client;

        private ARCameraManager cameraManager;
        private float lastSentAt;
        private int frameId;
        private bool busy;

        void Awake() => cameraManager = GetComponent<ARCameraManager>();
        void OnEnable() => cameraManager.frameReceived += OnFrameReceived;
        void OnDisable() => cameraManager.frameReceived -= OnFrameReceived;

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (busy || client == null || client.IsBusy) return;
            float interval = 1f / Mathf.Max(0.1f, sendFps);
            if (Time.time - lastSentAt < interval) return;

            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return;
            busy = true;
            lastSentAt = Time.time;
            StartCoroutine(EncodeAndSend(image));
        }

        private IEnumerator EncodeAndSend(XRCpuImage image)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(Mathf.Max(1, image.width / 2), Mathf.Max(1, image.height / 2)),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY,
            };

            var conversion = image.ConvertAsync(conversionParams);
            while (!conversion.status.IsDone()) yield return null;

            if (conversion.status != XRCpuImage.AsyncConversionStatus.Ready)
            {
                conversion.Dispose();
                image.Dispose();
                busy = false;
                yield break;
            }

            var rawData = conversion.GetData<byte>();
            var tex = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat,
                false);
            tex.LoadRawTextureData(rawData);
            tex.Apply();

            conversion.Dispose();
            image.Dispose();

            byte[] jpg = tex.EncodeToJPG(jpegQuality);
            Destroy(tex);

            frameId++;
            client.Send(jpg, frameId);
            busy = false;
        }
    }
}
