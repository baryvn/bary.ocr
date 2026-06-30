"""
Custom exceptions for ORC Services.

Provides a hierarchy of exceptions for specific error scenarios
across all services.
"""

from __future__ import annotations


class ORCBaseError(Exception):
    """Base exception for all ORC service errors."""

    def __init__(self, message: str, job_id: str | None = None) -> None:
        self.job_id = job_id
        super().__init__(message)


# ─── Storage Errors ───────────────────────────────────────────────

class StorageError(ORCBaseError):
    """Base error for storage operations (MinIO)."""


class FileUploadError(StorageError):
    """Failed to upload file to MinIO."""


class FileDownloadError(StorageError):
    """Failed to download file from MinIO."""


class FileNotFoundError(StorageError):
    """File does not exist in MinIO."""


# ─── Queue Errors ─────────────────────────────────────────────────

class QueueError(ORCBaseError):
    """Base error for message queue operations."""


class QueueConnectionError(QueueError):
    """Failed to connect to RabbitMQ."""


class QueuePublishError(QueueError):
    """Failed to publish message to queue."""


class MessageParsingError(QueueError):
    """Failed to parse incoming message."""


# ─── Database Errors ──────────────────────────────────────────────

class DatabaseError(ORCBaseError):
    """Base error for database operations."""


class DatabaseConnectionError(DatabaseError):
    """Failed to connect to Oracle DB."""


class DatabaseWriteError(DatabaseError):
    """Failed to write data to Oracle DB."""


# ─── OCR Errors ───────────────────────────────────────────────────

class OCRError(ORCBaseError):
    """Base error for OCR processing."""


class PDFRenderError(OCRError):
    """Failed to render PDF pages to images."""


class PreprocessingError(OCRError):
    """Failed during image preprocessing (deskew, denoise)."""


class OCREngineError(OCRError):
    """OCR engine (PaddleOCR) failed to process image."""


class PDFBuildError(OCRError):
    """Failed to build 2-layer PDF output."""


# ─── Extraction Errors ────────────────────────────────────────────

class ExtractionError(ORCBaseError):
    """Base error for data extraction processing."""


class TextExtractionError(ExtractionError):
    """Failed to extract text from PDF."""


class LLMConnectionError(ExtractionError):
    """Failed to connect to LLM server (llama.cpp)."""


class LLMInferenceError(ExtractionError):
    """LLM inference failed or returned invalid response."""


class LLMTimeoutError(ExtractionError):
    """LLM inference timed out."""


class ResultParsingError(ExtractionError):
    """Failed to parse LLM response into structured result."""


# ─── Job Errors ───────────────────────────────────────────────────

class JobError(ORCBaseError):
    """Base error for job lifecycle."""


class JobNotFoundError(JobError):
    """Job ID does not exist."""


class JobAlreadyProcessingError(JobError):
    """Job is already being processed by another worker."""


class MaxRetriesExceededError(JobError):
    """Job has exceeded maximum retry attempts."""
