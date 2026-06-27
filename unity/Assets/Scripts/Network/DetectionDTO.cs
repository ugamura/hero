using System;
using System.Collections.Generic;

namespace Hero.Network
{
    [Serializable]
    public class BBox
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }

    [Serializable]
    public class Detection
    {
        public int tracking_id;
        public string label;
        public float confidence;
        public BBox bbox;
    }

    [Serializable]
    public class ImageSize
    {
        public int w;
        public int h;
    }

    [Serializable]
    public class DetectResponse
    {
        public int frame_id;
        public int elapsed_ms;
        public ImageSize image_size;
        public List<Detection> detections;
    }
}
