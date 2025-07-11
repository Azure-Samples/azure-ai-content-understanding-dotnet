using ContentExtraction.Interfaces;
using ContentUnderstanding.Common;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ContentExtraction.Services
{
    public class ContentExtractionService : IContentExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private const string CacheDir = ".cache";
        private readonly string OutputPath = "./outputs/content_extraction/";

        public ContentExtractionService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(CacheDir))
            {
                Directory.CreateDirectory(CacheDir);
            }

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Document Content
        /// <remarks>Content Understanding API is designed to extract all textual content from a specified document file. 
        /// In addition to text extraction, it conducts a comprehensive layout analysis to identify and categorize tables and figures within the document. 
        /// The output is then presented in a structured markdown format, ensuring clarity and ease of interpretation.</remarks>
        /// </summary>
        /// <param name="filePath">The path to the document file to be analyzed. Must be a valid file path.</param>
        /// <returns>A task representing the asynchronous operation. The task completes when the document analysis is finished.</returns>
        public async Task AnalyzeDocumentAsync(string filePath)
        {
            Console.WriteLine("Document Content Extraction Sample is running...");

            const string analyzerId = "prebuilt-documentAnalyzer";
            var apiNameDescription = "extract document content";
            var response = await _client.BeginAnalyzeAsync(analyzerId, filePath, apiNameDescription);
            var resultJson = await _client.PollResultAsync(response);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            // write to json file.
            var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeDocumentAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);

            Console.WriteLine("\n===== Document Extraction has been saved to the following output file path =====");
            Console.WriteLine($"\n{output}");

            var resultData = resultJson.RootElement.GetProperty("result");
            var contents = resultData.GetProperty("contents");
            var firstContent = contents.EnumerateArray().First();

            Console.WriteLine("\n===== The markdown output contains layout information, which is very useful for Retrieval-Augmented Generation (RAG) scenarios. You can paste the markdown into a viewer such as Visual Studio Code and preview the layout structure. =====");
            Console.WriteLine(firstContent.GetProperty("markdown").GetString());
            Console.WriteLine("\n===== This statement allows you to get structural information of the tables in the documents. =====");
            Console.WriteLine("\nFor examle, the following code will print the first table in the document.");
            var tables = firstContent.GetProperty("tables");
            Console.WriteLine(JsonSerializer.Serialize(tables[0], new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>
        /// Audio Content
        /// <remarks>Our API output facilitates detailed analysis of spoken language, allowing developers to utilize the data for various applications, such as voice recognition, customer service analytics, and conversational AI. The structure of the output makes it easy to extract and analyze different components of the conversation for further processing or insights.
        /// 1. Speaker Identification: Each phrase is attributed to a specific speaker(in this case, "Speaker 2"). This allows for clarity in conversations with multiple participants.
        /// 2. Timing Information: Each transcription includes precise timing data:
        ///     - startTimeMs: The time (in milliseconds) when the phrase begins.
        ///     - endTimeMs: The time (in milliseconds) when the phrase ends.
        /// 3. This information is crucial for applications like video subtitles, allowing synchronization between the audio and the text.
        /// 4. Text Content: The actual spoken text is provided, which in this instance is "Thank you for calling Woodgrove Travel." This is the main content of the transcription.
        /// 5. Confidence Score: Each transcription phrase includes a confidence score (0.933 in this case), indicating the likelihood that the transcription is accurate.A higher score suggests greater reliability.
        /// 6. Word-Level Breakdown: The transcription is further broken down into individual words, each with its own timing information. This allows for detailed analysis of speech patterns and can be useful for applications such as language processing or speech recognition improvement.
        /// 7. Locale Specification: The locale is specified as "en-US," indicating that the transcription is in American English. This is important for ensuring that the transcription algorithms account for regional dialects and pronunciations.</remarks>
        /// </summary>
        /// <remarks>This method uses a prebuilt audio analyzer to extract content and metadata from the specified
        /// audio file. The analysis results are serialized and displayed in the console.</remarks>
        /// <param name="filePath">The path to the audio file to be analyzed. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the analysis is finished.</returns>
        public async Task AnalyzeAudioAsync(string filePath)
        {
            Console.WriteLine("\nAudio Content Extraction Sample is running...");

            const string analyzerId = "prebuilt-audioAnalyzer";
            var apiNameDescription = "extract audio content";
            var response = await _client.BeginAnalyzeAsync(analyzerId, filePath, apiNameDescription);
            var resultJson = await _client.PollResultAsync(response);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            // write to json file.
            var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeAudioAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);

            Console.WriteLine("\n===== Audio Extraction has been saved to the following output file path =====");
            Console.WriteLine($"\n{output}");
        }

        /// <summary>
        /// Video Content
        /// <remarks>Video output provides detailed information about audiovisual content, specifically video shots. Here are the key features it offers:
        /// 1. Shot Information: Each shot is defined by a start and end time, along with a unique identifier.For example, Shot 0:0.0 to 0:2.800 includes a transcript and key frames.
        /// 2. Transcript: The API includes a transcript of the audio, formatted in WEBVTT, which allows for easy synchronization with the video.It captures spoken content and specifies the timing of the dialogue.
        /// 3. Key Frames: It provides a series of key frames (images) that represent important moments in the video shot, allowing users to visualize the content at specific timestamps.
        /// 4. Description: Each shot is accompanied by a description, providing context about the visuals presented. This helps in understanding the scene or subject matter without watching the video.
        /// 5. Audio Visual Metadata: Details about the video such as dimensions (width and height), type(audiovisual), and the presence of key frame timestamps are included.
        /// 6. Transcript Phrases: The output includes specific phrases from the transcript, along with timing and speaker information, enhancing the usability for applications like closed captioning or search functionalities.</remarks>
        /// </summary>
        /// <remarks>This method uses a prebuilt video analyzer to process the video file and extract relevant
        /// content. The analysis results include metadata and key frames, which are saved for further use. Ensure the
        /// provided <paramref name="filePath"/> points to a valid video file.</remarks>
        /// <param name="filePath">The path to the video file to be analyzed. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task AnalyzeVideoAsync(string filePath)
        {
            Console.WriteLine("\nVideo Content Extraction Sample is running");

            const string analyzerId = "prebuilt-videoAnalyzer";
            var apiNameDescription = "extract video content";
            var response = await _client.BeginAnalyzeAsync(analyzerId, filePath, apiNameDescription);
            var resultJson = await _client.PollResultAsync(response);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            // write to json file.
            var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeVideoAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);

            Console.WriteLine("\n===== Video Extraction has been saved to the following output file path =====");
            Console.WriteLine($"\n{output}");

            // keep key frames
            await SaveKeyFrames(resultJson, response);
        }

        /// <summary>
        /// Video Content With Face
        /// <remarks>This is a gated feature, please go through process [Azure AI Resource Face Gating](https://learn.microsoft.com/en-us/legal/cognitive-services/computer-vision/limited-access-identity?context=%2Fazure%2Fai-services%2Fcomputer-vision%2Fcontext%2Fcontext#registration-process) Select `[Video Indexer] Facial Identification (1:N or 1:1 matching) to search for a face in a media or entertainment video archive to find a face within a video and generate metadata for media or entertainment use cases only` in the registration form.</remarks>
        /// </summary>
        /// <param name="filePath">The path to the video file to be analyzed. Must be a valid file path.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the analysis is finished and the
        /// extracted content, including key frames and face data, has been processed.</returns>
        public async Task AnalyzeVideoWithFaceAsync(string filePath)
        {
            Console.WriteLine("\nVideo Content(with face) Extraction Sample is running...");
            Console.WriteLine("\nThis is a gated feature: FaceID will not be extracted unless your account has been approved for Face API access.");
            Console.WriteLine("\nFor details on the gating logic, refer to the comments in the AnalyzeVideoWithFaceAsync() method.");

            const string analyzerId = "prebuilt-videoAnalyzer";
            var apiNameDescription = "extract video content with face";
            var response = await _client.BeginAnalyzeAsync(analyzerId, filePath, apiNameDescription);
            var resultJson = await _client.PollResultAsync(response);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            // write to json file.
            var output = $"{Path.Combine(OutputPath, $"{nameof(AnalyzeVideoWithFaceAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);

            Console.WriteLine("\n===== Video with face Extraction has been saved to the following output file path =====");
            Console.WriteLine($"\n{output}");

            // Save key frames and face pictures
            await SaveFacesAndKeyFrames(resultJson, response);
        }

        /// <summary>
        /// Get and Save Key Frames.
        /// </summary>
        /// <remarks>This method processes the "contents" array in the JSON document to identify keyframe IDs
        /// embedded in markdown content. It then attempts to download and save each keyframe image to the local cache
        /// directory. If an error occurs while saving a keyframe, the exception is logged, but the method continues
        /// processing other keyframes.</remarks>
        /// <param name="resultJson">A <see cref="JsonDocument"/> containing the result data from which keyframe IDs will be extracted. The JSON
        /// document must include a "result" property with a "contents" array containing markdown elements.</param>
        /// <param name="response">The <see cref="HttpResponseMessage"/> associated with the analyze operation, used to retrieve keyframe images.</param>
        /// <returns></returns>
        public async Task SaveKeyFrames(JsonDocument resultJson, HttpResponseMessage response)
        {
            // Save keyframes (optional)
            var keyframeIds = new HashSet<string>();
            var resultData = resultJson.RootElement.GetProperty("result");
            var contents = resultData.GetProperty("contents");

            // Iterate over contents to find keyframes if available
            foreach (var content in contents.EnumerateArray())
            {
                // Extract keyframe IDs from "markdown" if it exists and is a string
                if (content.TryGetProperty("markdown", out var markdownElement))
                {
                    string? markdown = markdownElement.GetString()!;

                    var matches = Regex.Matches(markdown, @"(keyFrame\.\d+)\.jpg");
                    foreach (Match match in matches)
                    {
                        keyframeIds.Add(match.Groups[1].Value);
                    }
                }
            }

            // Output the results
            Console.WriteLine($"\nUnique Keyframe IDs: {string.Join(", ", keyframeIds)}");

            // Save all keyframes images
            foreach (var keyframeId in keyframeIds)
            {
                try
                {
                    var imageBytes = await _client.GetImageFromAnalyzeOperationAsync(response, keyframeId);
                    await File.WriteAllBytesAsync($"{CacheDir}/{keyframeId}.jpg", imageBytes);
                    Console.WriteLine($"Saved keyframe: {keyframeId}.jpg");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save keyframe ({keyframeId}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get and Save Key Frames and Face Thumbnails.
        /// </summary>
        /// <remarks>This method processes the JSON response from an analysis operation to identify face and
        /// keyframe IDs.  It retrieves the associated images using the provided HTTP response and saves them to the local
        /// cache directory. If an image cannot be retrieved or saved, an error message is logged to the console.</remarks>
        /// <param name="resultJson">The JSON document containing the analysis results. Must include "result" and "contents" properties.</param>
        /// <param name="response">The HTTP response message used to retrieve images for the identified face and keyframe IDs.</param>
        /// <returns></returns>
        public async Task SaveFacesAndKeyFrames(JsonDocument resultJson, HttpResponseMessage response)
        {
            // Initialize sets for unique face IDs and keyframe IDs
            var faceIds = new HashSet<string>();
            var keyframeIds = new HashSet<string>();
            // Extract unique face IDs safely
            var resultData = resultJson.RootElement.GetProperty("result");
            var contents = resultData.GetProperty("contents");

            // Iterate over contents to find faces and keyframes if available
            foreach (var content in contents.EnumerateArray())
            {
                // Safely retrieve face IDs if "faces" exists and is a list
                if (content.TryGetProperty("faces", out var faces))
                {
                    foreach (var face in faces.EnumerateArray())
                    {
                        var faceId = face.GetProperty("faceId").GetString();
                        faceIds.Add($"face.{faceId}");
                    }
                }

                // Extract keyframe IDs from "markdown" if it exists and is a string
                if (content.TryGetProperty("markdown", out var markdownElement))
                {
                    var markdown = markdownElement.GetString()!;

                    var matches = Regex.Matches(markdown, @"(keyFrame\.\d+)\.jpg");
                    foreach (Match match in matches)
                    {
                        keyframeIds.Add(match.Groups[1].Value);
                    }
                }
            }

            // Output the results
            Console.WriteLine($"\nFound face IDs: {string.Join(", ", faceIds)}");
            Console.WriteLine($"Found keyframe IDs: {string.Join(", ", keyframeIds)}");

            // Save all face images
            foreach (var faceId in faceIds)
            {
                try
                {
                    var imageBytes = await _client.GetImageFromAnalyzeOperationAsync(response, faceId);
                    await File.WriteAllBytesAsync($"{CacheDir}/{faceId}.jpg", imageBytes);
                    Console.WriteLine($"Saved face image: {faceId}.jpg");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save face image ({faceId}): {ex.Message}");
                }
            }

            // Save all keyframes images
            foreach (var keyframeId in keyframeIds)
            {
                try
                {
                    var imageBytes = await _client.GetImageFromAnalyzeOperationAsync(response, keyframeId);
                    await File.WriteAllBytesAsync($"{CacheDir}/{keyframeId}.jpg", imageBytes);
                    Console.WriteLine($"Saved keyframe: {keyframeId}.jpg");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save keyframe ({keyframeId}): {ex.Message}");
                }
            }
        }
    }
}
