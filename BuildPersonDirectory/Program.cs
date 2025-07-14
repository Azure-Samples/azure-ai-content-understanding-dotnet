using BuildPersonDirectory.Extensions;
using BuildPersonDirectory.Interfaces;
using BuildPersonDirectory.Services;
using ContentUnderstanding.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildPersonDirectory
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
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

            var service = host.Services.GetService<IBuildPersonDirectoryService>()!;

            var directoryId = service.CreatePersonDirectory();
            var imagePath = "./data/face/family.jpg";
            // 2. Build Directory from Enrollment Data
            await service.BuildPersonDirectoryAsync(directoryId);

            // 3. Identify Persons in Test Image
            await service.IdentifyPersonsInImageAsync(directoryId, imagePath);

            // 4. AddNewFaceToPerson
            await service.AddNewFaceToPersonAsync(directoryId, "person_id", "new_face_image_path");

            // 5. AssociateExistingFaces
            await service.AssociateExistingFacesAsync(directoryId, "person_id", new List<string> { "face_id_1", "face_id_2" });

            // 6. UpdateFaceAssociation
            await service.UpdateFaceAssociationAsync(directoryId, "face_id", "new_person_id");

            // 7. UpdateMetadata
            await service.UpdateMetadataAsync(directoryId, "person_id");

            // 8. DeleteFaceAndPerson
            await service.DeleteFaceAndPersonAsync(directoryId, "person_id");

        }
    }
}
