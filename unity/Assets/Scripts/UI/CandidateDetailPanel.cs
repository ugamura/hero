using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Hero.Detection;

namespace Hero.UI
{
    /// <summary>候補の長押しで単語・信頼度を短時間表示する。</summary>
    public class CandidateDetailPanel : MonoBehaviour
    {
        public GameObject panelRoot;
        public Text wordText;
        public Text confidenceText;
        public float autoHideSec = 2.5f;
        private Coroutine hideRoutine;

        void Awake() { if (panelRoot != null) panelRoot.SetActive(false); }

        public void Show(Candidate candidate)
        {
            if (candidate == null || panelRoot == null) return;
            panelRoot.SetActive(true);
            if (wordText != null) wordText.text = candidate.Label.ToUpperInvariant();
            if (confidenceText != null) confidenceText.text = $"Confidence  {candidate.Confidence:P0}";
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        public void Hide() { if (panelRoot != null) panelRoot.SetActive(false); }
        private IEnumerator HideAfterDelay() { yield return new WaitForSeconds(autoHideSec); Hide(); }
    }
}
