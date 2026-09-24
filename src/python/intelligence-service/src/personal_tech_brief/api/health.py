"""Bootstrap health probes shared with the .NET service convention."""

from typing import Annotated, cast

from fastapi import APIRouter, Depends, Request, status

from personal_tech_brief.models.health import HealthStatus
from personal_tech_brief.settings import Settings

router = APIRouter(tags=["health"])


def get_settings(request: Request) -> Settings:
    """Retrieve the configuration validated during application initialization."""

    return cast(Settings, request.app.state.settings)


@router.get("/health/live", response_model=HealthStatus, status_code=status.HTTP_200_OK)
async def live() -> HealthStatus:
    """Confirm that the process can serve HTTP without external dependency checks."""

    return HealthStatus()


@router.get("/health/ready", response_model=HealthStatus, status_code=status.HTTP_200_OK)
async def ready(_: Annotated[Settings, Depends(get_settings)]) -> HealthStatus:
    """Confirm configuration was initialized without contacting LLMs or databases."""

    return HealthStatus()
