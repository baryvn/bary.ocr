#!/bin/bash
# e:\ecoit\sohoax10\orcservices\llm_server\entrypoint.sh
set -euo pipefail

# ==============================================================================
# LLM Server Entrypoint - llama.cpp server (Optimized for Tesla T4 16GB)
# ==============================================================================

# Configuration (defaults tuned for Tesla T4 16GB VRAM)
MODEL_PATH="${MODEL_PATH:-/models/gemma-4-E2B_q4_0-it.gguf}"
MMPROJ_PATH="${MMPROJ_PATH:-/models/gemma-4-E2B-it-mmproj.gguf}"
CTX_SIZE="${CTX_SIZE:-8192}"
N_GPU_LAYERS="${N_GPU_LAYERS:-99}"
CPU_THREADS="${CPU_THREADS:-8}"
BATCH_SIZE="${BATCH_SIZE:-512}"
UBATCH_SIZE="${UBATCH_SIZE:-256}"
# T4 16GB: tăng lên 4 parallel slots
PARALLEL_SLOTS="${PARALLEL_SLOTS:-4}"
PORT="${PORT:-8080}"

# Đảm bảo dùng GPU đầu tiên
export CUDA_VISIBLE_DEVICES="${CUDA_VISIBLE_DEVICES:-0}"

echo "=============================================="
echo " LLM Server Starting (Tesla T4 Optimized)"
echo "=============================================="
echo " Model:         ${MODEL_PATH}"
echo " Context:       ${CTX_SIZE}"
echo " GPU Layers:    ${N_GPU_LAYERS}"
echo " Threads:       ${CPU_THREADS}"
echo " Batch:         ${BATCH_SIZE}"
echo " UBatch:        ${UBATCH_SIZE}"
echo " Parallel:      ${PARALLEL_SLOTS}"
echo " Port:          ${PORT}"
echo " CUDA Devices:  ${CUDA_VISIBLE_DEVICES}"
echo "=============================================="

# Log GPU info nếu có nvidia-smi
if command -v nvidia-smi &>/dev/null; then
    echo "=== GPU Info ==="
    nvidia-smi --query-gpu=name,memory.total,driver_version,compute_cap \
               --format=csv,noheader 2>/dev/null || true
    echo "================"
fi

# Validate model file exists
if [ ! -f "${MODEL_PATH}" ]; then
    echo "ERROR: Model file not found at ${MODEL_PATH}"
    exit 1
fi

# Build command
CMD=(/app/llama-server \
    --model "${MODEL_PATH}" \
    --ctx-size "${CTX_SIZE}" \
    --n-gpu-layers "${N_GPU_LAYERS}" \
    --threads "${CPU_THREADS}" \
    --batch-size "${BATCH_SIZE}" \
    --ubatch-size "${UBATCH_SIZE}" \
    --parallel "${PARALLEL_SLOTS}" \
    --cont-batching \
    --flash-attn on \
    --port "${PORT}" \
    --host 0.0.0.0)

if [ -n "${MMPROJ_PATH:-}" ] && [ -f "${MMPROJ_PATH}" ]; then
    echo " MMPROJ:        ${MMPROJ_PATH}"
    CMD+=(--mmproj "${MMPROJ_PATH}")
fi

# Start llama-server
exec "${CMD[@]}" "$@"
