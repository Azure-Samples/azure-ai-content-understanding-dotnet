# Management Sample

This sample demonstrates how to manage analyzers in your Azure AI Content Understanding resource. You'll learn how to create custom analyzers, list all analyzers, retrieve analyzer details, and delete analyzers you no longer need.

## Overview

The Management sample demonstrates how to manage defaults and analyzers in your Azure AI Content Understanding resource:

1. **Update Defaults** - Configure default model deployment mappings
2. **Get Defaults** - Retrieve the current default model deployment mappings
3. **Create Analyzer** - Create a custom analyzer from a template
4. **List Analyzers** - View all analyzers in your resource (both custom and prebuilt)
5. **Get Analyzer Details** - Retrieve detailed information about a specific analyzer
6. **Delete Analyzer** - Remove analyzers that are no longer needed

## Prerequisites

1. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.

2. Ensure you have deployed the required models (GPT-4.1, GPT-4.1-mini, and text-embedding-3-large) in Azure AI Foundry.

3. Configure your `appsettings.json` file with your endpoint and deployment names.

4. **Model Deployment Configuration**
   - This sample uses `BootstrapAsync` which automatically configures model deployments during initialization.
   - The sample creates a custom analyzer based on `prebuilt-callCenter`, which requires model deployments to be configured.
   - Model deployment configuration happens automatically when you run the sample - no separate ModelDeploymentSetup step is required.

## Key Concepts

### Analyzer Lifecycle

Analyzers in Azure AI Content Understanding follow a lifecycle:

1. **Creation** - Create custom analyzers from templates or base analyzers
2. **Usage** - Use analyzers to analyze content (see [FieldExtraction](../FieldExtraction/) and [ContentExtraction](../ContentExtraction/) samples)
3. **Management** - List, retrieve details, and manage your analyzers
4. **Deletion** - Remove analyzers when they're no longer needed

### Custom Analyzers

Custom analyzers allow you to:
- **Extend Prebuilt Analyzers** - Build on top of existing prebuilt analyzers (like `prebuilt-callCenter`, `prebuilt-invoice`)
- **Define Custom Fields** - Specify exactly what fields to extract and how
- **Configure Extraction Methods** - Use `generate` for open-ended extraction or `classify` for classification tasks
- **Set Models** - Choose which AI models to use for analysis

### Analyzer Types

- **Prebuilt Analyzers** - Production-ready analyzers provided by Microsoft (e.g., `prebuilt-invoice`, `prebuilt-receipt`)
- **Custom Analyzers** - Analyzers you create for your specific use cases

### Asynchronous Operations

Analyzer creation is an asynchronous operation:
1. **Begin Creation** - Start the creation operation (returns immediately with an operation location)
2. **Poll for Completion** - Poll the operation location until creation completes
3. **Use the Analyzer** - Once created, the analyzer is ready to use

## Code Structure

### Main Entry Point

The `Program.cs` file demonstrates the complete management workflow, starting with defaults configuration:

```csharp
// 1. Update defaults (set model deployment mappings)
Console.WriteLine("=== Update Defaults ===");
var modelDeployments = new Dictionary<string, string?>
{
    ["gpt-4.1"] = gpt41Deployment,
    ["gpt-4.1-mini"] = gpt41MiniDeployment,
    ["text-embedding-3-large"] = textEmbedding3LargeDeployment
};
await service.UpdateDefaultsAsync(modelDeployments);

// 2. Get defaults (retrieve model deployment mappings)
Console.WriteLine("=== Get Defaults ===");
await service.GetDefaultsAsync();

// 3. Create a simple analyzer
Console.WriteLine("=== Create Analyzer ===");
string analyzerId = $"management_sample_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
await service.CreateAnalyzerAsync(analyzerId, contentAnalyzer);

// 4. List all analyzers
Console.WriteLine("=== List All Analyzers ===");
await service.ListAnalyzersAsync();

// 5. Get analyzer details
Console.WriteLine("=== Get Analyzer Details ===");
await service.GetAnalyzerDetailsAsync(analyzerId);

// 6. Delete analyzer
Console.WriteLine("=== Delete Analyzer ===");
await service.DeleteAnalyzerAsync(analyzerId);
```

