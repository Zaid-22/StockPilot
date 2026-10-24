#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
docker info >/dev/null
command -v openssl >/dev/null
if [[ ! -f .env.local ]]; then
  umask 077
  {
    printf 'LOCAL_DB_PASSWORD=Db-%s!\n' "$(openssl rand -hex 24)"
    printf 'LOCAL_ADMIN_EMAIL=admin@stockpilot.local\n'
    printf 'LOCAL_ADMIN_PASSWORD=Admin-%s!\n' "$(openssl rand -hex 18)"
  } > .env.local
fi
printf '%s\n' 'Starting the local development stack. Apple Silicon uses x86 emulation for SQL Server.'
docker compose --env-file .env.local -f compose.local.yaml up --build -d app
for attempt in {1..60}; do
  if curl --fail --silent http://127.0.0.1:5080/api/health/ready >/dev/null; then
    printf '%s\n' 'StockPilot is ready: http://127.0.0.1:5080/' 'Admin login is saved in .env.local (do not share or commit that file).' 'The Vite frontend on port 5173 can also use this backend.'
    exit 0
  fi
  sleep 1
done
printf '%s\n' 'Backend did not become ready. Inspect: docker compose --env-file .env.local -f compose.local.yaml logs --tail=100' >&2
exit 1
