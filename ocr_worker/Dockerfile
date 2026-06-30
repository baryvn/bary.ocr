FROM ghcr.io/ggml-org/llama.cpp:server-cuda AS base

# Switch to root to install dependencies
USER root

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
    software-properties-common wget supervisor \
    && apt-get update && apt-get install -y --no-install-recommends \
    python3.12 \
    python3.12-venv \
    python3.12-dev \
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender1 \
    libgomp1 \
    fonts-noto-core \
    fonts-dejavu-core \
    && wget https://bootstrap.pypa.io/get-pip.py \
    && python3.12 get-pip.py --break-system-packages \
    && rm -rf /var/lib/apt/lists/*

RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.12 1

# Create non-root user
RUN groupadd -r ocrworker && useradd -r -g ocrworker ocrworker

WORKDIR /app

# Llama server is already in /app/llama-server
RUN mkdir -p /app/llama_cpp_bin && mv /app/llama-server /app/llama_cpp_bin/ && mv /app/lib*.so* /app/llama_cpp_bin/ || true
ENV LD_LIBRARY_PATH=/app/llama_cpp_bin:$LD_LIBRARY_PATH

# Copy and install dependencies
COPY common/ /app/common/
COPY ocr_worker/ /app/ocr_worker/
RUN rm -rf /usr/lib/python3/dist-packages/jwt /usr/lib/python3/dist-packages/PyJWT* && \
    python -m pip install --break-system-packages --no-cache-dir /app/common && \
    python -m pip install --break-system-packages --no-cache-dir /app/ocr_worker && \
    python -m pip install --break-system-packages --no-cache-dir onnxruntime-gpu==1.19.2

# Copy model files directly to image
RUN mkdir -p /models
COPY models/PaddleOCR-VL-1.6-GGUF.gguf /models/PaddleOCR-VL-1.6-GGUF.gguf
COPY models/PaddleOCR-VL-1.6-GGUF-mmproj.gguf /models/PaddleOCR-VL-1.6-GGUF-mmproj.gguf

# Pre-download RapidOCR PP-OCRv4 detection models during build
RUN python -c "from rapidocr_onnxruntime import RapidOCR; RapidOCR(); print('RapidOCR models downloaded')"

# Make supervisor logs dir
RUN mkdir -p /var/log/supervisor && chown -R ocrworker:ocrworker /var/log/supervisor /app

# Switch to non-root user
USER ocrworker

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD python -c "import httpx; r = httpx.get('http://localhost:8091/health', timeout=5); exit(0 if r.status_code == 200 else 1)"

# Set environment
ENV PYTHONPATH=/app/common/src:/app/ocr_worker/src
ENV OCR_VL_MODEL_PATH=/models/PaddleOCR-VL-1.6-GGUF.gguf
ENV OCR_VL_MMPROJ_PATH=/models/PaddleOCR-VL-1.6-GGUF-mmproj.gguf
ENV OCR_VL_CTX_SIZE=4096
ENV OCR_VL_BATCH_SIZE=2048
ENV OCR_VL_UBATCH_SIZE=2048
ENV N_GPU_LAYERS=99
ENV OCR_VL_THREADS=12
ENV OCR_VL_PARALLEL=8

# Reset entrypoint from base image
ENTRYPOINT []

# Start supervisord
CMD ["/usr/bin/supervisord", "-n", "-c", "/app/ocr_worker/supervisord.conf"]
