using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Hero.Network
{
    /// <summary>JPEGフレームを検出サーバへ送り、追跡付き候補を受信する。</summary>
    public class DetectionClient : MonoBehaviour
    {
        public string endpoint = "http://127.0.0.1:8000/detect";
        [Min(0.5f)] public float timeoutSec = 2f;
        public bool IsBusy { get; private set; }
        public event Action<DetectResponse> OnDetected;
        public event Action<string> OnError;
        public event Action<string> OnStatusChanged;

        public void Send(byte[] jpegBytes, int frameId)
        {
            if (IsBusy || jpegBytes == null || jpegBytes.Length == 0) return;
            StartCoroutine(SendCoroutine(jpegBytes, frameId));
        }

        public void InjectForTesting(DetectResponse response)
        {
            OnStatusChanged?.Invoke("DEMO");
            OnDetected?.Invoke(response);
        }

        public void SetEndpoint(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) endpoint = value.Trim();
        }

        private IEnumerator SendCoroutine(byte[] jpegBytes, int frameId)
        {
            IsBusy = true;
            OnStatusChanged?.Invoke("DETECTING");
            var form = new WWWForm();
            form.AddBinaryData("image", jpegBytes, "frame.jpg", "image/jpeg");
            form.AddField("frame_id", frameId);

            using (var request = UnityWebRequest.Post(endpoint, form))
            {
                request.timeout = Mathf.CeilToInt(timeoutSec);
                yield return request.SendWebRequest();
                IsBusy = false;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string message = string.IsNullOrEmpty(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    OnStatusChanged?.Invoke("OFFLINE");
                    OnError?.Invoke(message);
                    yield break;
                }

                DetectResponse response;
                try { response = JsonUtility.FromJson<DetectResponse>(request.downloadHandler.text); }
                catch (Exception exception)
                {
                    OnStatusChanged?.Invoke("INVALID RESPONSE");
                    OnError?.Invoke("Parse error: " + exception.Message);
                    yield break;
                }
                if (response == null)
                {
                    OnStatusChanged?.Invoke("INVALID RESPONSE");
                    OnError?.Invoke("Detection response was empty.");
                    yield break;
                }
                OnStatusChanged?.Invoke($"LIVE  {response.elapsed_ms}ms");
                OnDetected?.Invoke(response);
            }
        }
    }
}
