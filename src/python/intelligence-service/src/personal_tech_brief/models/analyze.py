"""Request/response models for ``POST /internal/v1/items/analyze``."""

from __future__ import annotations

from enum import StrEnum
from typing import Annotated
from uuid import UUID

from pydantic import Field

from personal_tech_brief.models.base import CamelModel

UnitInterval = Annotated[float, Field(ge=0.0, le=1.0)]


class InterestPriority(StrEnum):
    """Relative priority of a candidate interest."""

    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class ImpactLevel(StrEnum):
    """Coarse impact signal for an analyzed item."""

    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"
    NONE = "none"


class AnalyzeItem(CamelModel):
    """The single content item to analyze."""

    source_item_id: UUID
    title: Annotated[str, Field(min_length=1, max_length=512)]
    excerpt: Annotated[str, Field(max_length=4000)] | None = None
    content: Annotated[str, Field(max_length=20_000)] | None = None
    language: Annotated[str, Field(min_length=1, max_length=35)] | None = None


class CandidateInterest(CamelModel):
    """An interest the caller wants scored against the item."""

    interest_id: UUID
    name: Annotated[str, Field(min_length=1, max_length=100)]
    priority: InterestPriority


class AnalyzeRequest(CamelModel):
    """Analyze request body."""

    correlation_id: UUID
    item: AnalyzeItem
    candidate_interests: Annotated[list[CandidateInterest], Field(max_length=100)] = Field(
        default_factory=list
    )


class NormalizedFeatures(CamelModel):
    """Deterministically normalized surface features."""

    keywords: list[str]
    event_descriptors: list[str]


class InterestMatch(CamelModel):
    """A match strength for one supplied interest."""

    interest_id: UUID
    match_strength: UnitInterval


class ImpactSignal(CamelModel):
    """The impact classification and its confidence."""

    level: ImpactLevel
    confidence: UnitInterval


class AnalyzeResponse(CamelModel):
    """Analyze response body (HTTP 200)."""

    correlation_id: UUID
    source_item_id: UUID
    analyzer_version: str
    language: str | None = None
    normalized: NormalizedFeatures
    topics: list[str]
    interest_matches: list[InterestMatch]
    impact: ImpactSignal
