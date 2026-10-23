#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
smoke_tmp="$(mktemp -d)"
app_pid=''
cleanup() {
  if [[ -n "$app_pid" ]]; then kill "$app_pid" 2>/dev/null || true; wait "$app_pid" 2>/dev/null || true; fi
  rm -rf "$smoke_tmp"
}
trap cleanup EXIT
: "${ConnectionStrings__StockPilot:?Set the isolated smoke database connection}"
: "${Bootstrap__AdminEmail:?Set the smoke admin email}"
: "${Bootstrap__AdminPassword:?Set the smoke admin password}"
export ASPNETCORE_ENVIRONMENT=Production
export DataProtection__KeyPath="$smoke_tmp/keys"
mkdir -p "$DataProtection__KeyPath"
export ASPNETCORE_URLS=https://127.0.0.1:5443
export ASPNETCORE_Kestrel__Certificates__Default__Path="$smoke_tmp/smoke.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password=Temporary-smoke-certificate
dotnet dev-certs https --export-path "$ASPNETCORE_Kestrel__Certificates__Default__Path" --password "$ASPNETCORE_Kestrel__Certificates__Default__Password"
cd "$root/artifacts/app"
dotnet StockPilot.Api.dll --initialize
# Prove bootstrap can be repeated without duplicating the administrator.
dotnet StockPilot.Api.dll --initialize
dotnet StockPilot.Api.dll > "$root/artifacts/smoke.log" 2>&1 &
app_pid=$!
for attempt in {1..30}; do
  if curl --insecure --fail --silent https://127.0.0.1:5443/api/health/ready > /dev/null; then break; fi
  if ! kill -0 "$app_pid" 2>/dev/null; then cat "$root/artifacts/smoke.log"; exit 1; fi
  sleep 1
done
python3 "$root/scripts/smoke-http.py"
