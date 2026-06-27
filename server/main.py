"""
AR Word Chain - Detection Server.

Usage:
    pip install -r requirements.txt
    uvicorn main:app --host 0.0.0.0 --port 8000 --reload
"""
from __future__ import annotations

import time
from pathlib import Path

import cv2
import numpy as np
import yaml
from fastapi import FastAPI, File, Form, UploadFile
from fastapi.middleware.cors import CORSMiddleware

from detector import YOLODetector
from label_map import make_normalizer
from schemas import DetectResponse, ImageSize

# ---------- Config ----------
BASE_DIR = Path(__file__).resolve().parent
with (BASE_DIR / "config.yaml").open(encoding="utf-8") as f:
    cfg = yaml.safe_load(f)

normalize = make_normalizer(
    label_map=cfg["label_normalize"],
    exclude=set(cfg["exclude_labels"]),
)

print(f"[init] Loading model: {cfg['model']['weights']}")
detector = YOLODetector(
    weights=cfg["model"]["weights"],
    conf=cfg["model"]["conf_threshold"],
    iou=cfg["model"]["iou_threshold"],
    use_tracker=cfg["model"]["use_tracker"],
    tracker_cfg=cfg["model"]["tracker"],
    normalize_fn=normalize,
)
print("[init] Model loaded.")

# ---------- App ----------
app = FastAPI(title="AR Word Chain Detection Server")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_methods=["*"], allow_headers=["*"],
)


@app.get("/healthz")
def healthz():
    return {"status": "ok", "model": cfg["model"]["weights"]}


@app.post("/detect", response_model=DetectResponse)
async def detect(
    image: UploadFile = File(...),
    frame_id: int = Form(-1),
):
    t0 = time.time()
    raw = await image.read()
    arr = np.frombuffer(raw, np.uint8)
    img = cv2.imdecode(arr, cv2.IMREAD_COLOR)
    if img is None:
        return DetectResponse(
            frame_id=frame_id, elapsed_ms=0,
            image_size=ImageSize(w=0, h=0), detections=[],
        )

    detections = detector.infer(img)
    elapsed = int((time.time() - t0) * 1000)
    return DetectResponse(
        frame_id=frame_id,
        elapsed_ms=elapsed,
        image_size=ImageSize(w=img.shape[1], h=img.shape[0]),
        detections=detections,
    )
