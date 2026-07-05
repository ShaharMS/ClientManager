#!/usr/bin/env bash
set -euo pipefail

TAG="${1:?Usage: build-release-bundles.sh <tag>}"
REPO_NAME="${REPO_NAME:-ClientManager}"
OUT_DIR="${OUT_DIR:-.release-bundles/${TAG}}"

mkdir -p "$OUT_DIR"

git rev-parse --verify "refs/tags/${TAG}^{commit}" >/dev/null

PREV=""
while IFS= read -r candidate; do
  if [[ "$candidate" == "$TAG" ]]; then
    break
  fi
  PREV="$candidate"
done < <(git tag -l --sort=v:refname)

FULL="${OUT_DIR}/${REPO_NAME}-${TAG}.full.bundle"
git bundle create "$FULL" "refs/tags/${TAG}"
git bundle verify "$FULL" >/dev/null

UPLOAD_FILES=("$FULL")

if [[ -n "$PREV" ]]; then
  INC="${OUT_DIR}/${REPO_NAME}-${PREV}-to-${TAG}.bundle"
  git bundle create "$INC" "refs/tags/${TAG}" --not "refs/tags/${PREV}"
  git bundle verify "$INC" >/dev/null
  UPLOAD_FILES+=("$INC")
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
  [[ -n "$PREV" ]] && printf 'Previous tag: %s\n' "$PREV"
fi
