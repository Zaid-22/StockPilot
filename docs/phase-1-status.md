# Phase 1 status — 23 October 2026

## Implemented

- Four backend layers and React JavaScript application.
- Identity cookie authentication, CSRF, fixed session expiration, lockout and login rate limiting.
- API authorization and Admin/Staff/Viewer roles.
- Login, logout, session restoration, protected navigation, read-only admin directory.
- Initial SQL Server migration and explicit administrator bootstrap.
- Authentication integration tests, JavaScript tests, GitHub Actions workflow.
- Publish instructions and production-mode HTTPS smoke checks for CI.

## Verification

- Phase 2 verification caught and fixed two Phase 1 defects: MVC's antiforgery filter services were missing, and the login record's validation attributes targeted properties instead of constructor parameters.
- All 16 authentication tests passed through the in-process xUnit runner, alongside 15 catalog tests. The suite uses the actual application HTTP pipeline and isolated SQLite databases.
- All 20 frontend tests passed, including the original 13 Phase 1 tests. ESLint and production build passed.
- Backend Release build passed with zero warnings/errors; the combined application published successfully. SQL Server migrations were generated and the model consistency check passed. The local global EF tool is 10.0.10; the checked-in tool manifest pins 10.0.13 for restored environments.
- The normal `dotnet test` runner cannot open its required local communication socket inside this execution sandbox. The new in-process runner executes the same xUnit suite without that listener. The standard runner remains configured in CI.
- Dependencies restored from the existing cache using a command-line `NuGetAudit=false` override because the vulnerability endpoint was unreachable. This override is not in project files or CI; a fresh online audit is required.
- Docker is now running, but this session is denied access to its socket. No reachable test SQL Server connection was supplied, so SQL Server integration tests have not run locally.
- CI and the published HTTPS check are configured but have not run remotely. No GitHub remote is configured and no live deployment is claimed.

Authentication behavior now has passing local test evidence. SQL Server verification, the standard CI test run, and the published HTTPS smoke check remain pending. See [Phase 2 status](phase-2-status.md) and the README for the prepared verification commands.
