using Application.Dtos.AppFile;
using Application.Dtos.Driver;
using Application.Extensions;
using Application.Services.ApplicationLogService;
using Application.Services.StorageService;
using Application.Types;
using Domain.Entitites.ApplicationContextDb;
using Infrastructure.Mediators;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web.Api.Toolkit.Entity.Infraestructure.Repositories;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.AppFileService
{
    /// <summary>
    /// Groups of saves and their versions. Everything a driver does starts on the driver: it asks for an
    /// upload url, sends the zip to MinIO and confirms it (or cancels), and reports what it finds in the
    /// folder. Nothing is pushed to it.
    /// </summary>
    public class AppFileService : IAppFileService
    {
        private readonly IBaseRepository<AppFile> _appFileRepository;
        private readonly IBaseRepository<AppStoredFile> _appStoredFileRepository;
        private readonly IBaseRepository<StoredFile> _storedFileRepository;
        private readonly IBaseRepository<UserApiKey> _userApiKeyRepository;
        private readonly IMinioStorageService _minioStorageService;
        private readonly IApplicationLogService _applicationLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppFileService(
            IBaseRepository<AppFile> appFileRepository,
            IBaseRepository<StoredFile> storedFileRepository,
            IBaseRepository<AppStoredFile> appStoredFileRepository,
            IBaseRepository<UserApiKey> userApiKeyRepository,
            IMinioStorageService minioStorageService,
            IApplicationLogService applicationLogService,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _appFileRepository = appFileRepository;
            _storedFileRepository = storedFileRepository;
            _appStoredFileRepository = appStoredFileRepository;
            _userApiKeyRepository = userApiKeyRepository;
            _minioStorageService = minioStorageService;
            _applicationLogService = applicationLogService;
            _httpContextAccessor = httpContextAccessor;
        }

        // Dates are written in UTC ("timestamp without time zone" loses the kind): mark them so the JSON
        // carries the "Z" and the driver compares them with its files correctly.
        private static DateTime? AsUtc(DateTime? value)
        {
            return value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        }

        private static AppFileResponseDto ToResponse(AppFile appFile, AppStoredFile? lastVersion = null)
        {
            return new AppFileResponseDto
            {
                Id = appFile.Id,
                Name = appFile.Name,
                VersionControl = appFile.VersionControl,
                CreateDate = appFile.CreateDate,
                UpdateDate = appFile.UpdateDate,
                UserId = appFile.UserId,
                Status = appFile.Status,
                StatusDetails = appFile.StatusDetails,
                StatusMessage = appFile.StatusMessage,
                LastSyncedAt = AsUtc(lastVersion?.CreateDate),
                LastSyncedSize = lastVersion?.StoredFile?.SizeInBytes
            };
        }

        /// <summary>Latest confirmed version of each group.</summary>
        private Dictionary<int, AppStoredFile> LastVersions(List<int> appFileIds)
        {
            return _appStoredFileRepository.Get()
                .Include(e => e.StoredFile)
                .Where(e => e.Completed && appFileIds.Contains(e.AppFileId))
                .ToList()
                .GroupBy(e => e.AppFileId)
                .ToDictionary(e => e.Key, e => e.OrderByDescending(x => x.CreateDate).First());
        }

        public async Task<BaseResponse<int>> CreateTraceId()
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse<int>(true)
            {
                Data = traceId
            };

            return response;
        }

        public BaseResponse<DriverStatusResponseDto> GetDriverStatus()
        {
            var lastSeen = _userApiKeyRepository.Get()
                .Where(e => e.LastUsed != null)
                .Max(e => e.LastUsed);
            var response = new BaseResponse<DriverStatusResponseDto>(true)
            {
                Data = new DriverStatusResponseDto
                {
                    LastSeen = AsUtc(lastSeen)
                }
            };

            return response;
        }

        public async Task<BaseResponse<AppFileResponseDto>> InsertFile(AppFileRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse<AppFileResponseDto>();

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                response.AddError(new ErrorMessage("The group name is required.", 400));

                return response;
            }

            var appFile = new AppFile
            {
                Name = req.Name.Trim(),
                VersionControl = req.VersionControl ?? false,
                Status = (int)AppFileStatusTypes.Unsynced,
                StatusMessage = AppFileStatusTypes.Unsynced.GetDescription(),
                StatusDetails = ((int)AppFileStatusTypes.Unsynced).ToString()
            };
            var appFileAddResult = await _appFileRepository.AddAsync(appFile);

            response.Success = appFileAddResult is not null;

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to insert file. Please check the provided data and try again."));

                await _applicationLogService.AddLogAsync(traceId, "File inserted unsuccessfully", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "File inserted successfully", ApplicationLogType.Message, ApplicationLogAction.Success, $"Entity: AppFile, ID: {appFile.Id}, Name: {appFile.Name}");

            response.Data = ToResponse(appFile);

            return response;
        }

        public BaseResponse<List<AppFileResponseDto>> GetFiles()
        {
            var appFiles = _appFileRepository.Get().OrderByDescending(e => e.Id).ToList();
            var lastVersions = LastVersions(appFiles.Select(e => e.Id).ToList());
            var response = new BaseResponse<List<AppFileResponseDto>>(true)
            {
                Data = appFiles.Select(e => ToResponse(e, lastVersions.GetValueOrDefault(e.Id))).ToList()
            };

            return response;
        }

        public BaseResponse<AppFileResponseDto> GetFileById(int id)
        {
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var response = new BaseResponse<AppFileResponseDto>(appFile != null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("File not found", 404));

                return response;
            }

            response.Data = ToResponse(appFile, LastVersions(new List<int> { appFile.Id }).GetValueOrDefault(appFile.Id));

            return response;
        }

        public async Task<BaseResponse<StoredFile>> DownloadFileWithToken(string token)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext == null)
            {
                var errorResponse = new BaseResponse<StoredFile>();
                errorResponse.AddError(new ErrorMessage("HTTP context not available", 500));
                return errorResponse;
            }

            // O token já foi validado pelo ValidateDownloadTokenAttribute
            // Apenas pegamos o fileId do HttpContext.Items
            if (!httpContext.Items.TryGetValue("DownloadAuth_FileId", out var fileIdObj) ||
                !int.TryParse(fileIdObj?.ToString(), out var fileId))
            {
                var errorResponse = new BaseResponse<StoredFile>();
                errorResponse.AddError(new ErrorMessage("File ID not found in token", 400));
                return errorResponse;
            }

            // O mediator já vai filtrar pelo userId que foi colocado no HttpContext.User pelo attribute
            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == fileId);

            if (appStoredFile == null)
            {
                var errorResponse = new BaseResponse<StoredFile>();
                errorResponse.AddError(new ErrorMessage("File not found or you don't have permission to download it", 404));
                return errorResponse;
            }

            var result = _storedFileRepository.Get().FirstOrDefault(e => e.Id == appStoredFile.StoredFileId);
            var response = new BaseResponse<StoredFile>(result is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to download file. The file may not exist or may be corrupted."));
            }
            else
            {
                // Garantir que o nome do arquivo tenha a extensão .zip
                if (!string.IsNullOrEmpty(result.Name) && !result.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    result.Name = $"{result.Name}.zip";
                }

                response.Data = result;
            }

            return response;
        }

        public async Task<BaseResponse<AppFileResponseDto>> UpdateFile(AppFileRequestDto req, int id)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var response = new BaseResponse<AppFileResponseDto>(appFile is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to update file. The file with the specified ID was not found.", 404));

                await _applicationLogService.AddLogAsync(traceId, "File update failed because file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            appFile.Update(string.IsNullOrWhiteSpace(req.Name) ? null : req.Name.Trim(), req.VersionControl);

            response.Success = _appFileRepository.Update(appFile);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to update file. Database operation was unsuccessful."));

                await _applicationLogService.AddLogAsync(traceId, "File update failed because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "File updated successfully", ApplicationLogType.Message, ApplicationLogAction.Success, $"Entity: AppFile, ID: {appFile.Id}, VersionControl: {appFile.VersionControl}");

            response.Data = ToResponse(appFile, LastVersions(new List<int> { appFile.Id }).GetValueOrDefault(appFile.Id));

            return response;
        }

        public BaseResponse<List<AppStoredFileResponseDto>> GetAppStoredFiles(int idAppFile)
        {
            var response = new BaseResponse<List<AppStoredFileResponseDto>>(true);

            response.Data = _appStoredFileRepository
                .Get()
                .Where(e => e.AppFileId == idAppFile && e.Completed)
                .OrderByDescending(e => e.CreateDate)
                .Select(e =>
                    new AppStoredFileResponseDto()
                    {
                        Id = e.Id,
                        AppFileId = e.AppFileId,
                        CreateDate = e.CreateDate,
                        UpdateDate = e.UpdateDate,
                        StoredFileId = e.StoredFileId,
                        SizeInBytes = e.StoredFile.SizeInBytes
                    }
                )
                .ToList();

            return response;
        }

        public async Task<BaseResponse> DeleteFile(int id)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var response = new BaseResponse(appFile is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage($"Falha ao excluir o arquivo. O arquivo com o ID {id} não foi encontrado.", 404));

                await _applicationLogService.AddLogAsync(traceId, "File deletion failed because file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            var appStoredFiles = _appStoredFileRepository.Get().Where(e => e.AppFileId == id).ToList();
            var appStoredFilesId = appStoredFiles.Select(e => e.StoredFileId).ToList();
            var storedFiles = _storedFileRepository.Get().Where(e => appStoredFilesId.Contains(e.Id)).ToList();

            foreach (var item in storedFiles)
            {
                item.SoftDelete();
                _storedFileRepository.Update(item);
            }

            foreach (var item in appStoredFiles)
            {
                item.SoftDelete();
                _appStoredFileRepository.Update(item);
            }

            appFile.SoftDelete();

            response.Success = _appFileRepository.Update(appFile);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Falha ao excluir o arquivo (soft delete). A operação no banco de dados não foi bem-sucedida."));

                await _applicationLogService.AddLogAsync(traceId, "File soft deletion failed because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);
            }
            else
            {
                await _applicationLogService.AddLogAsync(traceId, "File soft deleted successfully", ApplicationLogType.Message, ApplicationLogAction.Success);
            }

            return response;
        }

        /// <summary>Soft deletes a version and its file record.</summary>
        private bool SoftDeleteVersion(AppStoredFile appStoredFile)
        {
            var storedFile = _storedFileRepository.Get().FirstOrDefault(e => e.Id == appStoredFile.StoredFileId);

            if (storedFile != null)
            {
                storedFile.SoftDelete();

                if (!_storedFileRepository.Update(storedFile))
                {
                    return false;
                }
            }

            appStoredFile.SoftDelete();

            return _appStoredFileRepository.Update(appStoredFile);
        }

        public async Task<BaseResponse> DeleteStoredFile(int id)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var response = new BaseResponse(appStoredFile is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage($"Failed to delete stored file. The record with ID {id} was not found.", 404));

                await _applicationLogService.AddLogAsync(traceId, "Stored file deletion failed because record was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            response.Success = SoftDeleteVersion(appStoredFile);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to soft delete stored file. The database operation was unsuccessful."));

                await _applicationLogService.AddLogAsync(traceId, "Stored file soft deletion failed because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);
            }
            else
            {
                await _applicationLogService.AddLogAsync(traceId, "Stored file soft deleted successfully", ApplicationLogType.Message, ApplicationLogAction.Success);
            }

            return response;
        }

        public async Task<BaseResponse> SetAppFileStatus(int appFileId, AppFileStatusTypes status, string? message = null)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == appFileId);
            var response = new BaseResponse(appFile != null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to update the status. The file with the specified ID was not found.", 404));

                await _applicationLogService.AddLogAsync(traceId, "Status update failed because app file was not found", ApplicationLogType.Message, ApplicationLogAction.Error, $"Entity: AppFile, ID: {appFileId}");

                return response;
            }

            appFile.Update(
                status: (int)status,
                statusMessage: string.IsNullOrWhiteSpace(message) ? status.GetDescription() : message,
                statusDetails: ((int)status).ToString()
            );

            response.Success = _appFileRepository.Update(appFile);

            if (response.Success)
            {
                await _applicationLogService.AddLogAsync(traceId, $"AppFile status updated to {status}", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {appFileId}, Status: {status}, Message: {message}");
            }
            else
            {
                response.AddError(new ErrorMessage("Failed to update the status. Database operation was unsuccessful."));

                await _applicationLogService.AddLogAsync(traceId, $"Failed to update AppFile status to {status} because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);
            }

            return response;
        }

        public async Task<BaseResponse<AppStoredFileCreateResponseDto>> CreateStoredFile(AppStoredFileCreateRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse<AppStoredFileCreateResponseDto>(true);
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == req.AppFileId);

            if (appFile == null)
            {
                response.AddError(new ErrorMessage("Failed to create stored file. The app file was not found.", 404));

                return response;
            }

            var storedFile = await _storedFileRepository.AddAsync(new StoredFile
            {
                Name = $"{appFile.Name}.zip",
                MimeType = "application/zip",
                Bytes = Array.Empty<byte>(),
                SizeInBytes = req.OriginalFileSize
            });

            if (storedFile == null)
            {
                response.AddError(new ErrorMessage("Failed to create stored file metadata."));

                return response;
            }

            var appStoredFile = await _appStoredFileRepository.AddAsync(new AppStoredFile
            {
                AppFileId = req.AppFileId,
                StoredFileId = storedFile.Id,
                Completed = false
            });

            if (appStoredFile == null)
            {
                response.AddError(new ErrorMessage("Failed to create app stored file metadata."));

                return response;
            }

            var uploadUrlResult = await _minioStorageService.GenerateUploadUrlAsync(storedFile.Id);

            if (!uploadUrlResult.Success)
            {
                response.Errors.AddRange(uploadUrlResult.Errors);

                return response;
            }

            await SetAppFileStatus(appFile.Id, AppFileStatusTypes.Processing);

            response.Data = new AppStoredFileCreateResponseDto
            {
                AppStoredFileId = appStoredFile.Id,
                DiskFileId = storedFile.Id,
                Status = 200,
                UploadUrl = uploadUrlResult.Data
            };

            await _applicationLogService.AddLogAsync(traceId, "Stored file metadata created successfully", ApplicationLogType.Message, ApplicationLogAction.Success, $"AppFileId: {req.AppFileId}, StoredFileId: {storedFile.Id}, AppStoredFileId: {appStoredFile.Id}");

            return response;
        }

        public async Task<BaseResponse> CompleteStoredFile(AppStoredFileFinishRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);
            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == req.AppStoredFileId);

            if (appStoredFile == null)
            {
                response.AddError(new ErrorMessage("The upload was not found.", 404));

                return response;
            }

            // Confirming twice (a retried request) changes nothing.
            if (appStoredFile.Completed)
            {
                return response;
            }

            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == appStoredFile.AppFileId);

            if (appFile == null)
            {
                response.AddError(new ErrorMessage("The group of this upload was not found.", 404));

                return response;
            }

            appStoredFile.Complete();

            if (!_appStoredFileRepository.Update(appStoredFile))
            {
                response.AddError(new ErrorMessage("Failed to confirm the upload. Database operation was unsuccessful."));

                return response;
            }

            // Without version control the group keeps only the newest version.
            if (!appFile.VersionControl)
            {
                var previousVersions = _appStoredFileRepository.Get()
                    .Where(e => e.AppFileId == appFile.Id && e.Id != appStoredFile.Id && e.Completed)
                    .ToList();

                foreach (var item in previousVersions)
                {
                    if (!SoftDeleteVersion(item))
                    {
                        response.AddError(new ErrorMessage("Failed to remove the previous version. Database operation was unsuccessful."));

                        return response;
                    }
                }

                if (previousVersions.Count > 0)
                {
                    await _applicationLogService.AddLogAsync(traceId, $"{previousVersions.Count} previous version(s) replaced", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {appFile.Id}");
                }
            }

            await SetAppFileStatus(appFile.Id, AppFileStatusTypes.Synced);

            await _applicationLogService.AddLogAsync(traceId, "Upload confirmed", ApplicationLogType.Message, ApplicationLogAction.Success, $"AppFileId: {appFile.Id}, AppStoredFileId: {appStoredFile.Id}");

            return response;
        }

        public async Task<BaseResponse> CancelStoredFile(AppStoredFileFinishRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);
            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == req.AppStoredFileId);

            if (appStoredFile == null)
            {
                response.AddError(new ErrorMessage("The upload was not found.", 404));

                return response;
            }

            if (appStoredFile.Completed)
            {
                response.AddError(new ErrorMessage("The upload was already confirmed.", 409));

                return response;
            }

            if (!SoftDeleteVersion(appStoredFile))
            {
                response.AddError(new ErrorMessage("Failed to cancel the upload. Database operation was unsuccessful."));

                return response;
            }

            await SetAppFileStatus(appStoredFile.AppFileId, AppFileStatusTypes.Unsynced, req.Message);

            await _applicationLogService.AddLogAsync(traceId, "Upload cancelled", ApplicationLogType.Message, ApplicationLogAction.Warning, $"AppFileId: {appStoredFile.AppFileId}, AppStoredFileId: {appStoredFile.Id}, Message: {req.Message}");

            return response;
        }

        public async Task<BaseResponse> DeleteSoftDeletedItems()
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);

            try
            {
                // IgnoreMediator turns the soft delete filter off, so only rows marked Deleted are taken.
                var deletedAppFiles = _appFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .Where(e => e.Deleted)
                    .ToList();

                var deletedAppStoredFiles = _appStoredFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .Where(e => e.Deleted)
                    .ToList();

                var deletedStoredFiles = _storedFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .Where(e => e.Deleted)
                    .ToList();

                foreach (var item in deletedAppStoredFiles)
                {
                    _appStoredFileRepository.Remove(item);
                }

                foreach (var item in deletedStoredFiles)
                {
                    _storedFileRepository.Remove(item);
                }

                foreach (var item in deletedAppFiles)
                {
                    _appFileRepository.Remove(item);
                }

                var totalDeleted = deletedAppFiles.Count + deletedAppStoredFiles.Count + deletedStoredFiles.Count;

                await _applicationLogService.AddLogAsync(
                    traceId,
                    $"Permanently deleted {totalDeleted} items ({deletedAppFiles.Count} AppFiles, {deletedAppStoredFiles.Count} AppStoredFiles, {deletedStoredFiles.Count} StoredFiles)",
                    ApplicationLogType.Message,
                    ApplicationLogAction.Success
                );
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.AddError(new ErrorMessage($"Failed to delete items permanently: {ex.Message}"));
                await _applicationLogService.AddLogAsync(traceId, "Permanent deletion failed", ApplicationLogType.Message, ApplicationLogAction.Error, ex.Message);
            }

            return response;
        }
    }
}
