"""Integration tests for the analyze endpoint and its error mapping."""

from __future__ import annotations

from typing import Any
from uuid import uuid4

import pytest
from fastapi import FastAPI
from httpx import ASGITransport, AsyncClient

from personal_tech_brief.api.app import create_app
from personal_tech_brief.models.analyze import AnalyzeItem, CandidateInterest
from personal_tech_brief.providers.base import (
    ProviderFailureError,
    ProviderResponseInvalidError,
    ProviderTimeoutError,
    ProviderUnavailableError,
    SemanticFeatures,
)

ANALYZE_URL = "/internal/v1/items/analyze"


def _analyze_body(**overrides: Any) -> dict[str, Any]:
    correlation_id = overrides.pop("correlation_id", str(uuid4()))
    interests = overrides.pop(
        "candidate_interests",
        [{"interestId": str(uuid4()), "name": "container orchestration", "priority": "high"}],
    )
    item = {
        "sourceItemId": str(uuid4()),
        "title": "Kubernetes 1.30 release notes",
        "excerpt": "A new container orchestration release with security fixes.",
        "content": "The release contains a critical security vulnerability fix for the runtime.",
        "language": "en",
    }
    item.update(overrides.pop("item", {}))
    body = {"correlationId": correlation_id, "item": item, "candidateInterests": interests}
    body.update(overrides)
    return body


class _RaisingProvider:
    def __init__(self, error: Exception) -> None:
        self._error = error

    async def analyze(
        self,
        *,
        correlation_id: str,
        item: AnalyzeItem,
        content: str,
        candidate_interests: list[CandidateInterest],
    ) -> SemanticFeatures:
        raise self._error


async def _post(app: FastAPI, body: dict[str, Any]) -> Any:
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://testserver") as c:
        return await c.post(ANALYZE_URL, json=body)


@pytest.mark.anyio
async def test_analyze_with_fake_provider_returns_validated_features() -> None:
    app = create_app()
    correlation_id = str(uuid4())
    interest_id = str(uuid4())
    body = _analyze_body(
        correlation_id=correlation_id,
        candidate_interests=[
            {"interestId": interest_id, "name": "container orchestration", "priority": "high"}
        ],
    )

    response = await _post(app, body)

    assert response.status_code == 200
    payload = response.json()
    assert payload["correlationId"] == correlation_id
    assert payload["sourceItemId"] == body["item"]["sourceItemId"]
    assert payload["analyzerVersion"] == "analyze-1"
    assert payload["language"] == "en"
    assert isinstance(payload["normalized"]["keywords"], list)
    assert isinstance(payload["normalized"]["eventDescriptors"], list)
    match_ids = {m["interestId"] for m in payload["interestMatches"]}
    assert match_ids == {interest_id}
    for match in payload["interestMatches"]:
        assert 0.0 <= match["matchStrength"] <= 1.0
    assert payload["impact"]["level"] in {"high", "medium", "low", "none"}
    assert 0.0 <= payload["impact"]["confidence"] <= 1.0


@pytest.mark.anyio
async def test_analyze_is_deterministic_for_same_input() -> None:
    app = create_app()
    body = _analyze_body()

    first = await _post(app, body)
    second = await _post(app, body)

    assert first.json() == second.json()


@pytest.mark.anyio
async def test_analyze_matches_only_supplied_interest_ids() -> None:
    app = create_app()
    body = _analyze_body(candidate_interests=[])

    response = await _post(app, body)

    assert response.status_code == 200
    assert response.json()["interestMatches"] == []


@pytest.mark.anyio
async def test_analyze_bad_guid_is_boundary_400() -> None:
    app = create_app()
    body = _analyze_body(correlation_id="not-a-guid")

    response = await _post(app, body)

    assert response.status_code == 400
    assert response.headers["content-type"].startswith("application/problem+json")
    problem = response.json()
    assert problem["status"] == 400
    assert problem["title"]


@pytest.mark.anyio
async def test_analyze_empty_title_is_boundary_400() -> None:
    app = create_app()
    body = _analyze_body(item={"title": ""})

    response = await _post(app, body)

    assert response.status_code == 400


@pytest.mark.anyio
async def test_analyze_unknown_field_is_boundary_400() -> None:
    app = create_app()
    body = _analyze_body()
    body["unexpected"] = "value"

    response = await _post(app, body)

    assert response.status_code == 400


@pytest.mark.anyio
async def test_analyze_duplicate_interest_ids_is_semantic_422() -> None:
    app = create_app()
    duplicate = str(uuid4())
    body = _analyze_body(
        candidate_interests=[
            {"interestId": duplicate, "name": "kubernetes", "priority": "high"},
            {"interestId": duplicate, "name": "security", "priority": "low"},
        ],
    )

    response = await _post(app, body)

    assert response.status_code == 422
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["status"] == 422


@pytest.mark.anyio
async def test_analyze_invalid_structured_output_maps_to_502() -> None:
    app = create_app()
    app.state.provider = _RaisingProvider(ProviderResponseInvalidError("bad output"))
    correlation_id = str(uuid4())

    response = await _post(app, _analyze_body(correlation_id=correlation_id))

    assert response.status_code == 502
    problem = response.json()
    assert problem["status"] == 502
    assert "vulnerability" not in str(problem).lower()  # no raw content leaked


@pytest.mark.anyio
async def test_analyze_provider_unavailable_maps_to_503() -> None:
    app = create_app()
    app.state.provider = _RaisingProvider(ProviderUnavailableError("down"))

    response = await _post(app, _analyze_body())

    assert response.status_code == 503


@pytest.mark.anyio
async def test_analyze_provider_failure_maps_to_502() -> None:
    app = create_app()
    app.state.provider = _RaisingProvider(ProviderFailureError("boom"))

    response = await _post(app, _analyze_body())

    assert response.status_code == 502


@pytest.mark.anyio
async def test_analyze_provider_timeout_maps_to_504() -> None:
    app = create_app()
    app.state.provider = _RaisingProvider(ProviderTimeoutError("slow"))

    response = await _post(app, _analyze_body())

    assert response.status_code == 504
