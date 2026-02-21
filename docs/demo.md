# TradePilot Demo Steps

## Goal

Show end-to-end read-only flow:

`MT5 EA -> Connector -> API -> Web`

## Demo Sequence

1. Start Cloud API (`src/TradePilot.Api`).
2. Start Connector (`src/TradePilot.Connector`).
3. Start Web UI (`src/TradePilot.Web`).
4. Attach `TradePilotEA.mq5` to an MT5 chart.
5. Confirm EA inputs point to connector URL and matching shared secret.
6. Wait for snapshots to be sent every `1-2` seconds.
7. Open `http://localhost:5288/sources`.
8. Verify source appears.
9. Open dashboard for that source.
10. Verify account, positions, orders, and update cadence.

## Quick Verification Endpoints

Connector health:

```http
GET http://localhost:5138/health
```

API health:

```http
GET http://localhost:5261/health
```

API sources:

```http
GET http://localhost:5261/v1/mt/sources
```

API latest snapshot:

```http
GET http://localhost:5261/v1/mt/sources/{sourceId}/latest
```

## Expected Demo Outcome

- Source shows on Sources page.
- Dashboard values match MT5 account state.
- Dashboard updates approximately every 2 seconds.
- No trade actions are available (read-only mode).

## Automated Local E2E Check

Run:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/e2e-local-test.ps1
```

Result artifact:
- `docs/e2e-local-test-results.md`
