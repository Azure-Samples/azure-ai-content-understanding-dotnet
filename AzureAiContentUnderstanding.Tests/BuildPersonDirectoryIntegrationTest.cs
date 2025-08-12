using BuildPersonDirectory.Interfaces;
using BuildPersonDirectory.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ContentUnderstanding.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration test for building and managing a person directory using the IBuildPersonDirectoryService.
    /// This test covers directory creation, person enrollment, face association/disassociation, metadata update, and cleanup.
    /// </summary>
    public class BuildPersonDirectoryIntegrationTest
    {
        private readonly IBuildPersonDirectoryService service;
        private const string EnrollmentDataPath = "./data/face/enrollment_data";
        private string testImagePath = "./data/face/family.jpg";
        private string newFaceImagePath = "./data/face/NewFace_Bill.jpg";

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for AZURE_CU_CONFIG:Endpoint or AZURE_CU_CONFIG:ApiVersion are missing.
        /// </exception>
        public BuildPersonDirectoryIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint")))
                    {
                        throw new ArgumentException("Endpoint must be provided in appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion")))
                    {
                        throw new ArgumentException("API version must be provided in appsettings.json.");
                    }
                    services.AddConfigurations(opts =>
                    {
                        context.Configuration.GetSection("AZURE_CU_CONFIG").Bind(opts);
                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/build_person_directory";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingFaceClient>();
                    services.AddSingleton<IBuildPersonDirectoryService, BuildPersonDirectoryService>();
                })
                .Build();

            service = host.Services.GetService<IBuildPersonDirectoryService>()!;
        }

        /// <summary>
        /// Full workflow integration test for person directory management.
        /// Covers scenarios: directory creation, enrollment, identification, face management, metadata update, and cleanup.
        /// Asserts success and validity at each step. Any unexpected exception is captured and asserted as null.
        /// </summary>
        [Fact(DisplayName = "Build Person Directory Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            
            try
            {
                // Step 1: Create a unique person directory
                string directoryId = $"person_directory_id_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                var response = await service.CreatePersonDirectoryAsync(directoryId);
                Assert.NotNull(response);
                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                // Step 2: Build person directory from enrollment data and verify consistency
                List<string> subFolders = Directory.GetDirectories(EnrollmentDataPath).ToList();
                IList<Person> persons = await service.BuildPersonDirectoryAsync(directoryId);

                Assert.NotNull(subFolders);
                Assert.NotNull(persons);
                Assert.True(persons.Any());
                Assert.True(subFolders.Count == persons.Count);

                List<string> fileNames = subFolders.Select(s => Path.GetFileNameWithoutExtension(s)).ToList();
                Assert.NotNull(fileNames);
                Assert.True(fileNames.Any());
                Assert.True(fileNames.Count == persons.Count);

                foreach (var person in persons)
                {
                    Assert.True(fileNames?.Contains(person.Name ?? ""));
                }

                // Step 3: Identify persons in a test image
                List<DetectedFace> detectedFaces = await service.IdentifyPersonsInImageAsync(directoryId, testImagePath);
                Assert.NotNull(detectedFaces);
                Assert.True(detectedFaces.Any());

                // Step 4: Lookup enrolled persons and verify presence
                Person Alex = persons.Where(s => s.Name == "Alex").First();
                Person Bill = persons.Where(s => s.Name == "Bill").First();
                Person Clare = persons.Where(s => s.Name == "Clare").First();
                Person Jordan = persons.Where(s => s.Name == "Jordan").First();
                Person Mary = persons.Where(s => s.Name == "Mary").First();

                Assert.NotNull(Alex);
                Assert.NotNull(Bill);
                Assert.NotNull(Clare);
                Assert.NotNull(Jordan);
                Assert.NotNull(Mary);

                // Step 5: Add new face to Bill and verify association
                FaceResponse? faceResponse = await service.AddNewFaceToPersonAsync(directoryId, Bill.PersonId, newFaceImagePath);
                Assert.NotNull(faceResponse);
                Assert.NotNull(faceResponse.FaceId);
                Assert.Equal(Bill.PersonId, faceResponse.PersonId);

                PersonResponse personResponse = await service.GetPersonAsync(directoryId, faceResponse.PersonId!);
                Assert.DoesNotContain(faceResponse.FaceId, Bill.Faces);
                Assert.Contains(faceResponse.FaceId, personResponse.FaceIds);

                // Step 6: Delete Bill and verify deletion
                await service.DeletePersonAsync(directoryId, Bill.PersonId!);
                PersonResponse deletedBillPersonResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.Null(deletedBillPersonResponse);

                // Step 7: Re-add Bill, associate existing faces, and add new face
                PersonResponse addNewPersonResponse = await service.AddPersonAsync(
                    directoryId,
                    new Dictionary<string, object> { ["name"] = Bill.Name! }
                );
                Bill.PersonId = addNewPersonResponse.PersonId;

                await service.AssociateExistingFacesAsync(directoryId, Bill.PersonId, Bill.Faces);
                
                var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(newFaceImagePath);
                FaceResponse newFaceResponse = await service.AddFaceAsync(directoryId, imageData, Bill.PersonId);
                Assert.DoesNotContain(Bill.Faces, s => s == newFaceResponse.FaceId);

                PersonResponse newPersonResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.NotNull(newPersonResponse);
                Assert.Contains(newFaceResponse.FaceId, newPersonResponse.FaceIds);

                // Step 8: Correct Mary/Jordan face association (re-associate face from Mary to Jordan)
                Assert.True(!Jordan.Faces.Contains(Mary.Faces.First()));
                FaceResponse? jordanFaceResponse = await service.UpdateFaceAssociationAsync(directoryId, Mary.Faces.First(), Jordan.PersonId);
                PersonResponse jordanResponse = await service.GetPersonAsync(directoryId, jordanFaceResponse?.PersonId!);
                Assert.Contains(Mary.Faces.First(), jordanResponse.FaceIds);

                // Step 9: Update metadata for Bill and verify tags
                await service.UpdateMetadataAsync(directoryId, Bill.PersonId);
                PersonResponse billResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.NotNull(billResponse.Tags);

                // Step 10: Delete Mary's face and verify deletion
                PersonResponse deletedMaryPersonResponse = await service.DeleteFaceAndPersonAsync(directoryId, Mary.PersonId!);
                Assert.Null(deletedMaryPersonResponse);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }
    }
}
