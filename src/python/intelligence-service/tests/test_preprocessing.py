"""Unit tests for deterministic text preprocessing."""

from __future__ import annotations

from personal_tech_brief.text import (
    bigrams,
    keyword_counts,
    normalize_text,
    token_frequencies,
    tokenize,
)


def test_normalize_is_case_folded_and_whitespace_collapsed() -> None:
    assert normalize_text("  The   QUICK\tBrown\n") == "the quick brown"


def test_normalize_applies_unicode_nfkc() -> None:
    # Fullwidth 'ＡＢ' normalizes to ascii 'ab' after case folding.
    assert normalize_text("ＡＢ") == "ab"


def test_tokenize_splits_on_non_word_characters() -> None:
    assert tokenize("Kubernetes 1.30: release-notes!") == [
        "kubernetes",
        "1",
        "30",
        "release",
        "notes",
    ]


def test_tokenize_is_deterministic() -> None:
    text = "Repeated repeated REPEATED words words"
    assert tokenize(text) == tokenize(text)


def test_token_frequencies_counts_terms() -> None:
    assert token_frequencies("alpha beta alpha") == {"alpha": 2, "beta": 1}


def test_keyword_counts_drops_stopwords_and_short_tokens() -> None:
    counts = keyword_counts("The cache is a fast in memory store store")
    assert "the" not in counts
    assert "is" not in counts
    assert "a" not in counts
    assert counts["store"] == 2
    assert counts["cache"] == 1


def test_bigrams_are_ordered_adjacent_pairs() -> None:
    assert bigrams("alpha beta gamma") == ["alpha beta", "beta gamma"]
