using Application.Services.Identity;
using Application.Services.IdentityService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Api.Toolkit.Helpers.Api.Extensions;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Identity.Application.Dtos;

namespace ASP.NET_Core_Template.Controllers
{
    public class UserController : Controller
    {

        private readonly IIdentityService _identityService;

        public UserController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [AllowAnonymous]
        [HttpPost("/create-user")]
        public async Task<ActionResult<BaseResponse>> CreateUser([FromBody] CreateUserRequest userData)
        {
            var result = await _identityService.AddUser(userData);

            return this.Result(result);
        }

        [HttpPost("/sign-in")]
        public async Task<ActionResult<BaseResponse<LoginUserResponse>>> LoginUser([FromBody] LoginUserRequest loginData)
        {
            var result = await _identityService.LoginAsync(loginData);

            return this.Result(result);
        }

        [Authorize]
        [HttpDelete("/delete-account")]
        public async Task<ActionResult<BaseResponse>> DeleteUser([FromBody] LoginUserRequest loginData)
        {
            var result = await _identityService.DeleteSignedUser(loginData);

            return this.Result(result);
        }

        [Authorize]
        [HttpGet("/get-current-user")]
        public async Task<ActionResult<BaseResponse<Application.Dtos.User.GetUserResponseDto>>> GetCurrentUser()
        {
            var result = await _identityService.GetCurrentUserAsync();

            return this.Result(result);
        }

        [Authorize]
        [HttpPut("/update-account")]
        public async Task<ActionResult<BaseResponse>> UpdateUser([FromBody] PutUserRequest userData)
        {
            var result = await _identityService.PutUser(userData);

            return this.Result(result);
        }

        [Authorize]
        [HttpPost("/validade-email")]
        public async Task<ActionResult<BaseResponse>> ValidateEmail([FromBody] ValidateEmailRequest validate)
        {
            var result = await _identityService.ValidateEmailAsync(validate.Email);

            return this.Result(result);
        }

        [Authorize]
        [HttpPost("/validate-username")]
        public async Task<ActionResult<BaseResponse>> ValidateUsername([FromBody] ValidateUsernameRequest validate)
        {
            var result = await _identityService.ValidateUsernameAsync(validate.Username);

            return this.Result(result);
        }

        [Authorize]
        [HttpPost("/change-password")]
        public async Task<ActionResult<BaseResponse>> ChangePassword([FromBody] ChangePasswordRequest changePasswordData)
        {
            var result = await _identityService.ChangePasswordAsync(changePasswordData);

            return this.Result(result);
        }
    }
}
