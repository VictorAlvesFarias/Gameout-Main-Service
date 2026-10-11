using Application.Dtos.ApplicationLog;
using Application.Types;
using Domain.Entitites.ApplicationContextDb;
using Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web.Api.Toolkit.Entity.Infraestructure.Repositories;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.ApplicationLogService
{
    public class ApplicationLogService : IApplicationLogService
    {
        private readonly IBaseRepository<Trace> _traceRepository;
        private readonly IBaseRepository<ApplicationLog> _applicationLogRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationLogService(
            IBaseRepository<Trace> traceRepository,
            IBaseRepository<ApplicationLog> applicationLogRepository,
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _traceRepository = traceRepository;
            _applicationLogRepository = applicationLogRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<int> GetTraceId()
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                // Primeiro verifica se o trace id foi colocado nos Items pelo TracedAttribute
                if (httpContext.Items.TryGetValue("X-Trace-Application-Id", out var itemValue) && itemValue != null)
                {
                    if (int.TryParse(itemValue.ToString(), out var itemTraceId))
                    {
                        return itemTraceId;
                    }
                }

                // Depois verifica no header
                if (httpContext.Request.Headers.TryGetValue("X-Trace-Application-Id", out var traceIdValue))
                {
                    if (int.TryParse(traceIdValue.ToString(), out var traceId))
                    {
                        return traceId;
                    }
                }
            }

            var trace = new Trace();
            var result = await _traceRepository.AddAsync(trace);

            return result?.Id ?? 0;
        }

        public async Task AddLogAsync(int traceId, string message, string type, string action)
        {
            // Mantém compatibilidade com código existente, converte string para enum
            var log = new ApplicationLog
            {
                TraceId = traceId,
                Message = message,
                Details = string.Empty,
                Type = ConvertTypeToEnum(type),
                Action = ConvertActionToEnum(action),
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now
            };

            await _applicationLogRepository.AddAsync(log);
        }

        public async Task AddLogAsync(int traceId, string message, ApplicationLogType type, ApplicationLogAction action, string details = "")
        {
            var log = new ApplicationLog
            {
                TraceId = traceId,
                Message = message,
                Details = details ?? string.Empty,
                Type = (int)type,
                Action = (int)action,
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now
            };

            await _applicationLogRepository.AddAsync(log);
        }

        private int ConvertTypeToEnum(string type)
        {
            return type?.ToLower() switch
            {
                "code" => (int)ApplicationLogType.Code,
                "exception" => (int)ApplicationLogType.Exception,
                "json" => (int)ApplicationLogType.Json,
                "query" => (int)ApplicationLogType.Query,
                _ => (int)ApplicationLogType.Message
            };
        }

        private int ConvertActionToEnum(string action)
        {
            return action?.ToLower() switch
            {
                "error" => (int)ApplicationLogAction.Error,
                "warning" => (int)ApplicationLogAction.Warning,
                "success" => (int)ApplicationLogAction.Success,
                _ => (int)ApplicationLogAction.Info
            };
        }

        public BaseResponse<List<ApplicationLogResponseDto>> GetLogsByTraceId(int traceId)
        {
            var logs = _applicationLogRepository.Get()
                .Where(x => x.TraceId == traceId)
                .OrderBy(x => x.CreateDate)
                .Select(x => new ApplicationLogResponseDto
                {
                    Id = x.Id,
                    Message = x.Message,
                    Details = x.Details,
                    Type = ((ApplicationLogType)x.Type).ToString(),
                    Action = ((ApplicationLogAction)x.Action).ToString(),
                    TraceId = x.TraceId,
                    CreateDate = x.CreateDate,
                    UpdateDate = x.UpdateDate,
                    UserId = x.UserId
                })
                .ToList();

            var response = new BaseResponse<List<ApplicationLogResponseDto>>(true)
            {
                Data = logs
            };

            return response;
        }

        public BaseResponse<List<TraceResponseDto>> GetAllTraces()
        {
            var traces = _traceRepository.Get()
                .OrderByDescending(x => x.CreateDate)
                .Select(x => new TraceResponseDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    CreateDate = x.CreateDate,
                    UpdateDate = x.UpdateDate,
                    UserId = x.UserId,
                    LogsCount = x.Logs.Count
                })
                .ToList();

            var response = new BaseResponse<List<TraceResponseDto>>(true)
            {
                Data = traces
            };

            return response;
        }

        public BaseResponse<List<ApplicationLogResponseDto>> GetAllLogs()
        {
            var traces = _applicationLogRepository.Get()
                .OrderByDescending(x => x.CreateDate)
                 .Select(x => new ApplicationLogResponseDto
                 {
                     Id = x.Id,
                     Message = x.Message,
                     Details = x.Details,
                     Type = ((ApplicationLogType)x.Type).ToString(),
                     Action = ((ApplicationLogAction)x.Action).ToString(),
                     TraceId = x.TraceId,
                     CreateDate = x.CreateDate,
                     UpdateDate = x.UpdateDate,
                     UserId = x.UserId
                 })
                .ToList();

            var response = new BaseResponse<List<ApplicationLogResponseDto>>(true)
            {
                Data = traces
            };

            return response;
        }

        public async Task<BaseResponse<ApplicationLogResponseDto>> AddLog(ApplicationLogRequestDto request)
        {
            var traceId = await GetTraceId();

            var log = new ApplicationLog
            {
                TraceId = traceId,
                Message = request.Message,
                Details = request.Details ?? string.Empty,
                Type = (int)request.Type,
                Action = (int)request.Action,
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now
            };

            var result = await _applicationLogRepository.AddAsync(log);

            var responseDto = new ApplicationLogResponseDto
            {
                Id = result.Id,
                Message = result.Message,
                Details = result.Details,
                Type = request.Type.ToString(),
                Action = request.Action.ToString(),
                TraceId = result.TraceId,
                CreateDate = result.CreateDate,
                UpdateDate = result.UpdateDate,
                UserId = result.UserId
            };

            var response = new BaseResponse<ApplicationLogResponseDto>(true)
            {
                Data = responseDto
            };

            return response;
        }

        public async Task<BaseResponse> ClearAllLogs()
        {
            var response = new BaseResponse(true);

            try
            {
                var logs = _applicationLogRepository.Get().ToList();
                var traces = _traceRepository.Get().ToList();

                foreach (var l in logs)
                {
                    _applicationLogRepository.Remove(l);
                }

                foreach (var t in traces)
                {
                    _traceRepository.Remove(t);
                }

                return response;
            }
            catch (Exception ex)
            {
                response.AddError(new ErrorMessage($"Error clearing logs: {ex.Message}"));
                return response;
            }
        }
    }
}

