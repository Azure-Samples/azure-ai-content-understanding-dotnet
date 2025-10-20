using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Extensions
{
    /// <summary>
    /// Extension methods for BlobContainerClient to simplify blob upload operations.
    /// </summary>
    public static class BlobUploadExtensions
    {
        /// <summary>
        /// Uploads a file to a specified blob storage path asynchronously.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the target path.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the target blob container.</param>
        /// <param name="filePath">The local file path of the file to be uploaded. Cannot be null or empty.</param>
        /// <param name="targetBlobPath">The path within the blob container where the file will be uploaded. Cannot be null or empty.</param>
        /// <param name="overwrite">Whether to overwrite existing blob. Default is true.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the upload operation fails.</exception>
        public static async Task UploadFileAsync(
            this BlobContainerClient blobContainer,
            string filePath,
            string targetBlobPath,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            if (blobContainer == null)
                throw new ArgumentNullException(nameof(blobContainer));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (string.IsNullOrWhiteSpace(targetBlobPath))
                throw new ArgumentNullException(nameof(targetBlobPath));

            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                await blobClient.UploadAsync(filePath, overwrite, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to upload file '{filePath}' to blob '{targetBlobPath}'.", ex);
            }
        }

        /// <summary>
        /// Uploads JSON content to a specified blob in an Azure Blob Storage container.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the specified path. Ensure that the
        /// <paramref name="jsonContent"/> is properly formatted JSON.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the target blob container.</param>
        /// <param name="jsonContent">The JSON content to upload as a string. Cannot be null or empty.</param>
        /// <param name="targetBlobPath">The path within the blob container where the JSON content will be uploaded. Cannot be null or empty.</param>
        /// <param name="overwrite">Whether to overwrite existing blob. Default is true.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the upload operation fails.</exception>
        public static async Task UploadJsonAsync(
            this BlobContainerClient blobContainer,
            string jsonContent,
            string targetBlobPath,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            if (blobContainer == null)
                throw new ArgumentNullException(nameof(blobContainer));

            if (string.IsNullOrWhiteSpace(jsonContent))
                throw new ArgumentNullException(nameof(jsonContent));

            if (string.IsNullOrWhiteSpace(targetBlobPath))
                throw new ArgumentNullException(nameof(targetBlobPath));

            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
                await blobClient.UploadAsync(stream, overwrite, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to upload JSON content to blob '{targetBlobPath}'.", ex);
            }
        }

        /// <summary>
        /// Uploads a list of JSONL (JSON Lines) formatted strings to a specified blob in Azure Blob Storage.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the specified path. Ensure that the
        /// <paramref name="targetBlobPath"/> is correct to avoid unintentional data loss.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the Azure Blob Storage container where the data will be uploaded.</param>
        /// <param name="jsonContents">A list of strings, each representing a JSON object, to be uploaded as JSONL content.</param>
        /// <param name="targetBlobPath">The path within the blob container where the JSONL content will be stored.</param>
        /// <param name="overwrite">Whether to overwrite existing blob. Default is true.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the upload operation fails.</exception>
        public static async Task UploadJsonlAsync(
            this BlobContainerClient blobContainer,
            List<string> jsonContents,
            string targetBlobPath,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            if (blobContainer == null)
                throw new ArgumentNullException(nameof(blobContainer));

            if (jsonContents == null || jsonContents.Count == 0)
                throw new ArgumentNullException(nameof(jsonContents));

            if (string.IsNullOrWhiteSpace(targetBlobPath))
                throw new ArgumentNullException(nameof(targetBlobPath));

            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", jsonContents)));
                await blobClient.UploadAsync(stream, overwrite, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to upload JSONL content to blob '{targetBlobPath}'.", ex);
            }
        }
    }
}
