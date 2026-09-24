"""Provider abstractions for LLM-backed capabilities (ADR-006).

Two provider protocols share one exception hierarchy and one abstraction shape:
:class:`SemanticAnalysisProvider` yields validated structured features for the
analyze endpoint, and :class:`GenerationProvider` yields validated structured
content for the generate endpoint. Implementations are interchangeable behind
these protocols; the service maps the exceptions defined here onto RFC 9457 problem
responses without leaking provider, credential, prompt, or raw-content detail.
"""

from __future__ import annotations

from typing import Annotated, Protocol, runtime_checkable

from pydantic import Field

from personal_tech_brief.models.analyze import (
    AnalyzeItem,
    CandidateInterest,
    ImpactSignal,
    InterestMatch,
    NormalizedFeatures,
)
from personal_tech_brief.models.base import CamelModel
from personal_tech_brief.models.generate import GenerateUpdate, MatchedInterest


class SemanticFeatures(CamelModel):
    """Structured semantic-analysis output validated before leaving the service.

    This is the provider-facing contract: every provider (deterministic or LLM
    backed) must return exactly these validated fields. Reusing the analyze
    sub-models enforces the ``[0, 1]`` bounds and enum domains uniformly.
    """

    language: str | None = None
    normalized: NormalizedFeatures
    topics: list[str]
    interest_matches: list[InterestMatch]
    impact: ImpactSignal


class ProviderError(Exception):
    """Base class for recoverable semantic-analysis provider failures."""


class ProviderTimeoutError(ProviderError):
    """The provider did not respond within the configured per-attempt timeout."""


class ProviderUnavailableError(ProviderError):
    """The provider could not be reached (connection/transport failure)."""


class ProviderFailureError(ProviderError):
    """The provider returned an error response or otherwise failed a call."""


class ProviderResponseInvalidError(ProviderFailureError):
    """The provider responded but its structured output failed validation."""


@runtime_checkable
class SemanticAnalysisProvider(Protocol):
    """Yields validated structured features for a single content item."""

    async def analyze(
        self,
        *,
        correlation_id: str,
        item: AnalyzeItem,
        content: str,
        candidate_interests: list[CandidateInterest],
    ) -> SemanticFeatures:
        """Return structured semantic features for ``item``.

        ``content`` is the already content-size-bounded text to analyze. Providers
        must only produce interest matches for ids present in ``candidate_interests``
        and must return values within the bounds encoded on :class:`SemanticFeatures`.
        """
        ...


class GeneratedContent(CamelModel):
    """Structured generation output validated before leaving the service.

    This is the provider-facing contract for generate: every provider (deterministic
    or LLM backed) must return exactly these validated fields. The bounds mirror the
    ``GenerateResponse`` contract (concise title, non-empty source-grounded summary
    and why-relevant explanation) and are enforced uniformly.
    """

    title: Annotated[str, Field(min_length=1, max_length=200)]
    summary: Annotated[str, Field(min_length=1)]
    why_relevant: Annotated[str, Field(min_length=1)]


@runtime_checkable
class GenerationProvider(Protocol):
    """Yields validated structured content for one selected technology update."""

    async def generate(
        self,
        *,
        correlation_id: str,
        update: GenerateUpdate,
        matched_interests: list[MatchedInterest],
    ) -> GeneratedContent:
        """Return structured content for ``update``.

        ``update`` carries the already content-size-bounded source text. Providers
        must ground their output only in the supplied sources and matched interests,
        never inventing facts beyond them, and must return values within the bounds
        encoded on :class:`GeneratedContent`.
        """
        ...
