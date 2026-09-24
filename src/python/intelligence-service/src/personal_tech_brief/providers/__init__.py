"""Provider abstractions and implementations (ADR-006)."""

from personal_tech_brief.providers.base import (
    GeneratedContent,
    GenerationProvider,
    ProviderError,
    ProviderFailureError,
    ProviderResponseInvalidError,
    ProviderTimeoutError,
    ProviderUnavailableError,
    SemanticAnalysisProvider,
    SemanticFeatures,
)
from personal_tech_brief.providers.factory import (
    build_generation_provider,
    build_semantic_analysis_provider,
)
from personal_tech_brief.providers.fake import (
    FakeGenerationProvider,
    FakeSemanticAnalysisProvider,
)

__all__ = [
    "FakeGenerationProvider",
    "FakeSemanticAnalysisProvider",
    "GeneratedContent",
    "GenerationProvider",
    "ProviderError",
    "ProviderFailureError",
    "ProviderResponseInvalidError",
    "ProviderTimeoutError",
    "ProviderUnavailableError",
    "SemanticAnalysisProvider",
    "SemanticFeatures",
    "build_generation_provider",
    "build_semantic_analysis_provider",
]