**Source:** [`Program.cs`](Program.cs#L30-L157)

### Service Implementation

The `ManagementService` class implements all analyzer management operations. It uses the `AzureContentUnderstandingClient` (a thin REST client) to interact with the API.

**Source:** [`ManagementService.cs`](Services/ManagementService.cs)

## Update Defaults

The sample starts by configuring default model deployment mappings. This tells Content Understanding which model deployments to use for prebuilt analyzers.

### Code Example

```csharp
public async Task<Dictionary<string, object>> UpdateDefaultsAsync(Dictionary<string, string?> modelDeployments)
{
    var result = await _client.UpdateDefaultsAsync(modelDeployments);
    
    Console.WriteLine("✅ Default model deployments configured successfully");
    Console.WriteLine("   Model mappings:");
    
    if (result.TryGetValue("modelDeployments", out var modelDeploymentsValue))
    {
        var deployments = JsonSerializer.Deserialize<Dictionary<string, string>>(modelDeploymentsValue.ToString()!);
        if (deployments != null)
        {
            foreach (var (model, deployment) in deployments)
            {
                Console.WriteLine($"     {model} → {deployment}");
            }
        }
    }
    
    return result;
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L194-L220)

### Key Points

- **Model Mappings**: Maps model names (e.g., `gpt-4.1`) to your deployment names
- **Required Models**: GPT-4.1, GPT-4.1-mini, and text-embedding-3-large
- **Configuration Source**: Reads deployment names from `appsettings.json` or environment variables
- **Persistence**: Once configured, these mappings are stored in your Azure AI Foundry resource

## Get Defaults

After setting defaults, you can retrieve the current default model deployment mappings to verify the configuration.

### Code Example

```csharp
public async Task<Dictionary<string, object>> GetDefaultsAsync()
{
    var defaults = await _client.GetDefaultsAsync();
    
    Console.WriteLine("✅ Retrieved default settings");
    
    if (defaults.TryGetValue("modelDeployments", out var modelDeploymentsValue))
    {
        var modelDeployments = JsonSerializer.Deserialize<Dictionary<string, string>>(modelDeploymentsValue.ToString()!);
        if (modelDeployments != null && modelDeployments.Count > 0)
        {
            Console.WriteLine("\n📋 Model Deployments:");
            foreach (var (modelName, deploymentName) in modelDeployments)
            {
                Console.WriteLine($"   {modelName}: {deploymentName}");
            }
        }
        else
        {
            Console.WriteLine("\n   No model deployments configured");
        }
    }
    
    return defaults;
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L222-L250)

### Use Cases

- **Verify Configuration**: Check that model deployments are correctly configured
- **Debug Issues**: Troubleshoot problems with prebuilt analyzers
- **Audit Settings**: Review current default mappings

## Create a Simple Analyzer

This sample creates a custom analyzer based on `prebuilt-callCenter` that extracts structured information from call recordings.

### Analyzer Template Structure

The analyzer template defines:
- **Base Analyzer** - `prebuilt-callCenter` provides the foundation
- **Field Schema** - Custom fields to extract:
  - `Summary` - A one-paragraph summary (generate method)
  - `Topics` - Top 5 topics mentioned (generate method)
  - `Companies` - List of companies mentioned (generate method)
  - `People` - List of people with names and roles (generate method)
  - `Sentiment` - Overall sentiment classification (classify method)
  - `Categories` - Relevant business categories (classify method)
- **Configuration** - Settings like locales and return details
- **Models** - AI model to use (GPT-4.1)

### Code Example

```csharp
public async Task<JsonDocument> CreateAnalyzerAsync(string analyzerId, Dictionary<string, object> analyzerDefinition)
{
    Console.WriteLine($"Creating custom analyzer '{analyzerId}'...");

    // Convert analyzer definition to JSON and save to temp file
    string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
    try
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(analyzerDefinition, jsonOptions));

        // Create analyzer
        var response = await _client.BeginCreateAnalyzerAsync(
            analyzerId: analyzerId,
            analyzerTemplatePath: tempTemplatePath);

        // Wait for the analyzer to be created
        Console.WriteLine("Waiting for analyzer creation to complete...");
        var result = await _client.PollResultAsync(response);
        Console.WriteLine($"Analyzer '{analyzerId}' created successfully!");

        return result;
    }
    finally
    {
        // Clean up temp file
        if (File.Exists(tempTemplatePath))
        {
            File.Delete(tempTemplatePath);
        }
    }
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L28-L60)

### Field Extraction Methods

The analyzer uses two extraction methods:

1. **Generate** - For open-ended text generation:
   - `Summary` - Generates a paragraph summary
   - `Topics` - Generates a list of topics
   - `Companies` - Generates a list of company names
   - `People` - Generates structured objects with name and role

2. **Classify** - For classification tasks with predefined options:
   - `Sentiment` - Classifies as Positive, Neutral, or Negative
   - `Categories` - Classifies into business categories (Agriculture, Business, Finance, etc.)

### Analyzer ID Naming

**Important**: Analyzer IDs cannot contain hyphens. Use underscores instead:

```csharp
// ✅ Correct
string analyzerId = $"management_sample_{timestamp}";

// ❌ Incorrect
string analyzerId = $"notebooks-sample-management-{timestamp}";
```

**Source:** [`Program.cs`](Program.cs#L33)

## List All Analyzers

After creating analyzers, you can list all analyzers available in your resource, including both custom and prebuilt analyzers.

### Code Example

```csharp
public async Task<JsonElement[]?> ListAnalyzersAsync()
{
    var response = await _client.GetAllAnalyzersAsync();

    // Extract the analyzers array from the response
    var analyzers = response ?? Array.Empty<JsonElement>();

    Console.WriteLine($"Found {analyzers.Length} analyzers");

    // Display detailed information about each analyzer
    for (int i = 0; i < analyzers.Length; i++)
    {
        var analyzer = analyzers[i];
        Console.WriteLine($"Analyzer {i + 1}:");
        Console.WriteLine($"   ID: {analyzer.GetProperty("analyzerId").GetString()}");
        Console.WriteLine($"   Description: {analyzer.GetProperty("description").GetString()}");
        Console.WriteLine($"   Status: {analyzer.GetProperty("status").GetString()}");
        Console.WriteLine($"   Created at: {analyzer.GetProperty("createdAt").GetString()}");
        
        // Check if it's a prebuilt analyzer
        if (analyzerId?.StartsWith("prebuilt-") == true)
        {
            Console.WriteLine("   Type: Prebuilt analyzer");
        }
        else
        {
            Console.WriteLine("   Type: Custom analyzer");
        }
    }

    return analyzers;
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L50-L111)

### Output Information

For each analyzer, the list operation displays:
- **Analyzer ID** - Unique identifier
- **Description** - Analyzer description
- **Status** - Current status (e.g., "Succeeded", "Failed")
- **Created At** - Timestamp when the analyzer was created
- **Type** - Whether it's a prebuilt or custom analyzer
- **Tags** - Optional tags associated with the analyzer

## Get Analyzer Details by ID

You can retrieve detailed information about a specific analyzer using its ID. This is useful for:
- Reviewing the complete analyzer definition
- Understanding field schemas and configurations
- Debugging analyzer issues
- Documenting analyzer specifications

### Code Example

```csharp
public async Task<string> GetAnalyzerDetailsAsync(string analyzerId)
{
    var retrievedAnalyzer = await _client.GetAnalyzerDetailByIdAsync(analyzerId);

    Console.WriteLine($"Analyzer '{analyzerId}' retrieved successfully!");

    // Extract basic information
    Console.WriteLine($"   Description: {retrievedAnalyzer["description"]}");
    Console.WriteLine($"   Status: {retrievedAnalyzer["status"]}");
    Console.WriteLine($"   Created at: {retrievedAnalyzer["createdAt"]}");

    // Print the full analyzer response
    Console.WriteLine("\nFull Analyzer Details:");
    string jsonOutput = JsonSerializer.Serialize(
        retrievedAnalyzer,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    Console.WriteLine(jsonOutput);
    return jsonOutput;
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L119-L162)

### Analyzer Details Include

The detailed response includes:
- Complete field schema definitions
- Base analyzer information
- Configuration settings
- Model assignments
- Status and metadata
- Creation timestamps

## Delete an Analyzer

When you no longer need an analyzer, you can delete it to free up resources and keep your resource organized.

### Code Example

```csharp
public async Task DeleteAnalyzerAsync(string analyzerId)
{
    Console.WriteLine($"\nDeleting analyzer '{analyzerId}'...");
    await _client.DeleteAnalyzerAsync(analyzerId);
    Console.WriteLine($"Analyzer '{analyzerId}' deleted successfully!");
}
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L170-L180)

