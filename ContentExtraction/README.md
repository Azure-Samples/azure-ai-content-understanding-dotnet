# Content Extraction Sample

This sample demonstrates how to use the Azure AI Content Understanding API to extract semantic content from multimodal files—documents, audio, and video. The sample uses prebuilt analyzers to transform unstructured content into structured, machine-readable data optimized for retrieval-augmented generation (RAG) and automated workflows.

## Overview

The Content Extraction sample showcases three main capabilities:

1. **Document Content Extraction** - Extract text, tables, figures, and layout information from documents
2. **Audio Content Extraction** - Transcribe audio with speaker diarization and generate summaries
3. **Video Content Extraction** - Analyze video content with keyframe extraction and transcription

## Prerequisites

1. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.
2. Ensure you have deployed the required models (GPT-4.1, GPT-4.1-mini, and text-embedding-3-large) in Azure AI Foundry.
3. Configure your `appsettings.json` file with your endpoint and deployment names.

## Key Concepts

### Prebuilt Analyzers

This sample uses prebuilt analyzers that are ready to use without any configuration:

- **`prebuilt-documentSearch`** - Extracts content from documents with layout preservation
- **`prebuilt-audioSearch`** - Transcribes audio with speaker diarization
- **`prebuilt-videoSearch`** - Analyzes video with keyframe extraction and transcription

### Asynchronous Operations

Content Understanding operations are asynchronous. The workflow is:

1. **Begin Analysis** - Start the analysis operation (returns immediately with an operation location)
2. **Poll for Results** - Poll the operation location until the analysis completes
3. **Process Results** - Extract and display the structured results

### Content Types

The API returns different content types based on the input:

- **`document`** - For document files (PDF, images, Office documents)
- **`audioVisual`** - For audio and video files

## Code Structure

### Main Entry Point

The `Program.cs` file provides an interactive menu to run different extraction scenarios:

```csharp
var service = services.GetRequiredService<IContentExtractionService>();

while (true)
{
    Console.WriteLine("Please enter a number to run sample: ");
    Console.WriteLine("[1] - Extract Document Content");
    Console.WriteLine("[2] - Extract Document Content from URL");
    Console.WriteLine("[3] - Extract Audio Content");
    Console.WriteLine("[4] - Extract Video Content");
    
    string? input = Console.ReadLine();
    // ... handle user input
}
```

### Service Implementation

The `ContentExtractionService` class implements the core extraction logic. It uses the `AzureContentUnderstandingClient` (a thin REST client) to interact with the API.

## Document Content Extraction

The `prebuilt-documentSearch` analyzer transforms unstructured documents into structured, machine-readable data optimized for RAG scenarios.

### Key Capabilities

1. **Content Analysis:** Extracts text (printed and handwritten), selection marks, barcodes, mathematical formulas, hyperlinks, and annotations
2. **Figure Analysis:** Generates descriptions for images/charts/diagrams, converts charts to Chart.js syntax, and diagrams to Mermaid.js syntax
3. **Structure Analysis:** Identifies paragraphs with contextual roles, detects tables with complex layouts, and maps hierarchical sections
4. **GitHub Flavored Markdown:** Outputs richly formatted markdown that preserves document structure

### Code Example: Analyzing a Local Document

```csharp
public async Task<JsonDocument> AnalyzeDocumentAsync(string filePath)
{
    var analyzerId = "prebuilt-documentSearch";
    
    // Begin the analysis operation
    var response = await _client.BeginAnalyzeBinaryAsync(analyzerId, filePath);
    
    // Poll for results
    JsonDocument result = await _client.PollResultAsync(response);
    
    // Extract markdown from the first content element
    var resultProperty = result.RootElement.GetProperty("result");
    var contents = resultProperty.GetProperty("contents");
    
    if (contents.GetArrayLength() > 0)
    {
        var content = contents[0];
        if (content.TryGetProperty("markdown", out var markdownProp))
        {
            string markdown = markdownProp.GetString();
            Console.WriteLine(markdown);
        }
    }
    
    // Access document-specific properties
    if (content.TryGetProperty("kind", out var kind) && kind.GetString() == "document")
    {
        // Get page information
        var pages = content.GetProperty("pages");
        // Get table information
        var tables = content.GetProperty("tables");
    }
    
    return result;
}
```

