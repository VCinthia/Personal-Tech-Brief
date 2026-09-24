"""Shared Pydantic base configuration for the wire contract."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    """Base model whose JSON representation uses ``camelCase`` field names.

    Fields are declared in idiomatic ``snake_case`` in Python and serialized to the
    ``camelCase`` casing frozen in the contract. Unknown fields are rejected so that
    a malformed body fails boundary validation rather than being silently accepted.
    """

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        extra="forbid",
    )
