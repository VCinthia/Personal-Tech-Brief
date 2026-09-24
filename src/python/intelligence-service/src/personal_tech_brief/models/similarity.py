"""Request/response models for ``POST /internal/v1/similarity``."""

from __future__ import annotations

from typing import Annotated
from uuid import UUID

from pydantic import Field

from personal_tech_brief.models.base import CamelModel

UnitInterval = Annotated[float, Field(ge=0.0, le=1.0)]
_BoundedText = Annotated[str, Field(min_length=1, max_length=20_000)]


class SimilarityCandidate(CamelModel):
    """The candidate text scored against each representative."""

    text: _BoundedText


class SimilarityRepresentative(CamelModel):
    """One representative to score the candidate against."""

    update_id: UUID
    text: _BoundedText


class SimilarityRequest(CamelModel):
    """Similarity request body."""

    correlation_id: UUID
    candidate: SimilarityCandidate
    representatives: Annotated[list[SimilarityRepresentative], Field(max_length=100)] = Field(
        default_factory=list
    )


class SimilarityResult(CamelModel):
    """The similarity of the candidate to one representative."""

    update_id: UUID
    similarity: UnitInterval


class SimilarityResponse(CamelModel):
    """Similarity response body (HTTP 200)."""

    correlation_id: UUID
    algorithm_version: str
    results: list[SimilarityResult]
