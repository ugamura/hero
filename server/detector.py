"""YOLOv8 + ByteTrack wrapper."""
from __future__ import annotations
import numpy as np
from ultralytics import YOLO

from schemas import BBox, Detection


class YOLODetector:
    def __init__(self, weights: str, conf: float, iou: float,
                 use_tracker: bool, tracker_cfg: str, normalize_fn):
        self.model = YOLO(weights)
        self.conf = conf
        self.iou = iou
        self.use_tracker = use_tracker
        self.tracker_cfg = tracker_cfg
        self.normalize = normalize_fn

    def infer(self, img_bgr: np.ndarray) -> list[Detection]:
        h, w = img_bgr.shape[:2]
        if self.use_tracker:
            results = self.model.track(
                img_bgr, conf=self.conf, iou=self.iou,
                persist=True, tracker=self.tracker_cfg, verbose=False,
            )
        else:
            results = self.model.predict(
                img_bgr, conf=self.conf, iou=self.iou, verbose=False,
            )

        detections: list[Detection] = []
        r = results[0]
        if r.boxes is None:
            return detections

        boxes = r.boxes
        names = r.names
        ids = (boxes.id.int().tolist()
               if (self.use_tracker and boxes.id is not None)
               else [None] * len(boxes))

        for box, cls, conf, tid in zip(
            boxes.xyxy.cpu().numpy(),
            boxes.cls.cpu().numpy(),
            boxes.conf.cpu().numpy(),
            ids,
        ):
            raw_label = names[int(cls)]
            label = self.normalize(raw_label)
            if label is None:
                continue
            x1, y1, x2, y2 = box.tolist()
            detections.append(Detection(
                tracking_id=tid,
                label=label,
                confidence=float(conf),
                bbox=BBox(x=x1 / w, y=y1 / h,
                          w=(x2 - x1) / w, h=(y2 - y1) / h),
            ))
        return detections
