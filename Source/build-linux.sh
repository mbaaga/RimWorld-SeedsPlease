#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore SeedsPleaseLiteRedux.csproj
dotnet build SeedsPleaseLiteRedux.csproj --configuration Release --no-restore
