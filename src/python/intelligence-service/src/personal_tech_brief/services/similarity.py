"""Similarity orchestration over the deterministic algorithm (no model call)."""

from __future__ import annotations

from personal_tech_brief.models.similarity import (
    SimilarityRequest,
    SimilarityResponse,
    SimilarityResult,
)
from personal_tech_brief.similarity import ALGORITHM_VERSION, cosine_similarity


def score_similarity(request: SimilarityRequest) -> SimilarityResponse:
    """Score the candidate against each representative, preserving request order.

    Empty ``representatives`` yields an empty ``results`` list. Each result carries a
    deterministic similarity in ``[0, 1]``; there is no external dependency.
    """

    results = [
        SimilarityResult(
            update_id=representative.update_id,
            similarity=cosine_similarity(request.candidate.text, representative.text),
        )
        for representative in request.representatives
    ]
    return SimilarityResponse(
        correlation_id=request.correlation_id,
        algorithm_version=ALGORITHM_VERSION,
        results=results,
    )
