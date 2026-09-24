"""Integration tests for the generate endpoint and its provider/error mapping.

No test requires paid or live LLM access: the deterministic fake provider is the
default, and the Azure provider is exercised only through a mocked HTTP transport.
"""

from __future__ import annotations

import json
from collections.abc import Callable
from typing import Any
from uuid import uuid4

import httpx
import pytest
from fastapi import FastAPI
from httpx import ASGITransport, AsyncClient
from pydantic import SecretStr

from personal_tech_brief.api.app import create_app
from personal_tech_brief.models.generate import GenerateUpdate, MatchedInterest
from personal_tech_brief.providers.azure import AzureOpenAiGenerationProvider
from personal_tech_brief.providers.base import (
    GeneratedContent,
    ProviderTimeoutError,
)
from personal_tech_brief.providers.factory import build_generation_provider
from personal_tech_brief.providers.fake import FakeGenerationProvider
from personal_tech_brief.settings import Settings

GENERATE_URL = "/internal/v1/updates/generate"

_TOP_TITLE = "Kubernetes 1.40 released"
_TOP_EXCERPT = "A platform release with a critical runtime security fix."
_INTEREST_NAME = "Platform engineering"


def _generate_body(**overrides: Any) -> dict[str, Any]:
    correlation_id = overrides.pop("correlation_id", str(uuid4()))
    update = {
        "technologyUpdateId": str(uuid4()),
        "primaryTopic": "runtime-platforms",
        "sources": [
            {
                "sourceItemId": str(uuid4()),
                "title": _TOP_TITLE,
                "excerpt": _TOP_EXCERPT,
                "sourceUrl": "https://news.example.test/k8s-1-40",
                "publishedAtUtc": "2026-09-13T15:00:00Z",
            },
            {
                "sourceItemId": str(uuid4()),
                "title": "K8s 1.40 ships runtime hardening",
                "excerpt": None,
                "sourceUrl": None,
                "publishedAtUtc": None,
            },
        ],
    }
    update.update(overrides.pop("update", {}))
    interests = overrides.pop(
        "matched_interests",
        [{"interestId": str(uuid4()), "name": _INTEREST_NAME, "priority": "high"}],
    )
    body = {"correlationId": correlation_id, "update": update, "matchedInterests": interests}
    body.update(overrides)
    return body


class _RaisingGenerationProvider:
    def __init__(self, error: Exception) -> None:
        self._error = error

    async def generate(
        self,
        *,
        correlation_id: str,
        update: GenerateUpdate,
        matched_interests: list[MatchedInterest],
    ) -> GeneratedContent:
        raise self._error


def _completion(content: dict[str, Any]) -> dict[str, Any]:
    return {"choices": [{"message": {"content": json.dumps(content)}}]}


def _mock_client(handler: Callable[[httpx.Request], httpx.Response]) -> httpx.AsyncClient:
    return httpx.AsyncClient(transport=httpx.MockTransport(handler))


def _azure_settings() -> Settings:
    return Settings(
        azure_openai_endpoint="https://example.openai.azure.com",
        azure_openai_deployment="gpt",
        azure_openai_api_key=SecretStr("secret-not-committed"),  # test-only value
        generate_llm_max_retries=0,
    )


async def _post(app: FastAPI, body: dict[str, Any]) -> Any:
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://testserver") as c:
        return await c.post(GENERATE_URL, json=body)


def test_factory_defaults_to_fake_generation_without_azure_settings() -> None:
    provider = build_generation_provider(Settings())
    assert isinstance(provider, FakeGenerationProvider)


def test_factory_selects_azure_generation_when_configured() -> None:
    provider = build_generation_provider(_azure_settings())
    assert isinstance(provider, AzureOpenAiGenerationProvider)


