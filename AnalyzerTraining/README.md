# Analyzer Training Sample

This sample demonstrates how to enhance your analyzer's performance by training it with labeled data. Labeled data consists of samples that have been tagged with one or more labels to add context or meaning, which improves the analyzer's accuracy.

> **Note**: Currently, this feature is only available when the baseAnalyzerId is set to `prebuilt-document`.

In your own projects, you can use [Azure Content Understanding Studio](https://contentunderstanding.ai.azure.com/home) to annotate your data with the labeling tool.

## Overview

The Analyzer Training sample demonstrates how to:

1. **Prepare Labeled Data** - Upload training documents with their corresponding label files and OCR results to Azure Blob Storage
2. **Create Analyzer with Training Data** - Create a custom analyzer that uses labeled training data to improve extraction accuracy
3. **Analyze Documents** - Use the trained analyzer to extract fields from documents
4. **Clean Up** - Delete the analyzer after testing (optional)

## Prerequisites

1. **⚠️ IMPORTANT: Run ModelDeploymentSetup First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - This is a one-time setup that maps your deployed models to the prebuilt analyzers.
   - See the main [README.md](../README.md#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for detailed instructions.

2. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.

3. Ensure you have deployed the required models (GPT-4.1, GPT-4.1-mini, and text-embedding-3-large) in Azure AI Foundry.

4. **Set up Training Data Storage**:
   - Create an Azure Storage Account and Blob Container (see [Set up training data](../docs/set_env_for_training_data_and_reference_doc.md) for detailed instructions)
   - Configure training data settings in `appsettings.json` or as environment variables:
     
     **Option 1: Use SAS URL directly**
     - `TRAINING_DATA_SAS_URL` - The full SAS URL for your Azure Blob container (e.g., `https://mystorageaccount.blob.core.windows.net/mycontainer?sv=2023-01-03&ss=b&srt=co&sp=rwl&se=...`)
     - `TRAINING_DATA_PATH` - The folder path within the container (e.g., `training_data/` or `labeling-data/`)
     
     **Option 2: Use Storage Account and Container Names** (for automatic SAS URL generation)
     - `TRAINING_DATA_STORAGE_ACCOUNT_NAME` - The name of your Azure Storage account (e.g., `mystorageaccount`)
     - `TRAINING_DATA_CONTAINER_NAME` - The name of your blob container (e.g., `mycontainer`)
     - `TRAINING_DATA_PATH` - The folder path within the container (e.g., `training_data/` or `labeling-data/`). If not set or empty, defaults to container root.
     
     **Important Notes:**
     - The storage account name is **not** a container URL. It's just the account name.
     - When using storage account and container names, the sample will automatically generate a SAS URL using Azure AD authentication (`DefaultAzureCredential`). Ensure your Azure credentials have permissions to generate SAS tokens for the storage account.
     - To extract the account name and container name from a storage URL:
       - Storage URL format: `https://{account-name}.blob.core.windows.net/{container-name}/...`
       - Example: From `https://mystorageaccount.blob.core.windows.net/mycontainer/training_data/`
         - Account name: `mystorageaccount`
         - Container name: `mycontainer`
         - Path: `training_data/`
     - The `TRAINING_DATA_PATH` value will automatically have a trailing `/` added if not present. An empty value uses the container root.
     - Example `appsettings.json` configuration:
       ```json
       {
         "TRAINING_DATA_SAS_URL": null,
         "TRAINING_DATA_STORAGE_ACCOUNT_NAME": "mystorageaccount",
         "TRAINING_DATA_CONTAINER_NAME": "mycontainer",
         "TRAINING_DATA_PATH": "training_data/"
       }
       ```

5. **Prepare Training Data**:
   - The training folder should contain a flat (one-level) directory of labeled receipt documents
   - Each document must include:
     - The original file (e.g., PDF or image)
     - A corresponding `.labels.json` file with labeled fields
     - A corresponding `.result.json` file with OCR results
   - Sample training data is available in `ContentUnderstanding.Common/data/document_training/`

## Key Concepts

### What is Labeled Data?

Labeled data consists of document samples that have been manually annotated with the expected field values. This training data helps the analyzer:

- **Learn from Examples** - Understand how to extract specific fields from similar documents
- **Improve Accuracy** - Better recognize patterns and variations in document formats
- **Handle Edge Cases** - Learn to handle unusual or complex document layouts

### Training Data Structure

Each training document requires three files:

1. **Original Document** - The source file (PDF, image, etc.)
2. **Labels File** (`*.labels.json`) - Contains the expected field values for the document
3. **OCR Results File** (`*.result.json`) - Contains the OCR analysis results from `prebuilt-documentSearch`

### Knowledge Sources with Labeled Data

When creating an analyzer with training data, you specify `knowledgeSources` with `kind: "labeledData"`:

```csharp
var knowledgeSourceConfig = new Dictionary<string, object>
{
    ["kind"] = "labeledData",
    ["containerUrl"] = trainingStorageContainerSasUrl,
    ["prefix"] = trainingStorageContainerPathPrefix
};
```

This tells the analyzer to use the labeled data in the specified blob container to improve its extraction accuracy.

### When to Use Training Data

Use labeled training data when:

- You have domain-specific documents that prebuilt analyzers don't handle well
- You need higher accuracy for specific field extractions
- You have a collection of labeled documents ready for training
- You want to improve extraction for custom document types

## Code Structure

### Main Entry Point

The `Program.cs` file follows the Python notebook structure:

1. **Prerequisites** - Display setup instructions and verify model deployments
2. **Prepare Labeled Data** - Upload training documents to Azure Blob Storage
3. **Create Analyzer** - Create a custom analyzer with knowledge sources (labeled data)
4. **Analyze Document** - Use the trained analyzer to extract fields
5. **Clean Up** - Delete the analyzer (optional)

```csharp
// 1. Upload training data
await service.GenerateTrainingDataOnBlobAsync(
    trainingDocsFolder, 
    trainingDataSasUrl, 
    trainingDataPath);

// 2. Create analyzer with training data
var analyzerResult = await service.CreateAnalyzerAsync(
    analyzerId,
    contentAnalyzer,
    trainingDataSasUrl,
    trainingDataPath);

// 3. Analyze document with trained analyzer
await service.AnalyzeDocumentWithCustomAnalyzerAsync(
    analyzerId, 
    customAnalyzerSampleFilePath);
```

**Source:** [`Program.cs`](Program.cs#L65-L250)

### Service Implementation

The `AnalyzerTrainingService` class handles:

- **Training Data Upload** - Uploads documents, labels, and OCR results to blob storage
- **Analyzer Creation** - Creates analyzers with knowledge sources (labeled data)
- **Document Analysis** - Analyzes documents using trained analyzers
- **Result Display** - Displays extracted fields and metadata

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs)

## Prepare Labeled Data

### Training Data Upload

The sample uploads training documents along with their associated label and OCR result files to Azure Blob Storage. Each document must have corresponding `.labels.json` and `.result.json` files.

```csharp
public async Task GenerateTrainingDataOnBlobAsync(
    string trainingDocsFolder,
    string storageContainerSasUrl,
    string storageContainerPathPrefix)
{
    var containerClient = new BlobContainerClient(new Uri(storageContainerSasUrl));
    var files = Directory.GetFiles(trainingDocsFolder);

    foreach (var file in files)
    {
        // Check if file is a supported document type
        if (BlobFileConstants.IsSupportedDocumentType(fileName))
        {
            // Verify label and OCR result files exist
            string labelFileName = BlobFileConstants.GetLabelFilePath(fileName);
            string ocrResultFileName = BlobFileConstants.GetOcrResultFilePath(fileName);
            
            // Upload all three files to blob storage
            await containerClient.UploadFileAsync(file, fileBlobPath);
            await containerClient.UploadFileAsync(labelPath, labelBlobPath);
            await containerClient.UploadFileAsync(ocrResultPath, ocrResultBlobPath);
        }
    }
}
```

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L41-L88)

### Training Data Requirements

- **File Structure**: Flat directory (one level) containing all training documents
- **Required Files per Document**:
  - Original document file (PDF, image, etc.)
  - `{filename}.labels.json` - Labeled field values
  - `{filename}.result.json` - OCR analysis results
- **Supported Document Types**: `.pdf`, `.tiff`, `.jpg`, `.jpeg`, `.png`, `.bmp`, `.heif`

## Create Analyzer with Defined Schema

### Analyzer Definition

The sample creates a receipt analyzer that extracts merchant name, items, and total price:

```csharp
var contentAnalyzer = new Dictionary<string, object>
{
    ["baseAnalyzerId"] = "prebuilt-document",
    ["description"] = "Extract useful information from receipt with labeled training data",
    ["config"] = new Dictionary<string, object>
    {
        ["returnDetails"] = true,
        ["enableLayout"] = true,
        ["enableFormula"] = false,
        ["estimateFieldSourceAndConfidence"] = true
    },
    ["fieldSchema"] = new Dictionary<string, object>
    {
        ["name"] = "receipt schema",
        ["description"] = "Schema for receipt",
        ["fields"] = new Dictionary<string, object>
        {
            ["MerchantName"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["method"] = "extract",
                ["description"] = "Name of the merchant"
            },
            ["Items"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["method"] = "generate",
                ["description"] = "List of items purchased",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["method"] = "extract",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Quantity"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Quantity of the item"
                        },
                        ["Name"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Name of the item"
                        },
                        ["Price"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Price of the item"
                        }
                    }
                }
            },
            ["TotalPrice"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["method"] = "extract",
                ["description"] = "Total price on the receipt"
            }
        }
    },
    ["models"] = new Dictionary<string, object>
    {
        ["completion"] = "gpt-4.1",
        ["embedding"] = "text-embedding-3-large"  // Required when using knowledge sources
    }
};
```

**Source:** [`Program.cs`](Program.cs#L180-L250)

### Adding Knowledge Sources

The service automatically adds knowledge sources with labeled data to the analyzer definition:

```csharp
// Create knowledge source configuration for labeled data
var knowledgeSourceConfig = new Dictionary<string, object>
{
    ["kind"] = "labeledData",
    ["containerUrl"] = trainingStorageContainerSasUrl,
    ["prefix"] = trainingStorageContainerPathPrefix
};

analyzerDefinition["knowledgeSources"] = new List<Dictionary<string, object>> { knowledgeSourceConfig };
```

When creating the analyzer, the sample displays the knowledge source information:

```
Creating custom analyzer 'analyzer_training_sample_...'...
   Knowledge source: Training data uploaded to https://mystorageaccount.blob.core.windows.net/mycontainer
   Data path: training_data/
```

Note that the container URL is displayed without the SAS token for security. The actual SAS URL (with token) is used internally for API calls.

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L100-L145)

### Analyzer Creation Process

1. **Add Knowledge Sources** - Configure labeled data source
2. **Create Analyzer** - Send analyzer definition to API
3. **Poll for Completion** - Wait for analyzer creation to complete
4. **Display Results** - Show analyzer details and field schema

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L95-L200)

