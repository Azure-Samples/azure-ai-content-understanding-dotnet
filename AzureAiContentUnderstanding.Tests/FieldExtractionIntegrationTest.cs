using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for field extraction scenarios using IFieldExtractionService.
    /// Validates that analyzers built from various templates can process different sample files correctly,
    /// producing valid structured results and handling errors gracefully.
    /// </summary>
    public class FieldExtractionIntegrationTest
    {
        private readonly IFieldExtractionService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for field extraction.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion" are missing.
        /// </exception>
        public FieldExtractionIntegrationTest()
        {
            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IFieldExtractionService, FieldExtractionService>();
                }
            );

            service = host.Services.GetService<IFieldExtractionService>()!;
        }

        /// <summary>
        /// Runs integration tests for field extraction using multiple predefined templates and sample files.
        /// For each template/sample pair, verifies that the analyzer produces structured results with expected fields.
        /// </summary>
        /// <remarks>
        /// This test iterates through several field extraction scenarios:
        /// <list type="bullet">
        /// <item><description>Invoice extraction from PDF</description></item>
        /// <item><description>Call recording analytics from MP3</description></item>
        /// <item><description>Conversational audio analytics from MP3</description></item>
        /// <item><description>Marketing video analysis from MP4</description></item>
        /// </list>
        /// Each scenario ensures the service does not throw exceptions, produces a valid JSON result,
        /// and includes expected fields: "result", "contents", "markdown", and "fields".
        /// </remarks>
        [Fact(DisplayName = "Field Extraction Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                // Define test scenarios with analyzer definitions and sample files
                // Match the structure from field_extraction_service2.cs (Program)
                var extractionScenarios = new Dictionary<string, (Dictionary<string, object> analyzerDefinition, string fileName)>
                {
                    ["invoice"] = (CreateInvoiceAnalyzerDefinition(), "invoice.pdf"),
                    ["call_recording"] = (CreateCallRecordingAnalyzerDefinition(), "callCenterRecording.mp3"),
                    ["conversation_audio"] = (CreateConversationAudioAnalyzerDefinition(), "callCenterRecording.mp3"),
                    ["marketing_video"] = (CreateMarketingVideoAnalyzerDefinition(), "FlightSimulator.mp4")
                };

                foreach (var scenario in extractionScenarios)
                {
                    var scenarioName = scenario.Key;
                    var (analyzerDefinition, fileName) = scenario.Value;

                    // Display clear title for each sample (matching Program output)
                    string sampleTitle = scenarioName switch
                    {
                        "invoice" => "Custom Invoice Field Extraction",
                        "call_recording" => "Call Recording Analytics",
                        "conversation_audio" => "Conversational Audio Analytics",
                        "marketing_video" => "Marketing Video Analytics",
                        _ => $"Custom Analyzer: {scenarioName}"
                    };

                    Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                    Console.WriteLine($"=== {sampleTitle} ===");
                    Console.WriteLine($"Scenario: {scenarioName}");
                    Console.WriteLine($"File: {fileName}");
                    Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");

                    // Generate unique analyzer ID for this test run
                    // Match Program's ID format (underscores instead of hyphens)
                    string analyzerId = $"test_field_extraction_{scenarioName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

                    // Execute the analyzer creation and analysis
                    var result = await service.CreateAndUseAnalyzer(
                        analyzerId,
                        analyzerDefinition,
                        fileName);

                    // Validate the result
                    ValidateFieldExtractionResult(result, scenarioName);

                    Console.WriteLine($"✅ Scenario '{scenarioName}' completed successfully");
                }

                Console.WriteLine($"\n{'='.ToString().PadRight(80, '=')}");
                Console.WriteLine("=== Field Extraction Test Complete! ===");
                Console.WriteLine($"{'='.ToString().PadRight(80, '=')}");
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"\n❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Assert that no exceptions were thrown during the test
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Creates the analyzer definition for invoice extraction.
        /// Matches the structure from field_extraction_service2.cs (Program).
        /// </summary>
        private Dictionary<string, object> CreateInvoiceAnalyzerDefinition()
        {
            return new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-document",
                ["description"] = "Sample invoice analyzer that extracts vendor information, line items, and totals from commercial invoices",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["enableOcr"] = true,
                    ["enableLayout"] = true,
                    ["estimateFieldSourceAndConfidence"] = true
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["name"] = "InvoiceFields",
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["VendorName"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "extract",
                            ["description"] = "Name of the vendor or supplier, typically found in the header section"
                        },
                        ["Items"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "extract",
                            ["description"] = "List of items on the invoice",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["Description"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["method"] = "extract",
                                        ["description"] = "Description of the item"
                                    },
                                    ["Amount"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "number",
                                        ["method"] = "extract",
                                        ["description"] = "Amount of the item"
                                    }
                                }
                            }
                        }
                    }
                },
                ["models"] = new Dictionary<string, object>
                {
                    ["completion"] = "gpt-4.1"
                }
            };
        }

        /// <summary>
        /// Creates the analyzer definition for call recording analytics.
        /// Matches the structure from field_extraction_service2.cs (Program).
        /// </summary>
        private Dictionary<string, object> CreateCallRecordingAnalyzerDefinition()
        {
            return new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-callCenter",
                ["description"] = "Sample call recording analytics",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["locales"] = new[] { "en-US" }
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["name"] = "CallRecordingFields",
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Summary"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "generate",
                            ["description"] = "A one-paragraph summary"
                        },
                        ["Topics"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "Top 5 topics mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string"
                            }
                        },
                        ["Companies"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "List of companies mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string"
                            }
                        },
                        ["People"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "List of people mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["Name"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Person's name"
                                    },
                                    ["Role"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Person's title/role"
                                    }
                                }
                            }
                        },
                        ["Sentiment"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["description"] = "Overall sentiment",
                            ["enum"] = new[] { "Positive", "Neutral", "Negative" }
                        },
                        ["Categories"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "classify",
                            ["description"] = "List of relevant categories",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[]
                                {
                                    "Agriculture", "Business", "Finance", "Health",
                                    "Insurance", "Mining", "Pharmaceutical", "Retail",
                                    "Technology", "Transportation"
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates the analyzer definition for conversation audio analytics.
        /// Matches the structure from field_extraction_service2.cs (Program).
        /// </summary>
        private Dictionary<string, object> CreateConversationAudioAnalyzerDefinition()
        {
            return new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-audio",
                ["description"] = "Sample conversational audio analytics",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["locales"] = new[] { "en-US" }
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["name"] = "ConversationFields",
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Summary"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "generate",
                            ["description"] = "A one-paragraph summary"
                        },
                        ["Sentiment"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["description"] = "Overall sentiment",
                            ["enum"] = new[] { "Positive", "Neutral", "Negative" }
                        }
                    }
                },
                ["models"] = new Dictionary<string, object>
                {
                    ["completion"] = "gpt-4.1"
                }
            };
        }

        /// <summary>
        /// Creates the analyzer definition for marketing video analytics.
        /// Matches the structure from field_extraction_service2.cs (Program).
        /// </summary>
        private Dictionary<string, object> CreateMarketingVideoAnalyzerDefinition()
        {
            return new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-video",  // Note: Program uses "prebuilt-video" not "prebuilt-videoSearch"
                ["description"] = "Sample marketing video analytics",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["segmentationMode"] = "noSegmentation"
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["name"] = "VideoFields",
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Description"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Detailed summary of the video segment, focusing on product characteristics, lighting, and color palette."
                        },
                        ["Sentiment"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["enum"] = new[] { "Positive", "Neutral", "Negative" }
                        }
                    }
                },
                ["models"] = new Dictionary<string, object>
                {
                    ["completion"] = "gpt-4.1"
                }
            };
        }

        /// <summary>
        /// Validates the field extraction result structure and content.
        /// </summary>
        /// <param name="result">The JsonDocument result from field extraction.</param>
        /// <param name="scenarioName">The name of the scenario being tested.</param>
        private void ValidateFieldExtractionResult(JsonDocument result, string scenarioName)
        {
            Assert.NotNull(result);
            Console.WriteLine($"\n📋 Validating result for scenario: {scenarioName}");

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

            // Verify content kind
            if (content.TryGetProperty("kind", out var kind))
            {
                var kindValue = kind.GetString();
                Console.WriteLine($"✓ Content kind: {kindValue}");
            }

            // Verify markdown exists (may be empty for some content types)
            Assert.True(content.TryGetProperty("markdown", out var markdown),
                "Content should contain 'markdown' property");

            var markdownText = markdown.GetString();
            Console.WriteLine($"✓ Markdown content length: {markdownText?.Length ?? 0} characters");

            // For document content, markdown should not be empty
            if (content.TryGetProperty("kind", out var contentKind) && contentKind.GetString() == "document")
            {
                Assert.False(string.IsNullOrWhiteSpace(markdownText),
                    "Markdown content should not be empty for document type");
            }

            // Verify fields exist and are not empty
            Assert.True(content.TryGetProperty("fields", out var fields),
                "Content should contain 'fields' property");
            Assert.True(fields.ValueKind == JsonValueKind.Object,
                "Fields should be an object");

            var fieldsCount = fields.EnumerateObject().Count();
            Assert.True(fieldsCount > 0, "Fields should not be empty");
            Console.WriteLine($"✓ Extracted {fieldsCount} field(s)");

            // Log extracted field names
            var fieldNames = fields.EnumerateObject().Select(f => f.Name).ToList();
            Console.WriteLine($"✓ Field names: {string.Join(", ", fieldNames)}");

            // Scenario-specific validations
            ValidateScenarioSpecificFields(fields, scenarioName);
        }

        /// <summary>
        /// Performs scenario-specific field validations.
        /// </summary>
        private void ValidateScenarioSpecificFields(JsonElement fields, string scenarioName)
        {
            Console.WriteLine($"\n🔍 Scenario-specific validation for: {scenarioName}");

            switch (scenarioName)
            {
                case "invoice":
                    // Validate invoice-specific fields
                    Assert.True(fields.TryGetProperty("VendorName", out var vendorName),
                        "Invoice should have VendorName field");
                    Console.WriteLine($"  ✓ VendorName field found");

                    Assert.True(fields.TryGetProperty("Items", out var items),
                        "Invoice should have Items field");
                    if (items.TryGetProperty("type", out var itemsType))
                    {
                        Assert.Equal("array", itemsType.GetString());
                        Console.WriteLine($"  ✓ Items field is of type array");
                    }

                    // Check if Items has valueArray
                    if (items.TryGetProperty("valueArray", out var valueArray) &&
                        valueArray.ValueKind == JsonValueKind.Array)
                    {
                        var itemsCount = valueArray.GetArrayLength();
                        Console.WriteLine($"  ✓ Items contains {itemsCount} item(s)");
                    }

                    Console.WriteLine("✅ Invoice-specific fields validated");
                    break;

                case "call_recording":
                    // Validate call recording-specific fields
                    Assert.True(fields.TryGetProperty("Summary", out _),
                        "Call recording should have Summary field");
                    Console.WriteLine($"  ✓ Summary field found");

                    Assert.True(fields.TryGetProperty("Sentiment", out _),
                        "Call recording should have Sentiment field");
                    Console.WriteLine($"  ✓ Sentiment field found");

                    // Optional: Check for other fields
                    if (fields.TryGetProperty("Topics", out _))
                    {
                        Console.WriteLine($"  ✓ Topics field found");
                    }
                    if (fields.TryGetProperty("People", out _))
                    {
                        Console.WriteLine($"  ✓ People field found");
                    }

                    Console.WriteLine("✅ Call recording-specific fields validated");
                    break;

                case "conversation_audio":
                    // Validate conversation audio-specific fields
                    Assert.True(fields.TryGetProperty("Summary", out _),
                        "Conversation audio should have Summary field");
                    Console.WriteLine($"  ✓ Summary field found");

                    Assert.True(fields.TryGetProperty("Sentiment", out _),
                        "Conversation audio should have Sentiment field");
                    Console.WriteLine($"  ✓ Sentiment field found");

                    Console.WriteLine("✅ Conversation audio-specific fields validated");
                    break;

                case "marketing_video":
                    // Validate marketing video-specific fields
                    Assert.True(fields.TryGetProperty("Description", out _),
                        "Marketing video should have Description field");
                    Console.WriteLine($"  ✓ Description field found");

                    Assert.True(fields.TryGetProperty("Sentiment", out _),
                        "Marketing video should have Sentiment field");
                    Console.WriteLine($"  ✓ Sentiment field found");

                    Console.WriteLine("✅ Marketing video-specific fields validated");
                    break;

                default:
                    Console.WriteLine($"⚠️  No specific validation for scenario: {scenarioName}");
                    break;
            }
        }
    }
}
