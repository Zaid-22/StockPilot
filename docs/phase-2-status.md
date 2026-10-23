# Phase 2 status — 23 October 2026

## Implemented

- Products, categories, suppliers, and customers in the four-layer .NET backend and React JavaScript frontend.
- Create/edit/details, deactivate/reactivate, server-side search/status/category filters, and pagination.
- Admin/Staff write access, Viewer read access, CSRF-protected mutations, and server-side input validation.
- Normalized unique SKUs and category names, including inactive records; conflict responses and stale-edit protection.
- One seeded warehouse, atomic product/zero-balance creation, and no catalog operation that edits stock.
- SQL Server catalog migration with indexes, foreign keys, decimal precision, concurrency tokens, and non-negative constraints.
- HTTP integration tests, UI workflow tests, and an expanded published HTTPS smoke workflow.
- Phase 1 fixes for antiforgery registration and login request validation.

## Verified locally

| Check | Result |
| --- | --- |
| Backend Release build | Passed, zero warnings/errors |
| In-process xUnit integration suite | 31 passed, 0 failed, 0 skipped; SQLite |
| React/Vitest suite | 20 passed |
| ESLint | Passed |
| Frontend production build | Passed |
| EF model/migration consistency | Passed; no pending model changes |
| Combined .NET/React publish | Passed; `artifacts/app` |
| Verification/smoke script syntax | Passed |

The backend tests cover authentication and role restrictions, CSRF, expiration/lockout/rate limits, product persistence and zero balance, database SKU uniqueness, stale writes, filtering/paging, deactivation, contact records, and invalid input. Frontend tests cover the forms, conflicts, confirmation, read-only access, query filters, and existing session flows.

## Pending acceptance checks

- Run the same suite against SQL Server, including real migration application. Docker is running, but access to `/Users/zaidyehyaaiasra/.docker/run/docker.sock` is denied in this session. SQLite results do not establish SQL Server behavior.
- Run `scripts/smoke-published.sh` against a disposable SQL Server database to verify the published application over HTTPS. Its catalog checks were added and syntax-checked, but have not executed here.
- Run the configured GitHub Actions workflow after connecting a remote. No remote repository or hosted deployment is configured.
- Run a fresh online dependency audit; restores here used the existing cache with a command-line audit override because the vulnerability endpoint was unreachable. Audit remains enabled in the repository and CI.

The README includes one command to run the remaining SQL Server and HTTPS checks from an unrestricted terminal. The implementation is ready for those checks; acceptance is not claimed complete.

## Deferred

Purchasing, sales, stock movements/adjustments, multi-warehouse management, and account management remain outside Phase 2. The frontend build emits a bundle-size advisory (about 699 kB before compression); route splitting is a follow-up optimization.
