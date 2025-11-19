# Classifier Sample

This sample demonstrates how to use the Azure AI Content Understanding API to create classifiers that categorize documents and optionally extract fields using custom analyzers.

## Overview

The Classifier sample demonstrates three key capabilities:

1. **Create a Basic Classifier** - Categorize documents into predefined categories (e.g., Loan application, Invoice, Bank Statement)
2. **Create a Custom Analyzer** - Extract specific fields from documents (e.g., loan application details)
3. **Create an Enhanced Classifier** - Combine classification with field extraction in a single operation

## Prerequisites

1. **⚠️ IMPORTANT: Run ModelDeploymentSetup First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - This is a one-time setup that maps your deployed models to the prebuilt analyzers.
   - See the main [README.md](../README.md#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for detailed instructions.

2. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.

3. Ensure you have deployed the required models (GPT-4.1, GPT-4.1-mini, and text-embedding-3-large) in Azure AI Foundry.

4. Configure your `appsettings.json` file with your endpoint and deployment names.

## Key Concepts

### What is a Classifier?

A classifier categorizes documents into predefined categories. In Azure AI Content Understanding, classification is integrated directly into the analyzer operation. You define **`categories`** within the classifier's configuration, specifying category names and descriptions that the service uses to categorize your input files.

### Enhanced Classifiers with Custom Analyzers

You can enhance a classifier by associating specific analyzers with categories. When a document is classified into a category, the associated analyzer automatically extracts fields from that document segment.

For example:
- **Loan application** category → Custom loan analyzer (extracts ApplicationDate, ApplicantName, LoanAmount, etc.)
- **Invoice** category → Prebuilt invoice analyzer or custom invoice analyzer
- **Bank Statement** category → No analyzer (classification only)

This combines document classification with field extraction in one operation.

### Classifier Schema Structure

A classifier schema defines:
- **`categories`**: Dictionary of category names to category definitions
  - Each category can have:
    - **`description`**: Description of the category
    - **`analyzerId`**: (Optional) Analyzer to use for field extraction on documents in this category
- **`splitMode`**: How to handle multi-document files (`"auto"` or `"none"`)

Example:
```json
{
  "categories": {
    "Loan application": {
      "description": "Documents submitted by individuals or businesses to request funding...",
      "analyzerId": "loan_analyzer_123"
    },
    "Invoice": {
      "description": "Billing documents issued by sellers..."
    }
  },
  "splitMode": "auto"
}
```

For more detailed information about classification capabilities, best practices, and advanced scenarios, see the [Content Understanding classification documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/classifier).

## Code Structure

### Main Entry Point

The `Program.cs` file follows the Python notebook structure:

1. **Create a Basic Classifier** - Classify documents into categories
2. **Create a Custom Analyzer** - Extract fields from loan applications
3. **Create an Enhanced Classifier** - Combine classification with field extraction

```csharp
// Create basic classifier
var basicClassifierSchema = new Dictionary<string, object>
{
    ["categories"] = new Dictionary<string, object> { /* ... */ },
    ["splitMode"] = "auto"
};
await service.ClassifyDocumentAsync(classifierId, JsonSerializer.Serialize(basicClassifierSchema), filePath);

// Create custom analyzer
await service.CreateLoanAnalyzerAsync(loanAnalyzerId);

// Create enhanced classifier with custom analyzer
var enhancedClassifierSchema = new Dictionary<string, object>
{
    ["categories"] = new Dictionary<string, object>
    {
        ["Loan application"] = new Dictionary<string, object>
        {
            ["description"] = "...",
            ["analyzerId"] = loanAnalyzerId  // Use custom analyzer
        }
    },
    ["splitMode"] = "auto"
};
await service.ClassifyDocumentAsync(enhancedClassifierId, JsonSerializer.Serialize(enhancedClassifierSchema), filePath);
```

**Source:** [`Program.cs`](Program.cs#L60-L150)

### Service Implementation

The `ClassifierService` class implements classifier and analyzer workflows. It uses the `AzureContentUnderstandingClient` (a thin REST client) to interact with the API.

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs)

## Part 1: Create a Basic Classifier

### Classify Documents into Categories

This example creates a classifier that categorizes financial documents into three categories: Loan application, Invoice, and Bank Statement.

#### Code Example

```csharp
var classifierSchema = new Dictionary<string, object>
{
    ["categories"] = new Dictionary<string, object>
    {
        ["Loan application"] = new Dictionary<string, object>
        {
            ["description"] = "Documents submitted by individuals or businesses to request funding..."
        },
        ["Invoice"] = new Dictionary<string, object>
        {
            ["description"] = "Billing documents issued by sellers or service providers..."
        },
        ["Bank_Statement"] = new Dictionary<string, object>
        {
            ["description"] = "Official statements issued by banks..."
        }
    },
    ["splitMode"] = "auto"
};

var classifierSchemaJson = JsonSerializer.Serialize(classifierSchema);
await service.ClassifyDocumentAsync(classifierId, classifierSchemaJson, filePath);
```

**Source:** [`Program.cs`](Program.cs#L70-L95)

#### Key Capabilities

- **Automatic Categorization**: Classifies documents into predefined categories
- **Multi-Document Support**: With `splitMode: "auto"`, automatically splits and classifies different document types within a single file
- **Structured Output**: Returns classification results with category assignments and page ranges

## Part 2: Create a Custom Analyzer

### Extract Fields from Loan Applications

This example creates a custom analyzer that extracts specific fields from loan application documents.

#### Code Example

```csharp
public async Task<string> CreateLoanAnalyzerAsync(string analyzerId)
{
    var customAnalyzer = new Dictionary<string, object>
    {
        ["baseAnalyzerId"] = "prebuilt-documentAnalyzer",
        ["description"] = "Loan application analyzer - extracts key information from loan applications",
        ["config"] = new Dictionary<string, object>
        {
            ["returnDetails"] = true,
            ["enableLayout"] = true,
            ["estimateFieldSourceAndConfidence"] = true
        },
        ["fieldSchema"] = new Dictionary<string, object>
        {
            ["fields"] = new Dictionary<string, object>
            {
                ["ApplicationDate"] = new Dictionary<string, object>
                {
                    ["type"] = "date",
                    ["method"] = "generate",
                    ["description"] = "The date when the loan application was submitted."
                },
                ["ApplicantName"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["method"] = "generate",
                    ["description"] = "Full name of the loan applicant or company."
                },
                ["LoanAmountRequested"] = new Dictionary<string, object>
                {
                    ["type"] = "number",
                    ["method"] = "generate",
                    ["description"] = "The total loan amount requested by the applicant."
                }
                // ... more fields
            }
        }
    };
    
    // Create analyzer using thin client
    var createResponse = await _client.BeginCreateAnalyzerAsync(analyzerId, analyzerTemplatePath);
    var analyzerResult = await _client.PollResultAsync(createResponse);
    
    return analyzerId;
}
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L120-L220)

#### Extracted Fields

The loan analyzer extracts:
- **ApplicationDate** - Date when the loan application was submitted
- **ApplicantName** - Full name of the loan applicant or company
- **LoanAmountRequested** - Total loan amount requested
- **LoanPurpose** - Stated purpose or reason for the loan
- **CreditScore** - Credit score of the applicant (if available)
- **Summary** - Brief overview of the loan application details

## Part 3: Create an Enhanced Classifier

### Combine Classification with Field Extraction

This example creates an enhanced classifier that uses the custom loan analyzer for loan application documents, combining classification with field extraction in one operation.

#### Code Example

```csharp
// First, create the custom analyzer
await service.CreateLoanAnalyzerAsync(loanAnalyzerId);

// Then, create enhanced classifier with custom analyzer
var enhancedClassifierSchema = new Dictionary<string, object>
{
    ["categories"] = new Dictionary<string, object>
    {
        ["Loan application"] = new Dictionary<string, object>
        {
            ["description"] = "Documents submitted by individuals or businesses to request funding...",
            ["analyzerId"] = loanAnalyzerId  // Use custom analyzer
        },
        ["Invoice"] = new Dictionary<string, object>
        {
            ["description"] = "Billing documents issued by sellers..."
        },
        ["Bank_Statement"] = new Dictionary<string, object>
        {
            ["description"] = "Official statements issued by banks..."
        }
    },
    ["splitMode"] = "auto"
};

var enhancedClassifierSchemaJson = JsonSerializer.Serialize(enhancedClassifierSchema);
await service.ClassifyDocumentAsync(enhancedClassifierId, enhancedClassifierSchemaJson, filePath);
```

**Source:** [`Program.cs`](Program.cs#L110-L150)

#### Key Benefits

- **Single Operation**: Classify and extract fields in one API call
- **Category-Specific Extraction**: Different analyzers for different document types
- **Efficient Processing**: Process mixed document files with automatic segmentation

## Running the Sample

1. **⚠️ IMPORTANT: Configure Model Deployments First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - See the [Prerequisites](#prerequisites) section above for details.

2. **Build the project:**
   ```bash
   cd Classifier
   dotnet build
   ```

3. **Run the sample:**
   ```bash
   dotnet run
   ```
   
   The sample will:
   - Ask you to confirm that model deployments have been configured
   - Create a basic classifier and classify a document
   - Create a custom loan analyzer
   - Create an enhanced classifier that combines classification with field extraction
   - Clean up created analyzers and classifiers

## Output

The sample saves full classification results as JSON files in the `sample_output/classifier/` directory. Each result includes:

- **Classification Results** - Category assignments for each document segment
- **Extracted Fields** - (For enhanced classifier) Structured data extracted by custom analyzers
- **Segment Information** - Page ranges and segment IDs for multi-document files
- **Full Analysis Result** - Complete JSON response from the API

### Displaying Classification Results

The sample automatically displays classification results in a readable format:

```
📊 Classification Results:
================================================================================

🔖 Segment 1:
   Category: Loan application
   Start Page: 1
   End Page: 3
   Segment ID: segment_123

   📋 Extracted Fields (6):
      • ApplicationDate: 2024-01-15
      • ApplicantName: John Doe
      • LoanAmountRequested: 50000.00
      • LoanPurpose: Business expansion
      • CreditScore: 750
      • Summary: Loan application for business expansion...

🔖 Segment 2:
   Category: Invoice
   Start Page: 4
   End Page: 5
   Segment ID: segment_456
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L240-L320)

## Key Implementation Details

### Classifier Creation

Classifiers are created using the `BeginCreateClassifierAsync` method, which accepts a classifier schema as a JSON string:

```csharp
var createResponse = await _client.BeginCreateClassifierAsync(
    classifierId: classifierId,
    classifierSchema: classifierSchemaJson);

var classifierResult = await _client.PollResultAsync(createResponse);
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L40-L60)

### Document Classification

Documents are classified using the `BeginClassifierAsync` method:

```csharp
var classifyResponse = await _client.BeginClassifierAsync(
    classifierId: classifierId,
    fileLocation: resolvedFilePath);

var classificationResult = await _client.PollResultAsync(classifyResponse);
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L80-L90)

