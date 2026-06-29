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
from fastapi.responses import FileResponse, HTMLResponse

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


@app.get("/cert")
def get_cert():
    cert_path = BASE_DIR / "cert.pem"
    if not cert_path.exists():
        return {"error": "cert not found — run start_server_https.sh"}
    return FileResponse(
        cert_path,
        media_type="application/x-x509-ca-cert",
        filename="hero_dev_ca.pem",
    )


@app.get("/check", response_class=HTMLResponse)
def check():
    return """<!doctype html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>YOLO Live</title>
  <style>
    * { margin:0; padding:0; box-sizing:border-box; }
    body { background:#000; display:flex; flex-direction:column; height:100dvh; overflow:hidden;
           color:#eee; font-family:sans-serif; }
    #display { width:100%; flex:1; min-height:0; display:block; object-fit:contain; }
    #hud { background:rgba(0,0,0,.85); padding:7px 14px; display:flex;
           justify-content:space-between; align-items:center; font-size:13px; flex-shrink:0; }
    #labels { background:rgba(0,0,0,.85); padding:6px 12px; font-size:15px;
              min-height:38px; flex-shrink:0; overflow-x:auto; white-space:nowrap; }
    .tag { display:inline-block; background:#00e676; color:#000; border-radius:4px;
           padding:1px 9px; margin:2px; font-weight:bold; }
    #overlay { position:fixed; inset:0; display:flex; align-items:center; justify-content:center; }
    #start-btn { background:#00e676; color:#000; border:none; border-radius:14px;
                 padding:18px 40px; font-size:20px; font-weight:bold; cursor:pointer; }
  </style>
</head>
<body>
  <canvas id="display"></canvas>
  <div id="hud"><span id="status">待機中</span><span id="latency"></span></div>
  <div id="labels"><span style="color:#444">検出なし</span></div>
  <video id="video" autoplay playsinline muted style="display:none"></video>
  <div id="overlay"><button id="start-btn" onclick="startCamera()">📷 カメラ開始</button></div>
  <script>
    const video   = document.getElementById('video');
    const display = document.getElementById('display');
    const ctx     = display.getContext('2d');
    const statusEl  = document.getElementById('status');
    const latencyEl = document.getElementById('latency');
    const labelsEl  = document.getElementById('labels');
    const cap = document.createElement('canvas');
    const capCtx = cap.getContext('2d');

    let lastDetections = [];
    let frameId = 0;
    let sending = false;

    async function startCamera() {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        statusEl.textContent = '⚠️ HTTPS が必要です。https:// で開き直してください。';
        document.getElementById('overlay').style.display = 'none';
        return;
      }
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode:'environment', width:{ideal:640}, height:{ideal:480} }
        });
        video.srcObject = stream;
        document.getElementById('overlay').style.display = 'none';
        statusEl.textContent = '起動中...';
        video.onloadedmetadata = () => { renderLoop(); sendLoop(); };
      } catch (e) {
        statusEl.textContent = 'カメラエラー: ' + e.name + ' — ' + e.message;
      }
    }

    function renderLoop() {
      const vw = video.videoWidth, vh = video.videoHeight;
      if (!vw) { requestAnimationFrame(renderLoop); return; }

      // fit canvas to available space (letterbox)
      const maxW = display.parentElement.clientWidth;
      const maxH = display.clientHeight || window.innerHeight * 0.75;
      const scale = Math.min(maxW / vw, maxH / vh);
      display.width  = Math.round(vw * scale);
      display.height = Math.round(vh * scale);

      ctx.drawImage(video, 0, 0, display.width, display.height);

      const lw = Math.max(2, display.width / 250);
      const fs = Math.max(13, display.width / 28);
      ctx.lineWidth = lw;
      ctx.font = `bold ${fs}px sans-serif`;

      for (const d of lastDetections) {
        const x = d.bbox.x * display.width,  y = d.bbox.y * display.height;
        const w = d.bbox.w * display.width,   h = d.bbox.h * display.height;
        ctx.strokeStyle = '#00e676';
        ctx.strokeRect(x, y, w, h);
        const lh = fs + 6;
        const tw = ctx.measureText(d.label).width + 8;
        ctx.fillStyle = 'rgba(0,230,118,.9)';
        ctx.fillRect(x, y - lh, Math.max(w, tw), lh);
        ctx.fillStyle = '#000';
        ctx.fillText(d.label, x + 4, y - 5);
      }
      requestAnimationFrame(renderLoop);
    }

    async function sendLoop() {
      if (sending) { setTimeout(sendLoop, 100); return; }
      const vw = video.videoWidth, vh = video.videoHeight;
      if (!vw)    { setTimeout(sendLoop, 100); return; }

      cap.width = vw; cap.height = vh;
      capCtx.drawImage(video, 0, 0);
      cap.toBlob(async (blob) => {
        sending = true;
        const t0 = performance.now();
        const form = new FormData();
        form.append('image', blob, 'frame.jpg');
        form.append('frame_id', ++frameId);
        try {
          const res = await fetch('/detect', { method:'POST', body:form });
          const data = await res.json();
          const rtt = Math.round(performance.now() - t0);
          latencyEl.textContent  = `推論 ${data.elapsed_ms}ms / RTT ${rtt}ms`;
          statusEl.textContent   = `検出: ${data.detections.length}件`;
          lastDetections         = data.detections;
          labelsEl.innerHTML     = data.detections.length
            ? data.detections.map(d => `<span class="tag">${d.label}</span>`).join('')
            : '<span style="color:#444">検出なし</span>';
        } catch {
          statusEl.textContent = 'サーバ接続エラー';
          lastDetections = [];
        }
        sending = false;
        setTimeout(sendLoop, 150);
      }, 'image/jpeg', 0.7);
    }
  </script>
</body>
</html>"""


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
