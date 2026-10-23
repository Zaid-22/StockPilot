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

- Frontend: 13 tests passed; ESLint and production build passed.
- Frontend build reports a bundle-size advisory (about 652 kB before compression). Consider route splitting when feature pages are added.
- Backend: Release build passed with zero warnings/errors. SQL Server migration generated. Local global EF tool is 10.0.10; checked-in tool manifest pins 10.0.13 for restored environments.
- The normal `dotnet test` runner cannot open its required local communication socket inside this execution sandbox. This is an execution restriction, not a passing test result.
- Dependencies restored from the existing cache using a command-line `NuGetAudit=false` override because the vulnerability endpoint was unreachable. This override is not in project files or CI; a fresh online audit is required.
- Docker is not running and no test SQL Server connection was supplied. SQL Server integration tests have not been run locally.
- CI and the published HTTPS check are configured but have not run remotely. No GitHub remote is configured and no live deployment is claimed.

Phase 1 acceptance remains pending the standard backend test run, SQL Server verification, and the published application smoke check. Compilation alone does not complete those checks.
