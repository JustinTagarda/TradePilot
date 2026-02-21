# TradePilot Architecture

## Overview

TradePilot is a read-only monitoring stack for MetaTrader 5 account data:

1. MT5 EA reads account, positions, and orders from a running terminal.
2. EA signs each JSON snapshot and sends it to the local connector.
3. Connector validates inbound HMAC, re-signs, and forwards to cloud API.
4. Cloud API validates HMAC and stores latest snapshot per source.
5. Blazor Web UI reads sources and latest snapshots for display.

## Runtime Components

### MT5 EA (`mt5/TradePilotEA.mq5`)
- Runs inside MT5 terminal.
- Uses timer loop (`1-2` seconds) to build snapshot payload.
- Sends `POST` to connector `/ingest/snapshot`.
- Adds signed headers:
  - `X-Source-Id`
  - `X-Timestamp` (UTC unix seconds)
  - `X-Nonce`
  - `X-Signature`

### Local Connector (`src/TradePilot.Connector`)
- ASP.NET Core Minimal API.
- Endpoints:
  - `GET /health`
  - `POST /ingest/snapshot`
- Responsibilities:
  - Validate inbound HMAC from EA.
  - Enforce timestamp drift + nonce replay protection.
  - Forward snapshot to cloud API as `POST /v1/mt/snapshots`.
  - Re-sign outbound payload with fresh timestamp + nonce.

### Cloud API (`src/TradePilot.Api`)
- ASP.NET Core Minimal API.
- Endpoints:
  - `GET /health`
  - `POST /v1/mt/snapshots`
  - `GET /v1/mt/sources`
  - `GET /v1/mt/sources/{sourceId}/latest`
- Responsibilities:
  - Validate HMAC on ingest.
  - Store latest snapshot in memory per `sourceId`.
  - Serve list of sources and latest snapshot for UI.

### Web UI (`src/TradePilot.Web`)
- Blazor WebAssembly app.
- API client service calls:
  - `GET /v1/mt/sources`
  - `GET /v1/mt/sources/{sourceId}/latest`
- Pages:
  - Sources
  - Dashboard
- Dashboard refreshes data via polling every `2` seconds.

## Data Contract Summary

### MtSnapshot
- `sourceId`
- `timestampUtc`
- `account`
- `positions[]`
- `orders[]`

### MtAccount
- `broker`, `server`, `login`, `currency`
- `balance`, `equity`, `margin`, `freeMargin`, `marginLevel`

### MtPosition
- `ticket`, `symbol`, `side`, `volume`
- `openPrice`, `stopLoss`, `takeProfit`
- `currentPrice`, `profit`

### MtOrder
- `ticket`, `symbol`, `type`, `volume`
- `price`, `stopLoss`, `takeProfit`
- `timeUtc`

## Security Model

Signature format:

`HMACSHA256(secret, "{timestamp}.{nonce}.{body}")`

Validation rules:
- Required headers must exist.
- Timestamp must be within configured skew (default `300s`).
- Nonce replay must be rejected within skew window.
- Signature comparison is constant-time.

Trust boundaries:
- EA -> Connector uses inbound secret configuration.
- Connector -> API uses outbound secret configuration.
- Inbound signature is never forwarded unchanged; connector re-signs.
