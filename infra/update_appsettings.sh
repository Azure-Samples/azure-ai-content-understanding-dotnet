#!/bin/bash

# Get the Azure AI endpoint from azd environment
AZURE_AI_ENDPOINT=$(azd env get-value AZURE_AI_ENDPOINT)

# Path to the appsettings.json file
APPSETTINGS_FILE="ContentUnderstanding.Common/appsettings.json"

# Check if appsettings.json exists
if [ ! -f "$APPSETTINGS_FILE" ]; then
    echo "Error: $APPSETTINGS_FILE not found"
    exit 1
fi

# Check if the endpoint was retrieved successfully
if [ -z "$AZURE_AI_ENDPOINT" ]; then
    echo "Error: Could not retrieve AZURE_AI_ENDPOINT from azd environment"
    exit 1
fi

# Use sed to replace the endpoint in the JSON file
# This replaces the value of "Endpoint" in the "AZURE_CU_CONFIG" section
sed -i.bak 's|"Endpoint": "[^"]*"|"Endpoint": "'$AZURE_AI_ENDPOINT'"|g' "$APPSETTINGS_FILE"

echo "Successfully updated $APPSETTINGS_FILE with endpoint: $AZURE_AI_ENDPOINT" 