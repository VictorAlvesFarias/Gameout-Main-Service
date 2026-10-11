using Application.Dtos.AppFile;
using Application.Dtos.Driver;
using Application.Types;
using Domain.Entitites.ApplicationContextDb;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.AppFileService
{
    public interface IAppFileService
    {
        Task<BaseResponse<int>> CreateTraceId();
        BaseResponse<DriverStatusResponseDto> GetDriverStatus();
        BaseResponse<List<AppFileResponseDto>> GetFiles();
        BaseResponse<AppFileResponseDto> GetFileById(int id);
        BaseResponse<List<AppStoredFileResponseDto>> GetAppStoredFiles(int idAppFile);
        Task<BaseResponse<AppFileResponseDto>> InsertFile(AppFileRequestDto req);
        Task<BaseResponse<AppFileResponseDto>> UpdateFile(AppFileRequestDto req, int id);
        Task<BaseResponse<StoredFile>> DownloadFileWithToken(string token);
        Task<BaseResponse<AppStoredFileCreateResponseDto>> CreateStoredFile(AppStoredFileCreateRequestDto req);
        Task<BaseResponse> CompleteStoredFile(AppStoredFileFinishRequestDto req);
        Task<BaseResponse> CancelStoredFile(AppStoredFileFinishRequestDto req);
        Task<BaseResponse> DeleteFile(int id);
        Task<BaseResponse> DeleteStoredFile(int id);
        Task<BaseResponse> SetAppFileStatus(int appFileId, AppFileStatusTypes status, string? message = null);
        Task<BaseResponse> DeleteSoftDeletedItems();
    }
}
