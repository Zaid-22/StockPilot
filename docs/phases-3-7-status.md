# Phases 3–7 status — 24 October 2026

Phases 3–6 are implemented and committed locally. Phase 7's container, CI/smoke scripts, documentation, and demo walkthrough are prepared. SQL Server acceptance and live deployment are not complete.

## Phase delivery and commits

| Phase | Delivered | Separate logical commits |
| --- | --- | --- |
| 3 — Inventory | Paged balances and journal, reasoned adjustments, optimistic stock versions, atomic writes, idempotent retries, React screens | `983f1b1` backend; `ae6c71c` frontend |
| 4 — Purchasing | Draft/confirm/cancel, snapshot order lines, partial receiving, excess-receipt protection, React workflow | `cbfa346` backend; `4dde933` frontend |
| 5 — Sales | Draft/confirm/cancel, partial shipping, no-negative-stock enforcement, rollback on failed lines, React workflow | `2642c44` backend; `171b84a` frontend |
| 6 — Reporting | Live inventory/open-order summary, low-stock list, inclusive UTC activity ranges, fulfilled values, responsive navigation, lazy page loading | `45c6f0d` backend; `c2246c2` frontend |
| 7 — Deployment preparation | Non-root HTTPS image, Compose configuration, full-workflow smoke script, CI container build and frontend audit, release/demo documentation | `351821f` packaging; documentation committed separately |

`981e6c9` additionally preserves UTC timestamps after database reads and guards journal/receipt immutability through synchronous and asynchronous EF saves.

## Verified in this session

| Check | Evidence |
| --- | --- |
| Backend Release build | Passed; zero warnings/errors |
| Backend integration suite | **54 passed, 0 failed, 1 skipped** using the in-process xUnit runner and SQLite |
| SQL Server-only parallel shipping test | Explicitly skipped without `TEST_SQLSERVER_CONNECTION_STRING`; no concurrency acceptance claim |
| Frontend tests | **31 passed** across 7 files |
| ESLint | Passed |
| Frontend production build | Passed; pages split into separate chunks |
| EF migration/model consistency | No pending model changes |
| SQL migration deployment script | Generated successfully at `artifacts/migrations.sql`; not applied to SQL Server here |
| Combined React/.NET Release publish | Passed; output at `artifacts/app` |
| Published smoke/verification scripts | Shell and Python syntax checks passed; HTTP smoke execution pending |
| Compose configuration | `docker compose --env-file .env.example config --quiet` passed |
| Frontend dependency audit | `npm audit --audit-level=high` passed; 0 reported vulnerabilities |
| Browser layout checks | Desktop dashboard at 1440px; reports/orders at 390px; no page horizontal overflow, order dialog within viewport |

Browser layout checks used deterministic sample API responses. They establish rendering/layout behavior, not a live database-connected application. Integration tests use the real HTTP pipeline with TestServer and SQLite. Important cases include retries, stale versions, insufficient stock, invalid transitions, partial processing, whole-operation rollback, permissions, CSRF, and report values.

## Remaining acceptance work

1. **SQL Server:** run the complete suite against a dedicated test server. This applies real migrations and runs the parallel-shipment test. The local Docker daemon was unavailable; opening the detected Docker application failed with a missing-executable error. No SQL Server test connection was supplied.
2. **Published HTTPS:** run `scripts/smoke-published.sh` against a disposable SQL Server database. The expanded workflow is prepared, not claimed to have run.
3. **Container:** build and run the image with a real certificate, database connection, and persistent key volume. Compose configuration validation is not a container build/run.
4. **Standard runner:** the VSTest invocation did not produce a completed result in this session. Use the recorded in-process result as local evidence; CI remains configured to use the standard runner.
5. **.NET dependency audit:** the online package-vulnerability query timed out. Repository audit settings remain enabled. The installed global EF tool is 10.0.10; the checked-in manifest pins 10.0.13. Restore the pinned tool in an online environment.
6. **GitHub/host:** no remote repository, remote CI run, public domain, database hosting, or live deployment was configured. Follow [deployment instructions](deployment.md) after choosing the actual host.

The frontend still emits a bundle-size advisory for its approximately 658 kB main chunk before compression. Pages are lazy-loaded; further framework/vendor optimization is a backlog item, not an unverified performance claim.

## Scope retained

React remains JavaScript. There is one warehouse and whole-unit stock. All writes enforce roles and CSRF. Catalog edits do not alter stock. Sales confirmation does not reserve stock. Money uses three decimal places with a consistently chosen single currency; reports are operational values, not accounting statements. Draft lines are corrected by cancelling/recreating. Returns, tax, payments, accounting valuation, multi-warehouse operations, and user-provisioning screens remain deferred.

See [demo guide](demo-guide.md) for the end-to-end scenario and interview explanation. Each phase was committed independently; this work was not squashed into one commit.
