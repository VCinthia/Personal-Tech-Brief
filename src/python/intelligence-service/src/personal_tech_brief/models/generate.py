"""Request/response models for ``POST /internal/v1/updates/generate`` (Slice 6).

The generate endpoint is the expensive final-generation step: called by .NET only
for a candidate already selected for a brief, it produces a concise title, a
source-grounded summary, and a why-relevant explanation as validated structured
output. Field bounds mirror the frozen contract in
``docs/architecture/intelligence-api.md`` and the canonical fixtures in
``docs/contracts/intelligence/``.
"""

from __future__ import annotations

from datetime import datetime
from typing import Annotated
from uuid import UUID

from pydantic import Field

from personal_tech_brief.models.analyze import InterestPriority
from personal_tech_brief.models.base import CamelModel


class GenerateSource(CamelModel):
    """One source item backing the update to be generated."""

    source_item_id: UUID
    title: Annotated[str, Field(min_length=1, max_length=512)]
    excerpt: Annotated[str, Field(max_length=4000)] | None = None
    source_url: str | None = None
    published_at_utc: datetime | None = None


class GenerateUpdate(CamelModel):
    """The selected technology update and the sources that support it."""

    technology_update_id: UUID
    primary_topic: Annotated[str, Field(min_length=1, max_length=200)]
    sources: Annotated[list[GenerateSource], Field(min_length=1, max_length=50)]


class MatchedInterest(CamelModel):
    """An interest the update matched, supplied by the caller for the explanation."""

    interest_id: UUID
    name: Annotated[str, Field(min_length=1, max_length=100)]
    priority: InterestPriority


class GenerateRequest(CamelModel):
    """Generate request body."""

    correlation_id: UUID
    update: GenerateUpdate
    matched_interests: Annotated[list[MatchedInterest], Field(max_length=100)] = Field(
        default_factory=list
    )


class GenerateResponse(CamelModel):
    """Generate response body (HTTP 200)."""

    correlation_id: UUID
    technology_update_id: UUID
    generation_version: str
    title: Annotated[str, Field(min_length=1, max_length=200)]
    summary: Annotated[str, Field(min_length=1)]
    why_relevant: Annotated[str, Field(min_length=1)]
