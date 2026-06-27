using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hero.UI
{
    /// <summary>
    /// シェイクで呼ばれるヒントパネル。requiredLetter で始まる候補単語を表示。
    /// </summary>
    public class HintPanel : MonoBehaviour
    {
        public GameObject panelRoot;
        public Text hintText;
        public float autoHideSec = 3f;

        private Coroutine hideCo;

        void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Show(IList<string> words)
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            if (hintText != null)
            {
                hintText.text = (words == null || words.Count == 0)
                    ? "No hints available"
                    : "Try: " + string.Join(", ", words);
            }
            if (hideCo != null) StopCoroutine(hideCo);
            hideCo = StartCoroutine(HideAfter(autoHideSec));
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private IEnumerator HideAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            Hide();
        }
    }
}
