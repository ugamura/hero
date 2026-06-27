using UnityEngine;
using UnityEngine.UI;
using Hero.Game;

namespace Hero.UI
{
    /// <summary>スコア、必要文字、残り時間、履歴、ゲーム終了を表示する。</summary>
    public class HUDController : MonoBehaviour
    {
        public WordChainGameManager game;
        public Text scoreText;
        public Text requiredLetterText;
        public Text livesText;
        public Text timerText;
        public Text usedWordsText;
        public Text statusText;
        public Button resetButton;
        public Button playAgainButton;
        public GameObject gameOverPanel;
        public Text gameOverText;

        void OnEnable()
        {
            if (game != null)
            {
                game.OnStateChanged += Refresh;
                game.OnReset += Refresh;
                game.OnGameOver += HandleGameOver;
            }
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnResetClicked);
            Refresh();
        }

        void OnDisable()
        {
            if (game != null)
            {
                game.OnStateChanged -= Refresh;
                game.OnReset -= Refresh;
                game.OnGameOver -= HandleGameOver;
            }
            if (resetButton != null) resetButton.onClick.RemoveListener(OnResetClicked);
            if (playAgainButton != null) playAgainButton.onClick.RemoveListener(OnResetClicked);
        }

        private void OnResetClicked()
        {
            if (game != null) game.ResetGame();
        }

        private void HandleGameOver()
        {
            Refresh();
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (gameOverText != null && game != null) gameOverText.text = $"TIME UP\nSCORE {game.State.Score}";
        }

        private void Refresh()
        {
            if (game == null) return;
            GameState state = game.State;
            if (scoreText != null) scoreText.text = $"SCORE  {state.Score}";
            if (requiredLetterText != null) requiredLetterText.text = $"NEXT  {char.ToUpperInvariant(state.RequiredLetter)}";
            if (livesText != null) livesText.text = $"LIFE  {new string('●', Mathf.Max(0, state.Lives))}";
            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(state.TimeRemaining);
                timerText.text = $"{seconds / 60:0}:{seconds % 60:00}";
                timerText.color = seconds <= 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
            if (usedWordsText != null)
                usedWordsText.text = state.UsedWordHistory.Count == 0 ? "WORDS  -" : "WORDS  " + string.Join("  ›  ", state.UsedWordHistory);
            if (statusText != null) statusText.text = state.IsGameOver ? "Tap RESET to play again" : "Tap a green object  •  Double tap: skip  •  Shake: hint";
            if (gameOverPanel != null && !state.IsGameOver) gameOverPanel.SetActive(false);
        }
    }
}
