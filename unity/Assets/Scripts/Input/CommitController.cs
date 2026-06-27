using UnityEngine;
using Hero.Detection;
using Hero.Game;
using Hero.UI;

namespace Hero.Input
{
    /// <summary>ジェスチャをゲーム操作へ接続する。</summary>
    public class CommitController : MonoBehaviour
    {
        public InputDispatcher input;
        public CandidateManager candidates;
        public WordChainGameManager game;
        public HintPanel hintPanel;
        public CandidateDetailPanel detailPanel;

        void OnEnable()
        {
            if (input == null) return;
            input.OnSingleTap += HandleTap;
            input.OnDoubleTap += HandleDoubleTap;
            input.OnLongPress += HandleLongPress;
            input.OnShake += HandleShake;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.OnSingleTap -= HandleTap;
            input.OnDoubleTap -= HandleDoubleTap;
            input.OnLongPress -= HandleLongPress;
            input.OnShake -= HandleShake;
        }

        private Candidate FindCandidate(Vector2 position)
        {
            return candidates == null ? null : candidates.HitTest(position, new Vector2(Screen.width, Screen.height));
        }

        private void HandleTap(Vector2 position)
        {
            if (game == null || game.State.IsGameOver) return;
            Candidate candidate = FindCandidate(position);
            if (candidate != null) game.Commit(candidate);
        }

        private void HandleDoubleTap(Vector2 position)
        {
            if (game != null) game.Skip();
        }

        private void HandleLongPress(Vector2 position)
        {
            Candidate candidate = FindCandidate(position);
            if (candidate != null && detailPanel != null) detailPanel.Show(candidate);
        }

        private void HandleShake()
        {
            if (game == null || game.State.IsGameOver) return;
            var hints = game.Hint();
            if (hintPanel != null) hintPanel.Show(hints);
            else Debug.Log("[Hint] " + string.Join(", ", hints));
        }
    }
}
