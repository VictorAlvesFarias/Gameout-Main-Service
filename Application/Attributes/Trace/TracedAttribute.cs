using Application.Services.ApplicationLogService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Attributes.Trace
{
    public class TracedAttribute : ActionFilterAttribute
    {
        private const string HEADER_NAME = "X-Trace-Application-Id";

        public bool OnCreate { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var traceValue = string.Empty;
            var response = new BaseResponse();

            if (context.HttpContext.Request.Headers.TryGetValue(HEADER_NAME, out var value))
            {
                traceValue = value.ToString();
            }

            if (string.IsNullOrEmpty(traceValue) && OnCreate)
            {
                var service = context.HttpContext.RequestServices.GetService(typeof(IApplicationLogService)) as IApplicationLogService;
                var id = service.GetTraceId().GetAwaiter().GetResult();
                traceValue = id.ToString();
            }

            if (string.IsNullOrEmpty(traceValue) && !OnCreate)
            {
                response.Success = false;
                response.AddError(new ErrorMessage($"The {HEADER_NAME} header was not found."));

                context.Result = new ObjectResult(response)
                {
                    StatusCode = 500
                };

                return;
            }

            context.HttpContext.Items[HEADER_NAME] = traceValue;

            base.OnActionExecuting(context);
        }
    }
}
