using Azure.AI.ContentUnderstanding;
using System.Text.Json;

namespace Classifier.Interfaces
{
    public interface IClassifierService
    {
        /// <summary>
        /// Asynchronously classifies a document using the specified content classifier.
        /// </summary>
        /// <remarks>This method creates or replaces a content classifier, classifies the specified
        /// document, and then deletes the classifier. Ensure that the file at <paramref name="fileLocation"/> exists
        /// before calling this method.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier to be used.</param>
        /// <param name="classifier">The content classifier configuration to apply to the document.</param>
        /// <param name="fileLocation">The file path of the document to classify. Must be a valid path to an existing file.</param>
        /// <returns>A <see cref="ClassifyResult"/> containing the classification results, or <see langword="null"/> if the file
        /// is not found or an error occurs.</returns>
        Task<ClassifyResult?> ClassifyDocumentAsync(string classifierId, ContentClassifier classifier, string fileLocation);
    }
}
