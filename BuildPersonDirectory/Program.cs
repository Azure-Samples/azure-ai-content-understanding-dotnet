using BuildPersonDirectory.Interfaces;
using BuildPersonDirectory.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ContentUnderstanding.Common.Models;
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

            // 1. Create Person Directory
            var directoryId = $"person_directory_id_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            await service.CreatePersonDirectoryAsync(directoryId);
            var imagePath = "./data/face/family.jpg";
            // 2. Build Directory from Enrollment Data
            IList<Person> persons = await service.BuildPersonDirectoryAsync(directoryId);
            Person person = persons.First();
            Person lastPerson = persons.Last();

            // 3. Identify Persons in Test Image
            await service.IdentifyPersonsInImageAsync(directoryId, imagePath);

            // PLEASE UPDATE THE "person_id" and "new_face_image_path" to your own data
            await service.AddNewFaceToPersonAsync(directoryId, person.PersonId, imagePath);

            // PLEASE UPDATE THE "person_id", "face_id_1" and "face_id_2" to your own data
            await service.AssociateExistingFacesAsync(directoryId, person.PersonId, person.Faces);

            // PLEASE UPDATE THE "face_id" and "new_person_id" to your own data
            await service.UpdateFaceAssociationAsync(directoryId, person.Faces[0], lastPerson.PersonId);

            // PLEASE UPDATE THE "person_id" to your own data
            await service.UpdateMetadataAsync(directoryId, person.PersonId);

            // PLEASE UPDATE THE "person_id" to your own data
            await service.DeleteFaceAndPersonAsync(directoryId, person.PersonId);

        }
    }
}
