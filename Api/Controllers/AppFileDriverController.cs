using Application.Attributes.Trace;
using Application.Dtos.AppFile;
using Application.Services.AppFileService;
using Domain.Queues.AppFileDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Web.Api.Toolkit.Helpers.Api.Extensions;
using Web.Api.Toolkit.Helpers.Application.Dtos;


namespace ASP.NET_Core_Template.Controllers
{
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

        // The driver only knows its API key; the user id is the routing key of its command queue.
        [Authorize(AuthenticationSchemes = "ApiKey")]
        [HttpGet("driver-identity")]
        public ActionResult<BaseResponse<string>> DriverIdentity()
        {
            var response = new BaseResponse<string>(true)
            {
                Data = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };

            return this.Result(response);
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpGet("get-trace-id")]
        public async Task<ActionResult<BaseResponse<int>>> GetTraceId()
        {
            var traceId = await _appFileService.CreateTraceId();
            return this.Result(traceId);
        }

        [Traced]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("stream-file")]
        public async Task<ActionResult<BaseResponse>> UploadFile([FromForm] AppFileStreamFileRequestDto req)
        {
            var result = await _appFileService.SingleSync(req);
            return this.Result(result);
        }
        
        [Traced]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("request-sync")]
        public async Task<ActionResult<BaseResponse>> RequestSync([FromBody] AppFileSyncRequestDto req)
        {
            var result = await _appFileService.RequestSync(req);
            return this.Result(result);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPut("update-appfile-status")]
        public async Task<ActionResult<BaseResponse>> UpdateAppFileStatus([FromBody] UpdateAppFileStatusRequestDto request)
        {
            var result = await _appFileService.SetAppFileStatus(request.AppFileId, request.Status);
            return this.Result(result);
        }
    }
}
