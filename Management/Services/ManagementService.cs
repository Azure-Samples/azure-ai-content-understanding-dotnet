using Azure;
using ContentUnderstanding.Common;
using Management.Interfaces;
using System.Text.Json;

namespace Management.Services
{
    public class ManagementService : IManagementService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/management/";

        public ManagementService(AzureContentUnderstandingClient client)
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
        public async Task<JsonDocument> CreateAnalyzerAsync(string analyzerId, Dictionary<string, object> analyzerDefinition)
        {
            Console.WriteLine($"Creating custom analyzer '{analyzerId}'...");

            // Convert analyzer definition to JSON and save to temp file
            string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(analyzerDefinition, jsonOptions));

                // Create analyzer
                var response = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: tempTemplatePath);

                // Wait for the analyzer to be created
                Console.WriteLine("Waiting for analyzer creation to complete...");
                var result = await _client.PollResultAsync(response);
                Console.WriteLine($"Analyzer '{analyzerId}' created successfully!");

                return result;
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
        /// List all analyzers created in your resource.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <returns></returns>
        public async Task<JsonElement[]?> ListAnalyzersAsync()
        {
            var response = await _client.GetAllAnalyzersAsync();

            // Extract the analyzers array from the response
            var analyzers = response ?? Array.Empty<JsonElement>();

            Console.WriteLine($"Found {analyzers.Length} analyzers");
            Console.WriteLine();

            // Display detailed information about each analyzer
            for (int i = 0; i < analyzers.Length; i++)
            {
                var analyzer = analyzers[i];

                Console.WriteLine($"Analyzer {i + 1}:");

                // Get analyzer ID
                string? analyzerId = analyzer.TryGetProperty("analyzerId", out var idProp)
                    ? idProp.GetString()
                    : null;
                Console.WriteLine($"   ID: {analyzerId}");

                // Get description
                string? description = analyzer.TryGetProperty("description", out var descProp)
                    ? descProp.GetString()
                    : null;
                Console.WriteLine($"   Description: {description}");

                // Get status
                string? status = analyzer.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()
                    : null;
                Console.WriteLine($"   Status: {status}");

                // Get created at
                string? createdAt = analyzer.TryGetProperty("createdAt", out var createdProp)
                    ? createdProp.GetString()
                    : null;
                Console.WriteLine($"   Created at: {createdAt}");

                // Check if it's a prebuilt analyzer
                if (!string.IsNullOrEmpty(analyzerId) && analyzerId.StartsWith("prebuilt-"))
                {
                    Console.WriteLine("   Type: Prebuilt analyzer");
                }
                else
                {
                    Console.WriteLine("   Type: Custom analyzer");
                }

                // Show tags if available
                if (analyzer.TryGetProperty("tags", out var tags))
                {
                    Console.WriteLine($"   Tags: {tags}");
                }

                Console.WriteLine();
            }

            return analyzers;
        }

        /// <summary>
        /// Get analyzer details with id.
        /// </summary>
        /// <remarks>Remember the analyzer id when you create it. You can use the id to look up detail analyzer definitions afterwards.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer whose details are to be retrieved. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<string> GetAnalyzerDetailsAsync(string analyzerId)
        {
            var retrievedAnalyzer = await _client.GetAnalyzerDetailByIdAsync(analyzerId);

            Console.WriteLine($"Analyzer '{analyzerId}' retrieved successfully!");

            // Extract basic information
            string? description = null;
            string? status = null;
            string? createdAt = null;

            if (retrievedAnalyzer.TryGetValue("description", out var descValue))
            {
                description = descValue.ToString();
            }

            if (retrievedAnalyzer.TryGetValue("status", out var statusValue))
            {
                status = statusValue.ToString();
            }

            if (retrievedAnalyzer.TryGetValue("createdAt", out var createdValue))
            {
                createdAt = createdValue.ToString();
            }

            Console.WriteLine($"   Description: {description}");
            Console.WriteLine($"   Status: {status}");
            Console.WriteLine($"   Created at: {createdAt}");

            // Print the full analyzer response
            Console.WriteLine("\nFull Analyzer Details:");
            
            string jsonOutput = JsonSerializer.Serialize(
                retrievedAnalyzer,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

            Console.WriteLine(jsonOutput);
            return jsonOutput;
        }

        /// <summary>
        /// Delete Analyzer.
        /// </summary>
        /// <remarks>If you don't need an analyzer anymore, delete it with its id.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        public async Task DeleteAnalyzerAsync(string analyzerId)
        {
            Console.WriteLine($"\nDeleting analyzer '{analyzerId}'...");
            await _client.DeleteAnalyzerAsync(analyzerId);
            Console.WriteLine($"Analyzer '{analyzerId}' deleted successfully!");
        }

        /// <summary>
        /// Update default model deployment mappings.
        /// </summary>
        /// <param name="modelDeployments">A dictionary mapping model names to deployment names.</param>
        /// <returns>A dictionary containing the updated default settings.</returns>
        public async Task<Dictionary<string, object>> UpdateDefaultsAsync(Dictionary<string, string?> modelDeployments)
        {
            var result = await _client.UpdateDefaultsAsync(modelDeployments);
            
            Console.WriteLine("✅ Default model deployments configured successfully");
            Console.WriteLine("   Model mappings:");
            
            if (result.TryGetValue("modelDeployments", out var modelDeploymentsValue))
            {
                var modelDeploymentsJson = modelDeploymentsValue.ToString();
                if (modelDeploymentsJson != null)
                {
                    var deployments = JsonSerializer.Deserialize<Dictionary<string, string>>(modelDeploymentsJson);
                    if (deployments != null)
                    {
                        foreach (var (model, deployment) in deployments)
                        {
                            Console.WriteLine($"     {model} → {deployment}");
                        }
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get default settings including model deployment mappings.
        /// </summary>
        /// <returns>A dictionary containing the default settings.</returns>
        public async Task<Dictionary<string, object>> GetDefaultsAsync()
        {
            var defaults = await _client.GetDefaultsAsync();
            
            Console.WriteLine("✅ Retrieved default settings");
            
            if (defaults.TryGetValue("modelDeployments", out var modelDeploymentsValue))
            {
                var modelDeploymentsJson = modelDeploymentsValue.ToString();
                if (modelDeploymentsJson != null)
                {
                    var modelDeployments = JsonSerializer.Deserialize<Dictionary<string, string>>(modelDeploymentsJson);
                    if (modelDeployments != null && modelDeployments.Count > 0)
                    {
                        Console.WriteLine("\n📋 Model Deployments:");
                        foreach (var (modelName, deploymentName) in modelDeployments)
                        {
                            Console.WriteLine($"   {modelName}: {deploymentName}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n   No model deployments configured");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n   No model deployments configured");
            }
            
            return defaults;
        }
    }
}
