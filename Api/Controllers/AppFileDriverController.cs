using Application.Attributes.Trace;
using Application.Dtos.AppFile;
using Application.Services.AppFileService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Api.Toolkit.Helpers.Api.Extensions;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace ASP.NET_Core_Template.Controllers
{
    /// <summary>
    /// What the driver calls (X-API-Key). Every exchange starts on the driver; the backend never calls it.
    /// An upload is: create-app-stored-file (presigned MinIO url), PUT the zip to MinIO, then
    /// complete-app-stored-file, or cancel-app-stored-file when it fails.
    /// </summary>
    [ApiController]
    public class AppFileDriverController : ControllerBase
    {
        public readonly IAppFileService _appFileService;

        public AppFileDriverController
        (
            IAppFileService appFileService
        )
        {
            _appFileService = appFileService;
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpGet("get-trace-id")]
        public async Task<ActionResult<BaseResponse<int>>> GetTraceId()
        {
            var traceId = await _appFileService.CreateTraceId();
            return this.Result(traceId);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("create-app-stored-file")]
        public async Task<ActionResult<BaseResponse<AppStoredFileCreateResponseDto>>> CreateStoredFile([FromBody] AppStoredFileCreateRequestDto request)
        {
            var result = await _appFileService.CreateStoredFile(request);
            return this.Result(result);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("complete-app-stored-file")]
        public async Task<ActionResult<BaseResponse>> CompleteStoredFile([FromBody] AppStoredFileFinishRequestDto request)
        {
            var result = await _appFileService.CompleteStoredFile(request);
            return this.Result(result);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("cancel-app-stored-file")]
        public async Task<ActionResult<BaseResponse>> CancelStoredFile([FromBody] AppStoredFileFinishRequestDto request)
        {
            var result = await _appFileService.CancelStoredFile(request);
            return this.Result(result);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPut("update-appfile-status")]
        public async Task<ActionResult<BaseResponse>> UpdateAppFileStatus([FromBody] UpdateAppFileStatusRequestDto request)
        {
            var result = await _appFileService.SetAppFileStatus(request.AppFileId, request.Status, request.Message);
            return this.Result(result);
        }
    }
}
