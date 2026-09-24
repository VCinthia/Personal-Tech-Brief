"""Configurable async retry with exponential backoff and jitter.

Used by LLM-backed providers for transient failures. The policy (attempts, backoff,
jitter) is injected from :class:`~personal_tech_brief.settings.Settings`, never
hardcoded. The ``sleep`` and ``rng`` seams keep the loop deterministically testable
without a live provider or real wall-clock delays.
"""

from __future__ import annotations

import asyncio
import random
from collections.abc import Awaitable, Callable
from dataclasses import dataclass

from personal_tech_brief.providers.base import (
    ProviderTimeoutError,
    ProviderUnavailableError,
)

# Exceptions worth retrying: transient transport failures and per-attempt timeouts.
# A response that failed structured-output validation is not transient and is never
# retried (it is raised straight through by the caller).
_RETRYABLE: tuple[type[Exception], ...] = (ProviderUnavailableError, ProviderTimeoutError)


@dataclass(frozen=True, slots=True)
class RetryPolicy:
    """Bounded exponential-backoff-with-jitter retry policy."""

    max_retries: int
    backoff_base_seconds: float
    backoff_max_seconds: float
    jitter_seconds: float

    @property
    def max_attempts(self) -> int:
        """Total attempts, i.e. the initial call plus the configured retries."""

        return self.max_retries + 1


async def call_with_retries[T](
    operation: Callable[[], Awaitable[T]],
    *,
    policy: RetryPolicy,
    sleep: Callable[[float], Awaitable[None]] = asyncio.sleep,
    rng: random.Random | None = None,
) -> T:
    """Invoke ``operation`` with retries on transient provider errors.

    Retries only :data:`_RETRYABLE` exceptions, sleeping with exponential backoff
    plus bounded jitter between attempts. The last exception is re-raised once
    attempts are exhausted, preserving its type for error mapping.
    """

    jitter_source = rng if rng is not None else random.Random()
    last_error: Exception | None = None

    for attempt in range(policy.max_attempts):
        try:
            return await operation()
        except _RETRYABLE as error:
            last_error = error
            if attempt + 1 >= policy.max_attempts:
                break
            await sleep(_delay_for(attempt, policy, jitter_source))

    assert last_error is not None  # noqa: S101 - loop only exits here after a failure
    raise last_error


def _delay_for(attempt: int, policy: RetryPolicy, rng: random.Random) -> float:
    """Return the backoff delay for a zero-based attempt index."""

    base: float = policy.backoff_base_seconds * (2**attempt)
    capped: float = min(base, policy.backoff_max_seconds)
    jitter: float = 0.0
    if policy.jitter_seconds > 0.0:
        jitter = rng.uniform(0.0, policy.jitter_seconds)
    return capped + jitter
