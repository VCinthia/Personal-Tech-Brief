"""Domain-level service errors distinct from boundary validation."""

from __future__ import annotations


class SemanticValidationError(Exception):
    """A request is syntactically valid but semantically unprocessable (HTTP 422)."""
