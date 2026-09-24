"""Integration-level checks for the bootstrap health contract."""

import pytest
import uvicorn
from fastapi import FastAPI
from httpx import ASGITransport, AsyncClient
from pydantic import ValidationError

import personal_tech_brief.api.app as app_module
from personal_tech_brief.api.app import create_app
from personal_tech_brief.settings import Settings


@pytest.fixture
def app() -> FastAPI:
    """Provide a stateless API instance for in-process HTTP checks."""

    return create_app()


@pytest.fixture
def anyio_backend() -> str:
    """Use the asyncio backend supported by the bootstrap dependencies."""

    return "asyncio"


@pytest.mark.anyio
async def test_liveness_returns_a_non_sensitive_success_status(app: FastAPI) -> None:
    """Liveness requires no external service, including a live model provider."""

    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://testserver"
    ) as client:
        response = await client.get("/health/live")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


@pytest.mark.anyio
async def test_readiness_returns_a_non_sensitive_success_status(app: FastAPI) -> None:
    """Readiness only verifies already-initialized bootstrap configuration."""

    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://testserver"
    ) as client:
        response = await client.get("/health/ready")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_settings_reject_an_unknown_environment() -> None:
    """Initialization rejects unsupported environment values before serving traffic."""

    with pytest.raises(ValidationError):
        Settings.model_validate({"environment": "unsupported"})


def test_settings_load_host_and_port_from_environment(monkeypatch: pytest.MonkeyPatch) -> None:
    """The container host and port remain non-secret environment-backed settings."""

    monkeypatch.setenv("PERSONAL_TECH_BRIEF_HOST", "0.0.0.0")
    monkeypatch.setenv("PERSONAL_TECH_BRIEF_PORT", "9000")

    settings = Settings()

    assert settings.host == "0.0.0.0"
    assert settings.port == 9000


def test_main_binds_uvicorn_to_application_settings(monkeypatch: pytest.MonkeyPatch) -> None:
    """The executable honors the validated host and port configuration."""

    configured_app = create_app(Settings(host="0.0.0.0", port=9000))
    calls: list[tuple[FastAPI, str, int]] = []

    def fake_run(application: FastAPI, *, host: str, port: int) -> None:
        calls.append((application, host, port))

    monkeypatch.setattr(app_module, "app", configured_app)
    monkeypatch.setattr(uvicorn, "run", fake_run)

    app_module.main()

    assert calls == [(configured_app, "0.0.0.0", 9000)]
