using ContentUnderstanding.Common.Models;
using System.Text.Json;
using Xunit;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Unit tests for analyzer pagination functionality.
    /// Validates that the AnalyzerListResponse model correctly deserializes nextLink property.
    /// </summary>
    public class AnalyzerPaginationUnitTest
    {
        /// <summary>
        /// Tests that AnalyzerListResponse correctly deserializes a response with nextLink.
        /// </summary>
        [Fact(DisplayName = "AnalyzerListResponse should deserialize nextLink property")]
        [Trait("Category", "Unit")]
        public void AnalyzerListResponse_Should_Deserialize_NextLink()
        {
            // Arrange
            var jsonResponse = @"{
                ""value"": [
                    {
                        ""analyzerId"": ""test-analyzer-1"",
                        ""status"": ""ready""
                    },
                    {
                        ""analyzerId"": ""test-analyzer-2"",
                        ""status"": ""ready""
                    }
                ],
                ""nextLink"": ""https://example.services.ai.azure.com/contentunderstanding/analyzers?api-version=2025-11-01&$skip=20""
            }";

            // Act
            var response = JsonSerializer.Deserialize<AnalyzerListResponse>(jsonResponse);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Value);
            Assert.Equal(2, response.Value.Length);
            Assert.NotNull(response.NextLink);
            Assert.Equal("https://example.services.ai.azure.com/contentunderstanding/analyzers?api-version=2025-11-01&$skip=20", response.NextLink);
        }

        /// <summary>
        /// Tests that AnalyzerListResponse handles missing nextLink property correctly.
        /// </summary>
        [Fact(DisplayName = "AnalyzerListResponse should handle missing nextLink")]
        [Trait("Category", "Unit")]
        public void AnalyzerListResponse_Should_Handle_Missing_NextLink()
        {
            // Arrange
            var jsonResponse = @"{
                ""value"": [
                    {
                        ""analyzerId"": ""test-analyzer-1"",
                        ""status"": ""ready""
                    }
                ]
            }";

            // Act
            var response = JsonSerializer.Deserialize<AnalyzerListResponse>(jsonResponse);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Value);
            Assert.Single(response.Value);
            Assert.Null(response.NextLink);
        }

        /// <summary>
        /// Tests that AnalyzerListResponse handles null nextLink property correctly.
        /// </summary>
        [Fact(DisplayName = "AnalyzerListResponse should handle null nextLink")]
        [Trait("Category", "Unit")]
        public void AnalyzerListResponse_Should_Handle_Null_NextLink()
        {
            // Arrange
            var jsonResponse = @"{
                ""value"": [
                    {
                        ""analyzerId"": ""test-analyzer-1"",
                        ""status"": ""ready""
                    }
                ],
                ""nextLink"": null
            }";

            // Act
            var response = JsonSerializer.Deserialize<AnalyzerListResponse>(jsonResponse);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Value);
            Assert.Single(response.Value);
            Assert.Null(response.NextLink);
        }

        /// <summary>
        /// Tests that AnalyzerListResponse handles empty value array with nextLink.
        /// </summary>
        [Fact(DisplayName = "AnalyzerListResponse should handle empty value array with nextLink")]
        [Trait("Category", "Unit")]
        public void AnalyzerListResponse_Should_Handle_Empty_Value_With_NextLink()
        {
            // Arrange
            var jsonResponse = @"{
                ""value"": [],
                ""nextLink"": ""https://example.services.ai.azure.com/contentunderstanding/analyzers?api-version=2025-11-01&$skip=20""
            }";

            // Act
            var response = JsonSerializer.Deserialize<AnalyzerListResponse>(jsonResponse);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Value);
            Assert.Empty(response.Value);
            Assert.NotNull(response.NextLink);
        }
    }
}
