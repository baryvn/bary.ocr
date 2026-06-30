"""
PaddleOCR-VL-1.6 Client Engine.

Queries the PaddleOCR-VL-1.6 llama.cpp server to perform high-quality OCR.
Parses normalized bounding boxes and constructs TextBox results.
"""

from __future__ import annotations

import asyncio
import base64
import re
import time
from dataclasses import dataclass
from typing import Any

import cv2
import httpx
import numpy as np

from orc_common.config import get_settings
from orc_common.exceptions import OCRError
from orc_common.logging import get_logger

logger = get_logger(__name__, component="ocr-engine")


@dataclass
class TextBox:
    """Bounding box and text for a single recognized word/line."""
    text: str
    box: tuple[float, float, float, float]  # (x0, y0, x1, y1)
    confidence: float


class OCRPageResult:
    """Structured result from OCR on a single page."""

    __slots__ = ("text", "page_number", "confidence", "boxes")

    def __init__(
        self,
        text: str,
        page_number: int,
        confidence: float = 1.0,
        boxes: list[TextBox] | None = None,
    ) -> None:
        self.text = text
        self.page_number = page_number
        self.confidence = confidence
        self.boxes = boxes or []

    @property
    def has_content(self) -> bool:
        return len(self.text.strip()) > 10


class OCREngine:
    """
    OCR engine client that communicates with PaddleOCR-VL-1.6 server.
    """

    def __init__(self) -> None:
        self._client: httpx.AsyncClient | None = None
        self._initialized = False
        self._settings = get_settings()
        self._base_url = self._settings.ocr_vl.SERVER_URL.rstrip("/")

    async def initialize(self) -> None:
        """Initialize HTTP client for ocr-vl-server."""
        if self._initialized:
            return

        logger.info("Initializing PaddleOCR-VL-1.6 client engine...", server_url=self._base_url)
        try:
            self._client = httpx.AsyncClient(
                base_url=self._base_url,
                timeout=httpx.Timeout(
                    connect=10.0,
                    read=float(self._settings.ocr_vl.TIMEOUT),
                    write=10.0,
                    pool=30.0,
                ),
                limits=httpx.Limits(
                    max_connections=10,
                    max_keepalive_connections=5,
                    keepalive_expiry=60.0,
                ),
                headers={"Content-Type": "application/json"},
            )
            self._initialized = True
            logger.info("PaddleOCR-VL-1.6 client engine initialized successfully")
        except Exception as e:
            logger.error("Failed to initialize OCREngine client", error=str(e))
            raise

    async def close(self) -> None:
        """Close HTTP client."""
        if self._client:
            await self._client.aclose()
            self._client = None
        self._initialized = False

    async def ocr_page(
        self,
        image: np.ndarray,
        page_number: int,
    ) -> OCRPageResult:
        """
        Run OCR on a single page image via PaddleOCR-VL-1.6 server.
        """
        if not self._initialized:
            await self.initialize()

        logger.debug("Starting OCR-VL inference via API", page=page_number)
        start_time = time.time()

        try:
            # 1. Image preprocessing for Spotting mode.
            #
            #    The processor renders pages at OCR_DPI (default 96 DPI) before
            #    passing to this function. An A4 page at 96 DPI is ~794x1123px —
            #    ideal for Spotting inference on CPU.
            #
            #    PaddleOCR-VL outputs bounding boxes normalized to [0, 1000] relative
            #    to the image sent, so image size does NOT affect bbox accuracy.
            orig_height, orig_width = image.shape[:2]
            proc_image = image

            long_edge = max(orig_height, orig_width)

            if long_edge < 400:
                # Very small pages: upscale 2x for better localization
                proc_image = cv2.resize(
                    image, (orig_width * 2, orig_height * 2),
                    interpolation=cv2.INTER_LANCZOS4,
                )
                logger.debug(
                    "Upscaled tiny image for Spotting mode",
                    page=page_number,
                    orig=f"{orig_width}x{orig_height}",
                    new=f"{proc_image.shape[1]}x{proc_image.shape[0]}",
                )
            elif long_edge > 1500:
                # Safety cap: downscale if somehow larger than expected
                # (e.g. if OCR_DPI is set higher than default)
                scale = 1500.0 / long_edge
                nw = int(orig_width * scale)
                nh = int(orig_height * scale)
                proc_image = cv2.resize(image, (nw, nh), interpolation=cv2.INTER_AREA)
                logger.debug(
                    "Capped oversized image for Spotting mode",
                    page=page_number,
                    orig=f"{orig_width}x{orig_height}",
                    new=f"{nw}x{nh}",
                )

            proc_height, proc_width = proc_image.shape[:2]

            # 2. Encode to base64
            success, buffer = cv2.imencode('.jpg', proc_image, [cv2.IMWRITE_JPEG_QUALITY, 92])
            if not success:
                raise OCRError(f"Failed to encode page {page_number} to JPEG")

            img_b64 = base64.b64encode(buffer).decode('utf-8')
            img_data_url = f"data:image/jpeg;base64,{img_b64}"

            # 3. Prepare payload for OpenAI-compatible completions API.
            #
            #    Investigation (2026-06-13): The "Spotting:" prompt was tested but the
            #    GGUF model cannot output grounding tags (<|object_ref_start|> etc.)
            #    because these special tokens were NOT preserved during GGUF conversion —
            #    they tokenize as multiple sub-tokens instead of single special tokens.
            #
            #    "OCR:" prompt is used instead: reliable, faster (~90-120s at 96 DPI),
            #    and produces equivalent text quality for Vietnamese documents.
            #    The PDF text layer uses full-page invisible text (searchable PDF).
            payload = {
                "model": "PaddleOCR-VL",
                "messages": [
                    {
                        "role": "user",
                        "content": [
                            {"type": "image_url", "image_url": {"url": img_data_url}},
                            {"type": "text", "text": "OCR:"}
                        ]
                    }
                ],
                "temperature": 0.0,
                "max_tokens": self._settings.ocr_vl.MAX_TOKENS,
            }

            # 4. Perform POST request
            response = await self._client.post(
                "/v1/chat/completions",
                json=payload,
            )

            if response.status_code != 200:
                error_body = response.text[:500]
                raise OCRError(f"ocr-vl-server returned error {response.status_code}: {error_body}")

            res_json = response.json()
            response_text = res_json['choices'][0]['message']['content']

            # 5. Parse bounding boxes: pass proc_width/proc_height (dims of the image
            #    actually sent to the model) so bbox scaling stays correct.
            boxes = self._parse_ocr_text(response_text, proc_width, proc_height)

            # Reconstruct the page full text
            full_text = "\n".join([box.text for box in boxes])

            duration = time.time() - start_time
            logger.info(
                "OCR page processed via OCR-VL",
                page=page_number,
                lines=len(boxes),
                duration_sec=round(duration, 2)
            )

            return OCRPageResult(
                text=full_text,
                page_number=page_number,
                confidence=1.0,
                boxes=boxes,
            )

        except Exception as e:
            logger.error("OCR-VL inference failed", page=page_number, error=str(e))
            raise

    def _parse_ocr_text(self, response_text: str, image_width: int, image_height: int) -> list[TextBox]:
        """
        Parses Qwen2-VL style grounding tags to extract text and bounding boxes.
        Coordinates are scaled from [0, 1000] back to image pixel dimensions.

        If the model does not return grounding tags (plain-text output), the full
        OCR text is returned as a single TextBox covering the whole page. This
        ensures the text is searchable/copyable even without precise positions.
        """
        boxes = []

        # Pattern 1: Standard Qwen2-VL grounding format:
        # <|object_ref_start|>text<|object_ref_end|><|box_start|>(y1,x1,y2,x2)<|box_end|>
        pattern_grounded = r"<\|?object_ref_start\|?>(.*?)<\|?object_ref_end\|?>\s*<\|?box_start\|?>\((\d+),(\d+),(\d+),(\d+)\)<\|?box_end\|?>"
        matches = re.findall(pattern_grounded, response_text, re.DOTALL)

        if matches:
            for text, y1_s, x1_s, y2_s, x2_s in matches:
                y1, x1, y2, x2 = float(y1_s), float(x1_s), float(y2_s), float(x2_s)

                # Convert normalized coordinates [0, 1000] back to original pixels
                x0 = (x1 / 1000.0) * image_width
                y0 = (y1 / 1000.0) * image_height
                x1_px = (x2 / 1000.0) * image_width
                y1_px = (y2 / 1000.0) * image_height

                boxes.append(TextBox(
                    text=text.strip(),
                    box=(x0, y0, x1_px, y1_px),
                    confidence=1.0
                ))
            return boxes

        # Pattern 2: Reverse format: <|box_start|>(y1,x1,y2,x2)<|box_end|>text
        pattern_reverse = r"<\|?box_start\|?>\((\d+),(\d+),(\d+),(\d+)\)<\|?box_end\|?>\s*<\|?object_ref_start\|?>(.*?)<\|?object_ref_end\|?>"
        matches = re.findall(pattern_reverse, response_text, re.DOTALL)
        if matches:
            for y1_s, x1_s, y2_s, x2_s, text in matches:
                y1, x1, y2, x2 = float(y1_s), float(x1_s), float(y2_s), float(x2_s)

                x0 = (x1 / 1000.0) * image_width
                y0 = (y1 / 1000.0) * image_height
                x1_px = (x2 / 1000.0) * image_width
                y1_px = (y2 / 1000.0) * image_height

                boxes.append(TextBox(
                    text=text.strip(),
                    box=(x0, y0, x1_px, y1_px),
                    confidence=1.0
                ))
            return boxes

        # Fallback: Model returned plain text without grounding coordinates.
        # Strip any residual tags and treat the full text as a single page-covering
        # TextBox. This preserves searchability/copy-paste without guessing positions.
        clean_text = re.sub(r"<\|?.*?\|?>", "", response_text)
        clean_text = clean_text.strip()
        if clean_text:
            # Single box covering the entire page — invisible text layer will
            # make the content searchable even though position is approximate.
            boxes.append(TextBox(
                text=clean_text,
                box=(0.0, 0.0, float(image_width), float(image_height)),
                confidence=0.8,  # Lower confidence to reflect no positional data
            ))
            logger.debug(
                "No grounding tags found — using full-page fallback TextBox",
                chars=len(clean_text),
            )

        return boxes

    async def ocr_crop(
        self,
        crop_image: np.ndarray,
        region_index: int = 0,
    ) -> str:
        """
        OCR a small cropped text region via PaddleOCR-VL server.

        Optimized for small images from text detection crops:
        - No resize needed (crops are already small, ~400×40 px)
        - Lower max_tokens (single line of text)
        - Shorter timeout
        - Returns plain text string (not OCRPageResult)

        Args:
            crop_image: Small BGR image crop containing a text region.
            region_index: Index for logging purposes.

        Returns:
            Recognized text string, or empty string on failure.
        """
        if not self._initialized:
            await self.initialize()

        try:
            h, w = crop_image.shape[:2]

            # Upscale very small crops for better recognition
            if max(h, w) < 32:
                scale = 64.0 / max(h, w)
                crop_image = cv2.resize(
                    crop_image,
                    (int(w * scale), int(h * scale)),
                    interpolation=cv2.INTER_LANCZOS4,
                )

            # Encode to JPEG
            success, buffer = cv2.imencode(
                '.jpg', crop_image, [cv2.IMWRITE_JPEG_QUALITY, 95]
            )
            if not success:
                logger.warning("Failed to encode crop", region=region_index)
                return ""

            img_b64 = base64.b64encode(buffer).decode('utf-8')
            img_data_url = f"data:image/jpeg;base64,{img_b64}"

            payload = {
                "model": "PaddleOCR-VL",
                "messages": [
                    {
                        "role": "user",
                        "content": [
                            {"type": "image_url", "image_url": {"url": img_data_url}},
                            {"type": "text", "text": "OCR:"}
                        ]
                    }
                ],
                "temperature": 0.0,
                "max_tokens": self._settings.ocr_vl.CROP_MAX_TOKENS,
            }

            response = await self._client.post(
                "/v1/chat/completions",
                json=payload,
                timeout=float(self._settings.ocr_vl.CROP_TIMEOUT),
            )

            if response.status_code != 200:
                logger.warning(
                    "VLM crop OCR failed",
                    region=region_index,
                    status=response.status_code,
                )
                return ""

            res_json = response.json()
            text = res_json['choices'][0]['message']['content'].strip()

            # Clean residual tags if any
            text = re.sub(r"<\|?.*?\|?>", "", text).strip()

            return text

        except Exception as e:
            logger.warning(
                "OCR crop failed",
                region=region_index,
                error=str(e),
            )
            return ""

    async def ocr_pages_batch(
        self,
        images: list[np.ndarray],
        start_page: int = 1,
    ) -> list[OCRPageResult]:
        """
        Run OCR on a batch of page images.
        """
        results = []
        for i, img in enumerate(images):
            results.append(await self.ocr_page(img, start_page + i))
        return results

    @property
    def is_initialized(self) -> bool:
        return self._initialized

    async def health_check(self) -> bool:
        """Perform health check on server."""
        if not self._initialized or not self._client:
            return False
        try:
            response = await self._client.get("/health", timeout=5.0)
            return response.status_code == 200
        except Exception:
            return False
