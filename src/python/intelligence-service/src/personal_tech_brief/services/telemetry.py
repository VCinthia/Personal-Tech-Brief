"""Correlation-scoped telemetry that never records sensitive content.

Every request opens a scope carrying its ``correlationId`` and endpoint. Provider
calls record latency and a coarse outcome only. Raw content, prompts, and
credentials are never logged.
"""

from __future__ import annotations

import logging
import time
from collections.abc import Iterator
from contextlib import contextmanager

logger = logging.getLogger("personal_tech_brief")


@contextmanager
def request_scope(correlation_id: str, endpoint: str) -> Iterator[None]:
    """Open a logging scope for one request, timing it end to end."""

    started = time.perf_counter()
    logger.info(
        "request.start",
        extra={"correlation_id": correlation_id, "endpoint": endpoint},
    )
    try:
        yield
    finally:
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        logger.info(
            "request.finish",
            extra={
                "correlation_id": correlation_id,
                "endpoint": endpoint,
                "elapsed_ms": round(elapsed_ms, 3),
            },
        )


def record_provider_call(
    *, correlation_id: str, provider: str, outcome: str, latency_ms: float
) -> None:
    """Record a provider call's latency and outcome without sensitive detail."""

    logger.info(
        "provider.call",
        extra={
            "correlation_id": correlation_id,
            "provider": provider,
            "outcome": outcome,
            "latency_ms": round(latency_ms, 3),
        },
    )
