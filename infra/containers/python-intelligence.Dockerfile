FROM python:3.14-slim@sha256:cad9a2c871761c413caa6fdd6441c783451e740a48aaeba60ae62a8b53525ef6

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    UV_LINK_MODE=copy

RUN python -m pip install --no-cache-dir "uv==0.12.13"

WORKDIR /app
COPY src/python/intelligence-service/pyproject.toml src/python/intelligence-service/uv.lock ./
RUN uv sync --locked --no-dev --no-install-project

COPY src/python/intelligence-service/ ./
RUN uv sync --locked --no-dev

ENV PATH="/app/.venv/bin:$PATH" \
    PERSONAL_TECH_BRIEF_HOST=0.0.0.0
EXPOSE 8001

CMD ["personal-tech-brief-api"]
