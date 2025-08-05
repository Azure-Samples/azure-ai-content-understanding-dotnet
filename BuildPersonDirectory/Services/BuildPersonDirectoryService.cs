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
        public async Task<HttpResponseMessage> CreatePersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("Creating Person Directory...");

            HttpResponseMessage response = await _client.CreatePersonDirectoryAsync(directoryId);

            Console.WriteLine($"Created person directory with ID: {directoryId}");
            return response;
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
        public async Task<List<Person>> BuildPersonDirectoryAsync(string directoryId)
        {
            Console.WriteLine("\nBuilding Person Directory...");

            List<Person> persons = new List<Person>();

            if (!Directory.Exists(EnrollmentDataPath))
                throw new Exception($"Enrollment data directory not found: {EnrollmentDataPath}");

            var subFolders = Directory.GetDirectories(EnrollmentDataPath);
            Console.WriteLine($"Found {subFolders.Length} subfolders in enrollment data");

            // Iterate through all subfolders in the EnrollmentDataPath
            foreach (var subfolder in subFolders)
            {
                var personName = Path.GetFileName(subfolder);
                Console.WriteLine($"Processing person: {personName}");

                // Adds a person to the Person Directory, using the name of the subfolder as the person's name.
                var personResponse = await _client.AddPersonAsync(
                    directoryId,
                    new Dictionary<string, object> { ["name"] = personName }
                );

                var person = new Person
                {
                    PersonId = personResponse.PersonId,
                    Name = personName,
                    Faces = new List<string>() // IDE0028 fix: Simplified initialization
                };

                Console.WriteLine($"Created person with ID: {person.PersonId}");

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
                        var faceResponse = await _client.AddFaceAsync(directoryId, imageData, person.PersonId);
                        if (faceResponse != null && !string.IsNullOrWhiteSpace(faceResponse.FaceId))
                        {
                            person.Faces.Add(faceResponse.FaceId);
                            Console.WriteLine($"Added face from {filename} with face_id: {faceResponse.FaceId} to person_id: {person.PersonId}");
                        }
                        else
                        {
                            Console.Error.WriteLine($"Error: Failed to add face from {filename} to person_id: {person.PersonId}. FaceId was not returned.");
                            // Stop execution as a valid FaceId is required for further processing.
                            throw new Exception($"Failed to add face from {filename} to person_id: {person.PersonId}. FaceId was not returned.");
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Failed to add face from {filename} to person_id: {person.PersonId}");
                        throw;
                    }
                }

                persons.Add(person);
            }

            Console.WriteLine($"\nCompleted building person directory with {subFolders.Length} persons");
            return persons;
        }

        /// <summary>
        /// Asynchronously identifies persons in a given image using a specified directory of known individuals.
        /// </summary>
        /// <remarks>Detect multiple faces in an image and identify each one by matching it against enrolled persons in the Person Directory.</remarks>
        /// <param name="directoryId">The identifier of the directory containing known individuals for comparison.</param>
        /// <param name="imagePath">The file path to the image in which persons are to be identified.</param>
        /// <returns></returns>
        public async Task<List<DetectedFace>> IdentifyPersonsInImageAsync(string directoryId, string imagePath)
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

                        if (candidate.PersonId != null)
                        {
                            // Get person details to get name tag
                            var person = await _client.GetPersonAsync(directoryId, candidate.PersonId);
                            var name = person.Tags.TryGetValue("name", out var n) ? n : "Unknown";

                            Console.WriteLine($"Identified as: {name} (Confidence: {candidate.Confidence}, Person ID: {candidate.PersonId})");
                        }
                        else
                        {
                            Console.WriteLine("No person identified in directory");
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Person not identified in directory");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error identifying person: {ex.Message}");
                    throw;
                }
            }

            Console.WriteLine("Identification completed");
            return detectedFaces;
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
        public async Task<FaceResponse?> AddNewFaceToPersonAsync(string directoryId, string? personId, string newFaceImagePath)
        {
            Console.WriteLine("\nAdding new face to existing person...");

            if (!File.Exists(newFaceImagePath))
            {
                Console.WriteLine($"File not found: {newFaceImagePath}");
                return null;
            }

            try
            {
                // Convert the new face image to base64
                var imageData = AzureContentUnderstandingFaceClient.ReadFileToBase64(newFaceImagePath);
                // Add the new face to the person directory and associate it with the existing person
                var faceResponse = await _client.AddFaceAsync(directoryId, imageData, personId);
                Console.WriteLine($"Added face from {newFaceImagePath} with face_id: {faceResponse.FaceId} to person_id: {personId}");
                return faceResponse;
            }
            catch
            {
                Console.WriteLine($"Failed to add face from {newFaceImagePath} to person_id: {personId}");
                throw;
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
        /// Asynchronously adds a new face to the specified person directory using base64-encoded image data.
        /// </summary>
        /// <remarks>This method allows adding a face to the person directory either as an unassociated face 
        /// or associated with a specific person. If no person ID is provided, the face will be added without 
        /// any person association and can be associated later using other methods.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory where the face will be added. 
        /// This value cannot be null or empty.</param>
        /// <param name="imageData">The base64-encoded image data representing the face to be added. 
        /// This value cannot be null or empty.</param>
        /// <param name="personId">The optional unique identifier of the person to associate with the face. 
        /// If null, the face will be added without a specific person association.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a 
        /// <see cref="FaceResponse"/> object with details about the added face, including its unique identifier.</returns>
        public async Task<FaceResponse> AddFaceAsync(string personDirectoryId, string imageData, string? personId = null)
        {
            return await _client.AddFaceAsync(personDirectoryId, imageData, personId);
        }

        /// <summary>
        /// Asynchronously retrieves information about a specific person from the specified person directory.
        /// </summary>
        /// <param name="personDirectoryId">The identifier of the person directory containing the person.</param>
        /// <param name="personId">The identifier of the person to retrieve information for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PersonResponse"/>
        /// with details about the person.</returns>
        public async Task<PersonResponse> GetPersonAsync(string personDirectoryId, string personId)
        {
            PersonResponse personResponse = await _client.GetPersonAsync(personDirectoryId, personId);
            return personResponse;
        }

        /// <summary>
        /// Adds a new person to the specified directory with the provided tags.
        /// </summary>
        /// <remarks>This method is asynchronous and should be awaited. Ensure that the directory ID  and
        /// tags provided are valid and conform to the requirements of the underlying client.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the directory where the person will be added.  This value cannot be null or empty.</param>
        /// <param name="tags">A dictionary containing metadata tags associated with the person.  Keys represent tag names, and values
        /// represent tag values.  This parameter cannot be null.</param>
        /// <returns>A <see cref="PersonResponse"/> object containing details about the added person,  including their unique
        /// identifier and any additional information.</returns>
        public async Task<PersonResponse> AddPersonAsync(string personDirectoryId, Dictionary<string, dynamic> tags)
        {
            PersonResponse personResponse = await _client.AddPersonAsync(personDirectoryId, tags);
            return personResponse;
        }

        /// <summary>
        /// Updates the details of a person in the specified directory.
        /// </summary>
        /// <remarks>This method asynchronously updates the person's information in the specified
        /// directory. Ensure that the provided identifiers and optional data are valid and conform to the expected
        /// format.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the directory containing the person. Cannot be null or empty.</param>
        /// <param name="personId">The unique identifier of the person to update. Cannot be null or empty.</param>
        /// <param name="tags">An optional dictionary of tags to associate with the person. Keys represent tag names, and values represent
        /// tag values. If null, existing tags will remain unchanged.</param>
        /// <param name="faceIds">An optional list of face identifiers to associate with the person. If null, existing face associations will
        /// remain unchanged.</param>
        /// <returns>A <see cref="PersonResponse"/> object containing the updated details of the person, or <see
        /// langword="null"/> if the update operation fails.</returns>
        public async Task<HttpResponseMessage?> UpdatePersonAsync(string personDirectoryId, string personId, Dictionary<string, dynamic>? tags = null, List<string>? faceIds = null)
        {
            HttpResponseMessage? personResponse = await _client.UpdatePersonAsync(personDirectoryId, personId, tags, faceIds);
            return personResponse;
        }

        /// <summary>
        /// Deletes a person from the specified directory.
        /// </summary>
        /// <remarks>This method performs an asynchronous operation to delete a person from the specified
        /// directory. Ensure that the identifiers provided are valid and correspond to existing entities.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the directory containing the person to be deleted. This value cannot be null or
        /// empty.</param>
        /// <param name="personId">The unique identifier of the person to be deleted. This value cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DeletePersonAsync(string personDirectoryId, string personId)
        {
            await _client.DeletePersonAsync(personDirectoryId, personId);
        }

        /// <summary>
        /// Associates a list of existing face IDs with a specified person in a directory.
        /// </summary>
        /// <remarks>You can associate a list of already enrolled faces in the Person Directory with their respective persons. This is useful if you have existing face IDs to link to specific persons.</remarks>
        /// <param name="directoryId">The identifier of the directory containing the person.</param>
        /// <param name="personId">The unique ID of the person to whom the face should be associated.</param>
        /// <param name="faceIds">The list of face IDs to be associated.</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage?> AssociateExistingFacesAsync(string directoryId, string? personId, List<string> faceIds)
        {
            Console.WriteLine("\nAssociating existing faces to person...");

            if (faceIds == null || faceIds.Count == 0)
            {
                Console.WriteLine("No face IDs provided");
                return null;
            }

            try
            {
                // Associate the existing face IDs with the existing person
                HttpResponseMessage? response = await _client.UpdatePersonAsync(
                    directoryId,
                    personId,
                    faceIds: faceIds
                );
                Console.WriteLine($"Associated {faceIds.Count} faces to person {personId}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error associating faces: {ex.Message}");
                throw;
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
        public async Task<FaceResponse?> UpdateFaceAssociationAsync(string directoryId, string faceId, string? personId = null)
        {
            Console.WriteLine("\nUpdating face association...");

            try
            {
                // Remove the association of the existing face ID from the person or associate the existing face ID with a person.
                await _client.UpdateFaceAsync(directoryId, faceId, personId ?? "");

                var action = string.IsNullOrEmpty(personId) ?
                    "Disassociated" : "Associated";

                Console.WriteLine($"{action} face {faceId} to person: {personId ?? "None"}");
                FaceResponse json = await _client.GetFaceAsync(directoryId, faceId);
                Console.WriteLine($"The face information with the new person association: {JsonSerializer.Serialize(json)}");

                // Return the updated face information
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating face association: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates metadata for a specified directory and optionally for a person within the directory.
        /// </summary>
        /// <remarks>This method updates the metadata for the specified directory and, if a <paramref
        /// name="personId"/> is provided, also updates the metadata for the corresponding person within the directory.
        /// The metadata updates are performed asynchronously.  If <paramref name="personId"/> is null or empty, only
        /// the directory metadata is updated.</remarks>
        /// <param name="directoryId">The unique identifier of the directory whose metadata is to be updated. Cannot be null or empty.</param>
        /// <param name="personId">The unique identifier of the person whose metadata is to be updated. If null, only the directory metadata is
        /// updated.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<PersonResponse?> UpdateMetadataAsync(string directoryId, string? personId = null)
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

                    PersonResponse response = await _client.GetPersonAsync(directoryId, personId);
                    return response;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating metadata: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a person and all associated faces from the specified directory.
        /// </summary>
        /// <remarks>This method retrieves the specified person and deletes all associated faces before
        /// deleting the person. If the person does not exist or has no associated faces, the method will still attempt
        /// to delete the person.</remarks>
        /// <param name="directoryId">The identifier of the directory containing the person and their faces. This value cannot be null or empty.</param>
        /// <param name="personId">The identifier of the person to delete. This value cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<PersonResponse> DeleteFaceAndPersonAsync(string directoryId, string personId)
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

                // Get person to confirm deletion
                PersonResponse response = await _client.GetPersonAsync(directoryId, personId);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deletion: {ex.Message}");
                throw;
            }
        }
    }
}
