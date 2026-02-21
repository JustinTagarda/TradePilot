# TradePilot Roadmap

## Completed MVP Foundation

- Shared DTO contracts and JSON settings
- Cloud API ingest + read endpoints
- HMAC validation in API and connector
- In-memory latest snapshot store per source
- Connector forwarding with re-signing
- Blazor Web sources + dashboard + polling
- MT5 EA snapshot sender

## Remaining MVP Work

- Documentation polish and test expansion
- End-to-end local validation pass

## Post-MVP Backlog

### Persistence
- EF Core + SQLite
- Historical snapshot storage
- Retention policy
- History endpoints

### Realtime
- SignalR hub for source update push
- Web subscription client
- Polling fallback mode

### Deployment
- Environment-specific config strategy
- Managed secret handling
- Azure deployment profile and runbook
