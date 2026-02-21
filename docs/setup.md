# TradePilot Setup

## Prerequisites

- Windows machine
- .NET SDK 8+ (`dotnet --version`)
- MetaTrader 5 terminal
- VS Code (optional but recommended)

## Repository Bootstrap

From project root:

```powershell
dotnet restore TradePilot.sln
dotnet build TradePilot.sln
```

## Configuration

### Cloud API (`src/TradePilot.Api/appsettings.json`)

Set HMAC config:

- `Security:Hmac:AllowedClockSkewSeconds`
- `Security:Hmac:SharedSecret` or per-source map:
  - `Security:Hmac:SourceSecrets:{sourceId}`

Example:

```json
"Security": {
  "Hmac": {
    "AllowedClockSkewSeconds": 300,
    "SharedSecret": "",
    "SourceSecrets": {
      "demo-source-01": "replace-with-strong-secret"
    }
  }
}
```

### Connector (`src/TradePilot.Connector/appsettings.json`)

Set:

- `Connector:CloudApiBaseUrl` (default `http://localhost:5261`)
- `Connector:SourceId`
- `Security:InboundHmac` secrets for EA -> Connector validation
- `Security:OutboundHmac` secret for Connector -> API signing

Example:

```json
"Connector": {
  "CloudApiBaseUrl": "http://localhost:5261",
  "SourceId": "connector-local"
},
"Security": {
  "InboundHmac": {
    "AllowedClockSkewSeconds": 300,
    "SharedSecret": "",
    "SourceSecrets": {
      "demo-source-01": "replace-with-ea-secret"
    }
  },
  "OutboundHmac": {
    "SharedSecret": "replace-with-cloud-api-secret",
    "SourceSecrets": {}
  }
}
```

### Web UI (`src/TradePilot.Web/wwwroot/appsettings.json`)

Set API base URL:

```json
"Api": {
  "BaseUrl": "http://localhost:5261"
}
```

### MT5 EA Inputs (`mt5/TradePilotEA.mq5`)

Set EA inputs when attaching to chart:

- `InpSourceId` (must match API/connector source secret map)
- `InpSharedSecret` (must match connector inbound secret)
- `InpConnectorUrl` (default `http://127.0.0.1:5138/ingest/snapshot`)
- `InpTimerSeconds` (`1` or `2`)

## MT5 WebRequest Allowlist (Required)

In MetaTrader 5:

1. Open `Tools` -> `Options`.
2. Open the `Expert Advisors` tab.
3. Enable `Allow WebRequest for listed URL`.
4. Add connector URL base, for example:
   - `http://127.0.0.1:5138`
   - or `http://localhost:5138`
5. Click `OK`.

If this step is skipped, EA `WebRequest` calls will fail.

## Run Services

Use separate terminals.

Terminal 1 (API):

```powershell
dotnet run --project src/TradePilot.Api
```

Terminal 2 (Connector):

```powershell
dotnet run --project src/TradePilot.Connector
```

Terminal 3 (Web):

```powershell
dotnet run --project src/TradePilot.Web
```

Default development URLs:
- API: `http://localhost:5261`
- Connector: `http://localhost:5138`
- Web: `http://localhost:5288`
