#!/usr/bin/env bash
# Prints VERSION and CHANNEL (dev | rc | release) for the current git context.
# Usage in a workflow:  .github/scripts/version.sh >> "$GITHUB_OUTPUT"
set -euo pipefail

if [[ "${GITHUB_REF_TYPE:-}" == "tag" ]]; then
  VERSION="${GITHUB_REF_NAME#v}"
  if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$ ]]; then
    echo "Tag '${GITHUB_REF_NAME}' is not vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rc.N" >&2
    exit 1
  fi
  if [[ "$VERSION" == *-rc.* ]]; then CHANNEL=rc; else CHANNEL=release; fi
else
  # development build: the NEXT minor version, as a pre-release, numbered by the workflow run
  LAST_TAG="$(git describe --tags --abbrev=0 --match 'v[0-9]*' 2>/dev/null || echo v0.0.0)"
  LAST="${LAST_TAG#v}"
  LAST="${LAST%%-*}"                                  # strip a possible -rc.N
  IFS=. read -r MAJOR MINOR _ <<< "$LAST"
  VERSION="${MAJOR}.$((MINOR + 1)).0-dev.${GITHUB_RUN_NUMBER:-0}"
  CHANNEL=dev
fi

echo "VERSION=${VERSION}"
echo "CHANNEL=${CHANNEL}"
