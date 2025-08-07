using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstandingDotNet.Tests
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
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint")))
                    {
                        throw new ArgumentException("Endpoint must be provided in appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion")))
                    {
                        throw new ArgumentException("API version must be provided in appsettings.json.");
                    }
                    services.AddConfigurations(opts =>
                    {
                        context.Configuration.GetSection("AZURE_CU_CONFIG").Bind(opts);
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
        [Fact]
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
