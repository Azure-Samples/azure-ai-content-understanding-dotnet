using ContentUnderstanding.Common.Models;

namespace BuildPersonDirectory.Interfaces
{
    public interface IBuildPersonDirectoryService
    {
        Task<string> CreatePersonDirectoryAsync(string directoryId);

        Task<IList<Person>> BuildPersonDirectoryAsync(string directoryId);

        Task IdentifyPersonsInImageAsync(string directoryId, string imagePath);

        Task<FaceResponse> GetFaceAsync(string personDirectoryId, string faceId);

        Task AddNewFaceToPersonAsync(string directoryId, string? personId, string newFaceImagePath);

        Task AssociateExistingFacesAsync(string directoryId, string? personId, List<string> faceIds);

        Task UpdateFaceAssociationAsync(string directoryId, string faceId, string? personId = null);

        Task UpdateMetadataAsync(string directoryId, string? personId = null);

        Task DeleteFaceAndPersonAsync(string directoryId, string personId);
    }
}
