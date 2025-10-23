using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ConversationalFieldExtraction.Interfaces;
using ConversationalFieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    public class ConversationalConversationalFieldExtractionIntegrationTest
    {
        private readonly IConversationalFieldExtractionService service;

        public ConversationalConversationalFieldExtractionIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IConversationalFieldExtractionService, ConversationalFieldExtractionService>();
                })
                .Build();

            service = host.Services.GetService<IConversationalFieldExtractionService>()!;
        }

        [Fact(DisplayName = "Conversational Field Extraction Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            try
            {
                ContentAnalyzer contentAnalyzer = new ContentAnalyzer
                {
                    BaseAnalyzerId = "prebuilt-audioAnalyzer",
                    Description = "Sample call recording analytics",
                    Config = new ContentAnalyzerConfig
                    {
                        ReturnDetails = true,
                    },
                    FieldSchema = new ContentFieldSchema(fields: new Dictionary<string, ContentFieldDefinition>
                    {
                        ["Summary"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Generate,
                            Description = "A one-paragraph summary"
                        },
                        ["Topics"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Generate,
                            Description = "Top 5 topics mentioned",
                            Items = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.String,
                            }
                        },
                        ["Companies"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Generate,
                            Description = "List of companies mentioned",
                            Items = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.String,
                            }
                        },
                        ["People"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.Array,
                            Method = GenerationMethod.Generate,
                            Description = "List of people mentioned",
                            Items = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.Object,
                            }
                        },
                        ["Sentiment"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Classify,
                            Description = "Overall sentiment",
                        },
                        ["Categories"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.Array,
                            Method = GenerationMethod.Classify,
                            Description = "List of relevant categories",
                            Items = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.String,
                            }
                        },
                    })
                };

                contentAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Name", new ContentFieldDefinition
                {
                    Type = ContentFieldType.String,
                    Description = "Person's name",
                });
                contentAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Role", new ContentFieldDefinition
                {
                    Type = ContentFieldType.String,
                    Description = "Person's title/role",
                });
                contentAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Positive");
                contentAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Neutral");
                contentAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Negative");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Agriculture");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Business");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Finance");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Health");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Insurance");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Mining");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Pharmaceutical");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Retail");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Technology");
                contentAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Transportation");

                var extractionContentAnalyzer = new Dictionary<string, (ContentAnalyzer, string)>
                {
                    ["call_recording_pretranscribe_batch"] = (contentAnalyzer, "./data/batch_pretranscribed.json"),
                    ["call_recording_pretranscribe_fast"] = (contentAnalyzer, "./data/fast_pretranscribed.json"),
                    ["call_recording_pretranscribe_cu"] = (contentAnalyzer, "./data/cu_pretranscribed.json")
                };
                var analyzerId = $"conversational-field-extraction-sample-{Guid.NewGuid()}";

                foreach (var item in extractionContentAnalyzer)
                {
                    // Extract the template path and sample file path from the dictionary
                    var (analyzer, analyzerTemplatePath) = item.Value;

                    // Extract fields using the created analyzer
                    await ExtractFieldsWithAnalyzerAsync(analyzerId, analyzer, analyzerTemplatePath);

                    // Clean up the analyzer after use
                    await service.DeleteAnalyzerAsync(analyzerId);
                }
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Assert that no exceptions were thrown during the test.
            Assert.Null(serviceException);
        }

        private async Task ExtractFieldsWithAnalyzerAsync(string analyzerId, ContentAnalyzer analyzer, string analyzerSampleFilePath)
        {
            // Implementation for creating an analyzer from a template
            ContentAnalyzer createResult = await service.CreateAnalyzerFromTemplateAsync(analyzerId, analyzer);
            Assert.NotNull(createResult);
            Assert.False(createResult?.Warnings.Any(), "The warnings array should be empty");

            // Implementation for extracting fields using the created analyzer
            var result = await service.ExtractFieldsWithAnalyzerAsync(analyzerId, analyzerSampleFilePath);
            Assert.NotNull(result);
            Assert.False(result?.Warnings.Any(), "The warnings array should be empty");
            Assert.True(result?.Contents.Any());

            var content = result?.Contents[0];
            Assert.False(string.IsNullOrWhiteSpace(content?.Markdown), "The markdown content is empty");
            Assert.True(content?.Fields.Any());
        }
    }
}
