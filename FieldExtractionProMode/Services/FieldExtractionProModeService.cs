using Azure.Storage.Blobs;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Models;
using FieldExtractionProMode.Interfaces;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FieldExtractionProMode.Services
{
    public class FieldExtractionProModeService : IFieldExtractionProModeService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/field_extraction_pro_mode/";

        public FieldExtractionProModeService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Uploads training data files to a specified Azure Blob Storage container.
        /// </summary>
        /// <remarks>This method uploads each document from the specified folder to the blob storage,
        /// along with its corresponding label and OCR result files. The method expects each document to have a label
        /// file and an OCR result file with specific suffixes. If these files are not found, a <see
        /// cref="FileNotFoundException"/> is thrown.</remarks>
        /// <param name="trainingDocsFolder">The local directory containing the training documents and associated files.</param>
        /// <param name="storageContainerSasUrl">The SAS URL of the Azure Blob Storage container where files will be uploaded.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container where files will be stored. Must end with a slash.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the label file or OCR result file for a document does not exist in the specified folder.</exception>
        public async Task GenerateTrainingDataOnBlobAsync(
            string trainingDocsFolder,
            string storageContainerSasUrl,
            string storageContainerPathPrefix)
        {
            if (!storageContainerPathPrefix.EndsWith("/"))
            {
                storageContainerPathPrefix += "/";
            }

            BlobContainerClient containerClient = new BlobContainerClient(new Uri(storageContainerSasUrl));

            foreach (var fileName in Directory.GetFiles(trainingDocsFolder))
            {
                string fileNameOnly = Path.GetFileName(fileName);
                string fileExt = Path.GetExtension(fileName).ToLower();

                if ((fileExt == "" || _client.GetSupportedFileTypesDocument().Contains(fileExt)))
                {
                    string labelFilename = fileNameOnly + _client.GetLabelFileSuffix();
                    string labelPath = Path.Combine(trainingDocsFolder, labelFilename);
                    string ocrResultFilename = fileNameOnly + _client.GetOcrResultFileSuffix();
                    string ocrResultPath = Path.Combine(trainingDocsFolder, ocrResultFilename);

                    if (File.Exists(labelPath) && File.Exists(ocrResultPath))
                    {
                        string fileBlobPath = storageContainerPathPrefix + fileNameOnly;
                        string labelBlobPath = storageContainerPathPrefix + labelFilename;
                        string ocrResultBlobPath = storageContainerPathPrefix + ocrResultFilename;

                        await _client.UploadFileToBlobAsync(containerClient, fileName, fileBlobPath);
                        await _client.UploadFileToBlobAsync(containerClient, labelPath, labelBlobPath);
                        await _client.UploadFileToBlobAsync(containerClient, ocrResultPath, ocrResultBlobPath);
                    }
                    else
                    {
                        throw new FileNotFoundException(
                            $"Label file '{labelFilename}' or OCR result file '{ocrResultFilename}' does not exist in '{trainingDocsFolder}'. " +
                            $"Please ensure both files exist for '{fileNameOnly}'."
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Generates a knowledge base by analyzing documents in a specified folder and uploading the results to a blob
        /// storage container.
        /// </summary>
        /// <remarks>This method analyzes documents using a prebuilt document analyzer and uploads both
        /// the original and result files to the specified blob storage. If <paramref name="skipAnalyze"/> is <see
        /// langword="true"/>, only the files are uploaded without analysis.</remarks>
        /// <param name="referenceDocsFolder">The path to the folder containing reference documents to be analyzed.</param>
        /// <param name="storageContainerSasUrl">The SAS URL of the blob storage container where results will be uploaded.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container for uploaded files. Must end with a slash.</param>
        /// <param name="skipAnalyze">If <see langword="true"/>, skips the analysis step and only uploads files. Otherwise, performs analysis
        /// before uploading.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a document analysis fails or if the JSON result cannot be parsed. Ensure the documents are valid
        /// and the analyzer is correctly configured.</exception>
        public async Task GenerateKnowledgeBaseOnBlobAsync(
            string referenceDocsFolder,
            string storageContainerSasUrl,
            string storageContainerPathPrefix,
            bool skipAnalyze = false)
        {
            if (!storageContainerPathPrefix.EndsWith("/"))
            {
                storageContainerPathPrefix += "/";
            }

            var resources = new List<Dictionary<string, string>>();
            var containerClient = new BlobContainerClient(new Uri(storageContainerSasUrl));

            if (!skipAnalyze)
            {
                var analyzeList = GetAnalyzeList(referenceDocsFolder);
                foreach (var analyzeItem in analyzeList)
                {
                    Dictionary<string, object> analyzeResult = new Dictionary<string, object>();
                    try
                    {
                        var prebuiltDocumentAnalyzerId = "prebuilt-documentAnalyzer";
                        var response = await _client.BeginAnalyzeAsync(prebuiltDocumentAnalyzerId, analyzeItem.FilePath);
                        JsonDocument resultJson = await _client.PollResultAsync(response);
                        var jsonString = resultJson.RootElement.GetRawText();

                        var resultFileBlobPath = storageContainerPathPrefix + analyzeItem.ResultFileName;
                        var fileBlobPath = storageContainerPathPrefix + analyzeItem.Filename;

                        await _client.UploadJsonToBlobAsync(containerClient, jsonString, resultFileBlobPath);
                        await _client.UploadFileToBlobAsync(containerClient, analyzeItem.FilePath, fileBlobPath);

                        resources.Add(new Dictionary<string, string>
                        {
                            { "file", analyzeItem.Filename },
                            { "resultFile", analyzeItem.ResultFileName }
                        });
                    }
                    catch (JsonException jsonEx)
                    {
                        throw new InvalidOperationException($"Failed to parse JSON result for file '{analyzeItem.FilePath}'. Ensure the file is a valid document and the analyzer is set up correctly.", jsonEx);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to analyze file '{analyzeItem.FilePath}'. Ensure the file is a valid document and the analyzer is set up correctly.", ex);
                    }
                }
            }
            else
            {
                var uploadList = new List<ReferenceDocItem>();

                foreach (var dirPath in Directory.EnumerateDirectories(referenceDocsFolder, "*", SearchOption.AllDirectories))
                {
                    ProcessDirectory(dirPath, uploadList);
                }

                ProcessDirectory(referenceDocsFolder, uploadList);

                foreach (var uploadItem in uploadList)
                {
                    var resultFileBlobPath = storageContainerPathPrefix + uploadItem.ResultFileName;
                    var fileBlobPath = storageContainerPathPrefix + uploadItem.Filename;
                    await _client.UploadFileToBlobAsync(containerClient, uploadItem.FilePath, fileBlobPath);
                    await _client.UploadFileToBlobAsync(containerClient, uploadItem.ResultFilePath, resultFileBlobPath);
                    resources.Add(new Dictionary<string, string>
                {
                    { "file", uploadItem.Filename },
                    { "resultFile", uploadItem.ResultFileName }
                });
                }
            }

            List<string> jsons = resources.Select(r => JsonSerializer.Serialize(r)).ToList();

            // Upload sources.jsonl
            await _client.UploadJsonlToBlobAsync(
                containerClient, jsons, storageContainerPathPrefix + _client.GetKnowledgeSourceListFileName()
            );
        }

        /// <summary>
        /// Asynchronously creates an analyzer with a specified schema in Pro Mode.
        /// </summary>
        /// <remarks>This method initiates the creation of an analyzer in Pro Mode using the specified
        /// schema and reference documents. It logs the outcome of the operation and throws an exception if an error
        /// occurs during the creation process.</remarks>
        /// <param name="analyzerId">The unique identifier for the analyzer. Must not be null, empty, or whitespace.</param>
        /// <param name="analyzerSchema">The schema definition for the analyzer. Must not be null, empty, or whitespace.</param>
        /// <param name="proModeReferenceDocsStorageContainerSasUrl">The SAS URL for the storage container containing reference documents for Pro Mode.</param>
        /// <param name="proModeReferenceDocsStorageContainerPathPrefix">The path prefix within the storage container for reference documents in Pro Mode.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="analyzerId"/> or <paramref name="analyzerSchema"/> is null, empty, or consists
        /// only of white-space characters.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the analyzer creation fails due to an error in the provided configuration or deployment.</exception>
        public async Task CreateAnalyzerWithDefinedSchemaForProModeAsync(
            string analyzerId,
            string analyzerSchema,
            string proModeReferenceDocsStorageContainerSasUrl,
            string proModeReferenceDocsStorageContainerPathPrefix
            )
        {
            if (string.IsNullOrWhiteSpace(analyzerId))
            {
                throw new ArgumentException("Analyzer ID must be provided.", nameof(analyzerId));
            }

            if (string.IsNullOrWhiteSpace(analyzerSchema))
            {
                throw new ArgumentException("Analyzer schema must be provided.", nameof(analyzerSchema));
            }

            var response = await _client.BeginCreateAnalyzerAsync(analyzerId, analyzerSchema, proModeReferenceDocsStorageContainerSasUrl, proModeReferenceDocsStorageContainerPathPrefix, isProMode: true).ConfigureAwait(false);
            JsonDocument resultJson = await _client.PollResultAsync(response).ConfigureAwait(false);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            if (resultJson.RootElement.TryGetProperty("error", out JsonElement errorElement))
            {
                Console.WriteLine($"An issue was encountered when trying to create the analyzer.");
                Console.WriteLine($"Please double-check your deployment and configurations for potential problems.");
                throw new InvalidOperationException($"Failed to create analyzer: {errorElement.GetProperty("message").GetString()}");
            }
            else
            {
                Console.WriteLine($"Analyzer '{analyzerId}'");
                Console.WriteLine($"Created successfully with the following schema: {serializedJson}");
            }
        }

        /// <summary>
        /// Analyzes a document using a predefined schema in professional mode.
        /// </summary>
        /// <remarks>This method processes the document asynchronously and applies the specified analyzer
        /// schema. Ensure that the  <paramref name="analyzerId"/> corresponds to a valid and available analyzer
        /// configuration.</remarks>
        /// <param name="analyzerId">The identifier of the analyzer to be used for processing the document.</param>
        /// <param name="fileLocation">The file path of the document to be analyzed. Must be a valid path to an existing file.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task AnalyzeDocumentWithDefinedSchemaForProModeAsync(string analyzerId, string fileLocation)
        {
            var response = await _client.BeginAnalyzeAsync(analyzerId, fileLocation).ConfigureAwait(false);
            JsonDocument resultJson = await _client.PollResultAsync(response, timeoutSeconds: 600).ConfigureAwait(false);

            if (resultJson.RootElement.TryGetProperty("error", out JsonElement errorElement))
            {
                Console.WriteLine($"An issue was encountered when trying to analyze the document.");
                Console.WriteLine($"Please double-check your deployment and configurations for potential problems.");
                throw new InvalidOperationException($"Failed to analyze document: {errorElement.GetProperty("message").GetString()}");
            }
            else
            {
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                Console.WriteLine($"Document '{fileLocation}' analyzed successfully.");

                var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeDocumentWithDefinedSchemaForProModeAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
                await File.WriteAllTextAsync(output, serializedJson, encoding: Encoding.UTF8);
                Console.WriteLine("\n===== Document with defined schema for pro mode has been saved to the following output file path =====");
                Console.WriteLine($"\n{output}\n");

                Console.WriteLine("Extracted fields from the analysis result:");
                if (resultJson.RootElement.TryGetProperty("result", out JsonElement result))
                {
                    if (result.TryGetProperty("contents", out JsonElement contents))
                    {
                        if (contents.ValueKind == JsonValueKind.Array && contents.GetArrayLength() > 0)
                        {
                            if (contents[0].TryGetProperty("fields", out JsonElement fields))
                            {
                                Console.WriteLine($"{JsonSerializer.Serialize(fields, new JsonSerializerOptions { WriteIndented = true })}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No contents found in the analysis result.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No 'contents' property found in the analysis result.");
                    }
                }
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

        /// <summary>
        /// Processes the specified directory to identify and validate document files for upload.
        /// </summary>
        /// <remarks>This method checks each file in the directory to determine if it is a supported
        /// document type. If a document is supported, it verifies the existence of a corresponding result file.
        /// Unsupported files or files without corresponding result files will cause exceptions to be thrown.</remarks>
        /// <param name="dirPath">The path of the directory to process. Must not be null or empty.</param>
        /// <param name="uploadOnlyList">A list to which valid document files will be added. Each entry includes the original file and its
        /// corresponding result file.</param>
        /// <exception cref="FileNotFoundException">Thrown if a result file for a supported document does not exist in the directory.</exception>
        /// <exception cref="ArgumentException">Thrown if a file is not a supported document type, or if a result file does not correspond to an original
        /// file.</exception>
        private void ProcessDirectory(string dirPath, List<ReferenceDocItem> uploadOnlyList)
        {
            var filenames = new HashSet<string>(Directory.GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly), StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in filenames)
            {
                string filename = Path.GetFileName(filePath);
                string fileExt = Path.GetExtension(filename);

                if (_client.IsSupportedDocTypeByFileExt(fileExt, isDocument: true))
                {
                    string resultFileName = filename + _client.GetOcrResultFileSuffix();
                    string resultFilePath = Path.Combine(dirPath, resultFileName);

                    if (!File.Exists(resultFilePath))
                    {
                        throw new FileNotFoundException(
                            $"Result file '{resultFileName}' does not exist in '{dirPath}'. " +
                            $"Please run analyze first or remove this file from the folder."
                        );
                    }

                    uploadOnlyList.Add(new ReferenceDocItem
                    {
                        Filename = filename,
                        FilePath = filePath,
                        ResultFileName = resultFileName,
                        ResultFilePath = resultFilePath
                    });
                }
                else if (filename.EndsWith(_client.GetOcrResultFileSuffix(), StringComparison.OrdinalIgnoreCase))
                {
                    string originalFilename = filename.Substring(0, filename.Length - _client.GetOcrResultFileSuffix().Length);
                    string originalFilePath = Path.Combine(dirPath, originalFilename);

                    if (File.Exists(originalFilePath))
                    {
                        // skip result.json files corresponding to the file with supported document type
                        string originalFileExt = Path.GetExtension(originalFilename);
                        if (_client.IsSupportedDocTypeByFileExt(originalFileExt, isDocument: true))
                        {
                            continue;
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"The '{originalFilename}' is not a supported document type, " +
                                $"please remove the result file '{filename}' and '{originalFilename}'."
                            );
                        }
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"Result file '{filename}' is not corresponding to an original file, " +
                            $"please remove it."
                        );
                    }
                }
                else
                {
                    throw new ArgumentException(
                        $"File '{filename}' is not a supported document type, " +
                        $"please remove it or convert it to a supported type."
                    );
                }
            }
        }

        /// <summary>
        /// Asynchronously retrieves a list of reference document items from the specified folder.
        /// </summary>
        /// <param name="referenceDocsFolder">The path to the folder containing reference documents to analyze.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see
        /// cref="ReferenceDocItem"/> objects representing the documents to be analyzed.</returns>
        /// <exception cref="ArgumentException">Thrown if a file in the folder is not a supported document type.</exception>
        private List<ReferenceDocItem> GetAnalyzeList(string referenceDocsFolder)
        {
            var analyzeList = new List<ReferenceDocItem>();

            foreach (var dirpath in Directory.GetDirectories(referenceDocsFolder, "*", SearchOption.AllDirectories))
            {
                foreach (var filename in Directory.GetFiles(dirpath))
                {
                    string fileNameOnly = Path.GetFileName(filename);
                    string fileExt = Path.GetExtension(fileNameOnly);
                    if (_client.IsSupportedDocTypeByFileExt(fileExt, isDocument: true))
                    {
                        string filePath = Path.Combine(dirpath, fileNameOnly);
                        string resultFileName = fileNameOnly + _client.GetOcrResultFileSuffix();
                        analyzeList.Add(new ReferenceDocItem
                        {
                            Filename = fileNameOnly,
                            FilePath = filePath,
                            ResultFileName = resultFileName
                        });
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"File '{fileNameOnly}' is not a supported document type, please remove it or convert it to a supported type."
                        );
                    }
                }
            }

            // Also process files in the root folder
            foreach (var filename in Directory.GetFiles(referenceDocsFolder))
            {
                string fileNameOnly = Path.GetFileName(filename);
                string fileExt = Path.GetExtension(fileNameOnly);
                if (_client.IsSupportedDocTypeByFileExt(fileExt, isDocument: true))
                {
                    string filePath = Path.Combine(referenceDocsFolder, fileNameOnly);
                    string resultFileName = fileNameOnly + _client.GetOcrResultFileSuffix();
                    analyzeList.Add(new ReferenceDocItem
                    {
                        Filename = fileNameOnly,
                        FilePath = filePath,
                        ResultFileName = resultFileName
                    });
                }
                else
                {
                    throw new ArgumentException(
                        $"File '{fileNameOnly}' is not a supported document type, please remove it or convert it to a supported type."
                    );
                }
            }

            return analyzeList;
        }
    }
}
