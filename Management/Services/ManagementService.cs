using Azure;
using Azure.AI.ContentUnderstanding;
using Management.Interfaces;
using System.Text.Json;

namespace Management.Services
{
    public class ManagementService : IManagementService
    {
        private readonly ContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/management/";

        public ManagementService(ContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Create a simple analyzer.
        /// </summary>
        /// <remarks>We first create an analyzer from a template to extract invoice fields.</remarks>
        /// <returns>A <see cref="string"/> representing the unique identifier of the created analyzer.</returns>
        public async Task<string> CreateAnalyzerAsync(string analyzerId, ContentAnalyzer analyzer)
        {
            Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");

            // Start the create or replace operation
            var analyzerOperation = await _client.GetContentAnalyzersClient()
                .CreateOrReplaceAsync(
                    waitUntil: WaitUntil.Completed,
                    analyzerId: analyzerId,
                    resource: analyzer);

            // Get the result
            ContentAnalyzer result = analyzerOperation.Value;
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

            return analyzerId;
        }

        /// <summary>
        /// List all analyzers created in your resource.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <returns></returns>
        public async Task<List<ContentAnalyzer>> ListAnalyzersAsync()
        {
            // List all analyzers
            var analyzers = new List<ContentAnalyzer>();
            Console.WriteLine("\n===== Listing All Analyzers =====");
            try
            {
                await foreach (var analyzer in _client.GetContentAnalyzersClient().GetAllAsync())
                {
                    analyzers.Add(analyzer);
                }

                Console.WriteLine($"✅ Found {analyzers.Count} analyzers\n");

                // Display detailed information about each analyzer
                int counter = 1;
                foreach (var analyzer in analyzers)
                {
                    Console.WriteLine($"🔍 Analyzer {counter}:");
                    Console.WriteLine($"   ID: {analyzer.AnalyzerId}");
                    Console.WriteLine($"   Description: {analyzer.Description}");
                    Console.WriteLine($"   Status: {analyzer.Status}");
                    Console.WriteLine($"   Created at: {analyzer.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

                    // Check if it's a prebuilt analyzer
                    if (analyzer.AnalyzerId?.StartsWith("prebuilt-") == true)
                    {
                        Console.WriteLine($"   Type: Prebuilt analyzer");
                    }
                    else
                    {
                        Console.WriteLine($"   Type: Custom analyzer");
                    }

                    // Show tags if available
                    if (analyzer.Tags != null && analyzer.Tags.Count > 0)
                    {
                        Console.WriteLine($"   Tags:");
                        foreach (var tag in analyzer.Tags)
                        {
                            Console.WriteLine($"      - {tag.Key}: {tag.Value}");
                        }
                    }

                    Console.WriteLine();
                    counter++;
                }

                // Check for specific prebuilt analyzers
                var prebuiltAnalyzers = analyzers
                    .Where(a => a.AnalyzerId?.StartsWith("prebuilt-") == true)
                    .Select(a => a.AnalyzerId)
                    .ToList();

                if (prebuiltAnalyzers.Contains("prebuilt-documentAnalyzer"))
                {
                    Console.WriteLine("   ✅ prebuilt-documentAnalyzer is available");
                }
                if (prebuiltAnalyzers.Contains("prebuilt-videoAnalyzer"))
                {
                    Console.WriteLine("   ✅ prebuilt-videoAnalyzer is available");
                }

                Console.WriteLine("\n💡 Next steps:");
                Console.WriteLine("   - To create an analyzer: see CreateOrReplaceAnalyzer sample");
                Console.WriteLine("   - To get a specific analyzer: see GetAnalyzer sample");
                Console.WriteLine("   - To update an analyzer: see UpdateAnalyzer sample");
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

            return analyzers;
        }

        /// <summary>
        /// Get analyzer details with id.
        /// </summary>
        /// <remarks>Remember the analyzer id when you create it. You can use the id to look up detail analyzer definitions afterwards.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer whose details are to be retrieved. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<ContentAnalyzer> GetAnalyzerDetailsAsync(string analyzerId)
        {
            ContentAnalyzer retrievedAnalyzer = new ContentAnalyzer();

            try
            {
                Console.WriteLine($"📋 Retrieving analyzer '{analyzerId}'...");

                Response<ContentAnalyzer> response = await _client.GetContentAnalyzersClient()
                        .GetAsync(analyzerId);

                retrievedAnalyzer = response.Value;
                Console.WriteLine($"✅ Analyzer '{analyzerId}' retrieved successfully!");
                Console.WriteLine($"   Description: {retrievedAnalyzer.Description}");
                Console.WriteLine($"   Status: {retrievedAnalyzer.Status}");
                Console.WriteLine($"   Created at: {retrievedAnalyzer.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"   Base Analyzer: {retrievedAnalyzer.BaseAnalyzerId}");

                // Display field schema if available
                if (retrievedAnalyzer.FieldSchema != null)
                {
                    Console.WriteLine($"\n📋 Field Schema: {retrievedAnalyzer.FieldSchema.Name}");
                    Console.WriteLine($"   {retrievedAnalyzer.FieldSchema.Description}");
                    Console.WriteLine($"   Fields:");
                    foreach (var field in retrievedAnalyzer.FieldSchema.Fields)
                    {
                        Console.WriteLine($"      - {field.Key}: {field.Value.Type} ({field.Value.Method})");
                        Console.WriteLine($"        {field.Value.Description}");
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

            return retrievedAnalyzer;
        }

        /// <summary>
        /// Update an existing analyzer's description and tags.
        /// </summary>
        /// <remarks>This method updates only the mutable properties of an analyzer (description and tags). 
        /// To remove a tag, set its value to an empty string. To update a tag, set it to a new value. 
        /// To add a tag, add a new key-value pair.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to update. Cannot be null or empty.</param>
        /// <returns>A <see cref="ContentAnalyzer"/> representing the updated analyzer with the new description and tags.</returns>
        public async Task<ContentAnalyzer> UpdateAnalyzerAsync(string analyzerId)
        {
            ContentAnalyzer analyzerAfterUpdate = new ContentAnalyzer();

            try
            {
                Console.WriteLine($"📋 Retrieving analyzer '{analyzerId}'...");
                Response<ContentAnalyzer> beforeUpdateResponse = await _client.GetContentAnalyzersClient()
                        .GetAsync(analyzerId);

                ContentAnalyzer analyzerBeforeUpdate = beforeUpdateResponse.Value;
                Console.WriteLine($"✅ Analyzer '{analyzerId}' retrieved successfully!");
                Console.WriteLine($"   Description: {analyzerBeforeUpdate.Description}");
                Console.WriteLine($"   Status: {analyzerBeforeUpdate.Status}");
                Console.WriteLine($"   Created at: {analyzerBeforeUpdate.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"   Base Analyzer: {analyzerBeforeUpdate.BaseAnalyzerId}");

                Console.WriteLine($"✅ Initial analyzer state verified:");
                Console.WriteLine($"   Description: {analyzerBeforeUpdate.Description}");
                Console.Write($"   Tags: {{");
                if (analyzerBeforeUpdate.Tags != null)
                {
                    Console.Write(string.Join(", ", analyzerBeforeUpdate.Tags.Select(kv => $"'{kv.Key}': '{kv.Value}'")));
                }
                Console.WriteLine("}");

                // Create updated analyzer with only allowed properties (description and tags)
                Console.WriteLine($"🔄 Creating updated analyzer configuration...");
                // Update the value for tag1, remove tag2 by setting it to an empty string, and add tag3
                var updatedAnalyzer = new ContentAnalyzer
                {
                    Description = "Updated description"
                };

                // Modify tags - update tag1, remove tag2 (set to empty), add tag3
                updatedAnalyzer.Tags["tag1"] = "tag1_updated_value";
                updatedAnalyzer.Tags["tag2"] = "";  // Remove tag2
                updatedAnalyzer.Tags["tag3"] = "tag3_value";  // Add tag3

                // Update the analyzer using the protocol method
                Console.WriteLine($"📝 Updating analyzer '{analyzerId}' with new description and tags...");
                await _client.GetContentAnalyzersClient().UpdateAsync(
                    analyzerId: analyzerId,
                    content: updatedAnalyzer);

                Console.WriteLine($"✅ Analyzer updated successfully!");

                // Get the analyzer after update to verify the changes persisted
                Console.WriteLine($"📋 Getting analyzer '{analyzerId}' after update...");
                var afterUpdateResponse = await _client.GetContentAnalyzersClient().GetAsync(analyzerId);
                analyzerAfterUpdate = afterUpdateResponse.Value;

                Console.WriteLine($"✅ Updated analyzer state verified:");
                Console.WriteLine($"   Description: {analyzerAfterUpdate.Description}");
                Console.Write($"   Tags: {{");
                if (analyzerAfterUpdate.Tags != null)
                {
                    Console.Write(string.Join(", ", analyzerAfterUpdate.Tags.Select(kv => $"'{kv.Key}': '{kv.Value}'")));
                }
                Console.WriteLine("}");
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

            return analyzerAfterUpdate;
        }

        /// <summary>
        /// Delete Analyzer.
        /// </summary>
        /// <remarks>If you don't need an analyzer anymore, delete it with its id.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        public async Task DeleteAnalyzerAsync(string analyzerId)
        {
            Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}' (demo cleanup)...");
            await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
            Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

            Console.WriteLine("\n💡 Next steps:");
            Console.WriteLine("   - To create an analyzer: see CreateOrReplaceAnalyzer sample");
            Console.WriteLine("   - To list all analyzers: see ListAnalyzers sample");
            Console.WriteLine("   - To update an analyzer: see UpdateAnalyzer sample");
        }
    }
}
