"""Analyze orchestration: content bounding, provider call, feature assembly."""

from __future__ import annotations

import time

from personal_tech_brief.models.analyze import (
    AnalyzeRequest,
    AnalyzeResponse,
    CandidateInterest,
)
from personal_tech_brief.providers.base import (
    ProviderError,
    ProviderTimeoutError,
    SemanticAnalysisProvider,
)
from personal_tech_brief.services.errors import SemanticValidationError
from personal_tech_brief.services.telemetry import record_provider_call
from personal_tech_brief.settings import Settings

ANALYZER_VERSION = "analyze-1"


def bound_content(request: AnalyzeRequest, *, char_budget: int) -> str:
    """Return the deterministically size-bounded text to send to the provider.

    Title, excerpt, and content are concatenated in a fixed order and truncated to
    ``char_budget`` characters. Truncation never raises; it silently drops the tail.
    """

    parts = [request.item.title]
    if request.item.excerpt is not None:
        parts.append(request.item.excerpt)
    if request.item.content is not None:
        parts.append(request.item.content)
    combined = "\n".join(parts)
    return combined[:char_budget]


def _validate_semantics(candidate_interests: list[CandidateInterest]) -> None:
    """Reject semantically unprocessable requests (HTTP 422)."""

    seen: set[str] = set()
    for interest in candidate_interests:
        key = str(interest.interest_id)
        if key in seen:
            raise SemanticValidationError("candidateInterests contains duplicate interestId values")
        seen.add(key)


async def analyze_item(
    request: AnalyzeRequest,
    *,
    provider: SemanticAnalysisProvider,
    settings: Settings,
) -> AnalyzeResponse:
    """Run analysis for one item and assemble the validated response."""

    _validate_semantics(request.candidate_interests)
    content = bound_content(request, char_budget=settings.analyze_content_char_budget)
    correlation_id = str(request.correlation_id)

    # The overall processor->Python request budget (spec §20, 30s) is enforced by the
    # calling .NET client's timeout, not duplicated here: a server-side umbrella smaller
    # than the per-attempt LLM timeout would make the per-attempt timeout and retry policy
    # unreachable. The provider bounds each attempt (llm_per_attempt_timeout_seconds) and
    # the retry policy bounds the number of attempts, so total server work is bounded.
    started = time.perf_counter()
    try:
        features = await provider.analyze(
            correlation_id=correlation_id,
            item=request.item,
            content=content,
            candidate_interests=request.candidate_interests,
        )
    except ProviderTimeoutError:
        _record(correlation_id, provider, "timeout", started)
        raise
    except ProviderError:
        _record(correlation_id, provider, "failure", started)
        raise
    _record(correlation_id, provider, "success", started)

    return AnalyzeResponse(
        correlation_id=request.correlation_id,
        source_item_id=request.item.source_item_id,
        analyzer_version=ANALYZER_VERSION,
        language=features.language,
        normalized=features.normalized,
        topics=features.topics,
        interest_matches=features.interest_matches,
        impact=features.impact,
    )


def _record(
    correlation_id: str, provider: SemanticAnalysisProvider, outcome: str, started: float
) -> None:
    """Record provider latency/outcome without any sensitive content."""

    latency_ms = (time.perf_counter() - started) * 1000.0
    record_provider_call(
        correlation_id=correlation_id,
        provider=type(provider).__name__,
        outcome=outcome,
        latency_ms=latency_ms,
    )
