using AnalyzerTraining.Interfaces;
using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Storage.Blobs;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using System.Text.Json;

namespace AnalyzerTraining.Services
{
    public class AnalyzerTrainingService : IAnalyzerTrainingService
    {
        private readonly ContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/analyzer_training/";

        public AnalyzerTrainingService(ContentUnderstandingClient client) 
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

                        Console.WriteLine($"Uploaded training data for {fileName}");
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
        /// Create analyzer with defined schema.
        /// <remarks>Before creating the custom fields analyzer, you should fill the constant ANALYZER_ID with a business-related name. Here we randomly generate a name for demo purpose.
        /// We use **TRAINING_DATA_SAS_URL** and **TRAINING_DATA_PATH** that's set in the prerequisite step.</remarks>
        /// </summary>
        /// configuration.</param>
        /// <param name="trainingStorageContainerSasUrl">An optional SAS URL for the storage container containing the training data. If not provided, the method will use
        /// a default value.</param>
        /// <param name="trainingStorageContainerPathPrefix">An optional path prefix within the storage container to locate the training data. If not provided, the method
        /// will use a default value.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the unique identifier
        /// of the created analyzer.</returns>
        public async Task<ContentAnalyzer> CreateAnalyzerAsync(string analyzerTemplatePath, string trainingStorageContainerSasUrl, string trainingStorageContainerPathPrefix)
        {
            // Generate a unique analyzer ID using current timestamp
            string analyzerId = $"analyzer-training-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Create analyzer
            var contentAnalyzer = new ContentAnalyzer
            {
                BaseAnalyzerId = "prebuilt-documentAnalyzer",
                Description = "Custom analyzer created with labeled data",
                Config = new ContentAnalyzerConfig
                {
                    EnableFormula = true,
                    EnableLayout = true,
                    EnableOcr = true,
                    EstimateFieldSourceAndConfidence = true,
                    ReturnDetails = true
                },
                FieldSchema = new ContentFieldSchema(fields: new Dictionary<string, ContentFieldDefinition>
                {
                    ["MerchantName"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.String,
                        Method = GenerationMethod.Extract,
                        Description = "",
                    },
                    ["Items"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.Array,
                        Method = GenerationMethod.Generate,
                        Description = "",
                        Items = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.Object,
                            Method = GenerationMethod.Extract
                        }
                    },
                    ["TotalPrice"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.String,
                        Method = GenerationMethod.Extract,
                        Description = "",
                    },
                }),
                Mode = AnalysisMode.Standard,
                ProcessingLocation = ProcessingLocation.Geography,
                TrainingData = new BlobDataSource(new Uri(trainingStorageContainerSasUrl))
                {
                    Prefix = trainingStorageContainerPathPrefix,
                }
            };

            contentAnalyzer.FieldSchema.Fields["Items"].Items.Properties.Add("Quantity", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Extract,
                Description = "",
            });
            contentAnalyzer.FieldSchema.Fields["Items"].Items.Properties.Add("Name", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Extract,
                Description = "",
            });
            contentAnalyzer.FieldSchema.Fields["Items"].Items.Properties.Add("Price", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Extract,
                Description = "",
            });
            contentAnalyzer.Tags.Add("demo_type", "get_result");

            ContentAnalyzer result = new ContentAnalyzer();

            try
            {
                Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");

                var operation = await _client.GetContentAnalyzersClient()
                    .CreateOrReplaceAsync(waitUntil: WaitUntil.Completed, analyzerId, contentAnalyzer)
                    .ConfigureAwait(false);

                result = operation.Value;

                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
                Console.WriteLine($"   Status: {result.Status}");
                Console.WriteLine($"   Created At: {result.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"   Base Analyzer: {result.BaseAnalyzerId}");
                Console.WriteLine($"   Description: {result.Description}");

                // Display field schema information
                if (result.FieldSchema != null)
                {
                    Console.WriteLine($"\n📋 Field Schema: {result.FieldSchema.Name}");
                    Console.WriteLine($"   {result.FieldSchema.Description}");
                    Console.WriteLine($"   Fields:");
                    foreach (var field in result.FieldSchema.Fields)
                    {
                        Console.WriteLine($"      - {field.Key}: {field.Value.Type} ({field.Value.Method})");
                        Console.WriteLine($"        {field.Value.Description}");
                    }
                }

                // Display any warnings
                if (result.Warnings != null && result.Warnings.Count > 0)
                {
                    Console.WriteLine($"\n⚠️  Warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"      - {warning.Code}: {warning.Message}");
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"❌ Azure service request failed:");
                Console.WriteLine($"   Status: {ex.Status}");
                Console.WriteLine($"   Error Code: {ex.ErrorCode}");
                Console.WriteLine($"   Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An error occurred: {ex.Message}");
                Console.WriteLine($"   {ex.GetType().Name}");
            }

            return result;
        }

        /// <summary>
        /// ## Use created analyzer to extract document content.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <param name="analyzerId">The unique identifier of the custom analyzer to use for document analysis. This value must not be null or empty.</param>
        /// <param name="filePath">The file path of the document to analyze. The file must exist and be accessible.</param>
        /// <returns>A <see cref="JsonDocument"/> containing the analysis results of the document.</returns>
        public async Task<AnalyzeResult?> AnalyzeDocumentWithCustomAnalyzerAsync(string analyzerId, string filePath)
        {
            Console.WriteLine("\n===== Using Custom Analyzer for Document Analysis =====");
            AnalyzeResult? analyzeResult = null;
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                BinaryData binaryData = new BinaryData(bytes);
                Operation<AnalyzeResult> analyzeOperation = await _client.GetContentAnalyzersClient().AnalyzeBinaryAsync(
                    waitUntil: WaitUntil.Completed, analyzerId, contentType: "application/octet-stream", binaryData);

                analyzeResult = analyzeOperation.Value;
                Console.WriteLine("✅ Document analysis completed successfully!");

                // Display any warnings
                if (analyzeResult.Warnings != null && analyzeResult.Warnings.Count > 0)
                {
                    Console.WriteLine($"\n⚠️  Warnings:");
                    foreach (var warning in analyzeResult.Warnings)
                    {
                        Console.WriteLine($"      - {warning.Code}: {warning.Message}");
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"❌ Azure service request failed:");
                Console.WriteLine($"   Status: {ex.Status}");
                Console.WriteLine($"   Error Code: {ex.ErrorCode}");
                Console.WriteLine($"   Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An error occurred: {ex.Message}");
                Console.WriteLine($"   {ex.GetType().Name}");
            }

            return analyzeResult;
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
                await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

                Console.WriteLine("\n💡 Next steps:");
                Console.WriteLine("   - To retrieve an analyzer: see GetAnalyzer sample");
                Console.WriteLine("   - To use the analyzer for analysis: see AnalyzeBinary sample");
                Console.WriteLine("   - To delete an analyzer: see DeleteAnalyzer sample");
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"❌ Azure service request failed:");
                Console.WriteLine($"   Status: {ex.Status}");
                Console.WriteLine($"   Error Code: {ex.ErrorCode}");
                Console.WriteLine($"   Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An error occurred: {ex.Message}");
                Console.WriteLine($"   {ex.GetType().Name}");
            }
        }
    }
}
