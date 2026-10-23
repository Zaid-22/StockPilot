# StockPilot

Inventory, purchasing, and sales management with ASP.NET Core and **React JavaScript** (`.js` / `.jsx`).

Phase 1 implements Identity cookie sessions, Admin/Staff/Viewer roles, CSRF protection, login/logout, restored sessions, an admin-only account directory, error handling, tests, and CI. Inventory workflows start in subsequent phases. The overview labels future features as planned.

## Architecture

One backend deployment and one SQL Server database:

| Project | Responsibility |
| --- | --- |
| `StockPilot.Domain` | Business concepts and role names |
| `StockPilot.Application` | Use-case contracts and DTOs |
| `StockPilot.Infrastructure` | EF Core, migrations, Identity |
| `StockPilot.Api` | HTTP endpoints, authorization, composition |

Frontend: React, Vite, Material UI, React Router, TanStack Query, React Hook Form, and Zod. Application dependencies point inward; the API wires Infrastructure into Application interfaces.

## Local setup

Requirements: .NET 10 SDK compatible with `global.json`, Node.js 24, and a reachable SQL Server instance. On Apple Silicon, use a supported external SQL Server host or hosted database. SQLite is only a fast authentication-test database.

From the repository root:

```bash
dotnet restore backend/StockPilot.sln --locked-mode
dotnet tool restore
npm ci --prefix frontend
```

Set real development credentials in place of the placeholders. No default account is included:

```bash
dotnet user-secrets set 'ConnectionStrings:StockPilot' 'YOUR_SQL_SERVER_CONNECTION_STRING' --project backend/src/StockPilot.Api
dotnet user-secrets set 'Bootstrap:AdminEmail' 'YOUR_ADMIN_EMAIL' --project backend/src/StockPilot.Api
dotnet user-secrets set 'Bootstrap:AdminPassword' 'YOUR_UNIQUE_ADMIN_PASSWORD' --project backend/src/StockPilot.Api
```

Use at least 12 characters with upper/lowercase letters, a number, and a symbol. Bootstrap applies migrations, creates the roles, and creates the first administrator:

```bash
dotnet run --project backend/src/StockPilot.Api -- --initialize
dotnet user-secrets remove 'Bootstrap:AdminPassword' --project backend/src/StockPilot.Api
dotnet run --project backend/src/StockPilot.Api
```

In a second terminal:

```bash
npm run dev --prefix frontend
```

Open `http://localhost:5173`. Vite proxies `/api` to `http://localhost:5080`, preserving same-origin cookies. Production serves React and the API from the same HTTPS origin.

Bootstrap never resets an existing admin password or promotes an existing non-admin. Run initialization serially. Public signup and account creation/editing screens are outside this phase; the directory is read-only.

## Authentication and API

| Endpoint | Access / behavior |
| --- | --- |
| `GET /api/auth/csrf` | Anonymous; antiforgery cookie and request token |
| `POST /api/auth/login` | CSRF token and credentials required |
| `GET /api/auth/me` | Signed-in user's identity and roles |
| `POST /api/auth/logout` | Signed-in user and a fresh CSRF token |
| `GET /api/admin/users` | Admin only; first 50 accounts by email |
| `GET /api/health/live` | Anonymous liveness check |
| `GET /api/health/ready` | Anonymous database connectivity check |

Sessions use HttpOnly, SameSite=Strict cookies, Secure in production, with an eight-hour absolute lifetime and no sliding renewal. The frontend stores no authentication token in localStorage. It fetches a fresh CSRF token before each mutation because login changes the token's identity binding. All controller mutations require antiforgery validation.

Anonymous requests return `401`; insufficient privileges return `403`, without HTML redirects. Login failures do not reveal whether an account exists. Login allows 20 requests per IP per minute; five failed password attempts lock an account for 15 minutes. Session expiry and logout clear private query data. Ordinary logout removes this browser's cookie; immediate revocation of a copied cookie would require additional session tracking.

Staff and Viewer have authenticated workspace access. Their purchasing/sales permissions will be added with those features. The Admin directory proves server-side role enforcement.

OpenAPI JSON: `/openapi/v1.json` in Development. Console logs use JSON; errors use Problem Details; API responses are not cached.

## Verification

```bash
dotnet test backend/StockPilot.sln -c Release
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
```

Backend tests use isolated SQLite by default. Set `TEST_SQLSERVER_CONNECTION_STRING` to a dedicated test SQL Server to test real migrations and behavior. Each test creates and deletes a unique `StockPilotTest_<guid>` database, requiring create/drop database permissions. Never point tests at a production server.

GitHub Actions runs backend tests against SQL Server 2022, checks for a missing migration, checks the frontend, publishes the application, and tests HTTPS login/logout against the published build. CI database credentials are disposable and scoped to its isolated service.

See [docs/phase-1-status.md](docs/phase-1-status.md) for actual local evidence and outstanding checks. A configured workflow is not a completed remote run.

## Publishing

```bash
npm run build --prefix frontend
mkdir -p backend/src/StockPilot.Api/wwwroot
cp -R frontend/dist/. backend/src/StockPilot.Api/wwwroot/
dotnet publish backend/src/StockPilot.Api -c Release -o artifacts/app
```

On the host set `ConnectionStrings__StockPilot`, `DataProtection__KeyPath`, and `AllowedHosts`. The key directory must be persistent, writable only by the app account, and backed up with access controls. File persistence does not itself encrypt keys at rest. Supply HTTPS through Kestrel or a correctly configured trusted reverse proxy. Proxy deployments must configure trusted forwarded headers before HTTPS redirection; this starter does not trust an external proxy by default.

Apply migrations with a deployment account, not a permanently elevated runtime account. Set `Bootstrap__AdminEmail` and `Bootstrap__AdminPassword`, run `dotnet StockPilot.Api.dll --initialize`, then remove the bootstrap password from runtime configuration. Ordinary startup does not migrate the database.

For an isolated deployment check, set those three connection/bootstrap environment variables and run `bash scripts/smoke-published.sh` after publishing. It uses a temporary HTTPS certificate on loopback, tests secure cookies, and stops its process afterward. It initializes the database you specify: use a disposable database. External hosting has not been provisioned.

## Working agreement

Build one vertical feature at a time, keep JavaScript throughout the frontend, and commit logically after relevant checks. Once acceptance checks pass and blocking defects are fixed, move additional ideas to the backlog.

Next: Phase 2 catalog — products, categories, suppliers, customers, unique SKUs, search, pagination, and the first complete React-to-SQL business workflow.