## Use Created Analyzer to Extract Document Content

After the analyzer is successfully created, you can use it to analyze your input files:

```csharp
public async Task<JsonDocument> AnalyzeDocumentWithCustomAnalyzerAsync(
    string analyzerId, 
    string filePath)
{
    // Begin document analysis
    var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(analyzerId, resolvedFilePath);
    
    // Wait for analysis completion
    var analysisResult = await _client.PollResultAsync(analyzeResponse);
    
    // Display results
    DisplayAnalysisResults(analysisResult);
    
    // Save result
    return analysisResult;
}
```

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L202-L240)

## Running the Sample

1. **⚠️ IMPORTANT: Configure Model Deployments First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - See the [Prerequisites](#prerequisites) section above for details.

2. **Set up Training Data Storage**:
   - Create an Azure Storage Account and Blob Container
   - Configure training data settings in `appsettings.json`:
     - **Option 1**: Set `TRAINING_DATA_SAS_URL` (full SAS URL) and `TRAINING_DATA_PATH`
     - **Option 2**: Set `TRAINING_DATA_STORAGE_ACCOUNT_NAME`, `TRAINING_DATA_CONTAINER_NAME`, and `TRAINING_DATA_PATH`
   - See the [Prerequisites](#prerequisites) section above for detailed configuration instructions and how to extract account/container names from storage URLs

3. **Build the project:**
   ```bash
   cd AnalyzerTraining
   dotnet build
   ```

4. **Run the sample:**
   ```bash
   dotnet run
   ```
   
   The sample will:
   - Ask you to confirm that model deployments have been configured
   - Display current training data configuration from `appsettings.json` or environment variables
   - Prompt for missing training data configuration (SAS URL or storage account/container names, and path if not set)
   - Automatically generate SAS URL from storage account and container name if needed (using Azure AD authentication)
   - Upload training data to Azure Blob Storage
   - Create a custom analyzer with labeled training data (showing knowledge source container URL and data path)
   - Analyze a sample document using the trained analyzer
   - Display extracted fields (the main purpose of the sample)
   - Optionally delete the analyzer

## Output

The sample saves full analysis results as JSON files in the `sample_output/analyzer_training/` directory. Each result includes:

- **Extracted fields** - Structured data matching your field schema
- **Field metadata** - Confidence scores, source locations, and bounding boxes
- **Full analysis result** - Complete JSON response from the API

### Displaying Analysis Results

The sample focuses on displaying the extracted fields, which is the main purpose of analyzer training:

```
Analyzer Training Results:

MerchantName:
  Value: CONTOSO LTD.

Items:
  Array with 3 items:
    Item 1:
      Quantity: 1
      Name: Item A
      Price: 10.00
    Item 2:
      Quantity: 2
      Name: Item B
      Price: 20.00

TotalPrice:
  Value: 30.00
```

The sample does not display document/page/table information, as the focus is on demonstrating the trained analyzer's field extraction capabilities.

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L300-L404)

## Key Implementation Details

### Path Resolution

The sample includes a `ResolveDataFilePath()` helper method that automatically finds data files in multiple locations:

```csharp
private static string ResolveDataFilePath(string fileNameOrFolder)
{
    // Tries multiple locations:
    // 1. Current directory ./data/
    // 2. Assembly directory (output directory) data/
    // 3. ContentUnderstanding.Common/data/
}
```

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L520-L560)

