using UnityEngine;
using UnityEngine.UI;

namespace Hero.UI
{
    /// <summary>初回起動時だけ表示する3ページの操作ガイド。</summary>
    public class TutorialController : MonoBehaviour
    {
        private const string SeenKey = "Hero.TutorialSeen.v1";
        public GameObject tutorialRoot;
        public Text titleText;
        public Text bodyText;
        public Text pageText;
        public Button nextButton;
        public Button closeButton;
        public bool showOnStart = true;
        public bool pauseGameWhileOpen = true;
        public string[] titles = { "FIND", "CHAIN", "NEED HELP?" };
        [TextArea(2, 5)] public string[] bodies =
        {
            "Move your camera and look for a green box.\nTap the real object to choose its word.",
            "The last letter becomes the next letter.\nAPPLE → E → EGGPLANT → T",
            "Double tap to skip with a penalty.\nShake the phone to reveal a hint."
        };

        private int page;
        private float previousTimeScale = 1f;
        private bool pausedByTutorial;

        void Start()
        {
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (closeButton != null) closeButton.onClick.AddListener(Dismiss);
            if (showOnStart && PlayerPrefs.GetInt(SeenKey, 0) == 0) Show();
            else if (tutorialRoot != null) tutorialRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (closeButton != null) closeButton.onClick.RemoveListener(Dismiss);
            ResumeGame();
        }

        public void Show()
        {
            page = 0;
            if (tutorialRoot != null) tutorialRoot.SetActive(true);
            if (pauseGameWhileOpen && !pausedByTutorial)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                pausedByTutorial = true;
            }
            Refresh();
        }

        public void Next()
        {
            if (page + 1 >= Mathf.Min(titles.Length, bodies.Length)) Dismiss();
            else { page++; Refresh(); }
        }

        public void Dismiss()
        {
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            ResumeGame();
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset Tutorial Progress")]
        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(SeenKey);
        }

        private void ResumeGame()
        {
            if (!pausedByTutorial) return;
            Time.timeScale = previousTimeScale;
            pausedByTutorial = false;
        }

        private void Refresh()
        {
            int count = Mathf.Min(titles.Length, bodies.Length);
            if (count == 0) return;
            page = Mathf.Clamp(page, 0, count - 1);
            if (titleText != null) titleText.text = titles[page];
            if (bodyText != null) bodyText.text = bodies[page];
            if (pageText != null) pageText.text = $"{page + 1} / {count}";
            if (nextButton != null)
            {
                Text label = nextButton.GetComponentInChildren<Text>();
                if (label != null) label.text = page == count - 1 ? "PLAY" : "NEXT";
            }
        }
    }
}
