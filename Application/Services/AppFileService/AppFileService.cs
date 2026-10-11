using Application.Dtos.AppFile;
using Application.Configuration;
using Application.Extensions;
using Application.Services.ApplicationLogService;
using Application.Services.MessagingService;
using Application.Services.StorageService;
using Application.Types;
using Application.Workers;
using Domain.Entitites.ApplicationContextDb;
using Domain.Queues.AppFileDtos;
using Infrastructure.Context;
using Infrastructure.Mediators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Web.Api.Toolkit.Entity.Infraestructure.Repositories;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Ws.Application.Dtos;

namespace Application.Services.AppFileService
{
    public class AppFileService : IAppFileService
    {
        private readonly IBaseRepository<AppFile> _appFileRepository;
        private readonly IBaseRepository<AppStoredFile> _appStoredFileRepository;
        private readonly IBaseRepository<StoredFile> _storedFileRepository;
        private readonly ApplicationContext _applicationContext;
        private readonly AppFileWorker _webSocketWorker;
        private readonly IRabbitMqMessagePublisher _messagePublisher;
        private readonly IMinioStorageService _minioStorageService;
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly IApplicationLogService _applicationLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppFileService(
            IBaseRepository<AppFile> appFileRepository,
            IBaseRepository<StoredFile> storedFileRepository,
            IBaseRepository<AppStoredFile> appStoredFileRepository,
            ApplicationContext applicationContext,
            AppFileWorker webSocketWorker,
            IRabbitMqMessagePublisher messagePublisher,
            IMinioStorageService minioStorageService,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IApplicationLogService applicationLogService,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _appFileRepository = appFileRepository;
            _storedFileRepository = storedFileRepository;
            _appStoredFileRepository = appStoredFileRepository;
            _applicationContext = applicationContext;
            _webSocketWorker = webSocketWorker;
            _messagePublisher = messagePublisher;
            _minioStorageService = minioStorageService;
            _rabbitMqOptions = rabbitMqOptions.Value;
            _applicationLogService = applicationLogService;
            _httpContextAccessor = httpContextAccessor;
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

        public async Task<BaseResponse<AppFileResponseDto>> InsertFile(AppFileRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = new AppFile
            {
                Name = req.Name,
                Path = req.Path,
                VersionControl = req.VersionControl,
                Observer = req.Observer,
                AutoValidateSync = req.AutoValidateSync,
                Status = (int)AppFileStatusTypes.Unsynced
            };
            var appFileAddResult = await _appFileRepository.AddAsync(appFile);
            var response = new BaseResponse<AppFileResponseDto>(appFileAddResult is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to insert file. Please check the provided data and try again."));

                await _applicationLogService.AddLogAsync(traceId, "File inserted unsuccessfully", ApplicationLogType.Message, ApplicationLogAction.Error);
            }
            else
            {
                await _applicationLogService.AddLogAsync(traceId, "File inserted successfully", ApplicationLogType.Message, ApplicationLogAction.Success);

                response.Data = new AppFileResponseDto
                {
                    Id = appFile.Id,
                    Name = appFile.Name,
                    Path = appFile.Path,
                    VersionControl = appFile.VersionControl,
                    Observer = appFile.Observer,
                    CreateDate = appFile.CreateDate,
                    UpdateDate = appFile.UpdateDate,
                    UserId = appFile.UserId,
                    AutoValidateSync = appFile.AutoValidateSync,
                    Status = appFile.Status,
                    StatusDetails = appFile.StatusDetails,
                    StatusMessage = appFile.StatusMessage
                };
            }

            return response;
        }

        public BaseResponse<List<AppFileResponseDto>> GetFiles()
        {
            var result = _appFileRepository.Get().OrderByDescending(e => e.Id).ToList();
            var response = new BaseResponse<List<AppFileResponseDto>>();
            var responseMapper = (AppFile e) => new AppFileResponseDto()
            {
                Id = e.Id,
                Name = e.Name,
                Path = e.Path,
                VersionControl = e.VersionControl,
                Observer = e.Observer,
                CreateDate = e.CreateDate,
                UpdateDate = e.UpdateDate,
                AutoValidateSync = e.AutoValidateSync,
                Status = e.Status,
                StatusDetails = e.StatusDetails,
                StatusMessage = e.StatusMessage
            };
            var traceId = _applicationLogService.GetTraceId().Result;

            response.Data = result.Select(responseMapper).ToList();

            return response;
        }

        public BaseResponse<AppFileResponseDto> GetFileById(int id)
        {
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var response = new BaseResponse<AppFileResponseDto>(appFile != null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("File not found"));
                return response;
            }

            response.Data = new AppFileResponseDto
            {
                Id = appFile.Id,
                Name = appFile.Name,
                Path = appFile.Path,
                VersionControl = appFile.VersionControl,
                Observer = appFile.Observer,
                CreateDate = appFile.CreateDate,
                UpdateDate = appFile.UpdateDate,
                AutoValidateSync = appFile.AutoValidateSync,
                Status = appFile.Status,
                StatusDetails = appFile.StatusDetails,
                StatusMessage = appFile.StatusMessage
            };

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
                response.AddError(new ErrorMessage("Failed to update file. The file with the specified ID was not found."));

                await _applicationLogService.AddLogAsync(traceId, "File update failed because file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);
                

                return response;
            }

            appFile.Update(req.Name, req.Path, req.VersionControl, req.Observer, req.AutoValidateSync);

            response.Success = _appFileRepository.Update(appFile);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Failed to update file. Database operation was unsuccessful."));

                await _applicationLogService.AddLogAsync(traceId, "File update failed because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "File updated successfully", ApplicationLogType.Message, ApplicationLogAction.Success);

            await _messagePublisher.PublishAsync(
                _rabbitMqOptions.CommandExchangeName,
                appFile.UserId,
                new DriverCommandMessage
                {
                    Command = DriverCommandTypes.RefreshWatchers,
                    AppFileId = appFile.Id,
                    UserId = appFile.UserId,
                    Path = appFile.Path,
                    TraceId = traceId.ToString()
                }
            );

            response.Data = new AppFileResponseDto
            {
                Id = appFile.Id,
                Name = appFile.Name,
                Path = appFile.Path,
                VersionControl = appFile.VersionControl,
                Observer = appFile.Observer,
                CreateDate = appFile.CreateDate,
                UpdateDate = appFile.UpdateDate,
                UserId = appFile.UserId,
                AutoValidateSync = appFile.AutoValidateSync,
                Status = appFile.Status,
                StatusDetails = appFile.StatusDetails,
                StatusMessage = appFile.StatusMessage
            };

            return response;
        }

        public BaseResponse<List<AppStoredFileResponseDto>> GetAppStoredFiles(int idAppFile)
        {
            var traceId = _applicationLogService.GetTraceId().Result;
            var response = new BaseResponse<List<AppStoredFileResponseDto>>();

            response.Data = _appStoredFileRepository
                .Get()
                .Include(e => e.AppFile)
                .Where(e => e.AppFileId == idAppFile)
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
                ).OrderByDescending(e=>e.CreateDate).ToList();

            return response;
        }

