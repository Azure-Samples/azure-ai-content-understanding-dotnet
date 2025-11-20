using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Azure;
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
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IClassifierService, ClassifierService>();
                }
            );

            service = host.Services.GetService<IClassifierService>()!;
        }

        /// <summary>
        /// Executes an integration test for classifier workflows:
        /// 1. Creates a loan application analyzer.
        /// 2. Classifies a document using a basic classifier.
        /// 3. Classifies a document using an enhanced classifier (with custom analyzer).
        /// 4. Cleans up the created analyzer.
        /// Captures any exceptions and asserts that no unexpected errors occur.
        /// </summary>
        [Fact(DisplayName = "Classifier Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            string loanAnalyzerId = $"test_loan_analyzer_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            string basicClassifierId = $"test_basic_classifier_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            string enhancedClassifierId = $"test_enhanced_classifier_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            try
            {
                var documentPath = "./data/mixed_financial_docs.pdf";

                // Step 1: Create loan application analyzer
                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine($"Step 1: Creating loan analyzer: {loanAnalyzerId}");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
                await service.CreateLoanAnalyzerAsync(loanAnalyzerId);
                Console.WriteLine($"✅ Loan analyzer created successfully");

                // Step 2: Create basic classifier schema (without custom analyzer)
                // Match the structure used in classifier_program.cs
                var basicClassifierSchema = new Dictionary<string, object>
                {
                    ["baseAnalyzerId"] = "prebuilt-document",
                    ["description"] = $"Basic classifier for financial documents: {basicClassifierId}",
                    ["config"] = new Dictionary<string, object>
                    {
                        ["returnDetails"] = true,
                        ["enableSegment"] = true,
                        ["contentCategories"] = new Dictionary<string, object>
                        {
                            ["Loan application"] = new Dictionary<string, object>
                            {
                                ["description"] = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation."
                            },
                            ["Invoice"] = new Dictionary<string, object>
                            {
                                ["description"] = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                            },
                            ["Bank_Statement"] = new Dictionary<string, object>
                            {
                                ["description"] = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                            }
                        }
                    },
                    ["models"] = new Dictionary<string, string>
                    {
                        ["completion"] = "gpt-4.1"
                    },
                    ["tags"] = new Dictionary<string, string>
                    {
                        ["test_type"] = "basic_classification",
                        ["purpose"] = "integration_test"
                    }
                };

                // Step 3: Classify document using basic classifier
                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine("Step 2: Testing Basic Classifier");
                Console.WriteLine($"Classifier ID: {basicClassifierId}");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");

                var basicResult = await service.ClassifyDocumentAsync(basicClassifierId, basicClassifierSchema, documentPath);

                Assert.NotNull(basicResult);
                Console.WriteLine("✅ Basic classifier completed");
                ValidateClassificationResult(basicResult, expectFields: false, scenarioName: "Basic Classifier");

                // Step 4: Create enhanced classifier schema (with custom analyzer for loan applications)
                // Match the structure used in classifier_program.cs
                var enhancedClassifierSchema = new Dictionary<string, object>
                {
                    ["baseAnalyzerId"] = "prebuilt-document",
                    ["description"] = $"Enhanced classifier with custom loan analyzer: {enhancedClassifierId}",
                    ["config"] = new Dictionary<string, object>
                    {
                        ["returnDetails"] = true,
                        ["enableSegment"] = true,
                        ["contentCategories"] = new Dictionary<string, object>
                        {
                            ["Loan application"] = new Dictionary<string, object>
                            {
                                ["description"] = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation.",
                                ["analyzerId"] = loanAnalyzerId // Use the custom loan analyzer
                            },
                            ["Invoice"] = new Dictionary<string, object>
                            {
                                ["description"] = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                            },
                            ["Bank_Statement"] = new Dictionary<string, object>
                            {
                                ["description"] = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                            }
                        }
                    },
                    ["models"] = new Dictionary<string, string>
                    {
                        ["completion"] = "gpt-4.1"
                    },
                    ["tags"] = new Dictionary<string, string>
                    {
                        ["test_type"] = "enhanced_classification",
                        ["purpose"] = "integration_test"
                    }
                };

                // Step 5: Classify document using enhanced classifier
                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine("Step 3: Testing Enhanced Classifier");
                Console.WriteLine($"Classifier ID: {enhancedClassifierId}");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");

                var enhancedResult = await service.ClassifyDocumentAsync(enhancedClassifierId, enhancedClassifierSchema, documentPath);

                Assert.NotNull(enhancedResult);
                Console.WriteLine("✅ Enhanced classifier completed");
                ValidateClassificationResult(enhancedResult, expectFields: true, scenarioName: "Enhanced Classifier");

                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine("✅ All classifier tests completed successfully!");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"\n❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup: Delete the loan analyzer
                // Note: The basic and enhanced classifiers are automatically deleted by ClassifyDocumentAsync
                if (!string.IsNullOrEmpty(loanAnalyzerId))
                {
                    try
                    {
                        Console.WriteLine($"\n🧹 Cleanup: Deleting loan analyzer {loanAnalyzerId}");
                        await service.DeleteAnalyzerAsync(loanAnalyzerId);
                        Console.WriteLine("✅ Cleanup successful");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Cleanup failed: {ex.Message}");
                        // Don't fail the test due to cleanup errors
                    }
                }
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Validates the classification result structure and content.
        /// </summary>
        /// <param name="result">The classification result to validate.</param>
        /// <param name="expectFields">Whether to expect extracted fields in loan application segments.</param>
        /// <param name="scenarioName">The name of the test scenario for logging.</param>
        private void ValidateClassificationResult(JsonDocument result, bool expectFields, string scenarioName)
        {
            Assert.NotNull(result);
            Console.WriteLine($"\nValidating {scenarioName} result...");

            // Verify result structure
            Assert.True(result.RootElement.TryGetProperty("result", out var resultElement),
                "Result should contain 'result' property");

            // Check warnings (should be empty or not critical)
            if (resultElement.TryGetProperty("warnings", out var warnings) &&
                warnings.ValueKind == JsonValueKind.Array)
            {
                var warningsArray = warnings.EnumerateArray().ToList();
                if (warningsArray.Any())
                {
                    Console.WriteLine($"⚠️  Warnings found: {warningsArray.Count}");
                    foreach (var warning in warningsArray)
                    {
                        if (warning.TryGetProperty("code", out var code))
                        {
                            var warningCode = code.GetString();
                            var message = warning.TryGetProperty("message", out var msg) ? msg.GetString() : "";
                            Console.WriteLine($"  - {warningCode}: {message}");
                        }
                    }
                }
                // Don't fail on warnings, just log them
            }

            // Check contents (should exist and not be empty)
            Assert.True(resultElement.TryGetProperty("contents", out var contents),
                "Result should contain 'contents' array");
            Assert.True(contents.ValueKind == JsonValueKind.Array,
                "Contents should be an array");

            var contentsArray = contents.EnumerateArray().ToList();
            Assert.NotEmpty(contentsArray);
            Console.WriteLine($"✓ Found {contentsArray.Count} content item(s)");

            var content = contentsArray[0];

            // Check for segments (classifier with enableSegment should return segments)
            bool hasSegments = content.TryGetProperty("segments", out var segments) &&
                               segments.ValueKind == JsonValueKind.Array;

            if (hasSegments)
            {
                var segmentsArray = segments.EnumerateArray().ToList();
                Assert.NotEmpty(segmentsArray);
                Console.WriteLine($"✓ Found {segmentsArray.Count} segment(s)");

                // Validate each segment
                int segmentIndex = 0;
                foreach (var segment in segmentsArray)
                {
                    segmentIndex++;

                    // Each segment should have a category
                    Assert.True(segment.TryGetProperty("category", out var category),
                        $"Segment {segmentIndex} should have a category");
                    var categoryName = category.GetString();
                    Console.WriteLine($"  Segment {segmentIndex}: {categoryName}");

                    // Check page range
                    if (segment.TryGetProperty("startPageNumber", out var startPage) &&
                        segment.TryGetProperty("endPageNumber", out var endPage))
                    {
                        Console.WriteLine($"    Pages: {startPage.GetInt32()} - {endPage.GetInt32()}");
                    }

                    // If this is a loan application and we expect fields
                    if (expectFields && categoryName == "Loan application")
                    {
                        // Should have extracted fields from custom analyzer
                        if (segment.TryGetProperty("fields", out var fields))
                        {
                            Assert.True(fields.ValueKind == JsonValueKind.Object,
                                "Fields should be an object");
                            var fieldsCount = fields.EnumerateObject().Count();
                            Console.WriteLine($"    ✓ Extracted {fieldsCount} field(s) from custom analyzer");

                            if (fieldsCount > 0)
                            {
                                // Log some field names
                                var fieldNames = fields.EnumerateObject().Take(5).Select(f => f.Name);
                                Console.WriteLine($"    Field examples: {string.Join(", ", fieldNames)}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"    ⚠️  Warning: Expected fields but none found");
                        }
                    }

                    // Check for markdown content in segment
                    if (segment.TryGetProperty("markdown", out var segmentMarkdown))
                    {
                        var markdownText = segmentMarkdown.GetString();
                        var markdownLength = markdownText?.Length ?? 0;
                        if (markdownLength > 0)
                        {
                            Console.WriteLine($"    Markdown length: {markdownLength} characters");
                        }
                    }
                }

                Console.WriteLine($"✅ All segments validated successfully");
            }
            else
            {
                // Single document classification (no segmentation)
                Assert.True(content.TryGetProperty("category", out var category),
                    "Content should have a category");
                var categoryName = category.GetString();
                Console.WriteLine($"✓ Document category: {categoryName}");

                // Check for markdown
                if (content.TryGetProperty("markdown", out var markdown))
                {
                    var markdownText = markdown.GetString();
                    var markdownLength = markdownText?.Length ?? 0;
                    Console.WriteLine($"✓ Markdown length: {markdownLength} characters");
                }
            }

            Console.WriteLine($"✅ {scenarioName} validation completed");
        }
    }
}
