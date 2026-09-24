"""RFC 9457 ``application/problem+json`` response model."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

PROBLEM_JSON_MEDIA_TYPE = "application/problem+json"


class ProblemDetails(BaseModel):
    """A minimal RFC 9457 problem document with a correlation extension member.

    Only non-sensitive members are ever populated: titles are static and generic,
    and no provider, credential, prompt, or raw-content detail is included. The
    ``correlation_id`` extension member is serialized as ``correlationId``.
    """

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")

    type: str = "about:blank"
    title: str
    status: int
    detail: str | None = None
    correlation_id: str | None = None
