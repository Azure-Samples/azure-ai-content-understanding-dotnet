namespace BuildPersonDirectory.Interfaces
{
    public interface IBuildPersonDirectoryService
    {
        string CreatePersonDirectory();

        Task BuildPersonDirectoryAsync(string directoryId);

        Task IdentifyPersonsInImageAsync(string directoryId, string imagePath);

        Task AddNewFaceToPersonAsync(string directoryId, string personId, string newFaceImagePath);

        Task AssociateExistingFacesAsync(string directoryId, string personId, List<string> faceIds);

        Task UpdateFaceAssociationAsync(string directoryId, string faceId, string newPersonId = null);

        Task UpdateMetadataAsync(string directoryId, string personId = null);

        Task DeleteFaceAndPersonAsync(string directoryId, string personId);
    }
}
