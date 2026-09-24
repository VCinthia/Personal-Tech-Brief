"""Selection of the active providers from settings."""

from __future__ import annotations

from personal_tech_brief.providers.base import GenerationProvider, SemanticAnalysisProvider
from personal_tech_brief.providers.fake import (
    FakeGenerationProvider,
    FakeSemanticAnalysisProvider,
)
from personal_tech_brief.settings import Settings


def build_semantic_analysis_provider(settings: Settings) -> SemanticAnalysisProvider:
    """Return the Azure provider when fully configured, else the deterministic fake.

    The Azure module is imported lazily so that neither ``httpx`` usage nor the LLM
    integration is touched on the default (fake) path exercised by CI and dev.
    """

    if settings.azure_openai_configured:
        from personal_tech_brief.providers.azure import (
            AzureOpenAiSemanticAnalysisProvider,
        )

        return AzureOpenAiSemanticAnalysisProvider(settings)
    return FakeSemanticAnalysisProvider()


def build_generation_provider(settings: Settings) -> GenerationProvider:
    """Return the Azure generation provider when configured, else the deterministic fake.

    Selection mirrors the analyze provider: the Azure module is imported lazily so the
    default (fake) path exercised by CI and dev never touches the LLM integration.
    """

    if settings.azure_openai_configured:
        from personal_tech_brief.providers.azure import (
            AzureOpenAiGenerationProvider,
        )

        return AzureOpenAiGenerationProvider(settings)
    return FakeGenerationProvider()
