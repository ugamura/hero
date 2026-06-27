using System;
using System.Collections.Generic;
using UnityEngine;
using Hero.Detection;

namespace Hero.Game
{
    /// <summary>しりとり判定、スコア、制限時間を一元管理する。</summary>
    public class WordChainGameManager : MonoBehaviour
    {
        [Header("Initial state")]
        public char initialLetter = 'a';
        public int initialLives = 3;
        [Min(10f)] public float roundDurationSec = 60f;

        [Header("Scoring and penalties")]
        public int scorePerGood = 10;
        public int scorePenaltyPerBad = -2;
        public int scorePenaltyPerSkip = -5;
        [Min(0f)] public float timePenaltyPerBad = 3f;
        [Min(0f)] public float timePenaltyPerSkip = 3f;

        [Header("Vocabulary (for hints)")]
        public List<string> vocabulary = new List<string>
        {
            "bicycle","car","motorcycle","airplane","bus","train","truck","boat",
            "light","hydrant","sign","meter","bench",
            "bird","cat","dog","horse","sheep","cow","elephant","bear","zebra","giraffe",
            "backpack","umbrella","handbag","tie","suitcase",
            "frisbee","skis","snowboard","ball","kite","bat","glove","skateboard","surfboard","racket",
            "bottle","glass","cup","fork","knife","spoon","bowl",
            "banana","apple","sandwich","orange","broccoli","carrot","hotdog","pizza","donut","cake",
            "chair","couch","plant","bed","table","toilet",
            "tv","laptop","mouse","remote","keyboard","phone",
            "microwave","oven","toaster","sink","refrigerator",
            "book","clock","vase","scissors","teddybear","hairdrier","toothbrush"
        };

        public GameState State { get; private set; } = new GameState();

        public event Action<Candidate, JudgeResult> OnJudged;
        public event Action OnSkipped;
        public event Action OnHintRequested;
        public event Action OnStateChanged;
        public event Action OnReset;
        public event Action OnGameOver;

        public char RequiredLetter => State.RequiredLetter;
        public bool IsUsed(string word) => State.UsedWords.Contains(NormalizeWord(word));

        private int lastDisplayedSecond = -1;

        void Awake() => ResetGame();

        void Update()
        {
            if (State.IsGameOver) return;
            State.TimeRemaining = Mathf.Max(0f, State.TimeRemaining - Time.deltaTime);
            int currentSecond = Mathf.CeilToInt(State.TimeRemaining);
            if (currentSecond != lastDisplayedSecond)
            {
                lastDisplayedSecond = currentSecond;
                OnStateChanged?.Invoke();
            }
            if (State.TimeRemaining <= 0f) EndGame();
        }

        public void ResetGame()
        {
            char first = char.ToLowerInvariant(initialLetter);
            if (first < 'a' || first > 'z') first = 'a';
            State = new GameState
            {
                RequiredLetter = first,
                Lives = Mathf.Max(1, initialLives),
                TimeRemaining = Mathf.Max(10f, roundDurationSec)
            };
            lastDisplayedSecond = Mathf.CeilToInt(State.TimeRemaining);
            OnReset?.Invoke();
            OnStateChanged?.Invoke();
        }

        public JudgeResult Commit(Candidate candidate)
        {
            string word = NormalizeWord(candidate != null ? candidate.Label : null);
            if (State.IsGameOver || string.IsNullOrEmpty(word)) return JudgeResult.Bad;

            JudgeResult result;
            if (State.UsedWords.Contains(word))
            {
                result = JudgeResult.AlreadyUsed;
            }
            else if (word[0] != State.RequiredLetter)
            {
                result = JudgeResult.Bad;
                State.Score += scorePenaltyPerBad;
                State.Lives = Mathf.Max(0, State.Lives - 1);
                State.TimeRemaining = Mathf.Max(0f, State.TimeRemaining - timePenaltyPerBad);
            }
            else
            {
                result = JudgeResult.Good;
                State.UsedWords.Add(word);
                State.UsedWordHistory.Add(word);
                State.Score += scorePerGood;
                State.RequiredLetter = LastLetter(word);
            }

            Debug.Log($"[Game] Commit {word} -> {result}, score={State.Score}, next={State.RequiredLetter}");
            OnJudged?.Invoke(candidate, result);
            OnStateChanged?.Invoke();
            if (State.Lives <= 0 || State.TimeRemaining <= 0f) EndGame();
            return result;
        }

        public void Skip()
        {
            if (State.IsGameOver) return;
            State.Score += scorePenaltyPerSkip;
            State.TimeRemaining = Mathf.Max(0f, State.TimeRemaining - timePenaltyPerSkip);
            State.RequiredLetter = State.RequiredLetter == 'z' ? 'a' : (char)(State.RequiredLetter + 1);
            Debug.Log($"[Game] Skip, next={State.RequiredLetter}");
            OnSkipped?.Invoke();
            OnStateChanged?.Invoke();
            if (State.TimeRemaining <= 0f) EndGame();
        }

        public List<string> Hint(int max = 3)
        {
            var result = new List<string>();
            if (State.IsGameOver) return result;
            foreach (string raw in vocabulary)
            {
                string word = NormalizeWord(raw);
                if (string.IsNullOrEmpty(word) || word[0] != State.RequiredLetter || State.UsedWords.Contains(word)) continue;
                result.Add(word);
                if (result.Count >= max) break;
            }
            OnHintRequested?.Invoke();
            return result;
        }

        public static string NormalizeWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Trim().ToLowerInvariant().ToCharArray();
            var letters = new List<char>(chars.Length);
            foreach (char c in chars) if (c >= 'a' && c <= 'z') letters.Add(c);
            return new string(letters.ToArray());
        }

        private static char LastLetter(string word)
        {
            for (int i = word.Length - 1; i >= 0; i--)
                if (word[i] >= 'a' && word[i] <= 'z') return word[i];
            return 'a';
        }

        private void EndGame()
        {
            if (State.IsGameOver) return;
            State.IsGameOver = true;
            State.TimeRemaining = Mathf.Max(0f, State.TimeRemaining);
            OnStateChanged?.Invoke();
            OnGameOver?.Invoke();
        }
    }
}
