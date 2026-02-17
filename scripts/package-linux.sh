#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/src/StarRuptureSaveManager.Avalonia/StarRuptureSaveManager.Avalonia.csproj"
DIST_DIR="$ROOT_DIR/dist"
PUBLISH_ROOT="$DIST_DIR/publish"

WITH_ARM64=0
if [[ "${1:-}" == "--with-arm64" ]]; then
  WITH_ARM64=1
fi

RIDS=("linux-x64")
if [[ "$WITH_ARM64" -eq 1 ]]; then
  RIDS+=("linux-arm64")
fi

VERSION="dev"
if [[ -f "$ROOT_DIR/StarRuptureSaveManager.csproj" ]]; then
  VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' "$ROOT_DIR/StarRuptureSaveManager.csproj" || echo "dev")"
fi

mkdir -p "$DIST_DIR"
rm -rf "$PUBLISH_ROOT"
mkdir -p "$PUBLISH_ROOT"

echo "Packaging StarRupture Save Manager Avalonia $VERSION"
echo "RIDs: ${RIDS[*]}"

for rid in "${RIDS[@]}"; do
  out_dir="$PUBLISH_ROOT/$rid"
  rm -rf "$out_dir"
  mkdir -p "$out_dir"

  echo "Publishing $rid..."
  AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o "$out_dir"

  artifact="$DIST_DIR/StarRuptureSaveManager-Avalonia-${VERSION}-${rid}.tar.gz"
  tar -czf "$artifact" -C "$out_dir" .
  echo "Created $artifact"
done

echo "Done. Artifacts are in $DIST_DIR"
