"""Health-probe response models."""

from typing import Literal

from pydantic import BaseModel


class HealthStatus(BaseModel):
    """A non-sensitive successful health-probe response."""

    status: Literal["ok"] = "ok"