### Path Resolution

The sample includes a `ResolveDataFilePath()` helper method that automatically finds data files in multiple locations:

```csharp
private static string ResolveDataFilePath(string fileName)
{
    // Tries multiple locations:
    // 1. Current directory ./data/
    // 2. Assembly directory (output directory) data/
    // 3. ContentUnderstanding.Common/data/
}
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L380-L430)

### Analyzer Lifecycle

For custom analyzers, the sample follows this workflow:

1. **Create Analyzer** - Define and create the custom analyzer
2. **Use in Classifier** - Reference the analyzer in the classifier schema
3. **Classify Document** - Classify and extract fields in one operation
4. **Delete Analyzer** - Clean up the analyzer (in production, you might keep analyzers for reuse)

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L120-L220)

### Error Handling

The sample includes error handling that provides detailed error messages:

```csharp
catch (Exception ex)
{
    Console.WriteLine($"❌ An error occurred: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
    }
    return null;
}
```

**Source:** [`ClassifierService.cs`](Services/ClassifierService.cs#L100-L115)

### Asynchronous Operations

All Content Understanding operations are asynchronous:

- `BeginCreateClassifierAsync()` - Starts classifier creation
- `PollResultAsync()` - Waits for classifier creation to complete
- `BeginClassifierAsync()` - Starts document classification
- `DeleteAnalyzerAsync()` - Deletes the analyzer/classifier

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs)

## Learn More

- **[Content Understanding Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)** - Comprehensive introduction to the service
- **[Understanding Classifiers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/classifier)** - Detailed documentation on classifiers
- **[Analyzer Reference](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/analyzer-reference)** - Complete analyzer configuration documentation
- **[Prebuilt Analyzers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/prebuilt-analyzers)** - List of available prebuilt analyzers

## Related Samples

- **[FieldExtraction](../FieldExtraction/)** - Extract custom fields from documents, audio, and video
- **[ContentExtraction](../ContentExtraction/)** - Extract content from documents, audio, and video using prebuilt analyzers
- **[AnalyzerTraining](../AnalyzerTraining/)** - Train custom analyzers with labeled data for improved accuracy
- **[Management](../Management/)** - Create, list, and manage analyzers

