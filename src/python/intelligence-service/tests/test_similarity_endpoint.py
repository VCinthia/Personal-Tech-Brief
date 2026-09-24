"""Integration tests for the similarity endpoint."""

from __future__ import annotations

from typing import Any
from uuid import uuid4

import pytest
from fastapi import FastAPI
from httpx import ASGITransport, AsyncClient

from personal_tech_brief.api.app import create_app

SIMILARITY_URL = "/internal/v1/similarity"


async def _post(app: FastAPI, body: dict[str, Any]) -> Any:
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://testserver") as c:
        return await c.post(SIMILARITY_URL, json=body)


@pytest.mark.anyio
async def test_similarity_endpoint_echoes_correlation_and_preserves_order() -> None:
    app = create_app()
    correlation_id = str(uuid4())
    ids = [str(uuid4()) for _ in range(2)]
    body = {
        "correlationId": correlation_id,
        "candidate": {"text": "alpha beta gamma"},
        "representatives": [
            {"updateId": ids[0], "text": "alpha beta gamma"},
            {"updateId": ids[1], "text": "totally different words"},
        ],
    }

    response = await _post(app, body)

    assert response.status_code == 200
    payload = response.json()
    assert payload["correlationId"] == correlation_id
    assert payload["algorithmVersion"] == "similarity-1"
    assert [r["updateId"] for r in payload["results"]] == ids
    assert payload["results"][0]["similarity"] == 1.0
    assert payload["results"][1]["similarity"] == 0.0


@pytest.mark.anyio
async def test_similarity_endpoint_empty_representatives_returns_empty_results() -> None:
    app = create_app()
    body = {
        "correlationId": str(uuid4()),
        "candidate": {"text": "alpha"},
        "representatives": [],
    }

    response = await _post(app, body)

    assert response.status_code == 200
    assert response.json()["results"] == []


@pytest.mark.anyio
async def test_similarity_endpoint_missing_candidate_is_boundary_400() -> None:
    app = create_app()
    body = {"correlationId": str(uuid4()), "representatives": []}

    response = await _post(app, body)

    assert response.status_code == 400
    assert response.headers["content-type"].startswith("application/problem+json")
