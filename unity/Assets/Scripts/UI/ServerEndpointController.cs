using UnityEngine;
using UnityEngine.UI;
using Hero.Network;

namespace Hero.UI
{
    public class ServerEndpointController : MonoBehaviour
    {
        private const string EndpointKey = "Hero.ServerEndpoint";
        public DetectionClient client;
        public GameObject panelRoot;
        public InputField endpointInput;
        public Button openButton;
        public Button applyButton;
        public Button closeButton;
        public Text validationText;
        private float previousTimeScale = 1f;
        private bool paused;

        void Start()
        {
            if (client != null)
            {
                string saved = PlayerPrefs.GetString(EndpointKey, client.endpoint);
                client.SetEndpoint(saved);
                if (endpointInput != null) endpointInput.text = client.endpoint;
            }
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (applyButton != null) applyButton.onClick.AddListener(Apply);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            ResumeGame();
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (!paused) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; paused = true; }
        }
        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            ResumeGame();
        }
        private void ResumeGame()
        {
            if (!paused) return;
            Time.timeScale = previousTimeScale;
            paused = false;
        }

        public void Apply()
        {
            if (client == null || endpointInput == null) return;
            string value = endpointInput.text.Trim();
            if (!value.StartsWith("http://") && !value.StartsWith("https://")) value = "http://" + value;
            value = value.TrimEnd('/');
            if (!value.EndsWith("/detect")) value += "/detect";
            if (!System.Uri.TryCreate(value, System.UriKind.Absolute, out _))
            {
                if (validationText != null) validationText.text = "Enter a valid server address";
                return;
            }
            client.SetEndpoint(value);
            endpointInput.text = value;
            PlayerPrefs.SetString(EndpointKey, value);
            PlayerPrefs.Save();
            if (validationText != null) validationText.text = "Saved";
            Close();
        }
    }
}
