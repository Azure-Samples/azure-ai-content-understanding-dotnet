# Get the Azure AI endpoint from azd environment
$azureAiEndpoint = azd env get-value AZURE_AI_ENDPOINT

# Path to the appsettings.json file
$appsettingsFile = "ContentUnderstanding.Common/appsettings.json"

# Check if appsettings.json exists
if (!(Test-Path $appsettingsFile)) {
    Write-Error "Error: $appsettingsFile not found"
    exit 1
}

# Check if the endpoint was retrieved successfully
if ([string]::IsNullOrWhiteSpace($azureAiEndpoint)) {
    Write-Error "Error: Could not retrieve AZURE_AI_ENDPOINT from azd environment"
    exit 1
}

# Read the current appsettings.json content
$appsettingsContent = Get-Content -Path $appsettingsFile -Raw | ConvertFrom-Json

# Update the endpoint
$appsettingsContent.AZURE_CU_CONFIG.Endpoint = $azureAiEndpoint

# Write the updated content back to the file
$appsettingsContent | ConvertTo-Json -Depth 10 | Set-Content -Path $appsettingsFile

Write-Host "Successfully updated $appsettingsFile with endpoint: $azureAiEndpoint" 