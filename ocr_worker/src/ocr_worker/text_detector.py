"""
PP-OCRv4 text detection via RapidOCR (ONNX Runtime, CPU).

Provides line-level bounding boxes for text regions in document images.
Used in the hybrid pipeline: PP-OCRv4 detection → crop → OCR-VL recognition.

The detection model (DBNet++) is lightweight (~4MB) and runs in ~100ms on CPU.
"""

from __future__ import annotations

from dataclasses import dataclass

import cv2
import numpy as np

from orc_common.config import get_settings
from orc_common.logging import get_logger

logger = get_logger(__name__, component="text-detector")


@dataclass
class DetectedRegion:
    """A detected text region with polygon and axis-aligned bounding box."""

    polygon: list[list[float]]  # 4-point polygon [[x1,y1],[x2,y2],[x3,y3],[x4,y4]]
    bbox: tuple[float, float, float, float]  # Axis-aligned (x0, y0, x1, y1)
    score: float  # Detection confidence

    @property
    def width(self) -> float:
        return self.bbox[2] - self.bbox[0]

    @property
    def height(self) -> float:
        return self.bbox[3] - self.bbox[1]

    @property
    def area(self) -> float:
        return self.width * self.height


def _polygon_to_bbox(polygon: list[list[float]]) -> tuple[float, float, float, float]:
    """Convert 4-point polygon to axis-aligned bounding box (x0, y0, x1, y1)."""
    xs = [p[0] for p in polygon]
    ys = [p[1] for p in polygon]
    return (min(xs), min(ys), max(xs), max(ys))


def _sort_regions_reading_order(regions: list[DetectedRegion]) -> list[DetectedRegion]:
    """
    Sort detected regions in reading order: top-to-bottom, then left-to-right.

    Groups regions into "rows" by vertical overlap, then sorts each row
    left-to-right. This handles multi-column layouts correctly.
    """
    if not regions:
        return []

    # Sort by y-center first
    regions_sorted = sorted(regions, key=lambda r: (r.bbox[1] + r.bbox[3]) / 2)

    # Group into rows: regions whose vertical centers are within half-height
    rows: list[list[DetectedRegion]] = []
    current_row: list[DetectedRegion] = [regions_sorted[0]]
    current_y_center = (regions_sorted[0].bbox[1] + regions_sorted[0].bbox[3]) / 2

    for region in regions_sorted[1:]:
        y_center = (region.bbox[1] + region.bbox[3]) / 2
        row_height = max(r.height for r in current_row)

        if abs(y_center - current_y_center) < row_height * 0.5:
            # Same row
            current_row.append(region)
        else:
            # New row
            rows.append(current_row)
            current_row = [region]
            current_y_center = y_center

    rows.append(current_row)

    # Sort each row left-to-right, then flatten
    result: list[DetectedRegion] = []
    for row in rows:
        row.sort(key=lambda r: r.bbox[0])
        result.extend(row)

    return result


class TextDetector:
    """
    PP-OCRv4 text detection using RapidOCR (ONNX Runtime, CPU).

    Detects text line regions in document images and returns sorted
    bounding boxes in reading order.
    """

    def __init__(self) -> None:
        self._engine = None
        self._initialized = False
        self._settings = get_settings()

    def initialize(self) -> None:
        """Initialize RapidOCR detection engine."""
        if self._initialized:
            return

        try:
            from rapidocr_onnxruntime import RapidOCR

            # Initialize with detection only — no recognition or classification
            self._engine = RapidOCR(det_use_cuda=True)
            self._initialized = True
            logger.info("PP-OCRv4 text detector initialized (ONNX Runtime CUDA)")
        except ImportError:
            logger.error(
                "rapidocr-onnxruntime not installed. "
                "Install with: pip install rapidocr-onnxruntime"
            )
            raise
        except Exception as e:
            logger.error("Failed to initialize text detector", error=str(e))
            raise

    def detect(self, image: np.ndarray) -> list[DetectedRegion]:
        """
        Detect text regions in an image.

        Args:
            image: BGR image (numpy array from OpenCV).

        Returns:
            List of DetectedRegion sorted in reading order.
            Each region contains a 4-point polygon and axis-aligned bbox.
        """
        if not self._initialized:
            self.initialize()

        h, w = image.shape[:2]
        score_threshold = self._settings.ocr_vl.DET_SCORE_THRESHOLD
        min_box_size = self._settings.ocr_vl.DET_MIN_BOX_SIZE

        logger.debug("Running text detection", image_size=f"{w}x{h}")

        try:
            # RapidOCR returns: (result, elapsed_time)
            # result: list of [bbox, text, score] when rec is enabled
            # With det only: we use the internal detection
            # We run full pipeline but only care about bounding boxes
            result, elapsed = self._engine(
                image,
                use_det=True,
                use_cls=False,
                use_rec=False,  # Explicitly disable recognition, we only need detection
            )
        except Exception as e:
            logger.warning("Text detection failed, returning empty", error=str(e))
            return []

        if result is None:
            logger.debug("No text regions detected")
            return []

        regions: list[DetectedRegion] = []

        for item in result:
            # When use_rec=False, result is just a list of polygons: [[[x1,y1],[x2,y2],[x3,y3],[x4,y4]], ...]
            # When use_rec=True, it would be [[polygon, text, score], ...]
            # So item is directly the polygon points.
            polygon_points = item
            score = 1.0  # Detection score is not returned by RapidOCR when use_rec=False

            # Convert to list of [x, y] pairs
            polygon = [[float(p[0]), float(p[1])] for p in polygon_points]
            bbox = _polygon_to_bbox(polygon)

            # Filter by score
            if score < score_threshold:
                continue

            # Filter by minimum size
            box_w = bbox[2] - bbox[0]
            box_h = bbox[3] - bbox[1]
            if box_w < min_box_size or box_h < min_box_size:
                continue

            regions.append(DetectedRegion(
                polygon=polygon,
                bbox=bbox,
                score=score,
            ))

        # Sort in reading order
        regions = _sort_regions_reading_order(regions)

        det_time = elapsed if isinstance(elapsed, (int, float)) else 0
        logger.info(
            "Text detection completed",
            regions=len(regions),
            image_size=f"{w}x{h}",
            elapsed_ms=round(det_time * 1000 if det_time < 10 else det_time, 1),
        )

        return regions

    @property
    def is_initialized(self) -> bool:
        return self._initialized


def crop_region(
    image: np.ndarray,
    bbox: tuple[float, float, float, float],
    padding: int = 4,
) -> np.ndarray:
    """
    Crop a region from an image using axis-aligned bbox with padding.

    Args:
        image: Source image (BGR numpy array).
        bbox: (x0, y0, x1, y1) in pixel coordinates.
        padding: Extra pixels around the bbox (helps VLM recognition).

    Returns:
        Cropped image region as numpy array.
    """
    h, w = image.shape[:2]
    x0, y0, x1, y1 = bbox

    # Apply padding and clamp to image bounds
    x0 = max(0, int(x0) - padding)
    y0 = max(0, int(y0) - padding)
    x1 = min(w, int(x1) + padding)
    y1 = min(h, int(y1) + padding)

    crop = image[y0:y1, x0:x1]

    # Ensure crop is not empty
    if crop.size == 0:
        return image[max(0, int(bbox[1])):min(h, int(bbox[3]) + 1),
                      max(0, int(bbox[0])):min(w, int(bbox[2]) + 1)]

    return crop
