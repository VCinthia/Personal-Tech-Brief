"""Cross-language contract tests.

These bind the Python request/response models to the language-neutral canonical
JSON fixtures in ``docs/contracts/intelligence/`` — the SAME files the .NET
``IntelligenceApiContractTests`` validate against. Because both implementations
round-trip the one shared source of truth, a field rename, casing change, enum
change, or dropped field on either side fails that side's contract test.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
from pydantic import BaseModel

from personal_tech_brief.models.analyze import AnalyzeRequest, AnalyzeResponse
from personal_tech_brief.models.generate import GenerateRequest, GenerateResponse
from personal_tech_brief.models.similarity import SimilarityRequest, SimilarityResponse

# tests/ -> intelligence-service -> python -> src -> <repo root>
_CONTRACTS_DIR = Path(__file__).resolve().parents[4] / "docs" / "contracts" / "intelligence"


def _load(fixture: str) -> dict[str, Any]:
    loaded: dict[str, Any] = json.loads((_CONTRACTS_DIR / fixture).read_text(encoding="utf-8"))
    return loaded


def test_shared_contract_fixtures_are_present() -> None:
    assert _CONTRACTS_DIR.is_dir(), f"shared contract fixtures missing at {_CONTRACTS_DIR}"


@pytest.mark.parametrize(
    ("fixture", "model"),
    [
        ("analyze-request.json", AnalyzeRequest),
        ("similarity-request.json", SimilarityRequest),
        ("generate-request.json", GenerateRequest),
        ("generate-request-maxlengths.json", GenerateRequest),
        ("analyze-response.json", AnalyzeResponse),
        ("analyze-response-none.json", AnalyzeResponse),
        ("similarity-response.json", SimilarityResponse),
        ("similarity-response-empty.json", SimilarityResponse),
        ("generate-response.json", GenerateResponse),
    ],
)
def test_python_models_round_trip_the_canonical_fixture(
    fixture: str, model: type[BaseModel]
) -> None:
    """Each Python model parses the canonical JSON and re-emits identical JSON.

    Round-trip equality (parse then dump ``by_alias``) proves the Python side agrees
    with the shared contract on field names, camelCase, enum spellings, value ranges,
    and list ordering (interest matches / similarity results).
    """

    canonical = _load(fixture)
    parsed = model.model_validate(canonical)
    emitted = parsed.model_dump(by_alias=True, mode="json")
    assert emitted == canonical