        public async Task<BaseResponse> RequestSync(AppFileSyncRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == req.IdAppFile);
            var response = new BaseResponse(appFile != null);

            if (!response.Success)
            {
                response.Errors.Add(new ErrorMessage("Failed to request synchronization. The file with the specified ID was not found."));
                await _applicationLogService.AddLogAsync(traceId, "Sync request failed because file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);
                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "Sync request initiated", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {req.IdAppFile}, Path: {appFile.Path}");

            // Verificar se já está na fila de processamento
            var alreadyInQueue = _applicationContext.AppFileProcessingQueue.Contains(e => e.AppFileId == req.IdAppFile);
            
            if (alreadyInQueue)
            {
                await _applicationLogService.AddLogAsync(traceId, "File already in processing queue", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {req.IdAppFile}");
                return response;
            }

            // Atualizar status do AppFile para Processing
            appFile.Update(status: (int)AppFileStatusTypes.Processing, statusMessage: AppFileStatusTypes.Processing.GetDescription(), statusDetails: ((int)AppFileStatusTypes.Processing).ToString());
            _appFileRepository.Update(appFile);

            await _messagePublisher.PublishAsync(
                _rabbitMqOptions.CommandExchangeName,
                appFile.UserId,
                new DriverCommandMessage
                {
                    Command = DriverCommandTypes.StartBackup,
                    AppFileId = appFile.Id,
                    UserId = appFile.UserId,
                    Path = appFile.Path,
                    TraceId = traceId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new AppFileUpdateRequestMessage
                    {
                        AppFileId = appFile.Id,
                        Path = appFile.Path
                    })
                }
            );

            var client = _webSocketWorker.GetWebClient(appFile.UserId);

            if (client is not null)
            {
                _webSocketWorker.SendAsync(client.Id, new WebSocketRequest()
                {
                    Body = null,
                    Event = "NewsFilesRequestPing"
                });
            }

            await _applicationLogService.AddLogAsync(traceId, "Sync request sent to driver successfully", ApplicationLogType.Message, ApplicationLogAction.Success);

            return response;
        }

        public async Task<BaseResponse> SingleSync(AppFileStreamFileRequestDto req)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == req.AppFileId);

