param(
    [string]$ProfilePath = "infra/azure/deployment.profile.sample.json",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Assert-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

Assert-Command -Name az
Assert-Command -Name dotnet

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$profileFullPath = if ([System.IO.Path]::IsPathRooted($ProfilePath)) {
    $ProfilePath
}
else {
    Join-Path $repoRoot $ProfilePath
}

if (-not (Test-Path $profileFullPath)) {
    throw "Deployment profile not found: $profileFullPath"
}

$profile = Get-Content -Raw -Path $profileFullPath | ConvertFrom-Json

$environment = [string]$profile.environment
$location = [string]$profile.location
$resourceGroup = [string]$profile.resourceGroup
$api = $profile.api
$web = $profile.web
$keyVault = $profile.keyVault

if ([string]::IsNullOrWhiteSpace($environment) -or [string]::IsNullOrWhiteSpace($location) -or [string]::IsNullOrWhiteSpace($resourceGroup)) {
    throw "environment, location, and resourceGroup are required in the deployment profile."
}

if ($null -eq $api -or [string]::IsNullOrWhiteSpace([string]$api.appName)) {
    throw "api.appName is required in the deployment profile."
}

if ($null -eq $web -or [string]::IsNullOrWhiteSpace([string]$web.storageAccountName)) {
    throw "web.storageAccountName is required in the deployment profile."
}

$null = az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Azure login required. Opening interactive login..."
    az login | Out-Null
}

Write-Host "Creating resource group '$resourceGroup' in '$location'..."
az group create --name $resourceGroup --location $location | Out-Null

$planName = if ([string]::IsNullOrWhiteSpace([string]$api.appServicePlanName)) {
    "$($api.appName)-plan"
}
else {
    [string]$api.appServicePlanName
}

$sku = if ([string]::IsNullOrWhiteSpace([string]$api.sku)) {
    "B1"
}
else {
    [string]$api.sku
}

$runtime = if ([string]::IsNullOrWhiteSpace([string]$api.runtime)) {
    "DOTNET|8.0"
}
else {
    [string]$api.runtime
}

Write-Host "Ensuring App Service plan '$planName'..."
$existingPlan = az appservice plan show --resource-group $resourceGroup --name $planName --query name -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($existingPlan)) {
    az appservice plan create --name $planName --resource-group $resourceGroup --sku $sku --is-linux | Out-Null
}

Write-Host "Ensuring API web app '$($api.appName)'..."
$existingApiApp = az webapp show --resource-group $resourceGroup --name $api.appName --query name -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($existingApiApp)) {
    az webapp create --resource-group $resourceGroup --plan $planName --name $api.appName --runtime $runtime | Out-Null
}

$apiSettings = @(
    "ASPNETCORE_ENVIRONMENT=$environment",
    "Persistence__Enabled=true"
)

if (-not [string]::IsNullOrWhiteSpace([string]$api.persistenceConnectionString)) {
    $apiSettings += "Persistence__ConnectionString=$($api.persistenceConnectionString)"
}

if ($null -ne $api.retentionDays) {
    $apiSettings += "Persistence__RetentionDays=$($api.retentionDays)"
}

if ($api.allowedOrigins) {
    for ($i = 0; $i -lt $api.allowedOrigins.Count; $i++) {
        $origin = [string]$api.allowedOrigins[$i]
        if (-not [string]::IsNullOrWhiteSpace($origin)) {
            $apiSettings += "Cors__AllowedOrigins__$i=$origin"
        }
    }
}

if ($apiSettings.Count -gt 0) {
    Write-Host "Applying API app settings..."
    az webapp config appsettings set --resource-group $resourceGroup --name $api.appName --settings $apiSettings | Out-Null
}

