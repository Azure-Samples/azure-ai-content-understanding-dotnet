using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace FieldExtraction
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
                    services.AddSingleton<IFieldExtractionService, FieldExtractionService>();
                })
                .Build();

            var service = host.Services.GetService<IFieldExtractionService>()!;
            var extractionContentAnalyzer = new Dictionary<string, (ContentAnalyzer, string)>
            {
                ["invoice"] = (new ContentAnalyzer
                {
                    BaseAnalyzerId = "prebuilt-documentAnalyzer",
                    Description = "Sample invoice analyzer",
                    FieldSchema = new ContentFieldSchema(
                        fields: new Dictionary<string, ContentFieldDefinition>
                        {
                            ["VendorName"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.String,
                                Method = GenerationMethod.Extract,
                                Description = "Vendor issuing the invoice"
                            },
                            ["Items"] = new ContentFieldDefinition
                            {
                                Type = ContentFieldType.Array,
                                Method = GenerationMethod.Extract,
                                Items = new ContentFieldDefinition
                                {
                                    Type = ContentFieldType.Object,
                                }
                            }
                        })
                }, "./data/invoice.pdf"),
                ["call_recording"] = (new ContentAnalyzer
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
                }, "./data/callCenterRecording.mp3"),
                ["conversation_audio"] = (new ContentAnalyzer
                {
                    BaseAnalyzerId = "prebuilt-audioAnalyzer",
                    Description = "Sample conversational audio analytics",
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
                        ["Sentiment"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Classify,
                            Description = "Overall sentiment",
                        },
                    })
                }, "./data/callCenterRecording.mp3"),
                ["marketing_video"] = (new ContentAnalyzer
                {
                    BaseAnalyzerId = "prebuilt-videoAnalyzer",
                    Description = "Sample marketing video analytics",
                    Config = new ContentAnalyzerConfig
                    {
                        ReturnDetails = true,
                        SegmentationMode = SegmentationMode.NoSegmentation
                    },
                    FieldSchema = new ContentFieldSchema(fields: new Dictionary<string, ContentFieldDefinition>
                    {
                        ["Description"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Description = "Detailed summary of the video segment, focusing on product characteristics, lighting, and color palette."
                        },
                        ["Sentiment"] = new ContentFieldDefinition
                        {
                            Type = ContentFieldType.String,
                            Method = GenerationMethod.Classify,
                        },
                    })
                }, "./data/FlightSimulator.mp4")
            };

            // invoice
            var (invoiceAnalyzer, _) = extractionContentAnalyzer["invoice"];
            invoiceAnalyzer.FieldSchema.Fields["Items"].Items.Properties.Add("Description", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Method = GenerationMethod.Extract,
                Description = "Description of the item"
            });
            invoiceAnalyzer.FieldSchema.Fields["Items"].Items.Properties.Add("Amount", new ContentFieldDefinition
            {
                Type = ContentFieldType.Number,
                Method = GenerationMethod.Extract,
                Description = "Amount of the item"
            });

            // call_recording
            var (callRecordingAnalyzer, _) = extractionContentAnalyzer["call_recording"];
            callRecordingAnalyzer.Config.Locales.Add("en-US");
            callRecordingAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Name", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Description = "Person's name"
            });
            callRecordingAnalyzer.FieldSchema.Fields["People"].Items.Properties.Add("Role", new ContentFieldDefinition
            {
                Type = ContentFieldType.String,
                Description = "Person's title/role"
            });
            callRecordingAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Positive");
            callRecordingAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Neutral");
            callRecordingAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Negative");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Agriculture");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Business");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Finance");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Health");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Insurance");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Mining");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Pharmaceutical");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Retail");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Technology");
            callRecordingAnalyzer.FieldSchema.Fields["Categories"].Items.Enum.Add("Transportation");

            // conversation_audio
            var (conversationAudioAnalyzer, _) = extractionContentAnalyzer["conversation_audio"];
            conversationAudioAnalyzer.Config.Locales.Add("en-US");
            conversationAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Positive");
            conversationAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Neutral");
            conversationAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Negative");

            // marketing_video
            var (marketAudioAnalyzer, _) = extractionContentAnalyzer["marketing_video"];
            marketAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Positive");
            marketAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Neutral");
            marketAudioAnalyzer.FieldSchema.Fields["Sentiment"].Enum.Add("Negative");

            string analyzerId = $"field-extraction-sample-{Guid.NewGuid()}";

            foreach(var item in extractionContentAnalyzer)
            {
                Console.WriteLine($"\n\nProcessing {item.Key}...\n");
                var (contentAnalyzer, fileName) = item.Value;
                await service.CreateAndUseAnalyzer(
                    analyzerId,
                    contentAnalyzer,
                    fileName);
            }
        }
    }
}