### Training Data Upload

The upload process ensures that each document has the required label and OCR result files:

```csharp
// Verify required files exist
string labelFileName = BlobFileConstants.GetLabelFilePath(fileName);
string ocrResultFileName = BlobFileConstants.GetOcrResultFilePath(fileName);

if (File.Exists(labelPath) && File.Exists(ocrResultPath))
{
    // Upload all three files
    await containerClient.UploadFileAsync(file, fileBlobPath);
    await containerClient.UploadFileAsync(labelPath, labelBlobPath);
    await containerClient.UploadFileAsync(ocrResultPath, ocrResultBlobPath);
}
```

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L54-L78)

### Knowledge Sources Configuration

The service automatically configures knowledge sources with labeled data:

```csharp
var knowledgeSourceConfig = new Dictionary<string, object>
{
    ["kind"] = "labeledData",
    ["containerUrl"] = trainingStorageContainerSasUrl,
    ["prefix"] = trainingStorageContainerPathPrefix
};

// Optionally add file list path
var fileListPath = Environment.GetEnvironmentVariable("CONTENT_UNDERSTANDING_FILE_LIST_PATH");
if (!string.IsNullOrEmpty(fileListPath))
{
    knowledgeSourceConfig["fileListPath"] = fileListPath;
}

analyzerDefinition["knowledgeSources"] = new List<Dictionary<string, object>> { knowledgeSourceConfig };
```

