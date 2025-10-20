using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Extensions
{
    public static class BlobFileConstants
    {
        public const string LabelFileSuffix = ".labels.json";
        public const string OcrResultFileSuffix = ".result.json";
        public const string KnowledgeSourceListFileName = "sources.jsonl";

        /// <summary>
        /// Supported document file types (image and PDF formats).
        /// </summary>
        public static readonly HashSet<string> SupportedDocumentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".heif"
        };

        /// <summary>
        /// Supported document and text file types (includes document formats and text formats).
        /// </summary>
        public static readonly HashSet<string> SupportedDocumentTextTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".heif",
            ".docx", ".xlsx", ".pptx", ".txt", ".html", ".md", ".eml", ".msg", ".xml"
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string GetLabelFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            return $"{fileName}{LabelFileSuffix}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string GetOcrResultFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            return $"{fileName}{OcrResultFileSuffix}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsSupportedDocumentType(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName);
            return SupportedDocumentTypes.Contains(extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsSupportedDocumentTextType(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName);
            return SupportedDocumentTextTypes.Contains(extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void ValidateDocumentType(string fileName)
        {
            if (!IsSupportedDocumentType(fileName))
            {
                var extension = Path.GetExtension(fileName);
                throw new ArgumentException(
                    $"Unsupported document type '{extension}'. Supported types: {string.Join(", ", SupportedDocumentTypes)}",
                    nameof(fileName));
            }
        }

        /// <summary>
        /// Validates whether the specified file name corresponds to a supported document or text type.
        /// </summary>
        /// <remarks>This method checks the file extension against a predefined list of supported document
        /// and text types.</remarks>
        /// <param name="fileName">The name of the file to validate, including its extension.</param>
        /// <exception cref="ArgumentException">Thrown if the file extension is not a supported document or text type. The exception message includes the
        /// unsupported extension and a list of supported types.</exception>
        public static void ValidateDocumentTextType(string fileName)
        {
            if (!IsSupportedDocumentTextType(fileName))
            {
                var extension = Path.GetExtension(fileName);
                throw new ArgumentException(
                    $"Unsupported document/text type '{extension}'. Supported types: {string.Join(", ", SupportedDocumentTextTypes)}",
                    nameof(fileName));
            }
        }
    }
}