**Source:** [`ContentExtractionService.cs`](Services/ContentExtractionService.cs#L68-L199)

### Code Example: Analyzing a Document from URL

You can also analyze documents directly from publicly accessible URLs:

```csharp
public async Task<JsonDocument> AnalyzeDocumentFromUrlAsync(string documentUrl)
{
    var analyzerId = "prebuilt-documentSearch";
    
    // Begin analysis from URL
    var response = await _client.BeginAnalyzeUrlAsync(analyzerId, documentUrl);
    
    // Poll for results (same as local file)
    var result = await _client.PollResultAsync(response);
    
    // Process results...
    return result;
}
```

**Source:** [`ContentExtractionService.cs`](Services/ContentExtractionService.cs#L203-L299)

## Audio Content Extraction

The `prebuilt-audioSearch` analyzer provides conversation analysis capabilities for audio files. It automatically transcribes audio content, performs speaker diarization, and generates conversation summaries.

### Key Features

1. **Transcription:** Converts conversational audio into searchable text with sentence-level and word-level timestamps
2. **Speaker Diarization:** Distinguishes between speakers in a conversation
3. **Timing Information:** Precise timing data in milliseconds for each phrase
4. **Summary Generation:** Automatically generates a summary of the conversation
5. **Multilingual Support:** Supports automatic language detection and multilingual transcription

### Code Example: Analyzing Audio

```csharp
public async Task<JsonDocument> AnalyzeAudioAsync(string filePath)
{
    string analyzerId = "prebuilt-audioSearch";
    
    // Begin audio analysis
    var analysisResponse = await _client.BeginAnalyzeBinaryAsync(
        analyzerId: analyzerId,
        fileLocation: filePath
    );
    
    // Wait for completion
    var result = await _client.PollResultAsync(analysisResponse);
    
    // Extract audio-visual content
    var contents = result.RootElement.GetProperty("result").GetProperty("contents");
    var content = contents[0];
    
    if (content.GetProperty("kind").GetString() == "audioVisual")
    {
        // Get timing information
        long startTime = content.GetProperty("startTimeMs").GetInt64();
        long endTime = content.GetProperty("endTimeMs").GetInt64();
        
        // Get transcript phrases
        var transcriptPhrases = content.GetProperty("transcriptPhrases");
        foreach (var phrase in transcriptPhrases.EnumerateArray())
        {
            string speaker = phrase.GetProperty("speaker").GetString();
            string text = phrase.GetProperty("text").GetString();
            long startTimeMs = phrase.GetProperty("startTimeMs").GetInt64();
            // ... process each phrase
        }
    }
    
    return result;
}
```

**Source:** [`ContentExtractionService.cs`](Services/ContentExtractionService.cs#L312-L433)

## Video Content Extraction

The `prebuilt-videoSearch` analyzer provides comprehensive analysis of video content, combining visual frame extraction, audio transcription, and AI-powered insights.

### Key Features

1. **Transcription with Diarization:** Converts audio to searchable WebVTT transcripts with speaker identification
2. **Key Frame Extraction:** Intelligently extracts representative frames (~1 FPS) from each scene
3. **Shot Detection:** Identifies video segment boundaries aligned with camera cuts
4. **Segment-based Analysis:** Analyzes multiple frames per segment to identify actions and events
5. **Structured Output:** Content organized in GitHub Flavored Markdown with precise temporal alignment

### Code Example: Analyzing Video

```csharp
public async Task<JsonDocument> AnalyzeVideoAsync(string filePath)
{
    string analyzerId = "prebuilt-videoSearch";
    
    // Begin video analysis
    var analysisResponse = await _client.BeginAnalyzeBinaryAsync(
        analyzerId: analyzerId,
        fileLocation: filePath
    );
    
    // Wait for completion
    var result = await _client.PollResultAsync(analysisResponse);
    
    // Extract video-visual content
    var content = result.RootElement.GetProperty("result")
        .GetProperty("contents")[0];
    
    if (content.GetProperty("kind").GetString() == "audioVisual")
    {
        // Get keyframe timestamps
        var keyFrameTimesMs = content.GetProperty("keyFrameTimesMs");
        
        // Download keyframe images
        var keyframeIds = ExtractKeyframeIds(result);
        foreach (var keyframeId in keyframeIds)
        {
            byte[] imageContent = await _client.GetResultFileAsync(
                analysisResponse, 
                keyframeId
            );
            // Save image to file...
        }
    }
    
    return result;
}
```

**Source:** [`ContentExtractionService.cs`](Services/ContentExtractionService.cs#L446-L599)

## Running the Sample

1. **Build the project:**
   ```bash
   cd ContentExtraction
   dotnet build
   ```

2. **Run the sample:**
   ```bash
   dotnet run
   ```

3. **Select an option from the menu:**
   - `1` - Extract Document Content (from local file)
   - `2` - Extract Document Content from URL
   - `3` - Extract Audio Content
   - `4` - Extract Video Content

## Output

The sample saves full analysis results as JSON files in the `sample_output/` directory. Each result includes:

- **Markdown content** - Formatted markdown representation of the extracted content
- **Structured metadata** - Pages, tables, transcript phrases, keyframes, etc.
- **Timing information** - For audio/video content
- **Confidence scores** - For extracted elements

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

**Source:** [`ContentExtractionService.cs`](Services/ContentExtractionService.cs#L27-L60)

### Asynchronous Polling

The `PollResultAsync()` method handles the asynchronous operation pattern:

```csharp
public async Task<JsonDocument> PollResultAsync(
    HttpResponseMessage initialResponse,
    int timeoutSeconds = 120,
    int pollingIntervalSeconds = 2)
{
    // Extracts operation location from response headers
    // Polls until operation completes or times out
    // Returns the final JSON result
}
```

**Source:** [`AzureContentUnderstandingClient.cs`](../ContentUnderstanding.Common/AzureContentUnderstandingClient.cs#L595-L650)

## Learn More

- **[Content Understanding Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/overview)** - Comprehensive introduction to the service
- **[What's New](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/whats-new)** - Latest features and updates
- **[Document Elements](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/document/elements)** - Detailed documentation on document extraction
- **[Audio Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/audio/overview)** - Audio capabilities and markdown format
- **[Video Overview](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/video/overview)** - Video capabilities and elements

## Related Samples

- **[FieldExtraction](../FieldExtraction/)** - Extract specific fields from documents using custom analyzers
- **[Classifier](../Classifier/)** - Classify and categorize documents
- **[Management](../Management/)** - Create, list, and manage analyzers

