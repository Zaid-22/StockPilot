# StockPilot demo and project walkthrough

Use your own development database and administrator account. This guide does not add demo credentials, public signup, or a one-click login bypass.

## A five-minute walkthrough

1. **Catalog:** create the category “Office equipment”, product `KEY-001` / “Wireless keyboard”, selling price `12.125`, reorder level `3`, supplier “Amman Office Supplies”, and customer “Demo company”. New product stock starts at zero.
2. **Purchasing:** create a purchase draft for 10 keyboards at unit cost `4.125`. Open the draft and confirm it. Receive only 6 with delivery reference `DEL-001`. The order becomes Partial and stock becomes 6. Receiving its remaining 4 later will complete the order.
3. **Sales:** create and confirm a sales order for 4 keyboards at `12.125`. Confirmation does not reserve stock. Ship 3 with reference `SHIP-001`. Stock becomes 3, and the sales order remains Partial.
4. **Inventory:** show the low-stock indicator and the receipt/shipment history. Post an adjustment of `-1` with reason “One damaged keyboard”. Stock becomes 2. Try removing 3: the server refuses it and preserves the balance and journal.
5. **Reports:** choose today's UTC date. The report shows 6 received, 3 shipped, and net adjustments of -1; received order value is `24.750`, shipped order value `36.375`. These are fulfilled order values, not payments, revenue recognition, profit, or an accounting inventory valuation.
6. **Reliability:** explain that retrying an identical request with the same request ID returns its original result. A request ID cannot be reused with a different payload or actor. A stale stock/order version returns 409. Show the integration tests and the SQL Server-only parallel-shipment test.

Use a unique product/SKU for subsequent walkthroughs or use a new disposable database. Repeating this guide on existing data will not have the same totals.

## Engineering decisions to explain in an interview

- One application and one database keep the deployment manageable. Domain, Application, Infrastructure, and API projects separate responsibilities; React uses JavaScript.
- A controller handles HTTP and permissions. Application interfaces define use cases. Infrastructure services coordinate EF Core transactions and persistence.
- The stock balance is the current quantity. The append-only movement table explains how it changed. Product creation starts at zero; catalog edits cannot change stock.
- A receipt or shipment saves the order's processed quantities, balance changes, journal rows, and request receipt together. Failure on any line rolls back the entire operation.
- A GUID version is an optimistic concurrency token. The database update only succeeds if the saved version is still current. This prevents two stale writers from silently overwriting each other.
- A unique request ID plus payload fingerprint and actor ID makes retries safe. The stored response is the original result, not the current state of a record that may have changed later.
- Purchase and sales documents share an order/line model, while HTTP routes keep the workflows explicit. Prices, product names/SKUs, and contact names are captured on the order so later catalog edits do not rewrite its commercial details.
- Authentication uses ASP.NET Core Identity cookies and CSRF protection. Admin and Staff can operate the workspace; Viewer can read. The API enforces roles regardless of which buttons React shows.

## Scope and honest limitations

- One warehouse, whole-unit quantities, and one consistently chosen currency. Money uses C# decimals with three decimal places. No tax, payments, returns, reservations, accounting cost method, or inter-warehouse transfers.
- Draft lines are immutable in this version: cancel and recreate a draft to correct them. Orders cannot be cancelled after any quantity has been received or shipped. Correct stock through a documented adjustment; a return workflow is future work.
- Already confirmed orders can finish processing even if a catalog product/contact is later deactivated. New drafts and confirmations require active records.
- The current frontend retry ID survives retries while the form remains open. After a browser reload or session loss, inspect the order/history before creating another operation. There is no offline mutation queue.
- The account directory is read-only. Public signup, account provisioning screens, and copied-cookie revocation tracking remain outside this version.
- Local SQLite tests validate business behavior. SQL Server migrations, true concurrent HTTP shipping, the production HTTPS smoke test, and the container still require their own completed checks. See the current status document before claiming deployment readiness.

## CV wording

“Built StockPilot, an inventory and order-management application using ASP.NET Core, EF Core, SQL Server, and React JavaScript; implemented partial receiving/shipping, an audited stock ledger, optimistic concurrency, idempotent operations, role-based access, and automated integration/UI tests.”

Only add “deployed” after a working hosted version is verified. Do not describe the project as a complete accounting ERP or claim measured performance gains without evidence.
