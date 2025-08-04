using AnalyzerTraining.Interfaces;
using Azure.Storage.Blobs;
using ContentUnderstanding.Common;
using System.Text.Json;

namespace AnalyzerTraining.Services
{
    public class AnalyzerTrainingService : IAnalyzerTrainingService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/analyzer_training/";

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

                if (string.IsNullOrEmpty(fileExtension) || _client.GetSupportedFileTypesDocument().Contains(fileExtension.ToLower()))
                {
                    string labelFilename = fileName + _client.GetLabelFileSuffix();
                    string labelPath = Path.Combine(trainingDocsFolder, labelFilename);
                    string ocrResultFilename = fileName + _client.GetOcrResultFileSuffix();
                    string ocrResultPath = Path.Combine(trainingDocsFolder, ocrResultFilename);

                    if (File.Exists(labelPath) && File.Exists(ocrResultPath))
                    {
                        string fileBlobPath = storageContainerPathPrefix + fileName;
                        string labelBlobPath = storageContainerPathPrefix + labelFilename;
                        string ocrResultBlobPath = storageContainerPathPrefix + ocrResultFilename;

                        // Upload files
                        await _client.UploadFileToBlobAsync(containerClient, file, fileBlobPath);
                        await _client.UploadFileToBlobAsync(containerClient, labelPath, labelBlobPath);
                        await _client.UploadFileToBlobAsync(containerClient, ocrResultPath, ocrResultBlobPath);

                        Console.WriteLine($"Uploaded training data for {fileName}");
                    }
                    else
                    {
                        throw new FileNotFoundException(
                            $"Label file '{labelFilename}' or OCR result file '{ocrResultFilename}' " +
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
        public async Task<string> CreateAnalyzerAsync(string analyzerTemplatePath, string trainingStorageContainerSasUrl, string trainingStorageContainerPathPrefix)
        {
            Console.WriteLine("Creating Custom Analyzer with Training Data...");

            // Generate unique analyzer ID
            string analyzerId = $"train-sample-{Guid.NewGuid()}";
            Console.WriteLine($"Creating analyzer: {analyzerId}");

            try
            {
                var createResponse = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: analyzerTemplatePath,
                    trainingStorageContainerSasUrl,
                    trainingStorageContainerPathPrefix
                );

                // Poll for creation result
                var resultJson = await _client.PollResultAsync(createResponse);
                var status = resultJson.RootElement.GetProperty("status").GetString();

                if (status == "Succeeded")
                {
                    var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine($"Analyzer created successfully: {resultJson.RootElement.GetProperty("result").GetProperty("analyzerId")}");
                    Console.WriteLine(serializedJson);

                    return analyzerId;
                }

                throw new ApplicationException($"Analyzer creation failed. Status: {status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating analyzer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ## Use created analyzer to extract document content.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <param name="analyzerId">The unique identifier of the custom analyzer to use for document analysis. This value must not be null or empty.</param>
        /// <param name="filePath">The file path of the document to analyze. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the document analysis is finished.</returns>
        public async Task AnalyzeDocumentWithCustomAnalyzerAsync(string analyzerId, string filePath)
        {
            Console.WriteLine("\n===== Using Custom Analyzer for Document Analysis =====");

            try
            {
                var response = await _client.BeginAnalyzeAsync(analyzerId, filePath);
                var resultJson = await _client.PollResultAsync(response);
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });
                var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeDocumentWithCustomAnalyzerAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
                await File.WriteAllTextAsync(output, serializedJson);

                Console.WriteLine($"Document Extraction has been saved to the output file path: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during document analysis: {ex.Message}");
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
            Console.WriteLine("\n===== Deleting Analyzer =====");
            Console.WriteLine($"Deleting analyzer: {analyzerId}");

            try
            {
                var response = await _client.DeleteAnalyzerAsync(analyzerId);
                Console.WriteLine($"Analyzer {analyzerId} deleted successfully (Status: {response.StatusCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting analyzer: {ex.Message}");
                throw;
            }
        }
    }
}
