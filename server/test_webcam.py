"""Standalone server test using PC webcam.

Usage:
    # Terminal 1:  uvicorn main:app --port 8000
    # Terminal 2:  python test_webcam.py
"""
import time
import cv2
import requests

SERVER_URL = "http://127.0.0.1:8000/detect"
SEND_EVERY_N_FRAMES = 5
JPEG_QUALITY = 70

cap = cv2.VideoCapture(0)
assert cap.isOpened(), "Webcam not available"

frame_id = 0
last_detections = []

while True:
    ok, frame = cap.read()
    if not ok:
        break
    frame_id += 1
    h, w = frame.shape[:2]

    if frame_id % SEND_EVERY_N_FRAMES == 0:
        _, jpg = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, JPEG_QUALITY])
        try:
            t0 = time.time()
            res = requests.post(
                SERVER_URL,
                files={"image": ("frame.jpg", jpg.tobytes(), "image/jpeg")},
                data={"frame_id": frame_id},
                timeout=2.0,
            )
            rtt_ms = int((time.time() - t0) * 1000)
            if res.ok:
                data = res.json()
                last_detections = data["detections"]
                print(f"frame={frame_id} server={data['elapsed_ms']}ms "
                      f"rtt={rtt_ms}ms n={len(last_detections)}")
        except Exception as e:
            print(f"[err] {e}")

    for d in last_detections:
        bx = d["bbox"]
        x1 = int(bx["x"] * w)
        y1 = int(bx["y"] * h)
        x2 = int((bx["x"] + bx["w"]) * w)
        y2 = int((bx["y"] + bx["h"]) * h)
        tid = d.get("tracking_id")
        label = f"{d['label']} {d['confidence']:.2f}"
        if tid is not None:
            label += f" #{tid}"
        cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 230, 118), 2)
        cv2.putText(frame, label, (x1, max(0, y1 - 8)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 230, 118), 2)

    cv2.imshow("AR Word Chain - server test", frame)
    if cv2.waitKey(1) & 0xFF == ord("q"):
        break

cap.release()
cv2.destroyAllWindows()
