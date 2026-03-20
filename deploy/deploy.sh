#!/usr/bin/env bash

set -euo pipefail

DEPLOY_PATH="${DEPLOY_PATH:-$(pwd)}"
COMPOSE_FILE="${COMPOSE_FILE:-${DEPLOY_PATH}/docker-compose.yml}"
LOGIN_URL="${LOGIN_URL:-http://127.0.0.1:${IDP_HTTP_PORT:-8080}/login}"

: "${GHCR_USERNAME:?GHCR_USERNAME is required}"
: "${GHCR_PAT:?GHCR_PAT is required}"
: "${GHCR_IMAGE_PREFIX:?GHCR_IMAGE_PREFIX is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

cd "${DEPLOY_PATH}"

echo "Logging into GHCR..."
printf '%s' "${GHCR_PAT}" | docker login ghcr.io -u "${GHCR_USERNAME}" --password-stdin

export GHCR_IMAGE_PREFIX IMAGE_TAG

echo "Pulling images..."
docker compose -f "${COMPOSE_FILE}" pull

echo "Starting infrastructure services..."
docker compose -f "${COMPOSE_FILE}" up -d postgres redis

echo "Running database migrator..."
docker compose -f "${COMPOSE_FILE}" run --rm idp-migrator

echo "Starting IDP service..."
docker compose -f "${COMPOSE_FILE}" up -d idp

echo "Waiting for IDP login page..."
for attempt in $(seq 1 24); do
    if curl --fail --silent --show-error "${LOGIN_URL}" >/dev/null; then
        echo "IDP is reachable at ${LOGIN_URL}."
        docker image prune -f >/dev/null || true
        exit 0
    fi

    sleep 5
done

echo "IDP did not become ready in time." >&2
docker compose -f "${COMPOSE_FILE}" ps >&2 || true
docker compose -f "${COMPOSE_FILE}" logs --tail=200 idp >&2 || true
exit 1
