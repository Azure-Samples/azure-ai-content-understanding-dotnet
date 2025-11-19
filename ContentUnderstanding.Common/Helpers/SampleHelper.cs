// pylint: disable=line-too-long,useless-suppression
// coding=utf-8
// --------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
// --------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContentUnderstanding.Common.Helpers
{
    /// <summary>
    /// Helper functions for Azure AI Content Understanding samples.
    /// </summary>
    public static class SampleHelper
    {
        /// <summary>
        /// Extract the actual value from a field dictionary.
        /// </summary>
        /// <param name="fields">A dictionary of field names to field data dictionaries.</param>
        /// <param name="fieldName">The name of the field to extract.</param>
        /// <returns>The extracted value or null if not found.</returns>
        public static object? GetFieldValue(Dictionary<string, object>? fields, string fieldName)
        {
            if (fields == null || !fields.ContainsKey(fieldName))
            {
                return null;
            }

            var fieldData = fields[fieldName];

            // Extract the value from the field dictionary
            if (fieldData is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Object)
                {
                    // Try to get "value", "valueString", or "content" property
                    if (jsonElement.TryGetProperty("value", out var valueProperty))
                    {
                        return GetJsonElementValue(valueProperty);
                    }
                    if (jsonElement.TryGetProperty("valueString", out var valueStringProperty))
                    {
                        return valueStringProperty.GetString();
                    }
                    if (jsonElement.TryGetProperty("content", out var contentProperty))
                    {
                        return contentProperty.GetString();
                    }
                }
                return GetJsonElementValue(jsonElement);
            }

            if (fieldData is Dictionary<string, object> fieldDict)
            {
                // Try to get "value", "valueString", or "content" from dictionary
                if (fieldDict.TryGetValue("value", out var value))
                {
                    return value;
                }
                if (fieldDict.TryGetValue("valueString", out var valueString))
                {
                    return valueString;
                }
                if (fieldDict.TryGetValue("content", out var content))
                {
                    return content;
                }
            }

            return fieldData;
        }

        /// <summary>
        /// Extract the actual value from a field JsonElement.
        /// </summary>
        /// <param name="fields">A JsonElement containing field data.</param>
        /// <param name="fieldName">The name of the field to extract.</param>
        /// <returns>The extracted value or null if not found.</returns>
        public static object? GetFieldValue(JsonElement fields, string fieldName)
        {
            if (fields.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!fields.TryGetProperty(fieldName, out var fieldData))
            {
                return null;
            }

            // Extract the value from the field dictionary
            if (fieldData.ValueKind == JsonValueKind.Object)
            {
                // Try to get "value", "valueString", or "content" property
                if (fieldData.TryGetProperty("value", out var valueProperty))
                {
                    return GetJsonElementValue(valueProperty);
                }
                if (fieldData.TryGetProperty("valueString", out var valueStringProperty))
                {
                    return valueStringProperty.GetString();
                }
                if (fieldData.TryGetProperty("content", out var contentProperty))
                {
                    return contentProperty.GetString();
                }
            }

            return GetJsonElementValue(fieldData);
        }

        /// <summary>
        /// Helper method to extract value from JsonElement based on its type.
        /// </summary>
        private static object? GetJsonElementValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element,
                JsonValueKind.Object => element,
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Persist the full analysis result as JSON and return the file path.
        /// </summary>
        /// <param name="result">The analysis result as JsonDocument.</param>
        /// <param name="outputDir">Output directory path. Defaults to "test_output".</param>
        /// <param name="filenamePrefix">Filename prefix. Defaults to "analysis_result".</param>
        /// <returns>The full path to the saved JSON file.</returns>
        public static string SaveJsonToFile(
            JsonDocument result,
            string outputDir = "test_output",
            string filenamePrefix = "analysis_result")
        {
            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDir);

            // Generate timestamp in UTC
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            // Generate file path
            string fileName = $"{filenamePrefix}_{timestamp}.json";
            string path = Path.Combine(outputDir, fileName);

            // Write JSON to file with pretty formatting
            string jsonString = JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

            File.WriteAllText(path, jsonString, System.Text.Encoding.UTF8);

            Console.WriteLine($"💾 Analysis result saved to: {path}");

            return path;
        }

        /// <summary>
        /// Persist the full analysis result as JSON and return the file path.
        /// </summary>
        /// <param name="result">The analysis result as Dictionary.</param>
        /// <param name="outputDir">Output directory path. Defaults to "test_output".</param>
        /// <param name="filenamePrefix">Filename prefix. Defaults to "analysis_result".</param>
        /// <returns>The full path to the saved JSON file.</returns>
        public static string SaveJsonToFile(
            Dictionary<string, object> result,
            string outputDir = "test_output",
            string filenamePrefix = "analysis_result")
        {
            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDir);

            // Generate timestamp in UTC
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            // Generate file path
            string fileName = $"{filenamePrefix}_{timestamp}.json";
            string path = Path.Combine(outputDir, fileName);

            // Write JSON to file with pretty formatting
            string jsonString = JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

            File.WriteAllText(path, jsonString, System.Text.Encoding.UTF8);

            Console.WriteLine($"💾 Analysis result saved to: {path}");

            return path;
        }

        /// <summary>
        /// Persist any object as JSON and return the file path.
        /// </summary>
        /// <param name="result">The object to serialize.</param>
        /// <param name="outputDir">Output directory path. Defaults to "test_output".</param>
        /// <param name="filenamePrefix">Filename prefix. Defaults to "analysis_result".</param>
        /// <returns>The full path to the saved JSON file.</returns>
        public static string SaveJsonToFile(
            object result,
            string outputDir = "test_output",
            string filenamePrefix = "analysis_result")
        {
            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDir);

            // Generate timestamp in UTC
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            // Generate file path
            string fileName = $"{filenamePrefix}_{timestamp}.json";
            string path = Path.Combine(outputDir, fileName);

            // Write JSON to file with pretty formatting
            string jsonString = JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

            File.WriteAllText(path, jsonString, System.Text.Encoding.UTF8);

            Console.WriteLine($"💾 Analysis result saved to: {path}");

            return path;
        }
    }
}