### Important Notes

- **Permanent Operation** - Deletion is permanent and cannot be undone
- **No Recovery** - Once deleted, you'll need to recreate the analyzer if needed
- **Clean Up** - Regularly delete unused analyzers to keep your resource organized

## Running the Sample

### Build the Project

```bash
cd Management
dotnet build
```

### Run the Sample

```bash
dotnet run
```

### Expected Behavior

The sample will:

1. **Update Defaults** - Configure default model deployment mappings
2. **Get Defaults** - Retrieve and display the current default model deployment mappings
3. **Create** a custom analyzer based on `prebuilt-callCenter`
4. **List** all analyzers in your resource
5. **Retrieve** detailed information about the created analyzer
6. **Delete** the analyzer to clean up

### Sample Output

```
=== Update Defaults ===
Configuring default model deployments...
   GPT-4.1 deployment: gpt-4.1
   GPT-4.1-mini deployment: gpt-4.1-mini
   text-embedding-3-large deployment: text-embedding-3-large

✅ Default model deployments configured successfully
   Model mappings:
     gpt-4.1 → gpt-4.1
     gpt-4.1-mini → gpt-4.1-mini
     text-embedding-3-large → text-embedding-3-large

=== Get Defaults ===
✅ Retrieved default settings

📋 Model Deployments:
   gpt-4.1: gpt-4.1
   gpt-4.1-mini: gpt-4.1-mini
   text-embedding-3-large: text-embedding-3-large

=== Create Analyzer ===
Creating custom analyzer 'management_sample_1734652800'...
Waiting for analyzer creation to complete...
Analyzer 'management_sample_1734652800' created successfully!

Found 15 analyzers

Analyzer 1:
   ID: prebuilt-invoice
   Description: Extract structured data from invoices
   Status: Succeeded
   Created at: 2024-01-01T00:00:00Z
   Type: Prebuilt analyzer

...

Analyzer 15:
   ID: management_sample_1734652800
   Description: Sample call recording analytics
   Status: Succeeded
   Created at: 2024-12-19T22:56:33Z
   Type: Custom analyzer

Analyzer 'management_sample_1734652800' retrieved successfully!
   Description: Sample call recording analytics
   Status: Succeeded
   Created at: 2024-12-19T22:56:33Z

Full Analyzer Details:
{
  "analyzerId": "management_sample_1734652800",
  "description": "Sample call recording analytics",
  "status": "Succeeded",
  ...
}

Deleting analyzer 'management_sample_1734652800'...
Analyzer 'management_sample_1734652800' deleted successfully!
```

