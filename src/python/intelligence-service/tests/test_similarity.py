"""Unit tests for deterministic similarity scoring."""

from __future__ import annotations

from uuid import uuid4

from personal_tech_brief.models.similarity import (
    SimilarityCandidate,
    SimilarityRepresentative,
    SimilarityRequest,
)
from personal_tech_brief.services.similarity import score_similarity
from personal_tech_brief.similarity import ALGORITHM_VERSION, cosine_similarity


def test_identical_text_scores_one() -> None:
    assert cosine_similarity("kubernetes release notes", "kubernetes release notes") == 1.0


def test_identical_after_normalization_scores_one() -> None:
    assert cosine_similarity("Kubernetes  RELEASE notes", "kubernetes release   notes") == 1.0


def test_disjoint_text_scores_zero() -> None:
    assert cosine_similarity("alpha beta", "gamma delta") == 0.0


def test_similarity_is_within_unit_interval() -> None:
    score = cosine_similarity("alpha beta gamma", "beta gamma delta")
    assert 0.0 < score < 1.0


def test_similarity_is_symmetric_and_deterministic() -> None:
    a, b = "alpha beta gamma", "gamma alpha epsilon"
    assert cosine_similarity(a, b) == cosine_similarity(b, a)


def test_score_similarity_preserves_representative_order() -> None:
    ids = [uuid4() for _ in range(3)]
    request = SimilarityRequest(
        correlation_id=uuid4(),
        candidate=SimilarityCandidate(text="alpha beta gamma"),
        representatives=[
            SimilarityRepresentative(update_id=ids[0], text="zzz nothing"),
            SimilarityRepresentative(update_id=ids[1], text="alpha beta gamma"),
            SimilarityRepresentative(update_id=ids[2], text="beta gamma"),
        ],
    )

    response = score_similarity(request)

    assert response.algorithm_version == ALGORITHM_VERSION
    assert [result.update_id for result in response.results] == ids
    assert response.results[1].similarity == 1.0
    assert response.results[0].similarity == 0.0


def test_empty_representatives_yields_empty_results() -> None:
    request = SimilarityRequest(
        correlation_id=uuid4(),
        candidate=SimilarityCandidate(text="alpha"),
        representatives=[],
    )

    response = score_similarity(request)

    assert response.results == []
