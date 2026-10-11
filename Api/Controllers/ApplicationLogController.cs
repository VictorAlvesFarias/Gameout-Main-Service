using Application.Attributes.Trace;
using Application.Dtos.ApplicationLog;
using Application.Dtos.Pagination;
using Application.Services.ApplicationLogService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Api.Toolkit.Helpers.Api.Extensions;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace ASP.NET_Core_Template.Controllers
{
    [ApiController]
    [Route("api/application-log")]
    [Authorize]
    public class ApplicationLogController : ControllerBase
    {
        private readonly IApplicationLogService _applicationLogService;

        public ApplicationLogController(IApplicationLogService applicationLogService)
        {
            _applicationLogService = applicationLogService;
        }

        // Read by the web and by the driver.
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpGet("traces")]
        public ActionResult<BaseResponse<PagedResponseDto<ApplicationLogResponseDto>>> GetLogs([FromQuery] ApplicationLogFilterDto filter)
        {
            var result = _applicationLogService.GetLogs(filter);
            return this.Result(result);
        }

        [Traced(OnCreate = true)]
        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("add")]
        public async Task<ActionResult<BaseResponse<ApplicationLogResponseDto>>> AddLog([FromBody] ApplicationLogRequestDto request)
        {
            var result = await _applicationLogService.AddLog(request);
            return this.Result(result);
        }

        [Authorize]
        [HttpDelete("clear")]
        public async Task<ActionResult<BaseResponse>> ClearAllLogs()
        {
            var result = await _applicationLogService.ClearAllLogs();
            return this.Result(result);
        }
    }
}

