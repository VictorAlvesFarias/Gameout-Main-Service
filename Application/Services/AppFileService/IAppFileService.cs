using Application.Dtos.AppFile;
using Application.Types;
using Domain.Entitites.ApplicationContextDb;
using Domain.Queues.AppFileDtos;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.AppFileService
{
    public interface IAppFileService
    {
        Task<BaseResponse<int>> CreateTraceId();
        Task<BaseResponse<bool>> DriverIsConnected();
        BaseResponse<List<AppFileResponseDto>> GetFiles();
        BaseResponse<AppFileResponseDto> GetFileById(int id);
        BaseResponse<List<AppStoredFileResponseDto>> GetAppStoredFiles(int idAppFile);
        Task<BaseResponse<AppFileResponseDto>> InsertFile(AppFileRequestDto req);
        Task<BaseResponse<AppFileResponseDto>> UpdateFile(AppFileRequestDto req, int id);
        Task<BaseResponse<StoredFile>> DownloadFileWithToken(string token);
        Task<BaseResponse<AppStoredFileCreateResponseDto>> CreateStoredFile(AppStoredFileCreateRequestDto req);
        Task<BaseResponse> RequestSync(AppFileSyncRequestDto req);
        Task<BaseResponse> DeleteFile(int id);
        Task<BaseResponse> DeleteStoredFile(int id);
        Task<BaseResponse> SingleSync(AppFileStreamFileRequestDto req);
        Task<BaseResponse> SetAppFileStatus(int appFileId, AppFileStatusTypes status);
        Task<BaseResponse> CheckAppFileStatus(CheckAppFileStatusRequestDto request);
        Task<BaseResponse> HandleDriverEventAsync(DriverEventMessage message);
        Task<BaseResponse> DeleteSoftDeletedItems();
    }
}
