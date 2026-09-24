"""FastAPI application factory and local executable entry point."""

from typing import cast

import uvicorn
from fastapi import FastAPI

from personal_tech_brief.api.errors import register_exception_handlers
from personal_tech_brief.api.health import router as health_router
from personal_tech_brief.api.intelligence import router as intelligence_router
from personal_tech_brief.providers.factory import (
    build_generation_provider,
    build_semantic_analysis_provider,
)
from personal_tech_brief.settings import Settings


def create_app(settings: Settings | None = None) -> FastAPI:
    """Create the stateless internal API after validating local configuration."""

    resolved_settings = settings if settings is not None else Settings()
    app = FastAPI(
        title=resolved_settings.service_name,
        version="0.1.0",
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )
    app.state.settings = resolved_settings
    app.state.provider = build_semantic_analysis_provider(resolved_settings)
    app.state.generation_provider = build_generation_provider(resolved_settings)
    register_exception_handlers(app)
    app.include_router(health_router)
    app.include_router(intelligence_router)
    return app


app = create_app()


def main() -> None:
    """Run the API for local development."""

    settings = cast(Settings, app.state.settings)
    uvicorn.run(app, host=settings.host, port=settings.port)
