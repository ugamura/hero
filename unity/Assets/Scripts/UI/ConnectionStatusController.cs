using UnityEngine;
using UnityEngine.UI;
using Hero.Network;

namespace Hero.UI
{
    public class ConnectionStatusController : MonoBehaviour
    {
        public DetectionClient client;
        public Text statusText;
        public Color onlineColor = new Color(0f, 0.9f, 0.47f);
        public Color offlineColor = new Color(1f, 0.38f, 0.3f);

        void OnEnable()
        {
            if (client != null) client.OnStatusChanged += SetStatus;
        }

        void OnDisable()
        {
            if (client != null) client.OnStatusChanged -= SetStatus;
        }

        private void SetStatus(string value)
        {
            if (statusText == null) return;
            statusText.text = value;
            statusText.color = value == "OFFLINE" || value == "INVALID RESPONSE" ? offlineColor : onlineColor;
        }
    }
}
