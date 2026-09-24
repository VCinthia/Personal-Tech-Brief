"""Unit tests for the configurable async retry helper (no live provider)."""

from __future__ import annotations

import random

import pytest

from personal_tech_brief.providers.base import (
    ProviderResponseInvalidError,
    ProviderUnavailableError,
)
from personal_tech_brief.providers.retry import RetryPolicy, call_with_retries


def _policy(max_retries: int) -> RetryPolicy:
    return RetryPolicy(
        max_retries=max_retries,
        backoff_base_seconds=0.5,
        backoff_max_seconds=8.0,
        jitter_seconds=0.0,
    )


@pytest.fixture
def anyio_backend() -> str:
    return "asyncio"


@pytest.mark.anyio
async def test_returns_result_without_retrying_on_success() -> None:
    sleeps: list[float] = []

    async def sleep(delay: float) -> None:
        sleeps.append(delay)

    async def op() -> str:
        return "ok"

    result = await call_with_retries(op, policy=_policy(3), sleep=sleep)

    assert result == "ok"
    assert sleeps == []


@pytest.mark.anyio
async def test_retries_transient_then_succeeds() -> None:
    sleeps: list[float] = []
    attempts = {"count": 0}

    async def sleep(delay: float) -> None:
        sleeps.append(delay)

    async def op() -> str:
        attempts["count"] += 1
        if attempts["count"] < 3:
            raise ProviderUnavailableError("transient")
        return "recovered"

    result = await call_with_retries(op, policy=_policy(3), sleep=sleep, rng=random.Random(0))

    assert result == "recovered"
    assert attempts["count"] == 3
    # Two failures before success ⇒ two backoff sleeps with exponential growth.
    assert sleeps == [0.5, 1.0]


@pytest.mark.anyio
async def test_exhausts_retries_and_reraises_last_error() -> None:
    sleeps: list[float] = []

    async def sleep(delay: float) -> None:
        sleeps.append(delay)

    async def op() -> str:
        raise ProviderUnavailableError("still down")

    with pytest.raises(ProviderUnavailableError):
        await call_with_retries(op, policy=_policy(2), sleep=sleep)

    # 3 attempts total ⇒ 2 sleeps between them.
    assert len(sleeps) == 2


@pytest.mark.anyio
async def test_non_transient_error_is_not_retried() -> None:
    attempts = {"count": 0}

    async def sleep(delay: float) -> None:  # pragma: no cover - must not be called
        raise AssertionError("sleep should not be called")

    async def op() -> str:
        attempts["count"] += 1
        raise ProviderResponseInvalidError("bad output")

    with pytest.raises(ProviderResponseInvalidError):
        await call_with_retries(op, policy=_policy(3), sleep=sleep)

    assert attempts["count"] == 1


@pytest.mark.anyio
async def test_backoff_is_capped_and_jitter_is_bounded() -> None:
    sleeps: list[float] = []

    async def sleep(delay: float) -> None:
        sleeps.append(delay)

    async def op() -> str:
        raise ProviderUnavailableError("down")

    policy = RetryPolicy(
        max_retries=5,
        backoff_base_seconds=1.0,
        backoff_max_seconds=4.0,
        jitter_seconds=0.5,
    )

    with pytest.raises(ProviderUnavailableError):
        await call_with_retries(op, policy=policy, sleep=sleep, rng=random.Random(1))

    # Base doubles (1,2,4,8,16) then caps at 4; jitter adds at most 0.5.
    assert len(sleeps) == 5
    assert sleeps[0] >= 1.0
    assert all(delay <= 4.5 for delay in sleeps)
    assert sleeps[-1] <= 4.5
