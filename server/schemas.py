"""API request/response schemas."""
from typing import Optional
from pydantic import BaseModel


class BBox(BaseModel):
    """正規化座標 (0-1)、原点 左上。"""
    x: float
    y: float
    w: float
    h: float


class Detection(BaseModel):
    tracking_id: Optional[int] = None
    label: str
    confidence: float
    bbox: BBox


class ImageSize(BaseModel):
    w: int
    h: int


class DetectResponse(BaseModel):
    frame_id: int
    elapsed_ms: int
    image_size: ImageSize
    detections: list[Detection]