if ($keyVault -and -not [string]::IsNullOrWhiteSpace([string]$keyVault.name)) {
    Write-Host "Ensuring Key Vault '$($keyVault.name)'..."
    $existingVault = az keyvault show --resource-group $resourceGroup --name $keyVault.name --query name -o tsv 2>$null
    if ([string]::IsNullOrWhiteSpace($existingVault)) {
        az keyvault create --name $keyVault.name --resource-group $resourceGroup --location $location | Out-Null
    }

    az webapp identity assign --resource-group $resourceGroup --name $api.appName | Out-Null
    $principalId = az webapp identity show --resource-group $resourceGroup --name $api.appName --query principalId -o tsv
    az keyvault set-policy --name $keyVault.name --object-id $principalId --secret-permissions get list | Out-Null

    $secretSettings = @()
    if ($keyVault.references) {
        foreach ($reference in $keyVault.references) {
            $settingName = [string]$reference.settingName
            $secretName = [string]$reference.secretName

            if ([string]::IsNullOrWhiteSpace($settingName) -or [string]::IsNullOrWhiteSpace($secretName)) {
                continue
            }

            $secretId = az keyvault secret show --vault-name $keyVault.name --name $secretName --query id -o tsv
            if ([string]::IsNullOrWhiteSpace($secretId)) {
                throw "Secret '$secretName' was not found in vault '$($keyVault.name)'."
            }

            $secretSettings += "$settingName=@Microsoft.KeyVault(SecretUri=$secretId)"
        }
    }

    if ($secretSettings.Count -gt 0) {
        Write-Host "Applying Key Vault references for API secrets..."
        az webapp config appsettings set --resource-group $resourceGroup --name $api.appName --settings $secretSettings | Out-Null
    }
}

$storageAccountName = [string]$web.storageAccountName
$indexDocument = if ([string]::IsNullOrWhiteSpace([string]$web.indexDocument)) { "index.html" } else { [string]$web.indexDocument }
$errorDocument = if ([string]::IsNullOrWhiteSpace([string]$web.errorDocument)) { "index.html" } else { [string]$web.errorDocument }
$staticWebsiteContainer = '$web'

Write-Host "Ensuring storage account '$storageAccountName'..."
$existingStorage = az storage account show --resource-group $resourceGroup --name $storageAccountName --query name -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($existingStorage)) {
    az storage account create --name $storageAccountName --resource-group $resourceGroup --location $location --sku Standard_LRS --kind StorageV2 --allow-blob-public-access true --min-tls-version TLS1_2 | Out-Null
}

$storageKey = az storage account keys list --resource-group $resourceGroup --account-name $storageAccountName --query [0].value -o tsv
az storage blob service-properties update --account-name $storageAccountName --account-key $storageKey --static-website --index-document $indexDocument --404-document $errorDocument | Out-Null

$artifactRoot = Join-Path $repoRoot "artifacts/deploy/$environment"
$apiPublishDir = Join-Path $artifactRoot "api"
$webPublishDir = Join-Path $artifactRoot "web"

if (-not $SkipBuild) {
    Write-Host "Publishing API..."
    if (Test-Path $apiPublishDir) {
        Remove-Item -Path $apiPublishDir -Recurse -Force
    }

    dotnet publish (Join-Path $repoRoot "src/TradePilot.Api/TradePilot.Api.csproj") -c Release -o $apiPublishDir

    $apiZipPath = Join-Path $artifactRoot "api.zip"
    if (Test-Path $apiZipPath) {
        Remove-Item -Path $apiZipPath -Force
    }

    if (-not (Test-Path $artifactRoot)) {
        New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    }

    Compress-Archive -Path (Join-Path $apiPublishDir "*") -DestinationPath $apiZipPath -Force

    Write-Host "Deploying API package..."
    az webapp deploy --resource-group $resourceGroup --name $api.appName --src-path $apiZipPath --type zip | Out-Null

    Write-Host "Publishing Web..."
    if (Test-Path $webPublishDir) {
        Remove-Item -Path $webPublishDir -Recurse -Force
    }

    dotnet publish (Join-Path $repoRoot "src/TradePilot.Web/TradePilot.Web.csproj") -c Release -o $webPublishDir
}

$webRoot = Join-Path $webPublishDir "wwwroot"
if (-not (Test-Path $webRoot)) {
    throw "Web publish output was not found at '$webRoot'."
}

if (-not [string]::IsNullOrWhiteSpace([string]$web.apiBaseUrl)) {
    $webConfigPath = Join-Path $webRoot "appsettings.Production.json"
    $webConfig = @{
        Api = @{
            BaseUrl = [string]$web.apiBaseUrl
        }
    }
    $webConfig | ConvertTo-Json -Depth 5 | Set-Content -Path $webConfigPath
}

Write-Host "Uploading Web static files..."
az storage blob upload-batch --account-name $storageAccountName --account-key $storageKey --destination $staticWebsiteContainer --source $webRoot --overwrite | Out-Null

$webEndpoint = az storage account show --resource-group $resourceGroup --name $storageAccountName --query primaryEndpoints.web -o tsv
$apiEndpoint = "https://$($api.appName).azurewebsites.net"

Write-Host "Deployment complete."
Write-Host "API endpoint: $apiEndpoint"
Write-Host "Web endpoint: $webEndpoint"