When creating the analyzer, the sample displays the knowledge source information showing the container URL (without the SAS token) and the data path:

```csharp
// Extract container URL from SAS URL (remove SAS token)
var containerUrl = ExtractContainerUrlFromSasUrl(trainingStorageContainerSasUrl);
Console.WriteLine($"   Knowledge source: Training data uploaded to {containerUrl}");
Console.WriteLine($"   Data path: {trainingStorageContainerPathPrefix}");
```

**Source:** [`AnalyzerTrainingService.cs`](Services/AnalyzerTrainingService.cs#L100-L145)

### Analyzer Lifecycle

The sample follows this workflow:

1. **Upload Training Data** - Upload documents, labels, and OCR results to blob storage
2. **Create Analyzer** - Define and create the custom analyzer with knowledge sources
3. **Analyze Document** - Use the analyzer to extract fields from a document
4. **Display Results** - Show extracted fields and metadata
5. **Save Results** - Persist the analysis results to JSON
6. **Delete Analyzer** - Clean up the analyzer (in production, you might keep analyzers for reuse)

**Source:** [`Program.cs`](Program.cs#L65-L280)

### Error Handling

The sample includes comprehensive error handling:

```csharp
try
{
    await service.GenerateTrainingDataOnBlobAsync(...);
    Console.WriteLine("Training data upload completed.");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"❌ Missing required file: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to upload training data: {ex.Message}");
}
```

Note: Emojis are only used for warnings (⚠️) and errors (❌). Informational messages do not use emojis.

**Source:** [`Program.cs`](Program.cs#L213-L223)

### Asynchronous Operations

All Content Understanding operations are asynchronous:

- `BeginCreateAnalyzerAsync()` - Starts analyzer creation
- `PollResultAsync()` - Waits for analyzer creation to complete
- `BeginAnalyzeBinaryAsync()` - Starts file analysis
- `DeleteAnalyzerAsync()` - Deletes the analyzer

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs)

## Learn More

- **[Content Understanding Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)** - Comprehensive introduction to the service
- **[Analyzer Training](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/analyzer-training)** - Learn about training analyzers with labeled data
- **[Azure Content Understanding Studio](https://contentunderstanding.ai.azure.com/home)** - How to label your training data
- **[Analyzer Reference](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/analyzer-reference)** - Complete analyzer configuration documentation
- **[Set up Training Data](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/how-to/training-data)** - Detailed guide on preparing training data

## Related Samples

- **[ContentExtraction](../ContentExtraction/)** - Extract content from documents, audio, and video using prebuilt analyzers
- **[FieldExtraction](../FieldExtraction/)** - Extract custom fields from documents, audio, and video
- **[Management](../Management/)** - Create, list, and manage analyzers

