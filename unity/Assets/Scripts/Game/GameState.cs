using System.Collections.Generic;

namespace Hero.Game
{
    /// <summary>しりとりの現在状態。</summary>
    public class GameState
    {
        public char RequiredLetter = 'a';
        public HashSet<string> UsedWords = new HashSet<string>();
        public List<string> UsedWordHistory = new List<string>();
        public int Score;
        public int Lives = 3;
        public float TimeRemaining = 60f;
        public bool IsGameOver;
    }
}
