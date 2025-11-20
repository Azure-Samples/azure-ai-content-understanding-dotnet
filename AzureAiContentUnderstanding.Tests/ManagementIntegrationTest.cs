using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for analyzer management functionality using IManagementService.
    /// Validates analyzer creation, retrieval of details, listing, and deletion.
    /// </summary>
    public class ManagementIntegrationTest
    {
        private readonly IManagementService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for analyzer management.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion" are missing.
        /// </exception>
        public ManagementIntegrationTest()
        {
            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IManagementService, ManagementService>();
                }
            );

            service = host.Services.GetService<IManagementService>()!;
        }

        /// <summary>
        /// Runs the analyzer management workflow:
        /// 1. Creates an analyzer.
        /// 2. Retrieves and validates analyzer details.
        /// 3. Lists all analyzers.
        /// 4. Deletes the created analyzer.
        /// Ensures no exceptions occur and validates expected structure and content of responses.
        /// </summary>
        [Fact(DisplayName = "Management Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            string? analyzerId = null;

            try
            {
                analyzerId = $"test_analyzer_management_sample_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

                Console.WriteLine($"Starting Management Integration Test");
                Console.WriteLine($"Analyzer ID: {analyzerId}");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");

                // Step 1: Create a simple analyzer
                Console.WriteLine("\n📝 Step 1: Creating analyzer...");
                var analyzerDefinition = CreateCallRecordingAnalyzerDefinition();

                var createResult = await service!.CreateAnalyzerAsync(analyzerId, analyzerDefinition);
                Assert.NotNull(createResult);

                // Validate creation result structure
                Assert.True(createResult.RootElement.TryGetProperty("analyzerId", out var createdId));
                var createdAnalyzerId = createdId.GetString();
                Assert.Equal(analyzerId, createdAnalyzerId);

                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully");

                // Step 2: Get analyzer details and validate structure/content
                Console.WriteLine("\n🔍 Step 2: Retrieving analyzer details...");
                string detailsJson = await service.GetAnalyzerDetailsAsync(analyzerId);

                Assert.False(string.IsNullOrWhiteSpace(detailsJson), "Details JSON should not be empty");

                // Parse the JSON string to validate structure
                var details = JsonDocument.Parse(detailsJson);
                Assert.NotNull(details);

                var detailsRoot = details.RootElement;

                // Validate analyzer ID
                Assert.True(detailsRoot.TryGetProperty("analyzerId", out var detailAnalyzerId));
                Assert.Equal(analyzerId, detailAnalyzerId.GetString());

                // Validate warnings (should be empty or absent)
                if (detailsRoot.TryGetProperty("warnings", out var warnings))
                {
                    if (warnings.ValueKind == JsonValueKind.Array)
                    {
                        var warningsArray = warnings.EnumerateArray().ToList();
                        if (warningsArray.Any())
                        {
                            Console.WriteLine($"⚠️  Found {warningsArray.Count} warning(s)");
                            foreach (var warning in warningsArray)
                            {
                                if (warning.TryGetProperty("code", out var code))
                                {
                                    Console.WriteLine($"  - {code.GetString()}");
                                }
                            }
                        }
                        // Don't fail on warnings, just log them
                    }
                }

                // Validate status (should be 'ready' or 'succeeded')
                Assert.True(detailsRoot.TryGetProperty("status", out var status));
                var statusValue = status.GetString();
                Assert.True(
                    statusValue == "ready" || statusValue == "succeeded",
                    $"Expected status 'ready' or 'succeeded', but got '{statusValue}'"
                );
                Console.WriteLine($"✓ Status: {statusValue}");

                // Validate fieldSchema exists and has fields
                Assert.True(detailsRoot.TryGetProperty("fieldSchema", out var fieldSchema));
                Assert.True(fieldSchema.TryGetProperty("fields", out var fields));
                Assert.True(fields.ValueKind == JsonValueKind.Object);

                var fieldsCount = fields.EnumerateObject().Count();
                Assert.True(fieldsCount > 0, "Field schema should contain at least one field");
                Console.WriteLine($"✓ Field schema contains {fieldsCount} field(s)");

                // Validate base analyzer ID
                if (detailsRoot.TryGetProperty("baseAnalyzerId", out var baseAnalyzerId))
                {
                    Console.WriteLine($"✓ Base Analyzer: {baseAnalyzerId.GetString()}");
                }

                // Validate description
                if (detailsRoot.TryGetProperty("description", out var description))
                {
                    Console.WriteLine($"✓ Description: {description.GetString()}");
                }

                Console.WriteLine($"✅ Analyzer details retrieved and validated successfully");

                // Step 3: List all analyzers
                Console.WriteLine("\n📋 Step 3: Listing all analyzers...");
                var analyzers = await service.ListAnalyzersAsync();

                Assert.NotNull(analyzers);
                Assert.NotEmpty(analyzers);

                var analyzerIds = analyzers.Select(a =>
                {
                    if (a.TryGetProperty("analyzerId", out var id))
                    {
                        return id.GetString();
                    }
                    return null;
                }).Where(id => id != null).ToList();

                Console.WriteLine($"✓ Found {analyzers.Length} analyzer(s)");

                // Verify our created analyzer is in the list
                Assert.Contains(analyzerId, analyzerIds);
                Console.WriteLine($"✅ Created analyzer found in the list");

                // Step 4: Delete analyzer
                Console.WriteLine($"\n🗑️  Step 4: Deleting analyzer '{analyzerId}'...");
                await service.DeleteAnalyzerAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer deleted successfully");

                // Step 5: Verify analyzer is deleted
                Console.WriteLine("\n✔️  Step 5: Verifying deletion...");
                var analyzersAfterDelete = await service.ListAnalyzersAsync();

                var analyzerIdsAfterDelete = analyzersAfterDelete?.Select(a =>
                {
                    if (a.TryGetProperty("analyzerId", out var id))
                    {
                        return id.GetString();
                    }
                    return null;
                }).Where(id => id != null).ToList() ?? new List<string>();

                Assert.DoesNotContain(analyzerId, analyzerIdsAfterDelete);
                Console.WriteLine($"✅ Verified analyzer is no longer in the list");

                // Clear analyzerId so cleanup doesn't try to delete again
                analyzerId = null;

                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine("✅ All management operations completed successfully!");
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"\n❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup: Try to delete the analyzer if it still exists
                if (!string.IsNullOrEmpty(analyzerId))
                {
                    try
                    {
                        Console.WriteLine($"\n🧹 Cleanup: Attempting to delete analyzer '{analyzerId}'...");
                        await service!.DeleteAnalyzerAsync(analyzerId);
                        Console.WriteLine("✅ Cleanup successful");
                    }
                    catch (Exception cleanupEx)
                    {
                        Console.WriteLine($"⚠️  Cleanup failed (may already be deleted): {cleanupEx.Message}");
                        // Don't fail the test due to cleanup errors
                    }
                }
            }

            // Final assertion: No exceptions should be thrown during workflow
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Creates an analyzer definition for call recording analytics.
        /// </summary>
        private Dictionary<string, object> CreateCallRecordingAnalyzerDefinition()
        {
            return new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-callCenter",
                ["description"] = "Sample call recording analyzer for management testing",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["locales"] = new List<string> { "en-US" }
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Summary"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "generate",
                            ["description"] = "A one-paragraph summary of the call"
                        },
                        ["CustomerName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Name of the customer"
                        },
                        ["AgentName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Name of the agent"
                        },
                        ["CallDuration"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Duration of the call"
                        },
                        ["Topics"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string"
                            },
                            ["description"] = "Main topics discussed in the call"
                        },
                        ["Sentiment"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["description"] = "Overall sentiment of the call",
                            ["enum"] = new List<string> { "Positive", "Neutral", "Negative" }
                        },
                        ["IssueResolved"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["description"] = "Whether the customer's issue was resolved",
                            ["enum"] = new List<string> { "Yes", "No", "Partial" }
                        }
                    }
                },
                ["tags"] = new Dictionary<string, string>
                {
                    ["purpose"] = "integration-test",
                    ["type"] = "call-center"
                }
            };
        }
    }
}
