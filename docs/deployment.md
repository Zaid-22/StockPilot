# Running and deploying StockPilot

The application serves React and `/api` from the same HTTPS origin. It requires SQL Server. A static frontend deployment alone cannot run this .NET application or its cookie sessions.

## Local development

Follow the root README for .NET, Node, SQL Server, user-secrets, database initialization, and the two development processes. The frontend uses JavaScript only. On Apple Silicon, use a reachable external SQL Server or hosted SQL Server; no unsupported database emulation is required by this repository.

## Production container

`Dockerfile` builds the frontend and .NET application in separate stages, then runs the combined application as the .NET image's non-root `app` user. Certificates and database credentials are supplied at runtime. The default Compose binding is loopback only.

1. Copy `.env.example` to `.env`. Replace every placeholder, including the **absolute** certificate path. Use a runtime database account with only the required application permissions. `localhost` inside the container means the container itself; supply a database hostname reachable from the container.
2. For a local HTTPS check, create an encrypted development certificate. Use the same password in `.env`:

   ```bash
   mkdir -p certificates
   dotnet dev-certs https --export-path certificates/stockpilot.pfx --password 'REPLACE_CERT_PASSWORD'
   ```

   Trust the development certificate through `dotnet dev-certs https --trust` if you want your browser to trust localhost. Public hosting requires a certificate valid for its domain. The container's `app` user must be able to read the mounted certificate. Certificates are excluded from Git and the image build context.
3. Build the image:

   ```bash
   docker compose build
   ```

4. Initialize the target database once using a separate migration-capable connection. Set these variables in your terminal or deployment secret store, then run:

   ```bash
   # Set ConnectionStrings__StockPilot to the migration account's connection.
   # Set Bootstrap__AdminEmail and Bootstrap__AdminPassword to your own values.
   docker compose run --rm \
     -e ConnectionStrings__StockPilot \
     -e Bootstrap__AdminEmail \
     -e Bootstrap__AdminPassword \
     app --initialize
   unset ConnectionStrings__StockPilot Bootstrap__AdminEmail Bootstrap__AdminPassword
   ```

   The bootstrap email must be new or already an administrator. Bootstrap does not reset passwords or silently promote an existing user. Do not run migrations concurrently. Startup never applies migrations automatically.
5. Start the runtime service:

   ```bash
   docker compose up -d app
   docker compose logs --tail=100 app
   ```

   Open `https://localhost:8443`. Verify `/api/health/ready`, login, and one complete business workflow. `docker compose down` stops the service while preserving its Data Protection volume. Do not use `down --volumes` unless intentionally discarding that key ring.

## Public hosting

A host and database have **not** been provisioned. Choose a host capable of running this Linux .NET container and reaching SQL Server. Supply the database connection, a domain-specific `AllowedHosts`, a valid HTTPS certificate, and persistent Data Protection storage. Review firewall and certificate renewal configuration for that host before exposing a public port.

The supplied image terminates HTTPS in Kestrel. If a chosen host terminates TLS at a reverse proxy instead, add that host's exact trusted proxy/forwarded-header configuration before deploying; the application currently does not trust arbitrary proxy headers. Do not disable secure cookies to work around a proxy configuration problem.

Back up SQL Server and the Data Protection key volume. The key directory must only be writable by the app account; filesystem persistence alone does not encrypt the keys at rest. Test database restore and key restoration before relying on the deployment. Keep bootstrap and migration credentials out of the running app.

## Release verification

Use dedicated, disposable databases:

```bash
export TEST_SQLSERVER_CONNECTION_STRING='YOUR_TEST_SQL_SERVER_WITH_CREATE_DROP_PERMISSIONS'
export ConnectionStrings__StockPilot='YOUR_DISPOSABLE_SMOKE_DATABASE_CONNECTION'
export Bootstrap__AdminEmail='YOUR_SMOKE_ADMIN_EMAIL'
export Bootstrap__AdminPassword='YOUR_SMOKE_ADMIN_PASSWORD'
RUN_PUBLISHED_SMOKE=1 bash scripts/verify.sh
```

The test suite applies actual migrations to unique databases and deletes those databases afterward. It also runs the SQL Server-only parallel-shipment test. The published HTTPS smoke script checks secure sessions, catalog writes, stock adjustments, purchase receiving, sales shipping, idempotent retries, report access, and SPA routes. Its named smoke database remains available for inspection; remove it separately when finished.

The GitHub Actions workflow runs these checks and builds the container. It needs a GitHub remote and a pushed commit to execute. Passing the local SQLite suite is not a substitute for the SQL Server and published HTTPS checks.

Generate a reviewable SQL deployment script, when needed:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent \
  --project backend/src/StockPilot.Infrastructure \
  --startup-project backend/src/StockPilot.Api \
  --output artifacts/migrations.sql
```

Back up before schema changes. Use forward fixes for production issues; do not automatically drop tables or run destructive down migrations to revert application code.

Reference: [Microsoft's ASP.NET Core container HTTPS guidance](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-10.0).
