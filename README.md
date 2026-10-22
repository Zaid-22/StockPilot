# StockPilot

Inventory, purchasing, and sales management using ASP.NET Core and React JavaScript.

Phase 1: application structure, cookie authentication, roles, tests, and CI.
Inventory workflows are planned for later phases.

## Architecture

Domain contains business concepts. Application defines use cases and contracts.
Infrastructure implements persistence and Identity. API handles HTTP and composes the application.
The React frontend uses JavaScript (.js/.jsx) and Vite.

## Requirements

.NET 10 SDK (10.0.201 or a later feature band), Node.js 24+, and SQL Server access.

- Frontend: `cd frontend && npm install && npm run dev`
- API: `dotnet run --project backend/src/StockPilot.Api`

Setup and verification instructions grow with each implementation commit.
