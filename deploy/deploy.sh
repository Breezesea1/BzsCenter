#!/usr/bin/env bash

set -euo pipefail

DEPLOY_PATH="${DEPLOY_PATH:-$(pwd)}"
COMPOSE_FILES="${COMPOSE_FILES:-${DEPLOY_PATH}/docker-compose.yml}"
LOGIN_URL="${LOGIN_URL:-http://127.0.0.1:${IDP_HTTP_PORT:-8080}/login}"

: "${GHCR_USERNAME:?GHCR_USERNAME is required}"
: "${GHCR_PAT:?GHCR_PAT is required}"
: "${GHCR_IMAGE_PREFIX:?GHCR_IMAGE_PREFIX is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

cd "${DEPLOY_PATH}"

compose_args=()
for file in ${COMPOSE_FILES}; do
    compose_args+=( -f "$file" )
done

echo "Logging into GHCR..."
printf '%s' "${GHCR_PAT}" | docker login ghcr.io -u "${GHCR_USERNAME}" --password-stdin

export GHCR_IMAGE_PREFIX IMAGE_TAG

echo "Pulling images..."
docker compose "${compose_args[@]}" pull

echo "Starting IDP stack (migrator runs before idp)..."
docker compose "${compose_args[@]}" up -d idp

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
docker compose "${compose_args[@]}" ps >&2 || true
docker compose "${compose_args[@]}" logs --tail=200 idp >&2 || true
exit 1
