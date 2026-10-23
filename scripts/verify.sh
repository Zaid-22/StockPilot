#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

dotnet restore backend/StockPilot.sln --locked-mode
dotnet tool restore
npm ci --prefix frontend
dotnet build backend/StockPilot.sln -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
if [[ "${TEST_RUNNER:-standard}" == "in-process" ]]; then
  dotnet backend/tests/StockPilot.TestRunner/bin/Release/net10.0/StockPilot.TestRunner.dll
else
  dotnet test backend/StockPilot.sln -c Release --no-build --logger trx
fi
dotnet ef migrations has-pending-model-changes --project backend/src/StockPilot.Infrastructure --startup-project backend/src/StockPilot.Api --configuration Release --no-build
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
mkdir -p backend/src/StockPilot.Api/wwwroot
cp -R frontend/dist/. backend/src/StockPilot.Api/wwwroot/
dotnet publish backend/src/StockPilot.Api -c Release --no-restore -p:UseSharedCompilation=false -o artifacts/app

if [[ -z "${TEST_SQLSERVER_CONNECTION_STRING:-}" ]]; then
  printf '%s\n' 'SQLite tests passed. SQL Server verification was not requested; set TEST_SQLSERVER_CONNECTION_STRING to run it.'
fi
if [[ "${RUN_PUBLISHED_SMOKE:-0}" == "1" ]]; then
  bash scripts/smoke-published.sh
else
  printf '%s\n' 'Published build prepared. Set RUN_PUBLISHED_SMOKE=1 and the documented smoke variables to test HTTPS.'
fi
