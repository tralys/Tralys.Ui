#!/usr/bin/env bash
set -euo pipefail

# Usage: install-sdk.sh [install dir]   (default: the repo's .dotnet, which global.json here searches first)
# Installs the .NET SDK this gallery pins, and its wasm-tools workload from that SDK's own daily feed.
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dir="$(realpath -m "${1:-$here/../../.dotnet}")"
version="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$here/global.json")"
feed="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet${version%%.*}/nuget/v3/index.json"

if [ ! -d "$dir/sdk/$version" ]; then
    installer="$(mktemp)"
    trap 'rm -f "$installer"' EXIT
    curl -fsSL -o "$installer" https://dot.net/v1/dotnet-install.sh
    bash "$installer" --version "$version" --install-dir "$dir" --no-path
fi

cd "$here"
DOTNET_ROOT="$dir" "$dir/dotnet" workload install wasm-tools --skip-manifest-update \
    --source "$feed" --source https://api.nuget.org/v3/index.json
echo "gallery SDK $version ready in $dir"
