using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration test for classifier and enhanced classifier workflows using the IClassifierService.
    /// This test covers classifier creation, document classification, and enhanced classifier processing.
    /// </summary>
    public class ClassifierIntegrationTest
    {
        private readonly IClassifierService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for classifier testing.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion" are missing.
        /// </exception>
        public ClassifierIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Load configuration from environment variables or appsettings.json
                    string? endpoint = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint");

                    // API version for Azure Content Understanding service
                    string? apiVersion = Environment.GetEnvironmentVariable("AZURE_CU_CONFIG_ApiVersion") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion");

                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        throw new ArgumentException("Endpoint must be provided in environment variable or appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(apiVersion))
                    {
                        throw new ArgumentException("API version must be provided in environment variable or appsettings.json.");
                    }

                    services.AddConfigurations(opts =>
                    {
                        opts.Endpoint = endpoint;
                        opts.ApiVersion = apiVersion;
                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/classifier";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IClassifierService, ClassifierService>();
                })
                .Build();

            service = host.Services.GetService<IClassifierService>()!;
        }

        /// <summary>
        /// Executes an integration test for classifier workflows:
        /// 1. Creates a basic classifier using a schema.
        /// 2. Classifies a document using the created classifier.
        /// 3. Processes a document using an enhanced classifier.
        /// Captures any exceptions and asserts that no unexpected errors occur.
        /// </summary>
        [Fact(DisplayName = "Classifier Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                // File paths and IDs for test scenarios
                var analyzerTemplatePath = "./data/mixed_financial_docs.pdf";
                var (analyzerSchemaPath, enhancedSchemaPath) = ("./analyzer_templates/analyzer_schema.json", "./data/classifier/enhanced_schema.json");
                var classifierId = $"classifier-sample-{Guid.NewGuid()}";
                var classifierSchemaPath = "./data/classifier/schema.json";

                // Validate that the required files exist
                Assert.True(File.Exists(analyzerTemplatePath), "Analyzer template file does not exist.");
                Assert.True(File.Exists(analyzerSchemaPath), "Analyzer schema file does not exist.");
                Assert.True(File.Exists(enhancedSchemaPath), "Enhanced schema file does not exist.");
                Assert.True(File.Exists(classifierSchemaPath), "Classifier schema file does not exist.");

                // Read the JSON content from the schema files
                var (analyzerSchema, enhancedSchema) = (await File.ReadAllTextAsync(analyzerSchemaPath), await File.ReadAllTextAsync(enhancedSchemaPath));
                var classifierSchema = await File.ReadAllTextAsync(classifierSchemaPath);
                Assert.False(string.IsNullOrWhiteSpace(analyzerSchema), "Analyzer schema JSON should not be empty.");
                Assert.False(string.IsNullOrWhiteSpace(enhancedSchema), "Enhanced schema JSON should not be empty.");
                Assert.False(string.IsNullOrWhiteSpace(classifierSchema), "Classifier schema JSON should not be empty.");

                JsonElement analyzerSchemaJson = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(analyzerSchemaPath));
                Assert.True(analyzerSchemaJson.TryGetProperty("fieldSchema", out var fieldSchema));
                Assert.True(fieldSchema.TryGetProperty("fields", out var fields));

                JsonElement enhancedSchemaJson = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(enhancedSchemaPath));
                Assert.True(enhancedSchemaJson.TryGetProperty("categories", out var enhancedCategories));
                Assert.True(enhancedSchemaJson.TryGetProperty("splitMode", out var enhancedSplitMode));
                Assert.Equal("auto", enhancedSplitMode.ToString());

                JsonElement classifierSchemaJson = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(classifierSchemaPath));
                Assert.True(classifierSchemaJson.TryGetProperty("categories", out var classifierCategories));
                Assert.True(classifierSchemaJson.TryGetProperty("splitMode", out var classifierSplitMode));
                Assert.Equal("auto", classifierSplitMode.ToString());

                // Step 1: Create a basic classifier
                await CreateClassifierAsync(classifierId, classifierSchemaPath);

                // Step 2: Classify a document using the created classifier
                await ClassifyDocumentAsync(classifierId, analyzerTemplatePath);

                // Step 3: Process a document using the enhanced classifier
                await ProcessDocumentWithEnhancedClassifierAsync(analyzerSchemaPath, enhancedSchemaPath, analyzerTemplatePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Creates a basic classifier with the given ID and schema, and verifies its response.
        /// Checks that the classifier is ready, contains categories, and each category is properly described.
        /// </summary>
        /// <param name="classifierId">Unique identifier for the classifier.</param>
        /// <param name="classifierSchemaPath">File path to the classifier schema (JSON).</param>
        private async Task CreateClassifierAsync(string classifierId, string classifierSchemaPath)
        {
            // Create a basic classifier
            JsonDocument resultJson = await service.CreateClassifierAsync(classifierId, classifierSchemaPath);
            Assert.NotNull(resultJson);

            // Validate result structure and status
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var warnings));
            Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("status", out JsonElement status));
            Assert.Equal("ready", status.ToString());

            // Validate categories and descriptions
            Assert.True(result.TryGetProperty("categories", out JsonElement categories));
            var list = new List<(string name, string description)>();

            foreach (var category in categories.EnumerateObject())
            {
                Assert.NotEmpty(category.Name);
                Assert.NotEmpty(category.Value.ToString());
                Assert.True(category.Value.TryGetProperty("description", out JsonElement description));
                list.Add((category.Name, description.ToString()));
            }

            Assert.True(list.Any());
        }

        /// <summary>
        /// Classifies a document using the specified classifier and validates the result.
        /// Asserts that classification results are returned for the document.
        /// </summary>
        /// <param name="classifierId">ID of the classifier.</param>
        /// <param name="fileLocation">Path to the document to be classified.</param>
        private async Task ClassifyDocumentAsync(string classifierId, string fileLocation)
        {
            // Classify a document using the created classifier
            JsonDocument resultJson = await service.ClassifyDocumentAsync(classifierId, fileLocation);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("contents", out JsonElement contents));
            Assert.True(contents.EnumerateArray().Any());
        }

        /// <summary>
        /// Processes a document using an enhanced classifier and a custom analyzer.
        /// Validates that processed content contains markdown and fields.
        /// </summary>
        /// <param name="analyzerSchemaPath">Schema path for the custom analyzer.</param>
        /// <param name="enhancedSchemaPath">Schema path for the enhanced classifier.</param>
        /// <param name="analyzerTemplatePath">Path to the document to process.</param>
        private async Task ProcessDocumentWithEnhancedClassifierAsync(string analyzerSchemaPath, string enhancedSchemaPath, string analyzerTemplatePath)
        {
            var analyzerId = $"analyzer-loan-application-{Guid.NewGuid()}";
            var enhancedClassifierId = await service.CreateEnhancedClassifierWithCustomAnalyzerAsync(analyzerId, analyzerSchemaPath, enhancedSchemaPath);
            Assert.NotNull(enhancedClassifierId);
            JsonDocument resultJson = await service.ProcessDocumentWithEnhancedClassifierAsync(enhancedClassifierId, analyzerTemplatePath);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("contents", out JsonElement contents));
            Assert.True(contents.EnumerateArray().Any());
            var content = contents[0];
            Assert.True(content.TryGetProperty("markdown", out JsonElement markdown));
            Assert.True(!string.IsNullOrWhiteSpace(markdown.ToString()));
            Assert.True(content.TryGetProperty("fields", out JsonElement fields));
            Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));
        }
    }
}
