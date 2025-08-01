using System.Text.Json;

namespace ContentExtraction.Interfaces
{
    public interface IContentExtractionService
    {
        /// <summary>
        /// Document Content
        /// <remarks>Content Understanding API is designed to extract all textual content from a specified document file. 
        /// In addition to text extraction, it conducts a comprehensive layout analysis to identify and categorize tables and figures within the document. 
        /// The output is then presented in a structured markdown format, ensuring clarity and ease of interpretation.</remarks>
        /// </summary>
        /// <param name="filePath">The path to the document file to be analyzed. Must be a valid file path.</param>
        /// <returns>A task representing the asynchronous operation. The task completes when the document analysis is finished.</returns>
        Task<JsonDocument> AnalyzeDocumentAsync(string filePath);

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
        Task<JsonDocument> AnalyzeAudioAsync(string filePath);

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
        Task<JsonDocument> AnalyzeVideoAsync(string filePath);

        /// <summary>
        /// Video Content With Face
        /// <remarks>This is a gated feature, please go through process [Azure AI Resource Face Gating](https://learn.microsoft.com/en-us/legal/cognitive-services/computer-vision/limited-access-identity?context=%2Fazure%2Fai-services%2Fcomputer-vision%2Fcontext%2Fcontext#registration-process) Select `[Video Indexer] Facial Identification (1:N or 1:1 matching) to search for a face in a media or entertainment video archive to find a face within a video and generate metadata for media or entertainment use cases only` in the registration form.</remarks>
        /// </summary>
        /// <param name="filePath">The path to the video file to be analyzed. Must be a valid file path.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the analysis is finished and the
        /// extracted content, including key frames and face data, has been processed.</returns>
        Task<JsonDocument> AnalyzeVideoWithFaceAsync(string filePath);
    }
}
