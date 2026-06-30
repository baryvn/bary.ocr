# e:\ecoit\sohoax10\orcservices\llm_server\Dockerfile
FROM ghcr.io/ggml-org/llama.cpp:server-cuda

# Create model directory
RUN mkdir -p /models

# Copy model files directly to image
COPY models/gemma-4-E2B_q4_0-it.gguf /models/gemma-4-E2B_q4_0-it.gguf
COPY models/gemma-4-E2B-it-mmproj.gguf /models/gemma-4-E2B-it-mmproj.gguf

# Invalidate cache for entrypoint copy
ARG CACHE_BUST=1

ENV CTX_SIZE=8192

# Copy entrypoint script
COPY llm_server/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
