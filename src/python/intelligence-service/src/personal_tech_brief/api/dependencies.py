"""Shared request dependencies resolving application-scoped singletons."""

from __future__ import annotations

from typing import cast

from fastapi import Request

from personal_tech_brief.providers.base import GenerationProvider, SemanticAnalysisProvider
from personal_tech_brief.settings import Settings


def get_settings(request: Request) -> Settings:
    """Return the configuration validated during application initialization."""

    return cast(Settings, request.app.state.settings)


def get_provider(request: Request) -> SemanticAnalysisProvider:
    """Return the semantic-analysis provider selected during initialization."""

    return cast(SemanticAnalysisProvider, request.app.state.provider)


def get_generation_provider(request: Request) -> GenerationProvider:
    """Return the generation provider selected during initialization."""

    return cast(GenerationProvider, request.app.state.generation_provider)
