# Model Deployment Setup Sample

This sample configures the default model deployment mappings required for prebuilt analyzers in Azure AI Content Understanding. This is a **one-time setup** that must be run before using any samples that rely on prebuilt analyzers (such as `ContentExtraction`).

## Overview

The Model Deployment Setup sample maps your deployed models in Azure AI Foundry to the model names expected by prebuilt analyzers. This configuration is persisted in your Azure AI Foundry resource and only needs to be run once (or whenever you change your deployment names).

## Prerequisites

1. **Azure AI Foundry Resource**: Ensure you have created an Azure AI Foundry resource and have the endpoint configured. See the main [README.md](../README.md#configure-azure-ai-service-resource) for setup instructions.

2. **Deployed Models**: You must have deployed the following models in Azure AI Foundry:
   - **GPT-4.1** - Required for most prebuilt analyzers (e.g., `prebuilt-invoice`, `prebuilt-receipt`, `prebuilt-idDocument`)
   - **GPT-4.1-mini** - Required for RAG analyzers (e.g., `prebuilt-documentSearch`, `prebuilt-audioSearch`, `prebuilt-videoSearch`)
   - **text-embedding-3-large** - Required for all prebuilt analyzers that use embeddings

   For instructions on deploying models, see [Deploy models in Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-studio/how-to/deploy-models-openai).

3. **Configuration**: Configure your `appsettings.json` file with:
   - Your Azure AI endpoint
   - Your deployment names (see [Configuration](#configuration) below)

4. **Permissions**: Ensure you have the **Cognitive Services User** role on your Azure AI Foundry resource. See the main README for [permission setup instructions](../README.md#configure-azure-ai-service-resource).

## Key Concepts

### Model Deployment Mappings

Prebuilt analyzers use standardized model names (e.g., `gpt-4.1`, `gpt-4.1-mini`, `text-embedding-3-large`), but your actual deployments in Azure AI Foundry may have different names. This sample creates a mapping between the standard model names and your actual deployment names.

### One-Time Configuration

> **💡 Note:** This configuration step is only required **once per Azure Content Understanding resource**, unless the GPT deployment has been changed. You can skip this step if:
> - This configuration has already been run once for your resource, or
> - Your administrator has already configured the model deployments for you

The configuration is persisted in your Azure AI Foundry resource, so you only need to run this sample:
- Once when setting up a new resource
- When you change your deployment names
- When you need to update the mappings

### Prebuilt Analyzer Requirements

Different prebuilt analyzers require different models:

- **RAG Analyzers** (`prebuilt-documentSearch`, `prebuilt-audioSearch`, `prebuilt-videoSearch`):
  - Require: **GPT-4.1-mini** and **text-embedding-3-large**
  
- **Other Prebuilt Analyzers** (`prebuilt-invoice`, `prebuilt-receipt`, `prebuilt-idDocument`, etc.):
  - Require: **GPT-4.1** and **text-embedding-3-large**

This sample configures all three models to support all prebuilt analyzers.

## Configuration

Before running this sample, ensure your `appsettings.json` file contains the deployment names:

```json
{
  "AZURE_AI_ENDPOINT": "https://<your-resource-name>.services.ai.azure.com",
  "AZURE_AI_API_KEY": null,
  "AZURE_AI_API_VERSION": "2025-11-01",
  "GPT_4_1_DEPLOYMENT": "gpt-4.1",
  "GPT_4_1_MINI_DEPLOYMENT": "gpt-4.1-mini",
  "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT": "text-embedding-3-large"
}
```

**Important Notes:**
- Replace the deployment names with your actual deployment names if they differ from the defaults
- The deployment names must match exactly what you see in Azure AI Foundry
- See the main [README.md](../README.md#appsettingsjson-configuration-reference) for detailed configuration options

## Code Structure

### Main Entry Point

The `Program.cs` file performs the following steps:

1. **Validates Configuration**: Checks that all required deployment names are configured in `appsettings.json`
2. **Displays Configuration**: Shows which deployments will be mapped
3. **Configures Mappings**: Calls the Content Understanding API to set the default model deployments
4. **Verifies Success**: Displays the configured mappings and confirms success

### Code Example

```csharp
// Create host and get configuration
var host = ContentUnderstandingBootstrapper.CreateHost();
var client = host.Services.GetRequiredService<AzureContentUnderstandingClient>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

// Configure model deployments
var deploymentConfig = new ModelDeploymentConfiguration(client, configuration);
bool configured = await deploymentConfig.ConfigureDefaultModelDeploymentsAsync();

if (configured)
{
    // Configuration successful - mappings are now persisted in Azure AI Foundry
    Console.WriteLine("✅ Model deployment configuration completed successfully!");
}
```

**Source:** [`Program.cs`](Program.cs#L41-L64)

### Model Deployment Configuration

The `ModelDeploymentConfiguration` class handles the actual configuration:

```csharp
// Update defaults to map model names to your deployments
var result = await _client.UpdateDefaultsAsync(new Dictionary<string, string?>
{
    ["gpt-4.1"] = gpt41Deployment,
    ["gpt-4.1-mini"] = gpt41MiniDeployment,
    ["text-embedding-3-large"] = textEmbedding3LargeDeployment
});
```

**Source:** [`ModelDeploymentConfiguration.cs`](../ContentUnderstanding.Common/ModelDeploymentConfiguration.cs#L99-L107)

## Running the Sample

1. **Ensure Prerequisites are Met**:
   - Azure AI Foundry resource is created
   - All three models are deployed
   - `appsettings.json` is configured with deployment names

2. **Build the project:**
   ```bash
   cd ModelDeploymentSetup
   dotnet build
   ```

3. **Run the sample:**
   ```bash
   dotnet run
   ```

4. **Review the output:**
   - The sample will display which deployments it's configuring
   - On success, it will show the configured model mappings
   - On failure, it will provide guidance on what needs to be fixed

## Expected Output

### Successful Configuration

```
================================================================================
Azure AI Content Understanding - Model Deployment Setup
================================================================================

This sample configures the default model deployments required for
prebuilt analyzers (prebuilt-documentSearch, prebuilt-audioSearch,
prebuilt-videoSearch).

Required deployments:
  - GPT-4.1
  - GPT-4.1-mini
  - text-embedding-3-large

Make sure you have:
  1. Deployed all three models in Azure AI Foundry
  2. Configured your deployment names in appsettings.json

Press any key to continue or Ctrl+C to exit...

📋 Configuring default model deployments...
   GPT-4.1 deployment: gpt-4.1
   GPT-4.1-mini deployment: gpt-4.1-mini
   text-embedding-3-large deployment: text-embedding-3-large

================================================================================
✅ Model deployment configuration completed successfully!
================================================================================

You can now run other samples that use prebuilt analyzers.
The configuration is persisted in your Azure AI Foundry resource.
```

### Configuration Failure

If deployments are missing or incorrect, the sample will display:

```
⚠️  Warning: Missing required model deployment configuration(s):
   - GPT_4_1_DEPLOYMENT

   Prebuilt analyzers require GPT-4.1, GPT-4.1-mini, and text-embedding-3-large deployments.
   Please:
   1. Deploy all three models in Azure AI Foundry
   2. Set environment variables or add to appsettings.json:
      GPT_4_1_DEPLOYMENT=<your-gpt-4.1-deployment-name>
      GPT_4_1_MINI_DEPLOYMENT=<your-gpt-4.1-mini-deployment-name>
      TEXT_EMBEDDING_3_LARGE_DEPLOYMENT=<your-text-embedding-3-large-deployment-name>
   3. Restart the application
```

## Troubleshooting

### Common Issues

1. **"Deployment names don't exist in your Azure AI Foundry project"**
   - Verify that all three models are deployed in Azure AI Foundry
   - Check that the deployment names in `appsettings.json` match exactly (case-sensitive)
   - Ensure you're looking at the correct Azure AI Foundry project

2. **"Insufficient permissions to update defaults"**
   - Ensure you have the **Cognitive Services User** role on your Azure AI Foundry resource
   - See the main README for [permission setup instructions](../README.md#configure-azure-ai-service-resource)

3. **"Missing required model deployment configuration"**
   - Verify that `appsettings.json` contains all three deployment settings:
     - `GPT_4_1_DEPLOYMENT`
     - `GPT_4_1_MINI_DEPLOYMENT`
     - `TEXT_EMBEDDING_3_LARGE_DEPLOYMENT`
   - Ensure the values are not null or empty

4. **"Network connectivity issues"**
   - Check your internet connection
   - Verify that your Azure AI Foundry endpoint is accessible
   - Ensure firewall rules allow outbound connections to Azure services

## Key Implementation Details

### Configuration Priority

The sample reads configuration values using the following priority (matching Python's `dotenv` behavior):

1. **`appsettings.json`** - File-based configuration (highest priority)
2. **Environment Variables** - Fallback if not found in `appsettings.json`

This ensures that `appsettings.json` takes precedence over environment variables, making it easier to manage configuration in a single file.

**Source:** [`ModelDeploymentConfiguration.cs`](../ContentUnderstanding.Common/ModelDeploymentConfiguration.cs#L25-L43)

### API Call

The sample uses the `UpdateDefaultsAsync` method to configure model deployments:

```csharp
var result = await _client.UpdateDefaultsAsync(new Dictionary<string, string?>
{
    ["gpt-4.1"] = gpt41Deployment,
    ["gpt-4.1-mini"] = gpt41MiniDeployment,
    ["text-embedding-3-large"] = textEmbedding3LargeDeployment
});
```

This API call updates the default model deployment mappings in your Azure AI Foundry resource, which are then used by all prebuilt analyzers.

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs)

## Next Steps

After successfully running this sample:

1. **Run Content Extraction Sample**: Try the [ContentExtraction](../ContentExtraction/) sample to see prebuilt analyzers in action
2. **Explore Other Samples**: Check out other samples that use prebuilt analyzers:
   - [FieldExtraction](../FieldExtraction/) - Uses `prebuilt-invoice`, `prebuilt-receipt`, etc.
   - [Classifier](../Classifier/) - May use prebuilt analyzers for document classification

## Learn More

- **[Content Understanding Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)** - Comprehensive introduction to the service
- **[What's New](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/whats-new)** - Latest features and updates
- **[Deploy models in Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-studio/how-to/deploy-models-openai)** - Detailed guide on deploying models
- **[Prebuilt Analyzers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/analyzers/prebuilt)** - Documentation on available prebuilt analyzers

## Related Samples

- **[ContentExtraction](../ContentExtraction/)** - Extract content from documents, audio, and video using prebuilt analyzers
- **[FieldExtraction](../FieldExtraction/)** - Extract specific fields from documents using custom analyzers
- **[Management](../Management/)** - Create, list, and manage analyzers

