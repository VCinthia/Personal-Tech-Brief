"""Deterministic cosine similarity over normalized term-frequency vectors."""

from __future__ import annotations

import math
from collections import Counter

from personal_tech_brief.text import token_frequencies

# Identifies the similarity algorithm + preprocessing version for traceability.
ALGORITHM_VERSION = "similarity-1"


def _clamp_unit(value: float) -> float:
    """Clamp ``value`` into the closed interval ``[0, 1]``.

    Guards against floating-point error nudging an exact 1.0 past the boundary.
    """

    if value <= 0.0:
        return 0.0
    if value >= 1.0:
        return 1.0
    return value


def cosine_similarity(candidate_text: str, representative_text: str) -> float:
    """Return the cosine similarity of two texts in ``[0, 1]``.

    Similarity is computed over term-frequency vectors of the deterministically
    tokenized texts. Because term frequencies are non-negative, the cosine is
    always within ``[0, 1]``. Identical token multisets yield ``1.0`` and texts
    with no shared tokens yield ``0.0``. Two texts that both tokenize to nothing are
    treated as identical (``1.0``); if only one is empty the result is ``0.0``.
    """

    candidate = token_frequencies(candidate_text)
    representative = token_frequencies(representative_text)

    if not candidate and not representative:
        return 1.0
    if not candidate or not representative:
        return 0.0

    return _clamp_unit(_cosine(candidate, representative))


def _cosine(left: Counter[str], right: Counter[str]) -> float:
    """Compute the cosine of two non-empty term-frequency vectors."""

    smaller, larger = (left, right) if len(left) <= len(right) else (right, left)
    dot = sum(count * larger[token] for token, count in smaller.items())
    if dot == 0:
        return 0.0

    left_norm = math.sqrt(sum(count * count for count in left.values()))
    right_norm = math.sqrt(sum(count * count for count in right.values()))
    return dot / (left_norm * right_norm)
