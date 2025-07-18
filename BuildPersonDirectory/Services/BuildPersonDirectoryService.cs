using BuildPersonDirectory.Interfaces;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildPersonDirectory.Services
{
    public class BuildPersonDirectoryService : IBuildPersonDirectoryService
    {
        private readonly AzureContentUnderstandingFaceClient _client;
        private const string EnrollmentDataPath = "./data/face/enrollment_data";

        public BuildPersonDirectoryService(AzureContentUnderstandingFaceClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Asynchronously creates a person directory with the specified identifier.
        /// </summary>
        /// <param name="directoryId">The unique identifier for the directory to be created. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the identifier of the created
        /// directory.</returns>
        public async Task<string> CreatePersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("Creating Person Directory...");

            await _client.CreatePersonDirectoryAsync(directoryId);

            Console.WriteLine($"Created person directory with ID: {directoryId}");
            return directoryId;
        }

        /// <summary>
        /// Asynchronously builds a directory of persons by processing subfolders containing face images.
        /// </summary>
        /// <remarks>This method processes each subfolder in the enrollment data directory as a separate
        /// person. Each subfolder's name is used as the person's name, and all image files within the subfolder are
        /// processed as face images for that person.</remarks>
        /// <param name="directoryId">The identifier of the directory where persons will be added.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="Person"/>
        /// objects, each representing a person added to the directory.</returns>
        /// <exception cref="Exception">Thrown if the enrollment data directory does not exist.</exception>
        public async Task<IList<Person>> BuildPersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("\nBuilding Person Directory...");

            IList<Person> persons = new List<Person>();

            if (!Directory.Exists(EnrollmentDataPath))
                throw new Exception($"Enrollment data directory not found: {EnrollmentDataPath}");

            var subfolders = Directory.GetDirectories(EnrollmentDataPath);
            Console.WriteLine($"Found {subfolders.Length} subfolders in enrollment data");

            // Iterate through all subfolders in the EnrollmentDataPath
            foreach (var subfolder in subfolders)
            {
                var person = new Person();
                var personName = Path.GetFileName(subfolder);
                Console.WriteLine($"Processing person: {personName}");

                // Add a person for each subfolder
                var personResponse = await _client.AddPersonAsync(
                    directoryId,
                    new Dictionary<string, object> { ["name"] = personName }
                );
                var personId = personResponse.PersonId;
                person.PersonId = personId;
                person.Name = personName;

                Console.WriteLine($"Created person with ID: {personId}");

                // Process face images
                var imageFiles = Directory.GetFiles(subfolder)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                Console.WriteLine($"Found {imageFiles.Length} face images");

                // Iterate through all images in the subfolder
                foreach (var imageFile in imageFiles)
                {
                    var filename = Path.GetFileName(imageFile);
                    Console.Write($"- Adding face from {filename}... ");

                    try
                    {
                        // Convert image to base64
                        var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(imageFile);
                        // Add a face to the Person Directory and associate it to the added person
                        var faceResponse = await _client.AddFaceAsync(directoryId, imageData, personId);
                        person.Faces.Add(faceResponse.FaceId);
                        Console.WriteLine($"Added face from {filename} with face_id: {faceResponse.FaceId} to person_id: {personId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to add face from {filename} to person_id: {personId}");
                    }
                }

                persons.Add(person);
            }

            Console.WriteLine($"\nCompleted building person directory with {subfolders.Length} persons");
            return persons;
        }

        /// <summary>
        /// Asynchronously identifies persons in a given image using a specified directory of known individuals.
        /// </summary>
        /// <remarks>Detect multiple faces in an image and identify each one by matching it against enrolled persons in the Person Directory.</remarks>
        /// <param name="directoryId">The identifier of the directory containing known individuals for comparison.</param>
        /// <param name="imagePath">The file path to the image in which persons are to be identified.</param>
        /// <returns></returns>
        public async Task IdentifyPersonsInImageAsync(string directoryId, string imagePath)
        {
            Console.WriteLine("\nIdentifying Persons in Image...");
            Console.WriteLine($"Processing test image: {Path.GetFileName(imagePath)}");

            var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(imagePath);

            // Detect faces in the test image
            var detectionResponse = await _client.DetectFacesAsync(data: imageData);
            var detectedFaces = detectionResponse.DetectedFaces;
            Console.WriteLine($"Detected {detectedFaces.Count} faces in the image");

            foreach (var face in detectedFaces)
            {
                // CORRECTLY HANDLED: Use boundingBox dictionary
                var boundingBox = face.BoundingBox;

                // Extract values correctly from dictionary
                string faceAt = $"Face at ";

                foreach(var item in boundingBox)
                {
                    faceAt += $"[{item.Key}]: {item.Value}, ";
                }

                Console.WriteLine($"{faceAt.Remove(faceAt.Length - 1)}");

                try
                {
                    var identifyResponse = await _client.IdentifyPersonAsync(
                        directoryId,
                        imageData,
                        boundingBox
                    );

                    if (identifyResponse.PersonCandidates != null && identifyResponse.PersonCandidates.Count > 0)
                    {
                        var candidate = identifyResponse.PersonCandidates[0];

                        // Get person details to get name tag
                        var person = await _client.GetPersonAsync(directoryId, candidate.PersonId);
                        var name = person.Tags.TryGetValue("name", out var n) ? n : "Unknown";

                        Console.WriteLine($"  Identified as: {name} (Confidence: {candidate.Confidence}, Person ID: {candidate.PersonId})");
                    }
                    else
                    {
                        Console.WriteLine("  Person not identified in directory");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error identifying person: {ex.Message}");
                }
            }

            Console.WriteLine("Identification completed");
        }

        /// <summary>
        /// Asynchronously adds a new face to an existing person in the specified directory.
        /// </summary>
        /// <remarks>You can add a new face to the Person Directory and associate it with an existing person.</remarks>
        /// <param name="directoryId">The identifier of the directory where the person is located.</param>
        /// <param name="personId">The identifier of the person to whom the new face will be added. Can be <see langword="null"/> if not
        /// specified.</param>
        /// <param name="newFaceImagePath">The file path of the image containing the new face to be added. The file must exist.</param>
        /// <returns></returns>
        public async Task AddNewFaceToPersonAsync(string directoryId, string? personId, string newFaceImagePath)
        {
            Console.WriteLine("\nAdding new face to existing person...");

            if (!File.Exists(newFaceImagePath))
            {
                Console.WriteLine($"File not found: {newFaceImagePath}");
                return;
            }

            try
            {
                // Convert the new face image to base64
                var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(newFaceImagePath);
                // Add the new face to the person directory and associate it with the existing person
                var faceResponse = await _client.AddFaceAsync(directoryId, imageData, personId);
                Console.WriteLine($"Added face from {newFaceImagePath} with face_id: {faceResponse.FaceId} to person_id: {personId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add face from {newFaceImagePath} to person_id: {personId}");
            }
        }

        /// <summary>
        /// Asynchronously retrieves information about a specific face from the specified person directory.
        /// </summary>
        /// <param name="personDirectoryId">The identifier of the person directory containing the face.</param>
        /// <param name="faceId">The identifier of the face to retrieve information for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="FaceResponse"/>
        /// with details about the face.</returns>
        public async Task<FaceResponse> GetFaceAsync(string personDirectoryId, string faceId)
        {
            return await _client.GetFaceAsync(personDirectoryId, faceId);
        }

        /// <summary>
        /// Associates a list of existing face IDs with a specified person in a directory.
        /// </summary>
        /// <remarks>You can associate a list of already enrolled faces in the Person Directory with their respective persons. This is useful if you have existing face IDs to link to specific persons.</remarks>
        /// <param name="directoryId">The identifier of the directory containing the person.</param>
        /// <param name="personId">The unique ID of the person to whom the face should be associated.</param>
        /// <param name="faceIds">The list of face IDs to be associated.</param>
        /// <returns></returns>
        public async Task AssociateExistingFacesAsync(string directoryId, string? personId, List<string> faceIds)
        {
            Console.WriteLine("\nAssociating existing faces to person...");

            if (faceIds == null || faceIds.Count == 0)
            {
                Console.WriteLine("No face IDs provided");
                return;
            }

            try
            {
                // Associate the existing face IDs with the existing person
                await _client.UpdatePersonAsync(
                    directoryId,
                    personId,
                    faceIds: faceIds
                );
                Console.WriteLine($"Associated {faceIds.Count} faces to person {personId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error associating faces: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the association of a face with a person in the specified directory.
        /// </summary>
        /// <remarks>You can associate or disassociate a face from a person in the Person Directory. Associating a face links it to a specific person, while disassociating removes this link.</remarks>
        /// <param name="directoryId">The identifier of the directory containing the face.</param>
        /// <param name="faceId">The unique ID of the face.</param>
        /// <param name="personId">The unique ID of the person to be associated with the face.</param>
        /// <returns></returns>
        public async Task UpdateFaceAssociationAsync(string directoryId, string faceId, string? personId = null)
        {
            Console.WriteLine("\nUpdating face association...");

            try
            {
                // Remove the association of the existing face ID from the person or associate the existing face ID with a person.
                await _client.UpdateFaceAsync(directoryId, faceId, personId ?? "");

                var action = string.IsNullOrEmpty(personId) ?
                    "Disassociated" : "Associated";

                Console.WriteLine($"{action} face {faceId} to person: {personId ?? "None"}");
                var json = await _client.GetFaceAsync(directoryId, faceId);
                Console.WriteLine($"The face information with the new person association: {JsonSerializer.Serialize(json)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating face association: {ex.Message}");
            }
        }

        public async Task UpdateMetadataAsync(string directoryId, string? personId = null)
        {
            Console.WriteLine("\nUpdating metadata...");

            try
            {
                // Update directory metadata
                await _client.UpdatePersonDirectoryAsync(
                    directoryId,
                    "Updated directory description",
                    new Dictionary<string, object>
                    {
                        ["updated"] = "true",
                        ["sample"] = "true"
                    }
                );
                Console.WriteLine("Updated directory metadata");

                if (!string.IsNullOrEmpty(personId))
                {
                    // Update person metadata
                    await _client.UpdatePersonAsync(
                        directoryId,
                        personId,
                        new Dictionary<string, object>
                        {
                            ["role"] = "demo-subject",
                            ["status"] = "active"
                        }
                    );
                    Console.WriteLine($"Updated metadata for person {personId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating metadata: {ex.Message}");
            }
        }

        public async Task DeleteFaceAndPersonAsync(string directoryId, string personId)
        {
            Console.WriteLine("\nDeleting person and their faces...");

            try
            {
                // Get person to find associated faces
                var person = await _client.GetPersonAsync(directoryId, personId);
                var faceIds = person?.FaceIds ?? new List<string>();

                // Delete all faces
                foreach (var faceId in faceIds)
                {
                    await _client.DeleteFaceAsync(directoryId, faceId);
                    Console.WriteLine($"Deleted face {faceId}");
                }

                // Delete person
                await _client.DeletePersonAsync(directoryId, personId);
                Console.WriteLine($"Deleted person {personId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deletion: {ex.Message}");
            }
        }
    }
}
