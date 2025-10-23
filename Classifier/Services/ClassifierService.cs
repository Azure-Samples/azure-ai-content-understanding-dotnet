using Azure;
using Azure.AI.ContentUnderstanding;
using Classifier.Interfaces;
using ContentUnderstanding.Common;
using Microsoft.VisualBasic;
using System.Text.Json;

namespace Classifier.Services
{
    public class ClassifierService : IClassifierService
    {
        private readonly ContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/classifier/";

        public ClassifierService(ContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Asynchronously classifies a document using the specified content classifier.
        /// </summary>
        /// <remarks>This method creates or replaces a content classifier, classifies the specified
        /// document, and then deletes the classifier. Ensure that the file at <paramref name="fileLocation"/> exists
        /// before calling this method.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier to be used.</param>
        /// <param name="classifier">The content classifier configuration to apply to the document.</param>
        /// <param name="fileLocation">The file path of the document to classify. Must be a valid path to an existing file.</param>
        /// <returns>A <see cref="ClassifyResult"/> containing the classification results, or <see langword="null"/> if the file
        /// is not found or an error occurs.</returns>
        public async Task<ClassifyResult?> ClassifyDocumentAsync(string classifierId, ContentClassifier classifier, string fileLocation)
        {
            try
            {
                Console.WriteLine($"🔧 Creating classifier analyzer '{classifierId}'...");

                // Start the create or replace operation
                await _client.GetContentClassifiersClient().CreateOrReplaceAsync(
                    waitUntil: WaitUntil.Completed,
                    classifierId,
                    classifier);

                Console.WriteLine($"✅ Classifier created successfully!");

                Console.WriteLine("Classifying document...");
                Console.WriteLine($"Input file location: {fileLocation}");
                Console.WriteLine("Processing... This may take a few minutes for large documents.");

                if (!File.Exists(fileLocation))
                {
                    Console.WriteLine($"❌ Error: Sample file not found at {fileLocation}");
                    Console.WriteLine("Please ensure the sample file exists in the sample_files directory.");
                    return null; 
                }

                Console.WriteLine($"📄 Using sample file: {fileLocation}");
                byte[] fileBytes = await File.ReadAllBytesAsync(fileLocation);
                BinaryData fileData = new BinaryData(fileBytes);
                Console.WriteLine($"✅ File loaded successfully ({fileBytes.Length} bytes)");

                // Start the classification operation
                Console.WriteLine($"🚀 Starting content classification...");

                var operation = await _client.GetContentClassifiersClient()
                    .ClassifyBinaryAsync(
                        waitUntil: WaitUntil.Completed, 
                        classifierId, "application/pdf", 
                        input: fileData);

                Console.WriteLine("Classification completed successfully!");

                // Get the classification result using SDK wrapper
                ClassifyResult result = operation.Value;
                Console.WriteLine($"🔍 Classification Results:");
                Console.WriteLine($"   Classifier ID: {result.ClassifierId}");

                // Display warnings if any
                if (result.Warnings != null && result.Warnings.Count > 0)
                {
                    Console.WriteLine($"⚠️  Warnings ({result.Warnings.Count}):");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"   - {warning.Message}");
                    }
                }

                // Process and display classification results
                Console.WriteLine($"\n📊 Classification Results:");
                foreach (var content in result.Contents)
                {
                    Console.WriteLine($"\n📄 Content Type: {content.GetType().Name}");
                    Console.WriteLine($"   Category: {content.Category}");
                }

                // Clean up: delete the classifier
                Console.WriteLine($"\n🧹 Cleaning up...");
                await _client.GetContentClassifiersClient()
                    .DeleteAsync(classifierId);
                Console.WriteLine($"✅ Classifier deleted successfully!");

                Console.WriteLine($"\n💡 Next steps:");
                Console.WriteLine($"   - To analyze content from URL: see AnalyzeUrl sample");
                Console.WriteLine($"   - To analyze binary files: see AnalyzeBinary sample");
                Console.WriteLine($"   - To create a custom classifier: see CreateOrReplaceClassifier sample");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An error occurred: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