@pytest.mark.anyio
async def test_generate_with_fake_provider_is_source_grounded_and_echoes_correlation() -> None:
    app = create_app()
    correlation_id = str(uuid4())
    body = _generate_body(correlation_id=correlation_id)

    response = await _post(app, body)

    assert response.status_code == 200
    payload = response.json()
    assert payload["correlationId"] == correlation_id
    assert payload["technologyUpdateId"] == body["update"]["technologyUpdateId"]
    assert payload["generationVersion"] == "generate-1"
    # Title is grounded in the top source title.
    assert payload["title"] == _TOP_TITLE
    assert len(payload["title"]) <= 200
    # Summary is grounded in the supplied source excerpt (nothing invented).
    assert _TOP_EXCERPT in payload["summary"]
    # Why-relevant is grounded in the matched interest name.
    assert _INTEREST_NAME in payload["whyRelevant"]


@pytest.mark.anyio
async def test_generate_is_deterministic_for_same_input() -> None:
    app = create_app()
    body = _generate_body()

    first = await _post(app, body)
    second = await _post(app, body)

    assert first.json() == second.json()


@pytest.mark.anyio
async def test_generate_falls_back_to_topic_when_no_matched_interests() -> None:
    app = create_app()
    body = _generate_body(matched_interests=[])

    response = await _post(app, body)

    assert response.status_code == 200
    assert "runtime-platforms" in response.json()["whyRelevant"]


@pytest.mark.anyio
async def test_generate_bad_guid_is_boundary_400() -> None:
    app = create_app()
    body = _generate_body(correlation_id="not-a-guid")

    response = await _post(app, body)

    assert response.status_code == 400
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["status"] == 400


@pytest.mark.anyio
async def test_generate_empty_sources_is_boundary_400() -> None:
    app = create_app()
    body = _generate_body(update={"sources": []})

    response = await _post(app, body)

    assert response.status_code == 400


@pytest.mark.anyio
async def test_generate_unknown_field_is_boundary_400() -> None:
    app = create_app()
    body = _generate_body()
    body["unexpected"] = "value"

    response = await _post(app, body)

    assert response.status_code == 400


@pytest.mark.anyio
async def test_generate_duplicate_source_ids_is_semantic_422() -> None:
    app = create_app()
    duplicate = str(uuid4())
    body = _generate_body(
        update={
            "sources": [
                {"sourceItemId": duplicate, "title": "First", "excerpt": "a"},
                {"sourceItemId": duplicate, "title": "Second", "excerpt": "b"},
            ]
        }
    )

    response = await _post(app, body)

    assert response.status_code == 422
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["status"] == 422


@pytest.mark.anyio
async def test_generate_provider_timeout_maps_to_504() -> None:
    app = create_app()
    app.state.generation_provider = _RaisingGenerationProvider(ProviderTimeoutError("slow"))

    response = await _post(app, _generate_body())

    assert response.status_code == 504


@pytest.mark.anyio
async def test_generate_invalid_structured_output_maps_to_502() -> None:
    # A mocked Azure transport returns a completion whose JSON fails schema validation
    # (missing whyRelevant). The provider must map this to 502, never crash or leak.
    invalid = {"title": "K8s 1.40", "summary": "runtime hardening"}

    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=_completion(invalid))

    app = create_app()
    app.state.generation_provider = AzureOpenAiGenerationProvider(
        _azure_settings(), client=_mock_client(handler)
    )

    response = await _post(app, _generate_body())

    assert response.status_code == 502
    problem = response.json()
    assert problem["status"] == 502
    # No raw source content leaked into the problem document.
    assert _TOP_EXCERPT.lower() not in str(problem).lower()


@pytest.mark.anyio
async def test_azure_generation_returns_validated_content() -> None:
    valid = {
        "title": "Kubernetes 1.40 released",
        "summary": "A platform release with a critical runtime security fix.",
        "whyRelevant": "Relevant to platform engineering.",
    }

    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=_completion(valid))

    provider = AzureOpenAiGenerationProvider(_azure_settings(), client=_mock_client(handler))
    update = GenerateUpdate.model_validate(_generate_body()["update"])

    content = await provider.generate(
        correlation_id="cid",
        update=update,
        matched_interests=[],
    )

    assert content.title == "Kubernetes 1.40 released"
    assert content.why_relevant == "Relevant to platform engineering."
