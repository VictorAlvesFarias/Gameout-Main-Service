using Application.Services.DownloadSignatureService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Attributes.DownloadAuth
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ValidateDownloadTokenAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var service = context.HttpContext.RequestServices.GetService(typeof(IDownloadSignatureService)) as IDownloadSignatureService;
            
            if (service == null)
            {
                var response = new BaseResponse();
                
                response.AddError(new ErrorMessage("Download signature service not available", 500));

                context.Result = new ObjectResult(response) { StatusCode = 500 };
                
                return;
            }

            if (!context.HttpContext.Request.Query.TryGetValue("token", out var tokenValue) || string.IsNullOrEmpty(tokenValue))
            {
                var response = new BaseResponse();

                response.AddError(new ErrorMessage("Download token is required", 401));
                
                context.Result = new UnauthorizedObjectResult(response);
                
                return;
            }

            var token = tokenValue.ToString();
            var validationResult = service.ValidateAndExtractClaims(token);
            
            if (!validationResult.Success || validationResult.Data == null)
            {
                var response = new BaseResponse();
                
                foreach (var error in validationResult.Errors)
                {
                    response.AddError(error);
                }
                
                context.Result = new UnauthorizedObjectResult(response);

                return;
            }

            var claims = validationResult.Data;
            var userId = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var fileId = claims.FindFirst("fileId")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                var response = new BaseResponse();
                
                response.AddError(new ErrorMessage("User ID not found in token", 401));
                
                context.Result = new UnauthorizedObjectResult(response);

                return;
            }

            if (string.IsNullOrEmpty(fileId))
            {
                var response = new BaseResponse();

                response.AddError(new ErrorMessage("File ID not found in token", 401));
                
                context.Result = new UnauthorizedObjectResult(response);
                
                return;
            }

            var identity = new ClaimsIdentity(
                new[] 
                { 
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("fileId", fileId)
                }, 
                "DownloadToken"
            );

            context.HttpContext.User = new ClaimsPrincipal(identity);
            context.HttpContext.Items["DownloadAuth_FileId"] = fileId;
            context.HttpContext.Items["DownloadAuth_UserId"] = userId;

            base.OnActionExecuting(context);
        }
    }
}
