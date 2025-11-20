using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FieldExtraction
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IFieldExtractionService, FieldExtractionService>();
                }
            );

            // Verify client is available
            var client = host.Services.GetService<AzureContentUnderstandingClient>();
            if (client == null)
            {
                Console.WriteLine("❌ Failed to resolve AzureContentUnderstandingClient from DI container.");
                Console.WriteLine("   Please ensure AddContentUnderstandingClient() is called in ConfigureServices.");
                return;
            }

            // Print message about ModelDeploymentSetup
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Field Extraction Sample");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("⚠️  IMPORTANT: Before using prebuilt analyzers, you must configure model deployments.");
            Console.WriteLine();
            Console.WriteLine("   If you haven't already, please run the ModelDeploymentSetup sample first:");
            Console.WriteLine("   1. cd ../ModelDeploymentSetup");
            Console.WriteLine("   2. dotnet run");
            Console.WriteLine();
            Console.WriteLine("   This is a one-time setup that maps your deployed models to prebuilt analyzers.");
            Console.WriteLine("   See the main README.md for more details.");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.Write("Have you already configured model deployments? (y/n): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (answer != "y" && answer != "yes")
            {
                Console.WriteLine();
                Console.WriteLine("Please run the ModelDeploymentSetup sample first and then try again.");
                return;
            }
            Console.WriteLine();

            var service = host.Services.GetRequiredService<IFieldExtractionService>();

            // Part 1: Using Prebuilt Analyzers
            Console.WriteLine("=== Invoice Field Extraction with Prebuilt Analyzer ===");
            try
            {
                await service.AnalyzeWithPrebuiltAnalyzer("prebuilt-invoice", "invoice.pdf", "prebuilt_invoice_analysis_result");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
            Console.WriteLine();

            Console.WriteLine("=== Receipt Field Extraction with Prebuilt Analyzer ===");
            try
            {
                await service.AnalyzeWithPrebuiltAnalyzer("prebuilt-receipt", "receipt.png", "prebuilt_receipt_analysis_result");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
            Console.WriteLine();

            // Part 2: Creating Custom Analyzers

            // Define custom analyzers as dictionaries (matching Python notebook structure)
            var customAnalyzers = new Dictionary<string, (Dictionary<string, object>, string)>
            {
                ["invoice"] = (new Dictionary<string, object>
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
                }, "invoice.pdf"),
                ["call_recording"] = (new Dictionary<string, object>
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
                                    ["enum"] = new[] { "Agriculture", "Business", "Finance", "Health", "Insurance", "Mining", "Pharmaceutical", "Retail", "Technology", "Transportation" }
                                }
                            }
                        }
                    }
                }, "callCenterRecording.mp3"),
                ["conversation_audio"] = (new Dictionary<string, object>
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
                }, "callCenterRecording.mp3"),
                ["marketing_video"] = (new Dictionary<string, object>
                {
                    ["baseAnalyzerId"] = "prebuilt-video",
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
                }, "FlightSimulator.mp4")
            };

            // Process each custom analyzer
            foreach (var item in customAnalyzers)
            {
                // Analyzer ID cannot contain hyphens - use underscores instead
                string analyzerId = $"field_extraction_sample_{Guid.NewGuid().ToString().Replace("-", "_")}";

                // Display clear title for each sample
                string sampleTitle = item.Key switch
                {
                    "invoice" => "Custom Invoice Field Extraction",
                    "call_recording" => "Call Recording Analytics",
                    "conversation_audio" => "Conversational Audio Analytics",
                    "marketing_video" => "Marketing Video Analytics",
                    _ => $"Custom Analyzer: {item.Key}"
                };

                Console.WriteLine($"=== {sampleTitle} ===");
                var (analyzerDefinition, fileName) = item.Value;
                try
                {
                    await service.CreateAndUseAnalyzer(
                        analyzerId,
                        analyzerDefinition,
                        fileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("=== Field Extraction Sample Complete! ===");
        }
    }
}
