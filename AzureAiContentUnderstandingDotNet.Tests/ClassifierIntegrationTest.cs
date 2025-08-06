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
    public class ClassifierIntegrationTest
    {
        private readonly IClassifierService service;

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
        /// Executes an integration test for the analyzer training process, including classifier creation,  document
        /// classification, and enhanced classifier processing.
        /// </summary>
        /// <remarks>This test performs the following operations: <list type="bullet">
        /// <item><description>Creates a basic classifier using a specified schema.</description></item>
        /// <item><description>Classifies a document using the created classifier.</description></item>
        /// <item><description>Processes a document using an enhanced classifier.</description></item> </list> If any
        /// exception occurs during the test, it is captured and the test asserts that no exceptions were
        /// thrown.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                var analyzerTemplatePath = "./data/mixed_financial_docs.pdf";
                var (analyzerSchemaPath, enhancedSchemaPath) = ("./analyzer_templates/analyzer_schema.json", "./data/classifier/enhanced_schema.json");
                var classifierId = $"classifier-sample-{Guid.NewGuid()}";
                var classifierSchemaPath = "./data/classifier/schema.json";

                // Create a basic classifier
                await CreateClassifierAsync(classifierId, classifierSchemaPath);

                // Classify a document using the created classifier
                await ClassifyDocumentAsync(classifierId, analyzerTemplatePath);

                // Process a document using the enhanced classifier
                await ProcessDocumentWithEnhancedClassifierAsync(analyzerSchemaPath, enhancedSchemaPath, analyzerTemplatePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            Assert.Null(serviceException);
        }

        /// <summary>
        /// Creates a basic classifier asynchronously and validates the response structure and content.
        /// </summary>
        /// <remarks>
        /// This method creates a classifier using the specified ID and schema file, then performs comprehensive
        /// validation on the returned JSON response including status verification, categories enumeration,
        /// and content assertions to ensure the classifier was created successfully.
        /// </remarks>
        /// <param name="classifierId">The unique identifier for the classifier to be created.</param>
        /// <param name="classifierSchemaPath">The file path to the JSON schema file used for creating the classifier.</param>
        /// <returns>A task that represents the asynchronous operation of creating and validating the classifier.</returns>
        private async Task CreateClassifierAsync(string classifierId, string classifierSchemaPath)
        {
            // Create a basic classifier
            JsonDocument resultJson = await service.CreateClassifierAsync(classifierId, classifierSchemaPath);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var values));
            Assert.False(values.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("status", out JsonElement status));
            Assert.Equal("ready", status.ToString());
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
        /// Asynchronously classifies a document using the specified classifier.
        /// </summary>
        /// <remarks>This method uses the specified classifier to analyze the document and retrieve
        /// classification results. The classification results include the status of the operation and the identified
        /// categories.</remarks>
        /// <param name="classifierId">The unique identifier of the classifier to use for document classification. Cannot be null or empty.</param>
        /// <param name="fileLocation">The file path or location of the document to be classified. Must point to a valid file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
        /// Processes a document using an enhanced classifier with a custom analyzer.
        /// </summary>
        /// <remarks>This method creates a custom analyzer and an enhanced classifier using the provided
        /// schemas,  processes a document using the enhanced classifier, and validates the resulting output. The
        /// resulting document is expected to contain structured content, including markdown and fields.</remarks>
        /// <param name="analyzerSchemaPath">The file path to the schema defining the custom analyzer.</param>
        /// <param name="enhancedSchemaPath">The file path to the schema defining the enhanced classifier.</param>
        /// <param name="analyzerTemplatePath">The file path to the template used for processing the document.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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
