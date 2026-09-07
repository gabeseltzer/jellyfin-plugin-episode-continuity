#!/usr/bin/env bash
# Package the built plugin into artifacts/<name>_<version>.zip plus a manifest.json usable as a
# Jellyfin plugin repository (point the Dashboard > Plugins > Repositories at the raw manifest URL).
set -euo pipefail
cd "$(dirname "$0")/.."
CONFIG=${1:-Release}
PROJECT=Jellyfin.Plugin.EpisodeContinuity
VERSION=$(grep -oP '(?<=<AssemblyVersion>)[^<]+' "$PROJECT/$PROJECT.csproj")
ABI=$(grep -oP '(?<=targetAbi: ")[^"]+' build.yaml)
REPO_URL=${REPO_URL:-https://github.com/gabeseltzer/jellyfin-plugin-episode-continuity}
OUT=artifacts; ZIP="episodecontinuity_${VERSION}.zip"
DLL="$PROJECT/bin/$CONFIG/net9.0/$PROJECT.dll"
[ -f "$DLL" ] || dotnet build "$PROJECT/$PROJECT.csproj" -c "$CONFIG" --nologo -v quiet

rm -rf "$OUT" && mkdir -p "$OUT/stage"
cp "$DLL" "$OUT/stage/"
sed "s/__VERSION__/$VERSION/g" meta.json > "$OUT/stage/meta.json"
(cd "$OUT/stage" && zip -q "../$ZIP" ./*)
rm -rf "$OUT/stage"
CHECKSUM=$(md5sum "$OUT/$ZIP" | cut -d' ' -f1)
TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)
jq -n --arg v "$VERSION" --arg abi "$ABI" --arg sum "$CHECKSUM" --arg ts "$TIMESTAMP" \
  --arg url "$REPO_URL/releases/download/v${VERSION%.0}/$ZIP" '
[{
  guid: "3f8a1c6e-9d2b-4a7f-b5e4-c1d0e8f7a2b9",
  name: "Episode Continuity",
  description: "TODO",
  overview: "TODO",
  owner: "gabeseltzer",
  category: "General",
  imageUrl: "",
  versions: [{
    version: $v, changelog: "Initial release.", targetAbi: $abi, sourceUrl: $url, checksum: $sum, timestamp: $ts
  }]
}]' > "$OUT/manifest.json"
echo "Packaged $OUT/$ZIP ($CHECKSUM)"; ls -la "$OUT"
