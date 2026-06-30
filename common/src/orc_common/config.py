"""
Configuration module for ORC Services.

Uses Pydantic Settings to load configuration from environment variables.
Supports .env files for local development.
"""

from __future__ import annotations

from functools import lru_cache
from typing import Literal

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class RabbitMQSettings(BaseSettings):
    """RabbitMQ connection settings."""

    model_config = SettingsConfigDict(env_prefix="RABBITMQ_")

    HOST: str = "localhost"
    PORT: int = 5672
    USER: str = "guest"
    PASSWORD: str = "guest"
    VHOST: str = "/"
    HEARTBEAT: int = 600
    CONNECTION_TIMEOUT: int = 30

    @property
    def url(self) -> str:
        """Build AMQP connection URL."""
        return (
            f"amqp://{self.USER}:{self.PASSWORD}"
            f"@{self.HOST}:{self.PORT}/{self.VHOST}"
            f"?heartbeat={self.HEARTBEAT}"
            f"&connection_timeout={self.CONNECTION_TIMEOUT}"
        )


class MinIOSettings(BaseSettings):
    """MinIO connection settings."""

    model_config = SettingsConfigDict(env_prefix="MINIO_")

    ENDPOINT: str = "localhost:9000"
    ACCESS_KEY: str = "minioadmin"
    SECRET_KEY: str = "minioadmin"
    BUCKET: str = "ocr-documents"
    SECURE: bool = False
    REGION: str | None = None


class RedisSettings(BaseSettings):
    """Redis connection settings."""

    model_config = SettingsConfigDict(env_prefix="REDIS_")

    URL: str = "redis://localhost:6379/0"
    JOB_STATUS_TTL: int = 86400  # 24 hours in seconds
    MAX_CONNECTIONS: int = 20


class PostgresSettings(BaseSettings):
    """PostgreSQL connection settings."""

    model_config = SettingsConfigDict(env_prefix="POSTGRES_")

    HOST: str = "localhost"
    PORT: int = 5432
    DATABASE: str = "orc_archive"
    USER: str = "orc_user"
    PASSWORD: str = "orc_password"
    MIN_POOL_SIZE: int = 2
    MAX_POOL_SIZE: int = 10


class QueueSettings(BaseSettings):
    """RabbitMQ queue name settings."""

    model_config = SettingsConfigDict(env_prefix="QUEUE_")

    OCR_REQUEST: str = "ocr_request_queue"
    OCR_RESULT: str = "ocr_result_queue"
    EXTRACTION_REQUEST: str = "extraction_request_queue"
    EXTRACTION_RESULT: str = "extraction_result_queue"
    STATUS: str = "status_queue"
    DEAD_LETTER_EXCHANGE: str = "dlx_exchange"
    DEAD_LETTER_QUEUE: str = "dead_letter_queue"


class LLMSettings(BaseSettings):
    """LLM server (llama.cpp) settings."""

    model_config = SettingsConfigDict(env_prefix="LLM_")

    SERVER_URL: str = "http://localhost:8080"
    TIMEOUT: int = 300  # seconds
    MAX_RETRIES: int = 3
    TEMPERATURE: float = 0.1
    TOP_P: float = 0.9
    REPEAT_PENALTY: float = 1.1
    MAX_TOKENS: int = 2048
    CONTEXT_SIZE: int = 8192


class WorkerSettings(BaseSettings):
    """Worker-specific settings."""

    model_config = SettingsConfigDict(env_prefix="WORKER_")

    PREFETCH_COUNT: int = 1
    MAX_RETRIES: int = 3
    RETRY_DELAY_BASE: float = 5.0  # seconds, exponential backoff
    HEALTH_CHECK_PORT: int = 8081
    GRACEFUL_SHUTDOWN_TIMEOUT: int = 30  # seconds


class OCRVLSettings(BaseSettings):
    """PaddleOCR-VL-1.6 server settings (via llama.cpp)."""

    model_config = SettingsConfigDict(env_prefix="OCR_VL_")

    SERVER_URL: str = "http://localhost:8090"
    TIMEOUT: int = 300  # seconds per page (Spotting mode is slower than plain OCR)
    MAX_RETRIES: int = 3
    MAX_TOKENS: int = 8192  # Spotting mode can output many bbox tokens
    RENDER_DPI: int = 300   # DPI for PDF visual layer (high quality image)
    OCR_DPI: int = 96       # DPI for OCR inference image (lower = faster, ~794x1123 for A4)
    PAGE_CHUNK_SIZE: int = 10  # pages per processing chunk
    MAX_IMAGE_PIXELS: int = 3000  # legacy: max image dimension sent to VLM (unused by spotting path)

    # --- Hybrid detection pipeline (PP-OCRv4 + VL Recognition) ---
    DET_ENABLED: bool = True          # Enable hybrid detection → per-crop VLM pipeline
    DET_SCORE_THRESHOLD: float = 0.5  # Detection confidence threshold
    DET_MIN_BOX_SIZE: int = 10        # Minimum box dimension (pixels) to keep
    CROP_TIMEOUT: int = 60            # Timeout per crop VLM inference (seconds)
    CROP_MAX_TOKENS: int = 512        # Max output tokens per crop
    CROP_CONCURRENCY: int = 4         # Max concurrent API requests to VLM per worker


class Settings(BaseSettings):
    """
    Root configuration aggregating all sub-settings.

    Load order: .env file → environment variables → defaults
    """

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=True,
        extra="ignore",
    )

    # Environment
    ENV: Literal["dev", "staging", "production"] = "dev"
    SERVICE_NAME: str = "orc-service"
    LOG_LEVEL: str = "INFO"
    LOG_FORMAT: Literal["json", "console"] = "console"

    # Sub-settings (loaded separately from their own env prefixes)
    rabbitmq: RabbitMQSettings = Field(default_factory=RabbitMQSettings)
    minio: MinIOSettings = Field(default_factory=MinIOSettings)
    redis: RedisSettings = Field(default_factory=RedisSettings)
    postgres: PostgresSettings = Field(default_factory=PostgresSettings)
    queues: QueueSettings = Field(default_factory=QueueSettings)
    llm: LLMSettings = Field(default_factory=LLMSettings)
    worker: WorkerSettings = Field(default_factory=WorkerSettings)
    ocr_vl: OCRVLSettings = Field(default_factory=OCRVLSettings)

    @field_validator("LOG_LEVEL")
    @classmethod
    def validate_log_level(cls, v: str) -> str:
        valid_levels = {"DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"}
        upper = v.upper()
        if upper not in valid_levels:
            raise ValueError(f"Invalid log level: {v}. Must be one of {valid_levels}")
        return upper


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """
    Get cached application settings.

    Returns a singleton Settings instance. Uses lru_cache to avoid
    re-reading environment variables on every call.
    """
    return Settings()