## Key Implementation Details

### Asynchronous Analyzer Creation

Analyzer creation uses the standard asynchronous pattern:

```csharp
// Begin the creation operation
var response = await _client.BeginCreateAnalyzerAsync(
    analyzerId: analyzerId,
    analyzerTemplatePath: analyzerTemplatePath);

// Poll until completion
var result = await _client.PollResultAsync(response);
```

**Source:** [`ManagementService.cs`](Services/ManagementService.cs#L33-L39)

### Analyzer Template Format

The analyzer template is provided as a `Dictionary<string, object>`. The service automatically serializes it to JSON and writes it to a temporary file before passing it to the API:

```csharp
var contentAnalyzer = new Dictionary<string, object>
{
    ["baseAnalyzerId"] = "prebuilt-callCenter",
    ["description"] = "Sample call recording analytics",
    ["config"] = new Dictionary<string, object> { /* ... */ },
    ["fieldSchema"] = new Dictionary<string, object> { /* ... */ },
    ["models"] = new Dictionary<string, object> { /* ... */ }
};

// The service handles serialization and temp file creation
await service.CreateAnalyzerAsync(analyzerId, contentAnalyzer);
```

**Source:** [`Program.cs`](Program.cs#L62-L163)

### REST API Client

The sample uses `AzureContentUnderstandingClient` from `ContentUnderstanding.Common`, which is a thin REST client that directly maps to the Content Understanding REST API. This provides transparency into the actual HTTP requests being made.

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs#L268-L427)

### Error Handling

The client methods automatically handle HTTP errors and raise exceptions with detailed error messages. The sample relies on this built-in error handling.

## Learn More

- [Azure AI Content Understanding Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)
- [Prebuilt Analyzers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/prebuilt-analyzers)
- [Custom Analyzers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/custom-analyzers)
- [Field Extraction Methods](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/field-extraction)

## Related Samples

- **[FieldExtraction](../FieldExtraction/)** - Learn how to use prebuilt analyzers and create custom analyzers for field extraction
- **[ContentExtraction](../ContentExtraction/)** - Extract semantic content from documents, audio, and video
- **[Classifier](../Classifier/)** - Create classifiers to categorize documents
- **[AnalyzerTraining](../AnalyzerTraining/)** - Train custom analyzers with labeled samples for improved performance
- **[ModelDeploymentSetup](../ModelDeploymentSetup/)** - Configure model deployments required for prebuilt analyzers

