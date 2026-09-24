"""RFC 9457 problem+json error mapping and FastAPI exception handlers."""

from __future__ import annotations

from typing import Any

from fastapi import FastAPI, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from personal_tech_brief.models.problem import PROBLEM_JSON_MEDIA_TYPE, ProblemDetails
from personal_tech_brief.providers.base import (
    ProviderResponseInvalidError,
    ProviderTimeoutError,
    ProviderUnavailableError,
)
from personal_tech_brief.services.errors import SemanticValidationError

_TYPE_PREFIX = "urn:tih:intelligence:error:"


def _problem_response(problem: ProblemDetails) -> JSONResponse:
    """Serialize a problem document as an ``application/problem+json`` response."""

    return JSONResponse(
        status_code=problem.status,
        media_type=PROBLEM_JSON_MEDIA_TYPE,
        content=problem.model_dump(by_alias=True, exclude_none=True),
    )


def _correlation_from_body(body: Any) -> str | None:
    """Best-effort extraction of ``correlationId`` from an invalid request body."""

    if isinstance(body, dict):
        value = body.get("correlationId")
        if isinstance(value, str):
            return value
    return None


async def _handle_validation_error(_: Request, exc: Exception) -> JSONResponse:
    """Map boundary/body validation failures to HTTP 400 (not FastAPI's 422)."""

    assert isinstance(exc, RequestValidationError)
    problem = ProblemDetails(
        type=f"{_TYPE_PREFIX}validation",
        title="Request validation failed",
        status=status.HTTP_400_BAD_REQUEST,
        detail="The request body failed boundary validation.",
        correlation_id=_correlation_from_body(exc.body),
    )
    return _problem_response(problem)


async def _handle_semantic_error(_: Request, exc: Exception) -> JSONResponse:
    """Map semantic validation failures to HTTP 422."""

    assert isinstance(exc, SemanticValidationError)
    problem = ProblemDetails(
        type=f"{_TYPE_PREFIX}unprocessable",
        title="Request is semantically unprocessable",
        status=status.HTTP_422_UNPROCESSABLE_CONTENT,
        detail=str(exc),
    )
    return _problem_response(problem)


async def _handle_provider_timeout(_: Request, exc: Exception) -> JSONResponse:
    """Map provider timeouts to HTTP 504."""

    return _problem_response(
        ProblemDetails(
            type=f"{_TYPE_PREFIX}provider-timeout",
            title="Provider timed out",
            status=status.HTTP_504_GATEWAY_TIMEOUT,
        )
    )


async def _handle_provider_unavailable(_: Request, exc: Exception) -> JSONResponse:
    """Map provider unavailability to HTTP 503."""

    return _problem_response(
        ProblemDetails(
            type=f"{_TYPE_PREFIX}provider-unavailable",
            title="Provider unavailable",
            status=status.HTTP_503_SERVICE_UNAVAILABLE,
        )
    )


async def _handle_provider_failure(_: Request, exc: Exception) -> JSONResponse:
    """Map provider failures and invalid structured output to HTTP 502."""

    return _problem_response(
        ProblemDetails(
            type=f"{_TYPE_PREFIX}provider-failure",
            title="Provider failed",
            status=status.HTTP_502_BAD_GATEWAY,
        )
    )


def register_exception_handlers(app: FastAPI) -> None:
    """Register the problem+json handlers on the application.

    ``ProviderResponseInvalidError`` is registered before its base so that invalid
    structured output maps to 502 rather than the generic failure handler.
    """

    app.add_exception_handler(RequestValidationError, _handle_validation_error)
    app.add_exception_handler(SemanticValidationError, _handle_semantic_error)
    app.add_exception_handler(ProviderTimeoutError, _handle_provider_timeout)
    app.add_exception_handler(ProviderUnavailableError, _handle_provider_unavailable)
    app.add_exception_handler(ProviderResponseInvalidError, _handle_provider_failure)
    # Base provider failure (and any other ProviderError) → 502.
    from personal_tech_brief.providers.base import ProviderError

    app.add_exception_handler(ProviderError, _handle_provider_failure)