            if (appFile is null)
            {
                response.AddError(new ErrorMessage("Failed to perform single sync. The app file record was not found."));

                await _applicationLogService.AddLogAsync(traceId, "Single sync failed because AppFile was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            using var memoryStream = new MemoryStream();

            await req.File.CopyToAsync(memoryStream);
            
            var fileBytes = memoryStream.ToArray();

            if (fileBytes.Length == 0)
            {
                response.AddError(new ErrorMessage("Failed to perform single sync. The uploaded file is empty."));
                await _applicationLogService.AddLogAsync(traceId, "Single sync failed because uploaded file is empty", ApplicationLogType.Message, ApplicationLogAction.Error);
                
                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, $"File received with {fileBytes.Length} bytes", ApplicationLogType.Message, ApplicationLogAction.Info);

            var addedStoredFile = _storedFileRepository.AddAsync(new StoredFile()
            {
                Bytes = fileBytes,
                Name = appFile.Name,
                MimeType = "application/zip",
                SizeInBytes = req.OriginalFileSize
            }).Result;

            if (addedStoredFile is null)
            {
                response.AddError(new ErrorMessage("Failed to save stored file. Database operation was unsuccessful."));
                await _applicationLogService.AddLogAsync(traceId, "Single sync failed because StoredFile could not be saved", ApplicationLogType.Message, ApplicationLogAction.Error);
                
                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "Stream received from driver successfully", ApplicationLogType.Message, ApplicationLogAction.Success);

            var appStoredFile = await _appStoredFileRepository.AddAsync(new AppStoredFile
            {
                AppFileId = appFile.Id,
                StoredFileId = addedStoredFile.Id
            });
            
            if (appStoredFile is null)
            {
                response.AddError(new ErrorMessage("Failed to create app stored file record. Database operation was unsuccessful."));
                await _applicationLogService.AddLogAsync    (traceId, "Single sync failed because AppStoredFile could not be created", ApplicationLogType.Message, ApplicationLogAction.Error);
                
                return response;
            }

            appFile.Update(status: (int)AppFileStatusTypes.Synced, statusMessage: AppFileStatusTypes.Synced.GetDescription(), statusDetails: ((int)AppFileStatusTypes.Synced).ToString());

            var appFileUpdatedResult = _appFileRepository.Update(appFile);

            if (!appFileUpdatedResult)
            {
                response.AddError(new ErrorMessage("Failed to update app file status. Database operation was unsuccessful."));

                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, $"AppFile status updated to Synced", ApplicationLogType.Message, ApplicationLogAction.Success);

            var appStoredFileOld = _appStoredFileRepository.Get().Where(e => e.AppFileId == appFile.Id && e.Id != appStoredFile.Id).OrderByDescending(e => e.CreateDate).FirstOrDefault();

            if (appStoredFileOld is not null && !appFile.VersionControl)
            {
                var storedFileOld = _storedFileRepository.Get().FirstOrDefault(e=> e.Id == appStoredFileOld.StoredFileId);

                if (storedFileOld is not null)
                {
                    storedFileOld.SoftDelete();

                    if (!_storedFileRepository.Update(storedFileOld))
                    {
                        response.AddError(new ErrorMessage("Failed to soft delete old stored file record. Database operation was unsuccessful."));

                        return response;
                    }
                }

                appStoredFileOld.SoftDelete();    

                if (!_appStoredFileRepository.Update(appStoredFileOld))
                {
                    response.AddError(new ErrorMessage("Failed to soft delete old app stored file record. Database operation was unsuccessful."));

                    return response;
                }
            }

            var client = _webSocketWorker.GetWebClient(appFile.UserId);

            if (client is not null)
            {
                _webSocketWorker.SendAsync(client.Id, new WebSocketRequest()
                {
                    Body = null,
                    Event = "AppFileUpdatedPing"
                });
            }

            await _applicationLogService.AddLogAsync(traceId, "Processing completed successfully", ApplicationLogType.Message, ApplicationLogAction.Success);

            return response;
        }

        public async Task<BaseResponse> DeleteFile(int id)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var appStoredFiles = _appStoredFileRepository.Get().Where(e => e.AppFileId == id);
            var appStoredFilesId = appStoredFiles.Select(e=>e.StoredFileId).ToList();
            var storedFiles = _storedFileRepository.Get().Where(e => appStoredFilesId.Contains(e.Id));
            var response = new BaseResponse(appFile is not null);

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

            if (!response.Success)
            {
                response.AddError(new ErrorMessage($"Falha ao excluir o arquivo. O arquivo com o ID {id} não foi encontrado."));

                await _applicationLogService.AddLogAsync(traceId, "File deletion failed because file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            appFile.SoftDelete();
                
            var updateResult = _appFileRepository.Update(appFile);
                
            response.Success = updateResult;

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

        public async Task<BaseResponse> DeleteStoredFile(int id)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == id);
            var storedFile = _storedFileRepository.Get().FirstOrDefault(e => e.Id == appStoredFile.StoredFileId);
            var response = new BaseResponse(appStoredFile is not null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage($"Failed to delete stored file. The record with ID {id} was not found."));

                await _applicationLogService.AddLogAsync(traceId, "Stored file deletion failed because record was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            appStoredFile.SoftDelete();
            
            if (storedFile != null)
            {
                storedFile.SoftDelete();

                var updateStoredFileResult = _storedFileRepository.Update(storedFile);
                
                if (!updateStoredFileResult)
                {
                    response.Success = false;

                    response.AddError(new ErrorMessage("Failed to soft delete associated StoredFile. The database operation was unsuccessful."));
                    
                    await _applicationLogService.AddLogAsync(traceId, "StoredFile soft deletion failed because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);

                    return response;
                }
            }
            
            var updateAppStoredFileResult = _appStoredFileRepository.Update(appStoredFile);

            response.Success = updateAppStoredFileResult;

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

        public async Task<BaseResponse> SetAppFileStatus(int appFileId, AppFileStatusTypes status)
        {
            var traceId = await _applicationLogService.GetTraceId();
            
            await _applicationLogService.AddLogAsync(traceId, $"Watcher event received for AppFile status update to {status}", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {appFileId}, Target Status: {status}");
            
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == appFileId);
            var response = new BaseResponse(appFile != null);

            if (!response.Success)
            {
                response.Errors.Add(new ErrorMessage("Failed to request synchronization. The file with the specified ID was not found."));

                await _applicationLogService.AddLogAsync(traceId, "Sync request failed because app file was not found", ApplicationLogType.Message, ApplicationLogAction.Error);

                return response;
            }

            appFile.Update(
                status: (int)status,
                statusMessage: status.GetDescription(),
                statusDetails: ((int)status).ToString()
            );

            var updateResult = _appFileRepository.Update(appFile);

            if (updateResult)
            {
                await _applicationLogService.AddLogAsync(traceId, $"AppFile status updated to {status} successfully", ApplicationLogType.Message, ApplicationLogAction.Success, $"Entity: AppFile, ID: {appFileId}, Path: {appFile.Path}, Status: {status}");
            }
            else
            {
                await _applicationLogService.AddLogAsync(traceId, $"Failed to update AppFile status to {status} because database operation was unsuccessful", ApplicationLogType.Message, ApplicationLogAction.Error);

                response.Errors.Add(new ErrorMessage("Failed to request synchronization. The update is failed."));
            }

            var client = _webSocketWorker.GetWebClient(appFile.UserId);

            if (client is not null)
            {
                _webSocketWorker.SendAsync(client.Id, new WebSocketRequest()
                {
                    Body = null,
                    Event = "AppFileStatusUpdatePing"
                });
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
                response.AddError(new ErrorMessage("Failed to create stored file. The app file was not found."));
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
                StoredFileId = storedFile.Id
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

        public async Task<BaseResponse> HandleDriverEventAsync(DriverEventMessage message)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);

            if (message == null)
            {
                response.AddError(new ErrorMessage("Driver event is required."));
                return response;
            }

            // The driver reports folder states (Synced, PathNotFounded, LockedFiles...) in Status; other
            // values (Complete, Error) are upload states, so those fall back to the event type.
            var status = Enum.TryParse<AppFileStatusTypes>(message.Status, ignoreCase: true, out var reported) && Enum.IsDefined(reported)
                ? reported
                : message.Event switch
                {
                    DriverEventTypes.Completed => AppFileStatusTypes.Synced,
                    DriverEventTypes.Error => AppFileStatusTypes.Unsynced,
                    DriverEventTypes.Progress => AppFileStatusTypes.Processing,
                    _ => AppFileStatusTypes.Processing
                };

            if (message.AppFileId > 0)
            {
                await SetAppFileStatus(message.AppFileId, status);
            }

            await _applicationLogService.AddLogAsync(traceId, $"Driver event handled: {message.Event}", ApplicationLogType.Message, ApplicationLogAction.Info, message.Details ?? message.Message ?? string.Empty);

            return response;
        }

        public async Task<BaseResponse> CheckAppFileStatus(CheckAppFileStatusRequestDto request)
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);
            var appFile = _appFileRepository.Get().FirstOrDefault(e => e.Id == request.AppFileId);

            if (appFile == null)
            {
                response.AddError(new ErrorMessage("AppFile not found."));
                await _applicationLogService.AddLogAsync(traceId, "Status check failed because AppFile was not found", ApplicationLogType.Message, ApplicationLogAction.Error, $"Entity: AppFile, ID: {request.AppFileId}");
                return response;
            }

            await _applicationLogService.AddLogAsync(traceId, "Status check flow initiated for checking AppFile status with driver", ApplicationLogType.Message, ApplicationLogAction.Info, $"Entity: AppFile, ID: {request.AppFileId}, Path: {appFile.Path}, Name: {appFile.Name}");

            // Buscar o último arquivo sincronizado
            var lastSyncedFile = _appStoredFileRepository.Get()
                .Include(e => e.StoredFile)
                .Where(e => e.AppFileId == request.AppFileId && e.StoredFileId != null)
                .OrderByDescending(e => e.CreateDate)
                .FirstOrDefault();

            await _messagePublisher.PublishAsync(
                _rabbitMqOptions.CommandExchangeName,
                appFile.UserId,
                new DriverCommandMessage
                {
                    Command = DriverCommandTypes.ScanDirectories,
                    AppFileId = appFile.Id,
                    UserId = appFile.UserId,
                    Path = appFile.Path,
                    TraceId = traceId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new AppFileStatusCheckAllRequestMessage()
                    {
                        AppFileId = request.AppFileId,
                        Path = appFile.Path,
                        LastSyncedFileSize = lastSyncedFile?.StoredFile?.SizeInBytes,
                        LastSyncedFileDate = lastSyncedFile?.CreateDate
                    })
                }
            );

            await _applicationLogService.AddLogAsync(traceId, "Status check request sent to driver for AppFile successfully", ApplicationLogType.Message, ApplicationLogAction.Success, $"Entity: AppFile, ID: {request.AppFileId}, Path: {appFile.Path}, LastSyncedFileSize: {lastSyncedFile?.StoredFile?.SizeInBytes}, LastSyncedDate: {lastSyncedFile?.CreateDate}");

            return response;
        }

        public async Task<BaseResponse<bool>> DriverIsConnected()
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse<bool>(true);
            var clients = _webSocketWorker.GetClients();
            var driverConnected = clients.Any(e =>
                e.Value.Context != null &&
                e.Value.Context.TryGetValue("type", out var type) && type == "drive"
            );

            response.Data = driverConnected;

            return response;
        }

        public async Task<BaseResponse> DeleteSoftDeletedItems()
        {
            var traceId = await _applicationLogService.GetTraceId();
            var response = new BaseResponse(true);

            try
            {
                var deletedAppFiles = _appFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .ToList();

                var deletedAppStoredFiles = _appStoredFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .Include(e => e.StoredFile)
                    .ToList();

                var deletedStoredFiles = _storedFileRepository.IgnoreMediator(typeof(SoftDeleteMediator<>)).Get()
                    .ToList();

                foreach (var item in deletedAppFiles)
                {
                    _appFileRepository.Remove(item);
                }
                
                foreach (var item in deletedAppStoredFiles)
                {
                    _appStoredFileRepository.Remove(item);
                }
                
                foreach (var item in deletedStoredFiles)
                {
                    _storedFileRepository.Remove(item);
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
