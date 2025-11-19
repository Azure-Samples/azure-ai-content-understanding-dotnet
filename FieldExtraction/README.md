# Field Extraction Sample

This sample demonstrates how to use the Azure AI Content Understanding API to extract custom fields from multimodal files—documents, audio, and video.

Content Understanding provides **extensive prebuilt analyzers** ready to use without training. Always start with prebuilt analyzers before building custom solutions.

## Overview

The Field Extraction sample demonstrates both approaches:

1. **Part 1: Using Prebuilt Analyzers** - Extract fields using production-ready prebuilt analyzers (recommended starting point)
2. **Part 2: Creating Custom Analyzers** - Create custom analyzers when prebuilt options don't meet your needs

## Prerequisites

1. **⚠️ IMPORTANT: Run ModelDeploymentSetup First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - This is a one-time setup that maps your deployed models to the prebuilt analyzers.
   - See the main [README.md](../README.md#step-4-configure-model-deployments-required-for-prebuilt-analyzers) for detailed instructions.

2. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.

3. Ensure you have deployed the required models (GPT-4.1, GPT-4.1-mini, and text-embedding-3-large) in Azure AI Foundry.

4. Configure your `appsettings.json` file with your endpoint and deployment names.

## Key Concepts

### Why Start with Prebuilt Analyzers?

Azure AI Content Understanding provides **70+ production-ready prebuilt analyzers** that cover common scenarios across finance, healthcare, legal, tax, and business domains. These analyzers are:

- **Immediately Available** - No training, configuration, or customization needed
- **Battle-Tested** - Built on rich knowledge bases of thousands of real-world document examples
- **Continuously Improved** - Regularly updated by Microsoft to handle document variations
- **Cost-Effective** - Save development time and resources by using proven solutions
- **Comprehensive Coverage** - Extensive support for:
  - Financial documents (invoices, receipts, bank statements, credit cards)
  - Identity documents (passports, driver licenses, ID cards, health insurance)
  - Tax documents (40+ US tax forms including 1040, W-2, 1099 variants)
  - Mortgage documents (applications, appraisals, disclosures)
  - Business documents (contracts, purchase orders, procurement)
  - And many more specialized scenarios

> **Best Practice**: Always explore prebuilt analyzers first. Build custom analyzers only when prebuilt options don't meet your specific requirements.

### Complete List of Prebuilt Analyzer Categories

**Content Extraction & RAG**
- `prebuilt-read`, `prebuilt-layout` - OCR and layout analysis
- `prebuilt-documentSearch`, `prebuilt-imageSearch`, `prebuilt-audioSearch`, `prebuilt-videoSearch` - RAG-optimized

**Financial Documents**
- `prebuilt-invoice`, `prebuilt-receipt`, `prebuilt-creditCard`, `prebuilt-bankStatement.us`, `prebuilt-check.us`, `prebuilt-creditMemo`

**Identity & Healthcare**
- `prebuilt-idDocument`, `prebuilt-idDocument.passport`, `prebuilt-healthInsuranceCard.us`

**Tax Documents (US)**
- 40+ tax form analyzers including `prebuilt-tax.us.1040`, `prebuilt-tax.us.w2`, all 1099 variants, 1098 series, and more

**Mortgage Documents (US)**
- `prebuilt-mortgage.us.1003`, `prebuilt-mortgage.us.1004`, `prebuilt-mortgage.us.1005`, `prebuilt-mortgage.us.closingDisclosure`

**Legal & Business**
- `prebuilt-contract`, `prebuilt-procurement`, `prebuilt-purchaseOrder`, `prebuilt-marriageCertificate.us`

**Other Specialized**
- `prebuilt-utilityBill`, `prebuilt-payStub.us`, and more

> **Learn More**: [Complete Prebuilt Analyzers Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/prebuilt-analyzers)

### Build Custom Analyzers (When Needed)

Create custom analyzers only when prebuilt ones don't meet your needs:
- Extract fields specific to your business
- Process proprietary document types
- Customize extraction logic for unique requirements

## Code Structure

### Main Entry Point

The `Program.cs` file follows the Python notebook structure:

1. **Part 1**: Uses prebuilt analyzers (`prebuilt-invoice`, `prebuilt-receipt`)
2. **Part 2**: Creates custom analyzers for specific use cases

```csharp
// Part 1: Using Prebuilt Analyzers
await service.AnalyzeWithPrebuiltAnalyzer("prebuilt-invoice", "invoice.pdf", "prebuilt_invoice_analysis_result");
await service.AnalyzeWithPrebuiltAnalyzer("prebuilt-receipt", "receipt.png", "prebuilt_receipt_analysis_result");

// Part 2: Creating Custom Analyzers
var customAnalyzers = new Dictionary<string, (Dictionary<string, object>, string)>
{
    ["invoice"] = (invoiceAnalyzerDefinition, "invoice.pdf"),
    // ... more custom analyzers
};
```

**Source:** [`Program.cs`](Program.cs#L60-L280)

### Service Implementation

The `FieldExtractionService` class implements both prebuilt and custom analyzer workflows. It uses the `AzureContentUnderstandingClient` (a thin REST client) to interact with the API.

**Source:** [`FieldExtractionService.cs`](Services/FieldExtractionService.cs)

## Part 1: Using Prebuilt Analyzers

### 1. Invoice Field Extraction with Prebuilt Analyzer

The `prebuilt-invoice` analyzer automatically extracts structured data from invoice PDFs without any configuration. It identifies vendor information, invoice numbers, dates, line items, totals, taxes, and payment details.

#### Code Example

```csharp
public async Task<JsonDocument> AnalyzeWithPrebuiltAnalyzer(
    string prebuiltAnalyzerId, 
    string fileName, 
    string filenamePrefix)
{
    // Analyze the file
    var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(prebuiltAnalyzerId, resolvedFilePath);
    var analysisResult = await _client.PollResultAsync(analyzeResponse);
    
    // Display extracted fields
    DisplayExtractedFields(analysisResult);
    
    // Save result
    return analysisResult;
}
```

**Source:** [`FieldExtractionService.cs`](Services/FieldExtractionService.cs#L27-L62)

#### Key Capabilities

- **Automatic Field Extraction**: Extracts vendor name, invoice number, dates, line items, totals, taxes, payment terms
- **No Configuration Required**: Works out of the box with any invoice format
- **Structured Output**: Returns fields in a consistent JSON structure

### 2. Receipt Field Extraction with Prebuilt Analyzer

The `prebuilt-receipt` analyzer automatically extracts structured data from receipt images. It identifies merchant information, items, totals, taxes, and payment details.

**Source:** [`Program.cs`](Program.cs#L95-L110)

## Part 2: Creating Custom Analyzers

### Key Analyzer Configuration Components

- **`baseAnalyzerId`**: Specifies which prebuilt analyzer to inherit from:
  - **`prebuilt-document`** - For document-based custom analyzers (PDFs, images, Office docs)
  - **`prebuilt-audio`** - For audio-based custom analyzers
  - **`prebuilt-video`** - For video-based custom analyzers
  - **`prebuilt-image`** - For image-based custom analyzers

- **`fieldSchema`**: Defines the structured data to extract from content:
  - **`fields`**: Object defining each field to extract, with field names as keys
  - Each field definition includes:
    - **`type`**: Data type (`string`, `number`, `boolean`, `date`, `object`, `array`)
    - **`description`**: Clear explanation of the field - acts as a prompt to guide extraction accuracy
    - **`method`**: Extraction method to use:
      - **`"extract"`** - Extract values as they appear in content (literal text extraction). Requires `estimateFieldSourceAndConfidence: true`. Only supported for document analyzers.
      - **`"generate"`** - Generate values using AI based on content understanding (best for complex fields)
      - **`"classify"`** - Classify values against predefined categories (use with `enum`)
    - **`enum`**: (Optional) Fixed list of possible values for classification
    - **`items`**: (For arrays) Defines structure of array elements
    - **`properties`**: (For objects) Defines nested field structure

- **`config`**: Processing options that control analysis behavior:
  - **`returnDetails`**: Include confidence scores, bounding boxes, metadata (default: false)
  - **`enableOcr`**: Extract text from images/scans (default: true, document only)
  - **`enableLayout`**: Extract layout info like paragraphs, structure (default: true, document only)
  - **`estimateFieldSourceAndConfidence`**: Return source locations and confidence for extracted fields (document only)
  - **`locales`**: Language codes for transcription (audio/video, e.g., `["en-US"]`)
  - **`contentCategories`**: Define categories for classification and segmentation
  - **`enableSegment`**: Split content into categorized chunks (document/video)
  - **`segmentationMode`**: How to segment content (e.g., `"noSegmentation"` for video)

- **`models`**: Specifies which AI models to use:
  - **`completion`**: Model for extraction/generation tasks (e.g., `"gpt-4.1"`, `"gpt-4.1-mini"`)
  - **`embedding`**: Model for embedding tasks when using knowledge bases

For complete details, see the [Analyzer Reference Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/analyzer-reference).

### Document Analysis: Invoice Field Extraction

Let's extract fields from an invoice PDF. This custom analyzer identifies essential invoice elements such as vendor information, amounts, dates, and line items.

#### Code Example: Creating and Using Invoice Analyzer

```csharp
var invoiceAnalyzer = new Dictionary<string, object>
{
    ["baseAnalyzerId"] = "prebuilt-document",
    ["description"] = "Sample invoice analyzer that extracts vendor information, line items, and totals",
    ["config"] = new Dictionary<string, object>
    {
        ["returnDetails"] = true,
        ["enableOcr"] = true,
        ["enableLayout"] = true,
        ["estimateFieldSourceAndConfidence"] = true
    },
    ["fieldSchema"] = new Dictionary<string, object>
    {
        ["name"] = "InvoiceFields",
        ["fields"] = new Dictionary<string, object>
        {
            ["VendorName"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["method"] = "extract",
                ["description"] = "Name of the vendor or supplier"
            },
            ["Items"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["method"] = "extract",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Description"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract"
                        },
                        ["Amount"] = new Dictionary<string, object>
                        {
                            ["type"] = "number",
                            ["method"] = "extract"
                        }
                    }
                }
            }
        }
    },
    ["models"] = new Dictionary<string, object>
    {
        ["completion"] = "gpt-4.1"
    }
};

await service.CreateAndUseAnalyzer(analyzerId, invoiceAnalyzer, "invoice.pdf");
```

**Source:** [`Program.cs`](Program.cs#L125-L180)

### Audio Analysis: Call Recording Analyzer

This custom analyzer extracts structured information from call center recordings.

#### Code Example: Call Recording Analyzer

```csharp
var callRecordingAnalyzer = new Dictionary<string, object>
{
    ["baseAnalyzerId"] = "prebuilt-callCenter",
    ["description"] = "Sample call recording analytics",
    ["config"] = new Dictionary<string, object>
    {
        ["returnDetails"] = true,
        ["locales"] = new[] { "en-US" }
    },
    ["fieldSchema"] = new Dictionary<string, object>
    {
        ["fields"] = new Dictionary<string, object>
        {
            ["Summary"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["method"] = "generate",
                ["description"] = "A one-paragraph summary"
            },
            ["Sentiment"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["method"] = "classify",
                ["enum"] = new[] { "Positive", "Neutral", "Negative" }
            },
            ["Topics"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["method"] = "generate",
                ["items"] = new Dictionary<string, object> { ["type"] = "string" }
            }
        }
    }
};
```

**Source:** [`Program.cs`](Program.cs#L182-L250)

### Video Analysis: Marketing Video Analyzer

This custom analyzer extracts insights from video content.

**Source:** [`Program.cs`](Program.cs#L252-L280)

## Running the Sample

1. **⚠️ IMPORTANT: Configure Model Deployments First**
   - Before running this sample, you **must** run the [ModelDeploymentSetup](../ModelDeploymentSetup/) sample to configure model deployments.
   - See the [Prerequisites](#prerequisites) section above for details.

2. **Build the project:**
   ```bash
   cd FieldExtraction
   dotnet build
   ```

3. **Run the sample:**
   ```bash
   dotnet run
   ```
   
   The sample will:
   - Ask you to confirm that model deployments have been configured
   - **Part 1**: Process prebuilt analyzers (invoice, receipt)
   - **Part 2**: Create and process custom analyzers (invoice, call recording, conversation audio, marketing video)

## Output

The sample saves full analysis results as JSON files in the `sample_output/field_extraction/` directory. Each result includes:

- **Extracted fields** - Structured data matching your field schema
- **Field metadata** - Confidence scores, source locations (for extract method), and bounding boxes
- **Content metadata** - Pages, timing information, and other content details
- **Full analysis result** - Complete JSON response from the API

### Displaying Extracted Fields

The sample automatically displays extracted fields in a readable format:

```
Extracted Fields:
--------------------------------------------------------------------------------
VendorName: CONTOSO LTD.
  Confidence: 0.950
  Bounding Box: {...}

Items (array with 3 items):
  Item 1:
    Description: Service A
    Amount: 100.00
  Item 2:
    Description: Service B
    Amount: 200.00
```

**Source:** [`FieldExtractionService.cs`](Services/FieldExtractionService.cs#L64-L176)

## Key Implementation Details

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

**Source:** [`FieldExtractionService.cs`](Services/FieldExtractionService.cs#L290-L330)

### Analyzer Lifecycle

For custom analyzers, the sample follows this workflow:

1. **Create Analyzer** - Define and create the custom analyzer
2. **Analyze File** - Use the analyzer to extract fields from a file
3. **Display Results** - Show extracted fields in a readable format
4. **Save Results** - Persist the analysis results to JSON
5. **Delete Analyzer** - Clean up the analyzer (in production, you might keep analyzers for reuse)

**Source:** [`FieldExtractionService.cs`](Services/FieldExtractionService.cs#L178-L288)

### Error Handling

The sample includes improved error handling that provides detailed error messages:

```csharp
case "failed":
    // Extract error details for better error messages
    string errorMessage = "Request failed";
    if (json.RootElement.TryGetProperty("error", out var error))
    {
        // Extract error code and message
    }
    throw new ApplicationException($"Request failed: {errorMessage}");
```

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs#L620-L640)

### Asynchronous Operations

All Content Understanding operations are asynchronous:

- `BeginAnalyzeBinaryAsync()` - Starts file analysis
- `PollResultAsync()` - Waits for analysis to complete
- `BeginCreateAnalyzerAsync()` - Starts analyzer creation
- `DeleteAnalyzerAsync()` - Deletes the analyzer

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs)

## Learn More

- **[Content Understanding Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)** - Comprehensive introduction to the service
- **[What's New](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/whats-new)** - Latest features and updates
- **[Analyzer Reference](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/analyzer-reference)** - Complete analyzer configuration documentation
- **[Prebuilt Analyzers](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/prebuilt-analyzers)** - List of available prebuilt analyzers

## Related Samples

- **[ContentExtraction](../ContentExtraction/)** - Extract content from documents, audio, and video using prebuilt analyzers
- **[AnalyzerTraining](../AnalyzerTraining/)** - Train custom analyzers with labeled data for improved accuracy
- **[Management](../Management/)** - Create, list, and manage analyzers
