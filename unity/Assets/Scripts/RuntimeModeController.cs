using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Hero.Detection;

namespace Hero
{
    /// <summary>EditorではMock、実機ではARカメラ検出へ自動切替する。</summary>
    public class RuntimeModeController : MonoBehaviour
    {
        public MockDetectionFeeder mockFeeder;
        public FrameCaptureService frameCapture;
        public ARSession arSession;
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public ARAnchorManager anchorManager;
        public bool forceMockOnDevice;

        void Awake()
        {
            bool useMock = Application.isEditor || forceMockOnDevice;
            if (mockFeeder != null) mockFeeder.enabled = useMock;
            if (frameCapture != null) frameCapture.enabled = !useMock;
            if (arSession != null) arSession.enabled = !useMock;
            if (planeManager != null) planeManager.enabled = !useMock;
            if (raycastManager != null) raycastManager.enabled = !useMock;
            if (anchorManager != null) anchorManager.enabled = !useMock;
        }
    }
}
