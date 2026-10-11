using Microsoft.AspNetCore.Mvc;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Helpers.Domain.Interfaces;

namespace ASP.NET_Core_Template.Extensions
{
    /// <summary>
    /// File download in the toolkit's response format. Web.Api.Toolkit.Helpers.Api 1.0.59 dropped its
    /// FileResult extension, and the signed download is the only endpoint that needs it.
    /// </summary>
    public static class FileResultExtensions
    {
        public static IActionResult FileResult<T>(this ControllerBase controller, BaseResponse<T> result, bool enableRangeProcessing) where T : IFileBase
        {
            if (!result.Success)
            {
                return controller.StatusCode(result.Errors.FirstOrDefault()?.StatusCode ?? StatusCodes.Status500InternalServerError, result);
            }

            var fileName = string.IsNullOrEmpty(result.Data.Name) ? "download.zip" : result.Data.Name;

            return controller.File(result.Data.Bytes, result.Data.MimeType, fileName, enableRangeProcessing);
        }
    }
}
