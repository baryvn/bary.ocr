"""
Structured logging configuration for ORC Services.

Uses structlog for structured, contextual logging.
Supports JSON format (production) and console format (development).
"""

from __future__ import annotations

import logging
import sys
from typing import Any

import structlog


def setup_logging(
    log_level: str = "INFO",
    log_format: str = "console",
    service_name: str = "orc-service",
) -> None:
    """
    Configure structured logging for the service.

    Args:
        log_level: Logging level (DEBUG, INFO, WARNING, ERROR, CRITICAL).
        log_format: Output format - "json" for production, "console" for dev.
        service_name: Name of the service, added to all log entries.
    """
    # Shared processors for both stdlib and structlog
    shared_processors: list[structlog.types.Processor] = [
        structlog.contextvars.merge_contextvars,
        structlog.stdlib.add_logger_name,
        structlog.stdlib.add_log_level,
        structlog.stdlib.PositionalArgumentsFormatter(),
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.StackInfoRenderer(),
        structlog.processors.UnicodeDecoder(),
    ]

    if log_format == "json":
        # Production: JSON output
        renderer = structlog.processors.JSONRenderer(ensure_ascii=False)
    else:
        # Development: colored console output
        renderer = structlog.dev.ConsoleRenderer(colors=True)

    structlog.configure(
        processors=[
            *shared_processors,
            structlog.stdlib.ProcessorFormatter.wrap_for_formatter,
        ],
        logger_factory=structlog.stdlib.LoggerFactory(),
        wrapper_class=structlog.stdlib.BoundLogger,
        cache_logger_on_first_use=True,
    )

    # Configure stdlib logging
    formatter = structlog.stdlib.ProcessorFormatter(
        processor=renderer,
        foreign_pre_chain=shared_processors,
    )

    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(formatter)

    root_logger = logging.getLogger()
    root_logger.handlers.clear()
    root_logger.addHandler(handler)
    root_logger.setLevel(getattr(logging, log_level.upper()))

    # Suppress noisy third-party loggers
    for noisy_logger in [
        "aio_pika",
        "aiormq",
        "urllib3",
        "minio",
        "oracledb",
    ]:
        logging.getLogger(noisy_logger).setLevel(logging.WARNING)


def get_logger(name: str, **initial_context: Any) -> structlog.stdlib.BoundLogger:
    """
    Get a structured logger with initial context.

    Args:
        name: Logger name (typically __name__).
        **initial_context: Key-value pairs added to every log entry.

    Returns:
        Bound structured logger.

    Example:
        logger = get_logger(__name__, service="ocr-worker")
        logger.info("Processing started", job_id="abc-123", pages=45)
    """
    logger = structlog.get_logger(name)
    if initial_context:
        logger = logger.bind(**initial_context)
    return logger
