using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Hero.ARLayer
{
    /// <summary>
    /// D案: 深度オクルージョン。対応端末では仮想物(ラベル/マーカー)が
    /// 実物の家具などの後ろに回り込むと隠れ、現実空間に存在する説得力が増す。
    ///
    /// 注意: Mockデモには深度情報がないためEditorでは効果が出ない(実機のみ)。
    /// 必要な端末で手動アタッチして使う。
    /// </summary>
    public class AROcclusionSetup : MonoBehaviour
    {
        // 既定はOFF。深度非対応・不安定な端末では ARCore がネイティブクラッシュすることがあるため、
        // 動作確認済みの端末で明示的にONにして使う(実験的)。
        public bool enableOcclusion = false;
        [Tooltip("ARカメラ。未設定なら Camera.main を使用。")]
        public Camera arCamera;

        void Start()
        {
            if (Application.isEditor)
            {
                Debug.Log("[AROcclusionSetup] Editor(Mock)では深度オクルージョンは無効です。効果確認は実機で行ってください。");
                return;
            }
            if (!enableOcclusion) return;

            if (arCamera == null) arCamera = Camera.main;
            if (arCamera == null)
            {
                Debug.LogWarning("[AROcclusionSetup] ARカメラが見つからないため有効化できません。");
                return;
            }

            var manager = arCamera.GetComponent<AROcclusionManager>();
            if (manager == null) manager = arCamera.gameObject.AddComponent<AROcclusionManager>();
            manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;

            Debug.Log("[AROcclusionSetup] AROcclusionManager を有効化しました(深度API対応端末でのみ反映)。");
        }
    }
}
