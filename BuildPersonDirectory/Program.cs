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

            Console.WriteLine("# Person Directory");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine(">");
            Console.WriteLine("> This sample demonstrates how to identify faces in an image against a known set of persons.");
            Console.WriteLine("> It begins by building a Person Directory, where each subfolder in a specified directory represents an individual.");
            Console.WriteLine("> For each subfolder, a person is created and all face images within it are enrolled to that person.");
            Console.WriteLine("> For the concept, you can refer to the Jupyter notebooks in python, following the link is [https://github.com/Azure-Samples/azure-ai-content-understanding-python/blob/main/notebooks/build_person_directory.ipynb]");
            Console.WriteLine(">");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine("Enrollment image path: ./data/face/PD_enrollment.png");
            Console.WriteLine("Searching image path: ./data/face/PD_searching.png");

            // Create Person Directory
            var directoryId = $"person_directory_id_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            await service.CreatePersonDirectoryAsync(directoryId);
            var testImagePath = "./data/face/family.jpg";
            var newFaceImagePath = "./data/face/NewFace_Bill.jpg";

            // Build Directory from Enrollment Data
            IList<Person> persons = await service.BuildPersonDirectoryAsync(directoryId);
            Person? person_Alex = persons.Where(s => s.Name == "Alex").FirstOrDefault();
            Person? person_Bill = persons.Where(s => s.Name == "Bill").FirstOrDefault();
            Person? person_Jodan = persons.Where(s => s.Name == "Jodan").FirstOrDefault();
            Person? person_Mary = persons.Where(s => s.Name == "Mary").FirstOrDefault();

            // Identify Persons in Test Image
            // Detect multiple faces in an image and identify each one by matching it against enrolled persons in the Person Directory.
            await service.IdentifyPersonsInImageAsync(directoryId, testImagePath);

            // Adding and associating a new face
            // You can add a new face to the Person Directory and associate it with an existing person.
            if (person_Bill == null)
            {
                throw new Exception("Person Alex not found in the directory.");
            }
            await service.AddNewFaceToPersonAsync(directoryId, person_Bill.PersonId, newFaceImagePath);

            // Associating a list of already enrolled faces
            // You can associate a list of already enrolled faces in the Person Directory with their respective persons. This is useful if you have existing face IDs to link to specific persons.
            await service.AssociateExistingFacesAsync(directoryId, person_Bill.PersonId, person_Bill.Faces);

            // Associating and disassociating a face from a person
            // You can associate or disassociate a face from a person in the Person Directory. Associating a face links it to a specific person, while disassociating removes this link.
            if (person_Mary == null || person_Mary.Faces.Count == 0)
            {
                throw new Exception("Person Mary or her faces not found in the directory.");
            }
            if (person_Jodan == null)
            {
                throw new Exception("Person Jodan not found in the directory.");
            }
            await service.UpdateFaceAssociationAsync(directoryId, person_Mary.Faces.First(), person_Jodan.PersonId);

            // Updating metadata (tags and descriptions)
            // You can add or update tags for individual persons, and both descriptions and tags for the Person Directory. These metadata fields help organize, filter, and manage your directory.
            await service.UpdateMetadataAsync(directoryId, person_Bill.PersonId);

            // Deleting a face
            // You can also delete a specific face. Once the face is deleted, the association between the face and its associated person is removed.
            if (person_Mary.PersonId == null)
            {
                throw new Exception("Person Mary not found in the directory.");
            }
            await service.DeleteFaceAndPersonAsync(directoryId, person_Mary.PersonId);
        }
    }
}
