using BuildPersonDirectory.Interfaces;
using ContentUnderstanding.Common;

namespace BuildPersonDirectory.Services
{
    public class BuildPersonDirectoryService : IBuildPersonDirectoryService
    {
        private readonly AzureContentUnderstandingFaceClient _client;
        private readonly string OutputPath = "./outputs/build_person_directory/";
        private const string EnrollmentDataPath = "./data/face/enrollment_data";

        public BuildPersonDirectoryService(AzureContentUnderstandingFaceClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        public async Task<string> CreatePersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("Creating Person Directory...");

            await _client.CreatePersonDirectoryAsync(directoryId);

            Console.WriteLine($"Created person directory with ID: {directoryId}");
            return directoryId;
        }

        public async Task BuildPersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("\nBuilding Person Directory...");

            if (!Directory.Exists(EnrollmentDataPath))
                throw new Exception($"Enrollment data directory not found: {EnrollmentDataPath}");

            var subfolders = Directory.GetDirectories(EnrollmentDataPath);
            Console.WriteLine($"Found {subfolders.Length} subfolders in enrollment data");

            foreach (var subfolder in subfolders)
            {
                var personName = Path.GetFileName(subfolder);
                Console.WriteLine($"Processing person: {personName}");

                // Create person
                var personResponse = await _client.AddPersonAsync(
                    directoryId,
                    new Dictionary<string, object> { ["name"] = personName }
                );

                var personId = personResponse.PersonId;
                Console.WriteLine($"Created person with ID: {personId}");

                // Process face images
                var imageFiles = Directory.GetFiles(subfolder)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                Console.WriteLine($"Found {imageFiles.Length} face images");

                foreach (var imageFile in imageFiles)
                {
                    var filename = Path.GetFileName(imageFile);
                    Console.Write($"- Adding face from {filename}... ");

                    try
                    {
                        var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(imageFile);
                        var faceResponse = await _client.AddFaceAsync(directoryId, imageData, personId);
                        Console.WriteLine($"success! Face ID: {faceResponse.FaceId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"failed: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"\nCompleted building person directory with {subfolders.Length} persons");
        }

        public async Task IdentifyPersonsInImageAsync(string directoryId, string imagePath)
        {
            Console.WriteLine("\nIdentifying Persons in Image...");
            Console.WriteLine($"Processing test image: {Path.GetFileName(imagePath)}");

            var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(imagePath);

            // Detect faces
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

        #region Management Operations (Implementation Examples)
        public async Task AddNewFaceToPersonAsync(string directoryId, string personId, string newFaceImagePath)
        {
            Console.WriteLine("\nAdding new face to existing person...");

            if (!File.Exists(newFaceImagePath))
            {
                Console.WriteLine($"File not found: {newFaceImagePath}");
                return;
            }

            try
            {
                var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(newFaceImagePath);
                var faceResponse = await _client.AddFaceAsync(directoryId, imageData, personId);
                Console.WriteLine($"Added new face with ID: {faceResponse.FaceId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding face: {ex.Message}");
            }
        }

        public async Task AssociateExistingFacesAsync(string directoryId, string personId, List<string> faceIds)
        {
            Console.WriteLine("\nAssociating existing faces to person...");

            if (faceIds == null || faceIds.Count == 0)
            {
                Console.WriteLine("No face IDs provided");
                return;
            }

            try
            {
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

        public async Task UpdateFaceAssociationAsync(string directoryId, string faceId, string newPersonId = null)
        {
            Console.WriteLine("\nUpdating face association...");

            try
            {
                // Clear association if newPersonId is null
                await _client.UpdateFaceAsync(directoryId, faceId, newPersonId ?? "");

                var action = string.IsNullOrEmpty(newPersonId) ?
                    "Disassociated" : "Associated";

                Console.WriteLine($"{action} face {faceId} to person: {newPersonId ?? "None"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating face association: {ex.Message}");
            }
        }

        public async Task UpdateMetadataAsync(string directoryId, string personId = null)
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
        #endregion
    }
}
