# e:/ecoit/sohoax10/orcservices/ocr_worker/src/ocr_worker/api.py
import os
os.environ["OPENCV_IO_MAX_IMAGE_PIXELS"] = str(pow(2,40))
import asyncio
import cv2
import numpy as np
from fastapi import FastAPI, File, UploadFile
from pydantic import BaseModel
from contextlib import asynccontextmanager

from orc_common.logging import get_logger, setup_logging
from orc_common.config import get_settings

from ocr_worker.ocr_engine import OCREngine
from ocr_worker.text_detector import TextDetector, crop_region

logger = get_logger(__name__, service="ocr-api")

# Global instances
detector: TextDetector = None
engine: OCREngine = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    global detector, engine
    settings = get_settings()
    setup_logging(log_level=settings.LOG_LEVEL, log_format=settings.LOG_FORMAT, service_name="ocr-api")
    
    logger.info("Initializing TextDetector (PP-OCRv4)...")
    detector = TextDetector()
    detector.initialize()
    
    logger.info("Initializing OCREngine (PaddleOCR-VL via llama.cpp)...")
    engine = OCREngine()
    await engine.initialize()
    
    yield
    
    # Shutdown
    if engine:
        await engine.close()

app = FastAPI(title="OCR-VL API", lifespan=lifespan)

@app.get("/health")
async def health_check():
    return {"status": "ok"}

class TextBoxResponse(BaseModel):
    text: str
    box: list[float]  # [x0, y0, x1, y1]
    confidence: float


def _decode_image(contents: bytes) -> np.ndarray:
    """Decode uploaded image bytes thành numpy array BGR."""
    nparr = np.frombuffer(contents, np.uint8)
    image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    return image


def _map_text_to_regions(
    text: str,
    regions: list,
    image_w: int,
    image_h: int,
) -> list[TextBoxResponse]:
    """
    Map các dòng text từ response "OCR:" của model vào các bounding box 
    từ PaddleOCR detection theo thứ tự đọc (reading order).

    Nếu số dòng text nhiều hơn số bbox: các dòng thừa được bỏ qua.
    Nếu số bbox nhiều hơn số dòng text: các bbox thừa nhận text rỗng.
    Fallback: nếu không có regions, trả về 1 TextBox toàn trang.
    """
    lines = [l.strip() for l in text.splitlines() if l.strip()]

    if not regions:
        # Không detect được vùng nào → trả toàn bộ text làm 1 box toàn trang
        return [TextBoxResponse(
            text=text.strip(),
            box=[0.0, 0.0, float(image_w), float(image_h)],
            confidence=0.8,
        )]

    results = []
    for i, region in enumerate(regions):
        line_text = lines[i] if i < len(lines) else ""
        if not line_text:
            continue
        x0, y0, x1, y1 = region.bbox
        results.append(TextBoxResponse(
            text=line_text,
            box=[float(x0), float(y0), float(x1), float(y1)],
            confidence=float(region.score),
        ))
    return results


@app.post("/ocr_page", response_model=list[TextBoxResponse])
async def process_ocr_page(file: UploadFile = File(...)):
    """
    Xử lý OCR bằng cách Detect vùng chữ, sau đó Crop và gửi concurrent tới LLM.
    Target: Không nhồi ảnh to vào LLM để tránh quá tải VRAM và giảm thời gian.
    """
    contents = await file.read()
    image = _decode_image(contents)

    if image is None:
        logger.warning("Không thể decode ảnh upload")
        return []

    h, w = image.shape[:2]
    logger.info(f"Nhận ảnh {w}x{h} px. Bắt đầu OCR page bằng Crop Mode...")

    # 1. PaddleOCR Detection → bounding boxes (~200ms)
    regions = detector.detect(image)
    if not regions:
        logger.info("No regions detected, falling back to full-page OCR")
        result = await engine.ocr_page(image, 1)
        return [
            TextBoxResponse(
                text=b.text,
                box=list(b.box),
                confidence=b.confidence
            ) for b in result.boxes
        ]

    logger.info(f"Detected {len(regions)} regions. Starting concurrent crop OCR...")

    # 2. Crop and OCR concurrently
    concurrency_limit = int(os.getenv("OCR_VL_PARALLEL", "8"))
    semaphore = asyncio.Semaphore(concurrency_limit)

    async def _process_crop(i: int, region) -> TextBoxResponse | None:
        async with semaphore:
            crop = crop_region(image, region.bbox, padding=4)
            text = await engine.ocr_crop(crop, region_index=i)
            if text:
                return TextBoxResponse(
                    text=text.strip(),
                    box=list(region.bbox),
                    confidence=float(region.score)
                )
            return None

    tasks = [_process_crop(i, region) for i, region in enumerate(regions)]
    results = await asyncio.gather(*tasks)
    
    # Filter out empty results
    final_boxes = [res for res in results if res is not None and res.text]
    
    logger.info(f"Successfully recognized {len(final_boxes)}/{len(regions)} regions.")
    return final_boxes


@app.post("/ocr_image", response_model=list[TextBoxResponse])
async def process_ocr_image(file: UploadFile = File(...)):
    """
    Endpoint cũ: detect → crop từng vùng → gọi LLM per-crop.
    Giữ lại để backward compatible, nhưng /ocr_page nhanh hơn nhiều.
    """
    contents = await file.read()
    nparr = np.frombuffer(contents, np.uint8)
    image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    
    if image is None:
        return []

    # 1. Detect text regions
    regions = detector.detect(image)
    if not regions:
        logger.info("No regions detected, falling back to full-page OCR")
        result = await engine.ocr_page(image, 1)
        return [
            TextBoxResponse(
                text=b.text,
                box=list(b.box),
                confidence=b.confidence
            ) for b in result.boxes
        ]

    logger.info(f"Detected {len(regions)} regions. Starting crop OCR...")

    # 2. Crop and OCR concurrently
    concurrency_limit = getattr(get_settings().ocr_vl, 'CROP_CONCURRENCY', 4)
    semaphore = asyncio.Semaphore(concurrency_limit)

    async def _process_crop(i: int, region) -> TextBoxResponse | None:
        async with semaphore:
            crop = crop_region(image, region.bbox, padding=4)
            text = await engine.ocr_crop(crop, region_index=i)
            if text:
                return TextBoxResponse(
                    text=text,
                    box=list(region.bbox),
                    confidence=float(region.score)
                )
            return None

    tasks = [_process_crop(i, region) for i, region in enumerate(regions)]
    results = await asyncio.gather(*tasks)
    
    # Filter out empty results
    final_boxes = [res for res in results if res is not None]
    
    logger.info(f"Successfully recognized {len(final_boxes)}/{len(regions)} regions.")
    return final_boxes

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("api:app", host="0.0.0.0", port=8091, reload=False)
