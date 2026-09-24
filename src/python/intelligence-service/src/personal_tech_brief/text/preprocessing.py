"""Deterministic, dependency-free text preprocessing primitives.

These functions are pure and referentially transparent: the same input always
produces the same output, with no randomness, locale dependence, or I/O. They are
reused by the deterministic similarity algorithm and by the fake analysis provider
so keyword extraction and vectorization share one tokenization definition.
"""

from __future__ import annotations

import re
import unicodedata
from collections import Counter

# Tokens are maximal runs of Unicode word characters after case folding. Using an
# explicit pattern keeps behaviour identical across platforms and Python builds.
_TOKEN_PATTERN = re.compile(r"[^\W_]+", flags=re.UNICODE)

# A small, fixed English stop-word set. Kept intentionally minimal and static so
# keyword extraction stays deterministic and language-agnostic enough for a hint.
STOP_WORDS: frozenset[str] = frozenset(
    {
        "a",
        "an",
        "and",
        "are",
        "as",
        "at",
        "be",
        "but",
        "by",
        "for",
        "from",
        "has",
        "have",
        "in",
        "into",
        "is",
        "it",
        "its",
        "of",
        "on",
        "or",
        "that",
        "the",
        "their",
        "they",
        "this",
        "to",
        "was",
        "were",
        "will",
        "with",
    }
)


def normalize_text(text: str) -> str:
    """Return a canonical, case-folded, whitespace-collapsed form of ``text``.

    Applies Unicode NFKC normalization, case folding, and whitespace collapsing so
    that superficially different representations of the same content compare equal.
    """

    normalized = unicodedata.normalize("NFKC", text).casefold()
    return " ".join(normalized.split())


def tokenize(text: str) -> list[str]:
    """Split ``text`` into normalized word tokens in their original order."""

    normalized = unicodedata.normalize("NFKC", text).casefold()
    return _TOKEN_PATTERN.findall(normalized)


def token_frequencies(text: str) -> Counter[str]:
    """Return a term-frequency map of the tokens in ``text``."""

    return Counter(tokenize(text))


def keyword_counts(text: str, *, min_length: int = 3) -> Counter[str]:
    """Return frequencies of content-bearing tokens.

    Tokens shorter than ``min_length`` or present in :data:`STOP_WORDS` are excluded
    so that extracted keywords favour distinctive terms.
    """

    counts: Counter[str] = Counter()
    for token in tokenize(text):
        if len(token) < min_length or token in STOP_WORDS:
            continue
        counts[token] += 1
    return counts


def bigrams(text: str) -> list[str]:
    """Return adjacent token pairs (as ``"a b"`` strings) in order of appearance."""

    tokens = tokenize(text)
    return [f"{first} {second}" for first, second in zip(tokens, tokens[1:], strict=False)]
