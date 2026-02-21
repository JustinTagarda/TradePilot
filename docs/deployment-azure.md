# Azure Deployment Runbook

This runbook provides a baseline deployment path for `TradePilot.Api` and `TradePilot.Web`.

## Scope

- Deploy API (`src/TradePilot.Api`) to Azure App Service (Linux)
- Deploy Web (`src/TradePilot.Web`) to Azure Storage Static Website
- Configure environment-specific settings from a deployment profile
- Resolve API secrets from Azure Key Vault references

The local connector (`src/TradePilot.Connector`) is expected to run near MT5 and is not deployed to Azure in this baseline.

## Environment Configuration Strategy

Use separate profiles per environment (`Dev`, `Staging`, `Production`) and avoid committing secrets.

Configuration sources:

- Base defaults: `appsettings.json`
- Environment defaults: `appsettings.Production.json`
- Environment overrides in Azure App Settings
- Sensitive values from Key Vault references in App Settings

Production config files added in this repo:

- `src/TradePilot.Api/appsettings.Production.json`
- `src/TradePilot.Connector/appsettings.Production.json`
- `src/TradePilot.Web/wwwroot/appsettings.Production.json`

## Secret Management Strategy

For API secrets, store values in Azure Key Vault and reference them from App Service settings.

Recommended secrets:

- `Security__Hmac__SharedSecret`
- `Security__Hmac__SourceSecrets__{sourceId}`

Do not store real secrets in:

- `appsettings*.json`
- deployment profile JSON
- GitHub repo history

## Deployment Profile

Template:

- `infra/azure/deployment.profile.sample.json`

Create a local copy for each environment (example):

```powershell
Copy-Item infra/azure/deployment.profile.sample.json infra/azure/deployment.profile.json
```

Then update resource names, origins, and URLs.

## Prerequisites

- Azure CLI installed (`az --version`)
- .NET SDK 8+ (`dotnet --version`)
- Azure subscription access
- Authenticated CLI session (`az login`)

## One-Time Setup Per Environment

1. Create Key Vault secrets listed in `keyVault.references`.
2. Confirm storage account name is globally unique and lowercase.
3. Confirm API CORS allowed origins include the final web URL.

## Deploy Command

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-azure.ps1 -ProfilePath infra/azure/deployment.profile.json
```

Optional (skip build and deploy pre-built artifacts):

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/deploy-azure.ps1 -ProfilePath infra/azure/deployment.profile.json -SkipBuild
```

## Post-Deployment Checks

1. API health endpoint returns 200:

```http
GET https://<api-app-name>.azurewebsites.net/health
```

2. Web site loads and can reach API.
3. API logs show successful snapshot ingest.
4. CORS errors are absent in browser console.

## Rollback Baseline

1. Re-run deployment script with previous known-good build artifacts.
2. If needed, redeploy API package manually to the same App Service.
3. Re-upload previous static web publish output to `$web` container.

## Notes

- This is a baseline runbook. For production hardening, add private networking, WAF/front door, centralized telemetry, and CI/CD approvals.
