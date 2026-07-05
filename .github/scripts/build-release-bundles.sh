#!/usr/bin/env bash
set -euo pipefail

TAG="${1:?Usage: build-release-bundles.sh <tag>}"
REPO_NAME="${REPO_NAME:-ClientManager}"
OUT_DIR="${OUT_DIR:-.release-bundles/${TAG}}"

mkdir -p "$OUT_DIR"

git rev-parse --verify "refs/tags/${TAG}^{commit}" >/dev/null

PREV=""
while IFS= read -r candidate; do
  [[ "$candidate" == "$TAG" ]] && continue
  if git merge-base --is-ancestor "refs/tags/${candidate}^{commit}" "refs/tags/${TAG}^{commit}"; then
    PREV="$candidate"
  fi
done < <(git tag -l --sort=v:refname)

FULL="${OUT_DIR}/${REPO_NAME}-${TAG}.full.bundle"
git bundle create "$FULL" "refs/tags/${TAG}"
git bundle verify "$FULL" >/dev/null

UPLOAD_FILES=("$FULL")

if [[ -n "$PREV" ]]; then
  COMMIT_COUNT="$(git rev-list --count "${PREV}..${TAG}")"
  if [[ "$COMMIT_COUNT" -gt 0 ]]; then
    INC="${OUT_DIR}/${REPO_NAME}-${PREV}-to-${TAG}.bundle"
    git bundle create "$INC" "refs/tags/${TAG}" --not "refs/tags/${PREV}"
    git bundle verify "$INC" >/dev/null
    UPLOAD_FILES+=("$INC")
  fi
fi

{
  echo "full=$FULL"
  echo "previous=$PREV"
  echo "upload_files<<EOF"
  printf '%s\n' "${UPLOAD_FILES[@]}"
  echo "EOF"
} >> "${GITHUB_OUTPUT:-/dev/null}"

if [[ -z "${GITHUB_OUTPUT:-}" ]]; then
  printf 'Built %s\n' "${UPLOAD_FILES[*]}"
  [[ -n "$PREV" ]] && printf 'Previous ancestor tag: %s\n' "$PREV"
fi
