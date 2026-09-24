"""Deterministic, offline semantic-analysis provider.

This is the default provider in tests and in local/dev without provider config. It
derives stable structured features purely from the input text, performs no network
I/O, and uses no randomness, so CI never needs paid or live LLM access.
"""

from __future__ import annotations

from collections.abc import Iterable

from personal_tech_brief.models.analyze import (
    AnalyzeItem,
    CandidateInterest,
    ImpactLevel,
    ImpactSignal,
    InterestMatch,
    NormalizedFeatures,
)
from personal_tech_brief.models.generate import GenerateUpdate, MatchedInterest
from personal_tech_brief.providers.base import GeneratedContent, SemanticFeatures
from personal_tech_brief.text import bigrams, keyword_counts, tokenize

# Fixed lexicon of tokens that signal operational/technology impact. Static so the
# derived impact classification is fully deterministic.
_IMPACT_SIGNALS: frozenset[str] = frozenset(
    {
        "breaking",
        "critical",
        "cve",
        "deprecated",
        "deprecation",
        "exploit",
        "incident",
        "launch",
        "major",
        "outage",
        "release",
        "security",
        "vulnerability",
    }
)

_MAX_KEYWORDS = 10
_MAX_EVENT_DESCRIPTORS = 5
_MAX_TOPICS = 3


class FakeSemanticAnalysisProvider:
    """A provider whose output is a deterministic function of its input."""

    async def analyze(
        self,
        *,
        correlation_id: str,
        item: AnalyzeItem,
        content: str,
        candidate_interests: list[CandidateInterest],
    ) -> SemanticFeatures:
        """Return stable structured features derived from ``item`` and ``content``."""

        keyword_frequencies = keyword_counts(content)
        # Rank by frequency descending, breaking ties alphabetically for stability.
        ranked_keywords = [
            token
            for token, _count in sorted(
                keyword_frequencies.items(), key=lambda pair: (-pair[1], pair[0])
            )
        ]
        keywords = ranked_keywords[:_MAX_KEYWORDS]
        topics = ranked_keywords[:_MAX_TOPICS]
        event_descriptors = self._event_descriptors(item.title, content)

        normalized = NormalizedFeatures(keywords=keywords, event_descriptors=event_descriptors)
        content_tokens = set(tokenize(content))
        interest_matches = [
            InterestMatch(
                interest_id=interest.interest_id,
                match_strength=self._match_strength(interest, content_tokens),
            )
            for interest in candidate_interests
        ]

        return SemanticFeatures(
            language=item.language,
            normalized=normalized,
            topics=topics,
            interest_matches=interest_matches,
            impact=self._impact(content_tokens),
        )

    @staticmethod
    def _event_descriptors(title: str, content: str) -> list[str]:
        """Return deterministic event descriptors preferring the title's phrasing."""

        ordered: list[str] = []
        seen: set[str] = set()
        for phrase in (*bigrams(title), *bigrams(content)):
            if phrase in seen:
                continue
            seen.add(phrase)
            ordered.append(phrase)
            if len(ordered) == _MAX_EVENT_DESCRIPTORS:
                break
        return ordered

    @staticmethod
    def _match_strength(interest: CandidateInterest, content_tokens: set[str]) -> float:
        """Return the overlap of interest-name tokens present in the content in [0,1]."""

        name_tokens = set(tokenize(interest.name))
        if not name_tokens:
            return 0.0
        overlap = len(name_tokens & content_tokens)
        return overlap / len(name_tokens)

    @staticmethod
    def _impact(content_tokens: set[str]) -> ImpactSignal:
        """Classify impact deterministically from distinct impact-signal tokens."""

        matches = len(content_tokens & _IMPACT_SIGNALS)
        confidence = min(1.0, matches / 3.0)
        if matches >= 3:
            level = ImpactLevel.HIGH
        elif matches == 2:
            level = ImpactLevel.MEDIUM
        elif matches == 1:
            level = ImpactLevel.LOW
        else:
            level = ImpactLevel.NONE
        return ImpactSignal(level=level, confidence=confidence)


_MAX_TITLE_CHARS = 200
_MAX_SUMMARY_CHARS = 2_000
_MAX_WHY_RELEVANT_CHARS = 1_000


class FakeGenerationProvider:
    """A generation provider whose output is a deterministic function of its input.

    It is the default provider in tests and in local/dev without provider config. The
    title, summary, and why-relevant explanation are derived purely from the supplied
    sources and matched interests: it performs no network I/O, uses no randomness, and
    never invents facts beyond the request, so CI never needs paid or live LLM access.
    """

    async def generate(
        self,
        *,
        correlation_id: str,
        update: GenerateUpdate,
        matched_interests: list[MatchedInterest],
    ) -> GeneratedContent:
        """Return stable, source-grounded content derived from ``update``."""

        title = update.sources[0].title.strip()[:_MAX_TITLE_CHARS]
        return GeneratedContent(
            title=title,
            summary=self._summary(update)[:_MAX_SUMMARY_CHARS],
            why_relevant=self._why_relevant(update, matched_interests)[:_MAX_WHY_RELEVANT_CHARS],
        )

    @staticmethod
    def _summary(update: GenerateUpdate) -> str:
        """Compose a summary from the supplied source excerpts, falling back to titles.

        Only text present on the request is used; nothing is invented. Excerpts are
        preferred; when none are present the distinct source titles are used instead.
        """

        excerpts = [
            source.excerpt.strip()
            for source in update.sources
            if source.excerpt is not None and source.excerpt.strip()
        ]
        grounded = excerpts if excerpts else [source.title.strip() for source in update.sources]
        return " ".join(_distinct(grounded))

    @staticmethod
    def _why_relevant(update: GenerateUpdate, matched_interests: list[MatchedInterest]) -> str:
        """Explain relevance from the matched interests, falling back to the topic.

        Uses only the supplied interest names and primary topic; nothing is invented.
        """

        names = _distinct(interest.name.strip() for interest in matched_interests)
        if names:
            return f"Relevant to your interests: {', '.join(names)}."
        return f"Relevant to the {update.primary_topic.strip()} topic."


def _distinct(values: Iterable[str]) -> list[str]:
    """Return the non-empty values in order with duplicates removed (deterministic)."""

    ordered: list[str] = []
    seen: set[str] = set()
    for value in values:
        if not value or value in seen:
            continue
        seen.add(value)
        ordered.append(value)
    return ordered
