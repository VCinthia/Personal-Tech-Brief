"""Internal intelligence endpoints: analyze, similarity, and generate."""

from __future__ import annotations

from typing import Annotated

from fastapi import APIRouter, Depends, status

from personal_tech_brief.api.dependencies import (
    get_generation_provider,
    get_provider,
    get_settings,
)
from personal_tech_brief.models.analyze import AnalyzeRequest, AnalyzeResponse
from personal_tech_brief.models.generate import GenerateRequest, GenerateResponse
from personal_tech_brief.models.similarity import SimilarityRequest, SimilarityResponse
from personal_tech_brief.providers.base import GenerationProvider, SemanticAnalysisProvider
from personal_tech_brief.services.analyze import analyze_item
from personal_tech_brief.services.generate import generate_update
from personal_tech_brief.services.similarity import score_similarity
from personal_tech_brief.services.telemetry import request_scope
from personal_tech_brief.settings import Settings

router = APIRouter(prefix="/internal/v1", tags=["intelligence"])


@router.post(
    "/items/analyze",
    response_model=AnalyzeResponse,
    status_code=status.HTTP_200_OK,
)
async def analyze(
    payload: AnalyzeRequest,
    provider: Annotated[SemanticAnalysisProvider, Depends(get_provider)],
    settings: Annotated[Settings, Depends(get_settings)],
) -> AnalyzeResponse:
    """Preprocess an item and return validated structured semantic features."""

    with request_scope(str(payload.correlation_id), "items/analyze"):
        return await analyze_item(payload, provider=provider, settings=settings)


@router.post(
    "/similarity",
    response_model=SimilarityResponse,
    status_code=status.HTTP_200_OK,
)
async def similarity(payload: SimilarityRequest) -> SimilarityResponse:
    """Return deterministic batch similarity of a candidate against representatives."""

    with request_scope(str(payload.correlation_id), "similarity"):
        return score_similarity(payload)


@router.post(
    "/updates/generate",
    response_model=GenerateResponse,
    status_code=status.HTTP_200_OK,
)
async def generate(
    payload: GenerateRequest,
    provider: Annotated[GenerationProvider, Depends(get_generation_provider)],
    settings: Annotated[Settings, Depends(get_settings)],
) -> GenerateResponse:
    """Generate concise, source-grounded content for one selected technology update."""

    with request_scope(str(payload.correlation_id), "updates/generate"):
        return await generate_update(payload, provider=provider, settings=settings)
