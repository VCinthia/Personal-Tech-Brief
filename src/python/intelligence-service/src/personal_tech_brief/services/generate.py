"""Generate orchestration: content bounding, provider call, response assembly."""

from __future__ import annotations

import time
from collections.abc import Iterable

from personal_tech_brief.models.generate import (
    GenerateRequest,
    GenerateResponse,
    GenerateUpdate,
    MatchedInterest,
)
from personal_tech_brief.providers.base import (
    GenerationProvider,
    ProviderError,
    ProviderTimeoutError,
)
from personal_tech_brief.services.errors import SemanticValidationError
from personal_tech_brief.services.telemetry import record_provider_call
from personal_tech_brief.settings import Settings

GENERATION_VERSION = "generate-1"


def bound_update(update: GenerateUpdate, *, excerpt_char_budget: int) -> GenerateUpdate:
    """Return the update with each source excerpt truncated to the configured budget.

    Truncation is deterministic and never raises; it silently drops the tail so that
    total provider input is bounded regardless of caller input size (spec §20). Source
    ids, titles, urls, and timestamps are preserved for grounding and traceability.
    """

    bounded_sources = [
        source.model_copy(
            update={
                "excerpt": (
                    source.excerpt[:excerpt_char_budget] if source.excerpt is not None else None
                )
            }
        )
        for source in update.sources
    ]
    return update.model_copy(update={"sources": bounded_sources})


def _validate_semantics(update: GenerateUpdate, matched_interests: list[MatchedInterest]) -> None:
    """Reject semantically unprocessable requests (HTTP 422).

    Duplicate ``sourceItemId`` values in the sources, or duplicate ``interestId``
    values in the matched interests, are syntactically valid but not processable.
    """

    _reject_duplicate_ids(
        (str(source.source_item_id) for source in update.sources),
        message="update.sources contains duplicate sourceItemId values",
    )
    _reject_duplicate_ids(
        (str(interest.interest_id) for interest in matched_interests),
        message="matchedInterests contains duplicate interestId values",
    )


def _reject_duplicate_ids(ids: Iterable[str], *, message: str) -> None:
    seen: set[str] = set()
    for identifier in ids:
        if identifier in seen:
            raise SemanticValidationError(message)
        seen.add(identifier)


async def generate_update(
    request: GenerateRequest,
    *,
    provider: GenerationProvider,
    settings: Settings,
) -> GenerateResponse:
    """Run generation for one selected update and assemble the validated response."""

    _validate_semantics(request.update, request.matched_interests)
    bounded = bound_update(
        request.update,
        excerpt_char_budget=settings.generate_source_excerpt_char_budget,
    )
    correlation_id = str(request.correlation_id)

    # The overall processor->Python request budget (spec §20) is enforced by the
    # calling .NET client's timeout, not duplicated here: a server-side umbrella smaller
    # than the per-attempt LLM timeout would make the per-attempt timeout and retry
    # policy unreachable. The provider bounds each attempt
    # (generate_llm_per_attempt_timeout_seconds) and the retry policy bounds the number
    # of attempts, so total server work is bounded.
    started = time.perf_counter()
    try:
        content = await provider.generate(
            correlation_id=correlation_id,
            update=bounded,
            matched_interests=request.matched_interests,
        )
    except ProviderTimeoutError:
        _record(correlation_id, provider, "timeout", started)
        raise
    except ProviderError:
        _record(correlation_id, provider, "failure", started)
        raise
    _record(correlation_id, provider, "success", started)

    return GenerateResponse(
        correlation_id=request.correlation_id,
        technology_update_id=request.update.technology_update_id,
        generation_version=GENERATION_VERSION,
        title=content.title,
        summary=content.summary,
        why_relevant=content.why_relevant,
    )


def _record(
    correlation_id: str, provider: GenerationProvider, outcome: str, started: float
) -> None:
    """Record provider latency/outcome without any sensitive content."""

    latency_ms = (time.perf_counter() - started) * 1000.0
    record_provider_call(
        correlation_id=correlation_id,
        provider=type(provider).__name__,
        outcome=outcome,
        latency_ms=latency_ms,
    )


__all__ = ["GENERATION_VERSION", "bound_update", "generate_update"]
