"""Shared pytest fixtures for the intelligence service test suite."""

from __future__ import annotations

import pytest


@pytest.fixture
def anyio_backend() -> str:
    """Use the asyncio backend supported by the service dependencies."""

    return "asyncio"
