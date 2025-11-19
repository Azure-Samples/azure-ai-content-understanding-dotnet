using AnalyzerTraining.Interfaces;
using Azure.Storage.Blobs;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ContentUnderstanding.Common.Helpers;
using System.Text.Json;

namespace AnalyzerTraining.Services
{
    public class AnalyzerTrainingService : IAnalyzerTrainingService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./sample_output/analyzer_training/";

        public AnalyzerTrainingService(AzureContentUnderstandingClient client) 
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Uploads training data files, including labels and OCR results, from a local folder to a specified Azure Blob
        /// Storage container.
        /// </summary>
        /// <remarks>This method uploads training documents along with their associated label and OCR
        /// result files to Azure Blob Storage.  The method ensures that each document has corresponding label and OCR
        /// result files before uploading.  If any required file is missing, a <see cref="FileNotFoundException"/> is
        /// thrown.</remarks>
        /// <param name="trainingDocsFolder">The local folder containing the training documents, label files, and OCR result files.  Each document must
        /// have corresponding label and OCR result files in the same folder.</param>
        /// <param name="storageContainerSasUrl">The SAS URL of the Azure Blob Storage container where the files will be uploaded.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container where the files will be uploaded.  If the prefix does not end
        /// with a forward slash ('/'), one will be appended automatically.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException">Thrown if a required label file or OCR result file is missing for a document in the specified folder.</exception>
        public async Task GenerateTrainingDataOnBlobAsync(
            string trainingDocsFolder,
            string storageContainerSasUrl,
            string storageContainerPathPrefix)
        {
            if (!storageContainerPathPrefix.EndsWith("/"))
            {
                storageContainerPathPrefix += "/";
            }

            var containerClient = new BlobContainerClient(new Uri(storageContainerSasUrl));
            var files = Directory.GetFiles(trainingDocsFolder);

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileExtension = Path.GetExtension(fileName);

                if (string.IsNullOrEmpty(fileExtension) || BlobFileConstants.IsSupportedDocumentType(fileName))
                {
                    string labelFileName = BlobFileConstants.GetLabelFilePath(fileName);
                    string labelPath = Path.Combine(trainingDocsFolder, labelFileName);
                    string ocrResultFileName = BlobFileConstants.GetOcrResultFilePath(fileName);
                    string ocrResultPath = Path.Combine(trainingDocsFolder, ocrResultFileName);

                    if (File.Exists(labelPath) && File.Exists(ocrResultPath))
                    {
                        string fileBlobPath = storageContainerPathPrefix + fileName;
                        string labelBlobPath = storageContainerPathPrefix + labelFileName;
                        string ocrResultBlobPath = storageContainerPathPrefix + ocrResultFileName;

                        // Upload files
                        await containerClient.UploadFileAsync(file, fileBlobPath);
                        await containerClient.UploadFileAsync(labelPath, labelBlobPath);
                        await containerClient.UploadFileAsync(ocrResultPath, ocrResultBlobPath);

                        Console.WriteLine($"✅ Uploaded training data for {fileName}");
                    }
                    else
                    {
                        throw new FileNotFoundException(
                            $"Label file '{labelFileName}' or OCR result file '{ocrResultFileName}' " +
                            $"does not exist in '{trainingDocsFolder}'. " +
                            $"Please ensure both files exist for '{fileName}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Create analyzer with defined schema and labeled training data.
        /// <remarks>Before creating the custom fields analyzer, you should fill the constant ANALYZER_ID with a business-related name. Here we randomly generate a name for demo purpose.
        /// We use **TRAINING_DATA_SAS_URL** and **TRAINING_DATA_PATH** that's set in the prerequisite step.</remarks>
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <param name="analyzerDefinition">The analyzer definition as a dictionary.</param>
        /// <param name="trainingStorageContainerSasUrl">The SAS URL for the storage container containing the training data.</param>
        /// <param name="trainingStorageContainerPathPrefix">The path prefix within the storage container to locate the training data.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the analyzer details as JsonDocument.</returns>
        public async Task<JsonDocument> CreateAnalyzerAsync(
            string analyzerId,
            Dictionary<string, object> analyzerDefinition,
            string trainingStorageContainerSasUrl,
            string trainingStorageContainerPathPrefix)
        {
            // Add knowledge sources with labeled data for training
            if (!string.IsNullOrEmpty(trainingStorageContainerSasUrl) && !string.IsNullOrEmpty(trainingStorageContainerPathPrefix))
            {
                if (!trainingStorageContainerPathPrefix.EndsWith("/"))
                {
                    trainingStorageContainerPathPrefix += "/";
                }

                // Create knowledge source configuration for labeled data (used for analyzer training)
                var knowledgeSourceConfig = new Dictionary<string, object>
                {
                    ["kind"] = "labeledData",
                    ["containerUrl"] = trainingStorageContainerSasUrl,
                    ["prefix"] = trainingStorageContainerPathPrefix
                };

                // Optionally add file list path if specified in environment
                var fileListPath = Environment.GetEnvironmentVariable("CONTENT_UNDERSTANDING_FILE_LIST_PATH");
                if (!string.IsNullOrEmpty(fileListPath))
                {
                    knowledgeSourceConfig["fileListPath"] = fileListPath;
                }

                analyzerDefinition["knowledgeSources"] = new List<Dictionary<string, object>> { knowledgeSourceConfig };
            }

            // Convert analyzer definition to JSON and save to temp file
            string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(analyzerDefinition, jsonOptions));

                Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");
                if (!string.IsNullOrEmpty(trainingStorageContainerSasUrl))
                {
                    Console.WriteLine($"   With knowledge sources: Yes (labeledData)");
                }

                // Create analyzer using thin client
                var createResponse = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: tempTemplatePath);

                // Poll for analyzer creation completion
                Console.WriteLine("⏳ Waiting for analyzer creation to complete...");
                var analyzerResult = await _client.PollResultAsync(createResponse);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");

                // Display analyzer information
                if (analyzerResult.RootElement.TryGetProperty("status", out var status))
                {
                    Console.WriteLine($"   Status: {status.GetString()}");
                }
                if (analyzerResult.RootElement.TryGetProperty("createdAt", out var createdAt))
                {
                    Console.WriteLine($"   Created At: {createdAt.GetString()}");
                }
                if (analyzerResult.RootElement.TryGetProperty("baseAnalyzerId", out var baseAnalyzerId))
                {
                    Console.WriteLine($"   Base Analyzer: {baseAnalyzerId.GetString()}");
                }
                if (analyzerResult.RootElement.TryGetProperty("description", out var description))
                {
                    Console.WriteLine($"   Description: {description.GetString()}");
                }

                // Display field schema information
                if (analyzerResult.RootElement.TryGetProperty("fieldSchema", out var fieldSchema))
                {
                    if (fieldSchema.TryGetProperty("name", out var schemaName))
                    {
                        Console.WriteLine($"\n   Field Schema: {schemaName.GetString()}");
                    }
                    if (fieldSchema.TryGetProperty("description", out var schemaDescription))
                    {
                        Console.WriteLine($"   {schemaDescription.GetString()}");
                    }
                    if (fieldSchema.TryGetProperty("fields", out var fields))
                    {
                        Console.WriteLine($"   Fields:");
                        foreach (var field in fields.EnumerateObject())
                        {
                            if (field.Value.TryGetProperty("type", out var fieldType))
                            {
                                var method = field.Value.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : "N/A";
                                Console.WriteLine($"      - {field.Name}: {fieldType.GetString()} ({method})");
                                if (field.Value.TryGetProperty("description", out var fieldDescription))
                                {
                                    Console.WriteLine($"        {fieldDescription.GetString()}");
                                }
                            }
                        }
                    }
                }

                // Display any warnings
                if (analyzerResult.RootElement.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
                {
                    if (warnings.GetArrayLength() > 0)
                    {
                        Console.WriteLine($"\n⚠️  Warnings:");
                        foreach (var warning in warnings.EnumerateArray())
                        {
                            var code = warning.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";
                            var message = warning.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"      - {code}: {message}");
                        }
                    }
                }

                return analyzerResult;
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

        /// <summary>
        /// ## Use created analyzer to extract document content.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <param name="analyzerId">The unique identifier of the custom analyzer to use for document analysis. This value must not be null or empty.</param>
        /// <param name="filePath">The file path of the document to analyze. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the document analysis is finished and returns the result as JsonDocument.</returns>
        public async Task<JsonDocument> AnalyzeDocumentWithCustomAnalyzerAsync(string analyzerId, string filePath)
        {
            Console.WriteLine("\n===== Using Custom Analyzer for Document Analysis =====");
            
            // Resolve file path
            string resolvedFilePath = ResolveDataFilePath(filePath);
            if (!File.Exists(resolvedFilePath))
            {
                Console.WriteLine($"❌ Error: Sample file not found at {resolvedFilePath}");
                throw new FileNotFoundException("Sample file not found.", resolvedFilePath);
            }

            Console.WriteLine($"📄 Reading document file: {resolvedFilePath}");

            try
            {
                // Begin document analysis operation
                Console.WriteLine($"🔍 Starting document analysis with analyzer '{analyzerId}'...");
                var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(analyzerId, resolvedFilePath);

                // Wait for analysis completion
                Console.WriteLine("⏳ Waiting for document analysis to complete...");
                var analysisResult = await _client.PollResultAsync(analyzeResponse);
                Console.WriteLine("✅ Document analysis completed successfully!");

                // Display results
                DisplayAnalysisResults(analysisResult);

                // Save result
                SampleHelper.SaveJsonToFile(analysisResult, OutputPath, $"analyzer_training_result_{analyzerId}");

                return analysisResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Analysis failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Delete exist analyzer in Content Understanding Service.
        /// </summary>
        /// <remarks>This snippet is not required, but it's only used to prevent the testing analyzer from residing in your service. The custom fields analyzer could be stored in your service for reusing by subsequent business in real usage scenarios.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. This parameter cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DeleteAnalyzerAsync(string analyzerId)
        {
            try
            {
                // Clean up the created analyzer
                Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}'...");
                await _client.DeleteAnalyzerAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete analyzer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Displays the analysis results in a readable format.
        /// </summary>
        private void DisplayAnalysisResults(JsonDocument analysisResult)
        {
            if (analysisResult.RootElement.TryGetProperty("result", out var result))
            {
                if (result.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                {
                    var contentsArray = contents.EnumerateArray().ToList();
                    if (contentsArray.Count > 0)
                    {
                        var firstContent = contentsArray[0];

                        // Display markdown content
                        if (firstContent.TryGetProperty("markdown", out var markdown))
                        {
                            Console.WriteLine("\n📄 Markdown Content:");
                            Console.WriteLine("=".PadRight(50, '='));
                            var markdownText = markdown.GetString() ?? "";
                            Console.WriteLine(markdownText.Length > 500 ? markdownText.Substring(0, 500) + "..." : markdownText);
                            Console.WriteLine("=".PadRight(50, '='));
                        }

                        // Display extracted fields
                        Console.WriteLine("\n📊 Analyzer Training Results:");
                        if (firstContent.TryGetProperty("fields", out var fields))
                        {
                            DisplayFields(fields);
                        }
                        else
                        {
                            Console.WriteLine("No fields extracted");
                        }

                        // Display content metadata
                        Console.WriteLine("\n📋 Content Metadata:");
                        if (firstContent.TryGetProperty("category", out var category))
                        {
                            Console.WriteLine($"   Category: {category.GetString()}");
                        }
                        if (firstContent.TryGetProperty("startPageNumber", out var startPage))
                        {
                            Console.WriteLine($"   Start Page Number: {startPage.GetInt32()}");
                        }
                        if (firstContent.TryGetProperty("endPageNumber", out var endPage))
                        {
                            Console.WriteLine($"   End Page Number: {endPage.GetInt32()}");
                        }

                        // Check if this is document content
                        if (firstContent.TryGetProperty("kind", out var kind) && kind.GetString() == "document")
                        {
                            Console.WriteLine("\n📚 Document Information:");
                            var startPageNum = firstContent.TryGetProperty("startPageNumber", out var sp) ? sp.GetInt32() : 0;
                            var endPageNum = firstContent.TryGetProperty("endPageNumber", out var ep) ? ep.GetInt32() : 0;
                            Console.WriteLine($"Start page: {startPageNum}");
                            Console.WriteLine($"End page: {endPageNum}");
                            Console.WriteLine($"Total pages: {endPageNum - startPageNum + 1}");

                            if (firstContent.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                            {
                                var pagesArray = pages.EnumerateArray().ToList();
                                Console.WriteLine($"\n📄 Pages ({pagesArray.Count}):");
                                foreach (var page in pagesArray)
                                {
                                    var pageNum = page.TryGetProperty("pageNumber", out var pn) ? pn.GetInt32() : 0;
                                    var width = page.TryGetProperty("width", out var w) ? w.GetDouble() : 0;
                                    var height = page.TryGetProperty("height", out var h) ? h.GetDouble() : 0;
                                    var unit = firstContent.TryGetProperty("unit", out var u) ? u.GetString() : "units";
                                    Console.WriteLine($"  Page {pageNum}: {width} x {height} {unit}");
                                }
                            }

                            if (firstContent.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array)
                            {
                                var tablesArray = tables.EnumerateArray().ToList();
                                Console.WriteLine($"\n📊 Tables ({tablesArray.Count}):");
                                int idx = 1;
                                foreach (var table in tablesArray)
                                {
                                    var rowCount = table.TryGetProperty("rowCount", out var rc) ? rc.GetInt32() : 0;
                                    var colCount = table.TryGetProperty("columnCount", out var cc) ? cc.GetInt32() : 0;
                                    Console.WriteLine($"  Table {idx}: {rowCount} rows x {colCount} columns");
                                    idx++;
                                }
                            }
                        }
                    }
                }

                // Display any warnings
                if (result.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
                {
                    var warningsArray = warnings.EnumerateArray().ToList();
                    if (warningsArray.Count > 0)
                    {
                        Console.WriteLine($"\n⚠️  Warnings:");
                        foreach (var warning in warningsArray)
                        {
                            var code = warning.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";
                            var message = warning.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"      - {code}: {message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Displays extracted fields in a readable format.
        /// </summary>
        private void DisplayFields(JsonElement fields)
        {
            foreach (var field in fields.EnumerateObject())
            {
                var fieldName = field.Name;
                var fieldValue = field.Value;

                Console.WriteLine($"\n{fieldName}:");

                if (fieldValue.TryGetProperty("type", out var fieldType))
                {
                    var type = fieldType.GetString();
                    if (type == "string" && fieldValue.TryGetProperty("valueString", out var valueString))
                    {
                        Console.WriteLine($"  Value: {valueString.GetString()}");
                    }
                    else if (type == "number" && fieldValue.TryGetProperty("valueNumber", out var valueNumber))
                    {
                        Console.WriteLine($"  Value: {valueNumber.GetDouble()}");
                    }
                    else if (type == "array" && fieldValue.TryGetProperty("valueArray", out var valueArray))
                    {
                        var arrayItems = valueArray.EnumerateArray().ToList();
                        Console.WriteLine($"  Array with {arrayItems.Count} items:");
                        int idx = 1;
                        foreach (var item in arrayItems)
                        {
                            if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "object")
                            {
                                if (item.TryGetProperty("valueObject", out var valueObject))
                                {
                                    Console.WriteLine($"    Item {idx}:");
                                    foreach (var objField in valueObject.EnumerateObject())
                                    {
                                        if (objField.Value.TryGetProperty("type", out var objFieldType))
                                        {
                                            var objType = objFieldType.GetString();
                                            if (objType == "string" && objField.Value.TryGetProperty("valueString", out var objValueString))
                                            {
                                                Console.WriteLine($"      {objField.Name}: {objValueString.GetString()}");
                                            }
                                            else if (objType == "number" && objField.Value.TryGetProperty("valueNumber", out var objValueNumber))
                                            {
                                                Console.WriteLine($"      {objField.Name}: {objValueNumber.GetDouble()}");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"    Item {idx}: {item}");
                            }
                            idx++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the data file path by checking multiple locations.
        /// </summary>
        private static string ResolveDataFilePath(string fileName)
        {
            // Try current directory
            string currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // Try assembly directory (output directory)
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string assemblyPath = Path.Combine(assemblyDir, "data", fileName);
                    if (File.Exists(assemblyPath))
                    {
                        return assemblyPath;
                    }
                }
            }

            // Try ContentUnderstanding.Common/data/
            var commonDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "ContentUnderstanding.Common", "data", fileName);
            if (File.Exists(commonDataPath))
            {
                return commonDataPath;
            }

            // Try relative to current directory
            if (File.Exists(fileName))
            {
                return fileName;
            }

            return fileName;
        }
    }
}
