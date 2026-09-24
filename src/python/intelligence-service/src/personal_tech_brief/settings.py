"""Environment-backed configuration for the intelligence service."""

from enum import StrEnum

from pydantic import Field, SecretStr, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Environment(StrEnum):
    """Deployment environments supported by the service configuration."""

    DEVELOPMENT = "development"
    TEST = "test"
    PRODUCTION = "production"


class Settings(BaseSettings):
    """Validated, non-secret-by-default service configuration.

    Environment values use the ``PERSONAL_TECH_BRIEF_`` prefix. The service is
    stateless and never holds database or persistence configuration. Model-provider
    settings are optional: when the Azure OpenAI trio (endpoint, deployment, key) is
    absent the deterministic fake provider is used, so CI and local development never
    require paid or live LLM access.
    """

    model_config = SettingsConfigDict(env_prefix="PERSONAL_TECH_BRIEF_", extra="ignore")

    service_name: str = Field(default="personal-tech-brief-api", min_length=1)
    environment: Environment = Environment.DEVELOPMENT
    host: str = Field(default="127.0.0.1", min_length=1, max_length=253)
    port: int = Field(default=8001, ge=1, le=65535)

    # Content-size bounding applied deterministically before any provider call
    # (spec §20). Combined analyze text is truncated to this many characters.
    analyze_content_char_budget: int = Field(default=20_000, ge=1, le=20_000)

    # Provider timeout and retry policy (spec §20). These are configuration, never
    # hardcoded, and are surfaced on the request trace scope. The overall
    # processor->Python request budget (30s) is enforced by the calling .NET client,
    # not by a server-side umbrella; the service bounds each provider attempt and the
    # number of attempts instead. Spec §20 allows up to 3 attempts, i.e. 2 retries.
    llm_per_attempt_timeout_seconds: float = Field(default=45.0, gt=0.0)
    llm_max_retries: int = Field(default=2, ge=0, le=10)
    llm_backoff_base_seconds: float = Field(default=0.5, ge=0.0)
    llm_backoff_max_seconds: float = Field(default=8.0, ge=0.0)
    llm_backoff_jitter_seconds: float = Field(default=0.25, ge=0.0)

    # Generation (final-brief) provider budgets (spec §20 final generation, 60s per
    # attempt). The expensive generate step has a larger per-attempt budget than
    # analyze; the shared backoff/jitter policy above governs the delay between the
    # up-to-3 attempts. Each source excerpt is truncated to the char budget before the
    # provider call so total provider input is bounded regardless of caller input size.
    generate_llm_per_attempt_timeout_seconds: float = Field(default=60.0, gt=0.0)
    generate_llm_max_retries: int = Field(default=2, ge=0, le=10)
    generate_source_excerpt_char_budget: int = Field(default=2_000, ge=1, le=4_000)

    # Optional Azure OpenAI provider configuration. Presence of the full trio
    # selects the Azure provider; the key is a secret and is never logged.
    azure_openai_endpoint: str | None = Field(default=None)
    azure_openai_deployment: str | None = Field(default=None)
    azure_openai_api_version: str = Field(default="2024-10-21", min_length=1)
    azure_openai_api_key: SecretStr | None = Field(default=None)

    @field_validator("azure_openai_endpoint")
    @classmethod
    def _require_https_endpoint(cls, value: str | None) -> str | None:
        """Reject a non-HTTPS provider endpoint (defense against a misconfigured URL)."""

        if value is not None and not value.startswith("https://"):
            raise ValueError("azure_openai_endpoint must be an absolute https URL")
        return value

    @property
    def azure_openai_configured(self) -> bool:
        """Report whether the Azure OpenAI provider trio is fully configured."""

        return bool(
            self.azure_openai_endpoint
            and self.azure_openai_deployment
            and self.azure_openai_api_key
        )
