using ContentUnderstanding.Common.Models;

namespace BuildPersonDirectory.Interfaces
{
    public interface IBuildPersonDirectoryService
    {
        Task<HttpResponseMessage> CreatePersonDirectoryAsync(string directoryId);

        Task<List<Person>> BuildPersonDirectoryAsync(string directoryId);

        Task<List<DetectedFace>> IdentifyPersonsInImageAsync(string directoryId, string imagePath);

        Task<FaceResponse> GetFaceAsync(string personDirectoryId, string faceId);

        Task<FaceResponse> AddFaceAsync(string personDirectoryId, string imageData, string? personId = null);

        Task<PersonResponse> GetPersonAsync(string personDirectoryId, string personId);

        Task<PersonResponse> AddPersonAsync(string personDirectoryId, Dictionary<string, dynamic> tags);

        Task DeletePersonAsync(string personDirectoryId, string personId);

        Task<HttpResponseMessage?> UpdatePersonAsync(string personDirectoryId, string personId, Dictionary<string, dynamic>? tags = null, List<string>? faceIds = null);

        Task<FaceResponse?> AddNewFaceToPersonAsync(string directoryId, string? personId, string newFaceImagePath);

        Task<HttpResponseMessage?> AssociateExistingFacesAsync(string directoryId, string? personId, List<string> faceIds);

        Task<FaceResponse?> UpdateFaceAssociationAsync(string directoryId, string faceId, string? personId = null);

        Task<PersonResponse?> UpdateMetadataAsync(string directoryId, string? personId = null);

        Task<PersonResponse> DeleteFaceAndPersonAsync(string directoryId, string personId);
    }
}
