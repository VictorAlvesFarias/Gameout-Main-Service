using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.StorageService
{
    public interface IMinioStorageService
    {
        Task<BaseResponse<string>> GenerateUploadUrlAsync(int storedFileId);
        Task<BaseResponse<string>> GenerateDownloadUrlAsync(int storedFileId);
        string GetObjectKey(int storedFileId);
    }
}