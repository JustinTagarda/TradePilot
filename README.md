# TradePilot

TradePilot is a read-only MetaTrader 5 companion solution built with .NET. It streams account snapshots from a running MT5 terminal to a secure API and displays them in a browser dashboard for monitoring and analysis.

## Purpose

TradePilot is designed to provide a practical, production-style architecture for:

- secure ingestion of trading telemetry from MT5
- reliable forwarding between trust boundaries
- browser-based monitoring of account state in near real time
- cloud-ready deployment while keeping local development simple

The solution is intentionally read-only: no order placement, modification, or account actions are performed.

## What The App Does

- reads account, positions, and pending orders from MT5 through an Expert Advisor (EA)
- sends signed snapshots from EA to a local connector (`/ingest/snapshot`)
- validates signatures, timestamps, and nonces at each hop
- forwards snapshots to cloud API (`/v1/mt/snapshots`) with re-signing
- stores latest snapshots per source
- stores historical snapshots in SQLite with retention cleanup
- publishes realtime update signals via SignalR
- renders Sources and Dashboard pages in Blazor WebAssembly

## Architecture

Data flow:

`MT5 EA -> Local Connector -> Cloud API -> Blazor Web UI`

Main components:

- `mt5/TradePilotEA.mq5`: MT5 Expert Advisor snapshot sender
- `src/TradePilot.Connector`: local minimal API for inbound validation and forwarding
- `src/TradePilot.Api`: backend minimal API for ingest, query, history, and SignalR hub
- `src/TradePilot.Web`: WebAssembly UI for source list and dashboard views
- `src/TradePilot.Shared`: shared contracts and JSON serialization settings
- `src/TradePilot.Tests`: xUnit test suite

## Technology Stack

- .NET 8
- C#
- ASP.NET Core Minimal APIs
- Blazor WebAssembly
- SignalR
- EF Core + SQLite
- Serilog
- OpenAPI / Swagger
- xUnit
- PowerShell scripts for local E2E and deployment helpers

## Security Model

Requests are signed using HMAC SHA-256 with the payload:

`{timestamp}.{nonce}.{body}`

Headers used:

- `X-Source-Id`
- `X-Timestamp`
- `X-Nonce`
- `X-Signature`

Validation includes:

- required header checks
- timestamp skew checks
- nonce replay protection
- constant-time signature comparison

Connector re-signs outbound requests; inbound signatures are not forwarded downstream.

## API Endpoints

Cloud API (`src/TradePilot.Api`):

- `GET /health`
- `POST /v1/mt/snapshots`
- `GET /v1/mt/sources`
- `GET /v1/mt/sources/{sourceId}/latest`
- `GET /v1/mt/sources/{sourceId}/history`
- SignalR hub: `/hubs/mt`

Connector (`src/TradePilot.Connector`):

- `GET /health`
- `POST /ingest/snapshot`

## Local Run

From repository root:

```powershell
dotnet restore TradePilot.sln
dotnet build TradePilot.sln
dotnet test TradePilot.sln
```

Run services in separate terminals:

```powershell
dotnet run --project src/TradePilot.Api
dotnet run --project src/TradePilot.Connector
dotnet run --project src/TradePilot.Web
```

Default URLs:

- API: `http://localhost:5261`
- Connector: `http://localhost:5138`
- Web: `http://localhost:5288`

## MT5 Setup Notes

- Compile and attach `TradePilotEA` to a chart
- Enable MT5 `Algo Trading`
- Add connector base URL to MT5 WebRequest allowlist
- Ensure EA secret(s) match connector/API configuration

## Testing

Unit tests:

```powershell
dotnet test TradePilot.sln
```

Local E2E script:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/e2e-local-test.ps1
```

E2E report output:

- `docs/e2e-local-test-results.md`

## Deployment Baseline

Azure deployment baseline includes:

- profile template: `infra/azure/deployment.profile.sample.json`
- deployment script: `scripts/deploy-azure.ps1`
- runbook: `docs/deployment-azure.md`

## Documentation

- Setup: `docs/setup.md`
- Architecture: `docs/architecture.md`
- Demo: `docs/demo.md`
- Roadmap: `docs/roadmap.md`
- Azure deployment: `docs/deployment-azure.md`
