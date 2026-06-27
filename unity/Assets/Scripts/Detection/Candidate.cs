using UnityEngine;
using Hero.Network;
using NetworkDetection = Hero.Network.Detection;

namespace Hero.Detection
{
    public enum CandidateState { Match, Unmatch, Used, LowConf }

    /// <summary>画面上の単一候補。bbox は EMA で平滑化する。</summary>
    public class Candidate
    {
        public int TrackingId;
        public string Label;
        public float Confidence;
        public BBox SmoothedBBox = new BBox();
        public CandidateState State;
        public float LastSeenTime;
        public NetworkDetection LatestRaw;

        private const float EmaAlpha = 0.5f;

        public Candidate(NetworkDetection detection)
        {
            TrackingId = detection.tracking_id;
            UpdateIdentity(detection);
            if (detection.bbox != null)
            {
                SmoothedBBox.x = detection.bbox.x;
                SmoothedBBox.y = detection.bbox.y;
                SmoothedBBox.w = detection.bbox.w;
                SmoothedBBox.h = detection.bbox.h;
            }
        }

        public void UpdateFrom(NetworkDetection detection)
        {
            UpdateIdentity(detection);
            if (detection.bbox == null) return;
            SmoothedBBox.x = Mathf.Lerp(SmoothedBBox.x, detection.bbox.x, EmaAlpha);
            SmoothedBBox.y = Mathf.Lerp(SmoothedBBox.y, detection.bbox.y, EmaAlpha);
            SmoothedBBox.w = Mathf.Lerp(SmoothedBBox.w, detection.bbox.w, EmaAlpha);
            SmoothedBBox.h = Mathf.Lerp(SmoothedBBox.h, detection.bbox.h, EmaAlpha);
        }

        private void UpdateIdentity(NetworkDetection detection)
        {
            Label = detection.label ?? string.Empty;
            Confidence = detection.confidence;
            LatestRaw = detection;
            LastSeenTime = Time.time;
        }

        public float Area => Mathf.Max(0f, SmoothedBBox.w) * Mathf.Max(0f, SmoothedBBox.h);

        public bool Contains(Vector2 normalizedPosition)
        {
            return normalizedPosition.x >= SmoothedBBox.x
                && normalizedPosition.x <= SmoothedBBox.x + SmoothedBBox.w
                && normalizedPosition.y >= SmoothedBBox.y
                && normalizedPosition.y <= SmoothedBBox.y + SmoothedBBox.h;
        }
    }
}
