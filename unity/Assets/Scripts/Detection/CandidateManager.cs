using System.Collections.Generic;
using UnityEngine;
using Hero.Network;
using Hero.Game;
using NetworkDetection = Hero.Network.Detection;

namespace Hero.Detection
{
    /// <summary>検出結果の追跡、状態分類、タップ解決を担当する。</summary>
    public class CandidateManager : MonoBehaviour
    {
        [Range(0f, 1f)] public float lowConfThreshold = 0.5f;
        [Min(0.1f)] public float candidateTTL = 1.0f;
        public DetectionClient client;
        public WordChainGameManager game;

        private readonly Dictionary<int, Candidate> active = new Dictionary<int, Candidate>();
        private readonly List<int> removalBuffer = new List<int>();
        public IReadOnlyCollection<Candidate> Active => active.Values;

        void OnEnable()
        {
            if (client != null) client.OnDetected += HandleDetected;
            if (game != null) game.OnStateChanged += RefreshStates;
        }

        void OnDisable()
        {
            if (client != null) client.OnDetected -= HandleDetected;
            if (game != null) game.OnStateChanged -= RefreshStates;
        }

        void Update()
        {
            float now = Time.time;
            removalBuffer.Clear();
            foreach (var pair in active)
                if (now - pair.Value.LastSeenTime > candidateTTL) removalBuffer.Add(pair.Key);
            foreach (int key in removalBuffer) active.Remove(key);
        }

        private void HandleDetected(DetectResponse response)
        {
            if (response == null || response.detections == null) return;
            for (int i = 0; i < response.detections.Count; i++)
            {
                NetworkDetection detection = response.detections[i];
                if (detection == null || detection.bbox == null || string.IsNullOrWhiteSpace(detection.label)) continue;
                int key = detection.tracking_id > 0 ? detection.tracking_id : StableFallbackId(detection, i);
                if (active.TryGetValue(key, out Candidate candidate)) candidate.UpdateFrom(detection);
                else active[key] = new Candidate(detection) { TrackingId = key };
            }
            RefreshStates();
        }

        public CandidateState ClassifyState(Candidate candidate)
        {
            if (candidate.Confidence < lowConfThreshold) return CandidateState.LowConf;
            string word = WordChainGameManager.NormalizeWord(candidate.Label);
            if (string.IsNullOrEmpty(word)) return CandidateState.Unmatch;
            if (game == null) return CandidateState.Match;
            if (game.IsUsed(word)) return CandidateState.Used;
            return word[0] == game.RequiredLetter ? CandidateState.Match : CandidateState.Unmatch;
        }

        public Candidate HitTest(Vector2 screenPosition, Vector2 screenSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f) return null;
            Vector2 normalized = new Vector2(
                Mathf.Clamp01(screenPosition.x / screenSize.x),
                Mathf.Clamp01(1f - screenPosition.y / screenSize.y));

            Candidate best = null;
            foreach (Candidate candidate in active.Values)
            {
                if (candidate.State == CandidateState.LowConf || !candidate.Contains(normalized)) continue;
                if (best == null || candidate.Area < best.Area ||
                    (Mathf.Approximately(candidate.Area, best.Area) && candidate.Confidence > best.Confidence))
                    best = candidate;
            }
            return best;
        }

        public void Clear()
        {
            active.Clear();
        }

        private void RefreshStates()
        {
            foreach (Candidate candidate in active.Values) candidate.State = ClassifyState(candidate);
        }

        private static int StableFallbackId(NetworkDetection detection, int index)
        {
            unchecked
            {
                int hash = 17;
                string label = detection.label.ToLowerInvariant();
                for (int i = 0; i < label.Length; i++) hash = hash * 31 + label[i];
                hash = hash * 31 + Mathf.RoundToInt((detection.bbox.x + detection.bbox.w * 0.5f) * 10f);
                hash = hash * 31 + Mathf.RoundToInt((detection.bbox.y + detection.bbox.h * 0.5f) * 10f);
                hash = hash * 31 + index;
                return hash > 0 ? -hash : hash;
            }
        }
    }
}
