#!/usr/bin/env bash
# Runs once after the devcontainer is created.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p dist/plugins media .dev/jellyfin/config .dev/jellyfin/cache

# Docker creates the NuGet volume's mount-point parent (~/.nuget) root-owned, and the shared
# named volume itself is root-owned on first mount. NuGet needs to write ~/.nuget/NuGet/NuGet.Config
# next to the volume, so check both the parent and the volume before handing them to the dev user.
mkdir -p "$HOME/.nuget"
if [ ! -w "$HOME/.nuget" ] || [ ! -w "$HOME/.nuget/packages" ]; then
  sudo chown -R "$(id -u):$(id -g)" "$HOME/.nuget"
fi

echo "dotnet SDKs:"
dotnet --list-sdks

if [ ! -f media/.generated ]; then
  echo "Generating synthetic test media (one-time)..."
  bash scripts/make-test-media.sh
fi

dotnet restore Jellyfin.Plugin.EpisodeContinuity.sln --nologo -v quiet

echo
echo "Jellyfin: http://localhost:8097 (first run: complete the setup wizard, add /media as a Shows library)"
echo "Deploy plugin: scripts/deploy.sh"
