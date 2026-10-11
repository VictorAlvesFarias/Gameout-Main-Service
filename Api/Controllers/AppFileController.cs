using Application.Attributes.DownloadAuth;
using Application.Attributes.Trace;
using ASP.NET_Core_Template.Extensions;
using Application.Dtos.AppFile;
using Application.Services.AppFileService;
using Application.Services.DownloadSignatureService;
using Domain.Entitites.ApplicationContextDb;
using Domain.Queues.AppFileDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Api.Toolkit.Helpers.Api.Extensions;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace ASP.NET_Core_Template.Controllers
{
    public class AppFileController : Controller
    {
        public readonly IAppFileService _appFileService;
        private readonly IDownloadSignatureService _downloadSignatureService;
        private readonly Controller _controller;

        public AppFileController
        (
            IAppFileService appFileService,
            IDownloadSignatureService downloadSignatureService
        )
        {
            _appFileService = appFileService;
            _downloadSignatureService = downloadSignatureService;
            _controller = this;
        }

        [Authorize]
        [HttpPost("upload-file")]
        public async Task<ActionResult<BaseResponse<AppFileResponseDto>>> UploadFile([FromBody] AppFileRequestDto request)
        {
            var result = await _appFileService.InsertFile(request);
            return this.Result(result);
        }

        [Authorize]
        [HttpPut("update-file")]
        public async Task<ActionResult<BaseResponse<AppFileResponseDto>>> UpdateFile([FromBody] AppFileRequestDto request, int id)
        {
            var result = await _appFileService.UpdateFile(request, id);
            return this.Result(result);
        }

        [Authorize]
        [HttpDelete("delete-file")]
        public async Task<ActionResult<BaseResponse>> DeleteFile(int id)
        {
            var result = await _appFileService.DeleteFile(id);
            return this.Result(result);
        }

        [Authorize]
        [HttpDelete("delete-stored-file")]
        public async Task<ActionResult<BaseResponse>> DeleteStoredFile(int id)
        {
            var result = await _appFileService.DeleteStoredFile(id);
            return this.Result(result);
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpGet("get-files")]
        public ActionResult<BaseResponse<List<AppFileResponseDto>>> GetFiles()
        {
            var result = _appFileService.GetFiles();
            return this.Result(result);
        }

        [Authorize]
        [HttpGet("get-file-by-id")]
        public ActionResult<BaseResponse<AppFileResponseDto>> GetFileById(int id)
        {
            var result = _appFileService.GetFileById(id);
            return this.Result(result);
        }

        [Authorize]
        [HttpGet("get-stored-files")]
        public ActionResult<BaseResponse<List<AppStoredFileResponseDto>>> GetStoredFiles(int idAppFile)
        {
            var result = _appFileService.GetAppStoredFiles(idAppFile);
            return this.Result(result);
        }

        [Authorize]
        [HttpPost("single-sync")]
        public async Task<ActionResult<BaseResponse>> SingleSync([FromBody] AppFileSyncRequestDto req)
        {
            var result = await _appFileService.RequestSync(req);
            return this.Result(result);
        }

        [Authorize]
        [HttpGet("generate-download-url")]
        public ActionResult<BaseResponse<string>> GenerateDownloadUrl(int id)
        {
            var result = _downloadSignatureService.GenerateSignedDownloadUrl(id);
            return this.Result(result);
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("create-app-stored-file")]
        public async Task<ActionResult<BaseResponse<AppStoredFileCreateResponseDto>>> CreateStoredFile([FromBody] AppStoredFileCreateRequestDto request)
        {
            var result = await _appFileService.CreateStoredFile(request);
            return this.Result(result);
        }

        [HttpGet("download-file-signed")]
        [ValidateDownloadToken]
        public async Task<IActionResult> DownloadFileSigned(string token)
        {
            var result = await _appFileService.DownloadFileWithToken(token);
            return this.FileResult(result, true);
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("check-app-file-status")]
        public async Task<ActionResult<BaseResponse>> CheckAppFileStatus([FromBody] CheckAppFileStatusRequestDto request)
        {
            var result = await _appFileService.CheckAppFileStatus(request);
            return this.Result(result);
        }

        [Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
        [HttpPost("driver-is-connected")]
        public async Task<ActionResult<BaseResponse<bool>>> DriverIsConnected()
        {
            var result = await _appFileService.DriverIsConnected();
            return this.Result(result);
        }

        [Authorize]
        [HttpDelete("delete-soft-deleted-items")]
        public async Task<ActionResult<BaseResponse>> DeleteSoftDeletedItems()
        {
            var result = await _appFileService.DeleteSoftDeletedItems();
            return this.Result(result);
        }
    }
}
