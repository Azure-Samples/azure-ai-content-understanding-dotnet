using BuildPersonDirectory.Interfaces;
using BuildPersonDirectory.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ContentUnderstanding.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class BuildPersonDirectoryIntegrationTest
    {
        private readonly IBuildPersonDirectoryService service;
        private const string EnrollmentDataPath = "./data/face/enrollment_data";
        private string testImagePath = "./data/face/family.jpg";
        private string newFaceImagePath = "./data/face/NewFace_Bill.jpg";

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildPersonDirectoryIntegrationTest"/> class.
        /// </summary>
        /// <remarks>This constructor sets up the required services and configurations for testing the
        /// <see cref="IBuildPersonDirectoryService"/> implementation. It validates the presence of necessary
        /// configuration values and registers dependencies such as HTTP clients and token providers.</remarks>
        /// <exception cref="ArgumentException">Thrown if the required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion"
        /// are missing or empty in the application settings.</exception>
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/content_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingFaceClient>();
                    services.AddSingleton<IBuildPersonDirectoryService, BuildPersonDirectoryService>();
                })
                .Build();

            service = host.Services.GetService<IBuildPersonDirectoryService>()!;
        }

        /// <summary>
        /// Tests the functionality of building a person directory asynchronously and verifies the enrollment of
        /// persons.
        /// </summary>
        /// <remarks>This test method performs the following actions: <list type="bullet">
        /// <item><description>Creates a new person directory using a unique directory ID.</description></item>
        /// <item><description>Builds the person directory by enrolling persons from subfolders in the specified data
        /// path.</description></item> <item><description>Validates that the number of enrolled persons matches the
        /// number of subfolders.</description></item> <item><description>Checks that the names of enrolled persons
        /// correspond to the subfolder names.</description></item> <item><description>Identifies persons in a test
        /// image using the created directory and verifies the detection results.</description></item> </list> If any
        /// exception occurs during the test, it is captured and asserted to ensure no unexpected errors are
        /// thrown.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task BuildPersonDirectoryAsyncTest()
        {
            Exception? serviceException = null;
            
            try
            {
                string directoryId = $"person_directory_id_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                // create a new directory
                var response = await service.CreatePersonDirectoryAsync(directoryId);
                Assert.NotNull(response);
                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                // Build the person directory for the given directory ID and return a list of all enrolled persons.
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

                // Identify persons in the test image
                List<DetectedFace> detectedFaces = await service.IdentifyPersonsInImageAsync(directoryId, testImagePath);
                Assert.NotNull(detectedFaces);
                Assert.True(detectedFaces.Any());

                // All enrolled persons
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

                // Add new face to person
                FaceResponse? faceResponse = await service.AddNewFaceToPersonAsync(directoryId, Bill.PersonId, newFaceImagePath);
                Assert.NotNull(faceResponse);
                Assert.NotNull(faceResponse.FaceId);
                Assert.Equal(Bill.PersonId, faceResponse.PersonId);

                // get person
                PersonResponse personResponse = await service.GetPersonAsync(directoryId, faceResponse.PersonId!);
                Assert.DoesNotContain(faceResponse.FaceId, Bill.Faces);
                Assert.Contains(faceResponse.FaceId, personResponse.FaceIds);

                // delete Bill's person
                await service.DeletePersonAsync(directoryId, Bill.PersonId!);
                PersonResponse deletedBillPersonResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.Null(deletedBillPersonResponse);

                // Re-add Bill to the directory
                PersonResponse addNewPersonResponse = await service.AddPersonAsync(
                    directoryId,
                    new Dictionary<string, object> { ["name"] = Bill.Name! }
                );
                Bill.PersonId = addNewPersonResponse.PersonId;

                // associate existing faces with Bill
                await service.AssociateExistingFacesAsync(directoryId, Bill.PersonId, Bill.Faces);
                
                // add new face for Bill
                var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(newFaceImagePath);
                FaceResponse newFaceResponse = await service.AddFaceAsync(directoryId, imageData, Bill.PersonId);
                Assert.DoesNotContain(Bill.Faces, s => s == newFaceResponse.FaceId);

                // after new face added, we can get the person again
                PersonResponse newPersonResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.NotNull(newPersonResponse);
                Assert.Contains(newFaceResponse.FaceId, newPersonResponse.FaceIds);

                // Associating and disassociating a face from a person
                // You can associate or disassociate a face with a person in the Person Directory.
                // Associating a face links it to a specific person, while disassociating removes that link.
                // In the previous step, one of the daughter Jordan's face images was incorrectly added to the person "Mary".
                // We will now re-associate this face with the correct person, "Jordan".
                Assert.True(!Jordan.Faces.Contains(Mary.Faces.First()));
                FaceResponse? jordanFaceResponse = await service.UpdateFaceAssociationAsync(directoryId, Mary.Faces.First(), Jordan.PersonId);
                PersonResponse jordanResponse = await service.GetPersonAsync(directoryId, jordanFaceResponse?.PersonId!);
                Assert.Contains(Mary.Faces.First(), jordanResponse.FaceIds);

                // Updating metadata (tags and descriptions)
                // You can add or update tags for individual persons, and both descriptions and tags for the Person Directory. These metadata fields help organize, filter, and manage your directory.
                await service.UpdateMetadataAsync(directoryId, Bill.PersonId);
                PersonResponse billResponse = await service.GetPersonAsync(directoryId, Bill.PersonId!);
                Assert.NotNull(billResponse.Tags);

                // Deleting a face with Mary's person
                // You can also delete a specific face. Once the face is deleted, the association between the face and its associated person is removed.
                PersonResponse deletedMaryPersonResponse = await service.DeleteFaceAndPersonAsync(directoryId, Mary.PersonId!);
                Assert.Null(deletedMaryPersonResponse);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            Assert.Null(serviceException);
        }
    }
}
