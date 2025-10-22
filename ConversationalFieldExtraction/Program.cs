using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ConversationalFieldExtraction.Interfaces;
using ConversationalFieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConversationalFieldExtraction
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IConversationalFieldExtractionService, ConversationalFieldExtractionService>();
                })
                .Build();

            var service = host.Services.GetService<IConversationalFieldExtractionService>()!;

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

                // Create the analyzer from the template
                await service.CreateAnalyzerFromTemplateAsync(analyzerId, analyzer);

                // Extract fields using the created analyzer
                await service.ExtractFieldsWithAnalyzerAsync(analyzerId, analyzerTemplatePath);

                // Clean up the analyzer after use
                await service.DeleteAnalyzerAsync(analyzerId);
            }
        }
    }
}
