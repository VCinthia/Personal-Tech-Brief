"""Tests for the Azure provider using a mocked HTTP layer (never a live LLM)."""

from __future__ import annotations

import json
from collections.abc import Callable
from typing import Any
from uuid import uuid4

import httpx
import pytest
from pydantic import SecretStr

from personal_tech_brief.models.analyze import AnalyzeItem, CandidateInterest, InterestPriority
from personal_tech_brief.providers.azure import AzureOpenAiSemanticAnalysisProvider
from personal_tech_brief.providers.base import (
    ProviderResponseInvalidError,
    ProviderTimeoutError,
    ProviderUnavailableError,
)
from personal_tech_brief.providers.factory import build_semantic_analysis_provider
from personal_tech_brief.providers.fake import FakeSemanticAnalysisProvider
from personal_tech_brief.settings import Settings


def _azure_settings() -> Settings:
    return Settings(
        azure_openai_endpoint="https://example.openai.azure.com",
        azure_openai_deployment="gpt",
        azure_openai_api_key=SecretStr("secret-not-committed"),  # test-only value
        llm_max_retries=0,
    )


def _completion(content: dict[str, Any]) -> dict[str, Any]:
    return {"choices": [{"message": {"content": json.dumps(content)}}]}


def _client(handler: Callable[[httpx.Request], httpx.Response]) -> httpx.AsyncClient:
    return httpx.AsyncClient(transport=httpx.MockTransport(handler))


def _item() -> AnalyzeItem:
    return AnalyzeItem(source_item_id=uuid4(), title="Release", language="en")


@pytest.fixture
def anyio_backend() -> str:
    return "asyncio"


def test_factory_defaults_to_fake_without_azure_settings() -> None:
    provider = build_semantic_analysis_provider(Settings())
    assert isinstance(provider, FakeSemanticAnalysisProvider)


def test_factory_selects_azure_when_configured() -> None:
    provider = build_semantic_analysis_provider(_azure_settings())
    assert isinstance(provider, AzureOpenAiSemanticAnalysisProvider)


@pytest.mark.anyio
async def test_azure_returns_validated_features_and_filters_interests() -> None:
    interest_id = uuid4()
    unknown_id = uuid4()
    valid = {
        "language": "en",
        "normalized": {"keywords": ["release"], "eventDescriptors": ["new release"]},
        "topics": ["release"],
        "interestMatches": [
            {"interestId": str(interest_id), "matchStrength": 0.75},
            {"interestId": str(unknown_id), "matchStrength": 0.9},
        ],
        "impact": {"level": "medium", "confidence": 0.5},
    }

    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=_completion(valid))

    provider = AzureOpenAiSemanticAnalysisProvider(_azure_settings(), client=_client(handler))
    features = await provider.analyze(
        correlation_id="cid",
        item=_item(),
        content="release notes",
        candidate_interests=[
            CandidateInterest(
                interest_id=interest_id, name="release", priority=InterestPriority.HIGH
            )
        ],
    )

    assert [m.interest_id for m in features.interest_matches] == [interest_id]
    assert features.interest_matches[0].match_strength == 0.75
    assert features.impact.level.value == "medium"


@pytest.mark.anyio
async def test_azure_invalid_structured_output_raises_response_invalid() -> None:
    invalid = {
        "language": "en",
        "normalized": {"keywords": ["release"], "eventDescriptors": []},
        "topics": ["release"],
        "interestMatches": [{"interestId": str(uuid4()), "matchStrength": 5.0}],
        "impact": {"level": "medium", "confidence": 0.5},
    }

    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=_completion(invalid))

    provider = AzureOpenAiSemanticAnalysisProvider(_azure_settings(), client=_client(handler))

    with pytest.raises(ProviderResponseInvalidError):
        await provider.analyze(
            correlation_id="cid",
            item=_item(),
            content="release notes",
            candidate_interests=[],
        )


@pytest.mark.anyio
async def test_azure_server_error_raises_unavailable() -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(500, json={"error": "boom"})

    provider = AzureOpenAiSemanticAnalysisProvider(_azure_settings(), client=_client(handler))

    with pytest.raises(ProviderUnavailableError):
        await provider.analyze(
            correlation_id="cid", item=_item(), content="x", candidate_interests=[]
        )


@pytest.mark.anyio
async def test_azure_timeout_raises_timeout() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout("timed out", request=request)

    provider = AzureOpenAiSemanticAnalysisProvider(_azure_settings(), client=_client(handler))

    with pytest.raises(ProviderTimeoutError):
        await provider.analyze(
            correlation_id="cid", item=_item(), content="x", candidate_interests=[]
        )


@pytest.mark.anyio
async def test_azure_missing_content_raises_response_invalid() -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"choices": []})

    provider = AzureOpenAiSemanticAnalysisProvider(_azure_settings(), client=_client(handler))

    with pytest.raises(ProviderResponseInvalidError):
        await provider.analyze(
            correlation_id="cid", item=_item(), content="x", candidate_interests=[]
        )
