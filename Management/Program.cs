using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Management
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IManagementService, ManagementService>();
                })
                .Build();

            var service = host.Services.GetService<IManagementService>()!;

            var analyzerId = $"analyzer-management-sample-{Guid.NewGuid()}";
            var contentAnalyzer = new ContentAnalyzer
            {
                BaseAnalyzerId = "prebuilt-callCenter",
                Description = "Sample call recording analytics",
                Config = new ContentAnalyzerConfig
                {
                    ReturnDetails = true,
                },
                FieldSchema = new ContentFieldSchema(
                        fields: new Dictionary<string, ContentFieldDefinition>
                        {
                            ["Summary"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.String,
                                Method = GenerationMethod.Generate,
                                Description = "A one-paragraph summary"
                            },
                            ["Topics"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.Array,
                                Method = GenerationMethod.Generate,
                                Items = new ContentFieldDefinition
                                {
                                    Type = ContentFieldType.String,
                                },
                                Description = "Top 5 topics mentioned"
                            },
                            ["Companies"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.Array,
                                Method = GenerationMethod.Generate,
                                Items = new ContentFieldDefinition
                                {
                                    Type = ContentFieldType.String,
                                },
                                Description = "List of companies mentioned"
                            },
                            ["People"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.Array,
                                Method = GenerationMethod.Generate,
                                Items = new ContentFieldDefinition
                                {
                                    Type = ContentFieldType.Object,
                                },
                                Description = "List of people mentioned"
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
                                Items = new ContentFieldDefinition
                                {
                                    Type = ContentFieldType.String,
                                },
                                Description = "List of relevant categories",
                            }
                        })
            };
            // call_recording
            contentAnalyzer.Config.Locales.Add("en-US");
            contentAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Name", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Description = "Person's name"
            });
            contentAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Role", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Description = "Person's title/role"
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

            // 1. Create a simple analyzer
            await service!.CreateAnalyzerAsync(analyzerId, analyzer: contentAnalyzer);

            // 2. List all analyzers
            await service.ListAnalyzersAsync();

            // 3. Get analyzer details
            await service.GetAnalyzerDetailsAsync(analyzerId);

            // 4. Update analyzer
            await service.UpdateAnalyzerAsync(analyzerId);

            // 5. Delete analyzer
            await service.DeleteAnalyzerAsync(analyzerId);
        }
    }
}
