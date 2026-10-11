using Application.Dtos.User;
using Application.Services.Identity;
using Domain.Entitites.ApplicationContextDb;
using Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Identity.Application.Configuration;
using Web.Api.Toolkit.Identity.Application.Dtos;
using Web.Api.Toolkit.Identity.Application.Extensions;

namespace Application.Services.IdentityService
{
    public class IdentityService : IIdentityService
    {
        private readonly ApplicationContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<JwtOptions> _jwtOptions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IdentityService(
            ApplicationContext context,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IOptions<JwtOptions> jwtOptions,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _jwtOptions = jwtOptions;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<BaseResponse> PutUser(PutUserRequest userData)
        {
            var response = new BaseResponse();

            if (!_httpContextAccessor.HttpContext.Request.Cookies.TryGetValue("id", out var userId))
            {
                response.AddError(new ErrorMessage("Usuário não autenticado."));
                return response;
            }

            if (userId != userData.IdentityUserId)
            {
                response.AddError(new ErrorMessage("O usuário não tem permissão para atualizar esta conta."));
                return response;
            }

            var user = await _userManager.FindByIdAsync(userData.IdentityUserId);

            if (user == null)
            {
                response.AddError(new ErrorMessage("Faça login novamente e tente mais tarde."));
                return response;
            }

            user.Name = userData.Name ?? user.Name;
            user.Email = userData.Email ?? user.Email;
            user.UserName = userData.Username ?? user.UserName;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                response.AddErrors(result.Errors.ToList().ConvertAll(item => new ErrorMessage(item.Description)));
                return response;
            }

            response.Success = true;
            return response;
        }

        public async Task<BaseResponse> AddUser(CreateUserRequest userData)
        {
            var response = new BaseResponse();

            var validateEmail = await ValidateEmailAsync(userData.Email);
            if (!validateEmail.Success)
                return validateEmail;

            var validateUsername = await ValidateUsernameAsync(userData.Username);
            if (!validateUsername.Success)
                return validateUsername;

            var user = new ApplicationUser
            {
                UserName = userData.Username,
                Email = userData.Email,
                CreateDate = DateTime.UtcNow,
                EmailConfirmed = false,
                Name = userData.Name
            };

            var createdUser = await _userManager.CreateAsync(user, userData.Password);
            response.Success = createdUser.Succeeded;

            if (!response.Success)
                response.AddErrors(createdUser.Errors.ToList().ConvertAll(item => new ErrorMessage(item.Description)));

            return response;
        }

        public async Task<BaseResponse<LoginUserResponse>> LoginAsync(LoginUserRequest loginData)
        {
            var response = new BaseResponse<LoginUserResponse>();
            var user = await _userManager.GetUser(loginData.AccessKey);

            if (user == null)
            {
                response.AddError(new ErrorMessage("Usuário não encontrado."));
                
                return response;
            }

            var login = await _signInManager.PasswordSignInAsync(user, loginData.Password, false, false);

            if (!login.Succeeded)
            {
                response.AddError(new ErrorMessage("Senha ou usuário incorretos."));
                
                return response;
            }

            var token = await _userManager.CreateDefaultToken(user, _jwtOptions);

            response.Data = new LoginUserResponse
            {
                Email = user.Email,
                Name = user.Name,
                Username = user.UserName,
                Id = user.Id,
                Token = token.Data,
                ExpectedExpirationTokenDateTime = DateTime.UtcNow.AddSeconds(_jwtOptions.Value.AccessTokenExpiration),
                ExpirationTokenTime = _jwtOptions.Value.AccessTokenExpiration
            };

            return response;
        }

        public async Task<BaseResponse> DeleteSignedUser(LoginUserRequest loginData)
        {
            var response = new BaseResponse();

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is null) 
            { 
                response.AddError(new ErrorMessage("Usuário não autenticado."));
                return response;
            }

            var user = await _userManager.GetUser(loginData.AccessKey);

            if (user == null)
            {
                response.AddError(new ErrorMessage("Usuário não encontrado."));
                return response;
            }

            var login = await _signInManager.PasswordSignInAsync(user, loginData.Password, false, false);

            if (!login.Succeeded)
            {
                response.AddError(new ErrorMessage("Senha ou usuário incorretos."));
                return response;
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                response.AddErrors(result.Errors.ToList().ConvertAll(item => new ErrorMessage(item.Description)));
            }

            return response;
        }

        public async Task<BaseResponse> ValidateUsernameAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            var response = new BaseResponse(user == null);

            if (!response.Success)
            {
                response.AddError(new ErrorMessage("Nome de usuário já utilizado."));
            }

            return response;
        }

        public async Task<BaseResponse> ValidateEmailAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            var response = new BaseResponse(user == null);

            if (!response.Success)
            { 
                response.AddError(new ErrorMessage("E-mail já utilizado."));
            }

            return response;
        }

        public async Task<BaseResponse> ChangePasswordAsync(ChangePasswordRequest changePasswordData)
        {
            var response = new BaseResponse();

            if (!_httpContextAccessor.HttpContext.Request.Cookies.TryGetValue("id", out var userId))
            {
                response.AddError(new ErrorMessage("Usuário não autenticado."));
                return response;
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                response.AddError(new ErrorMessage("Usuário não encontrado."));
                return response;
            }

            var changed = await _userManager.ChangePasswordAsync(user, changePasswordData.Passowrd, changePasswordData.NewPassword);

            if (!changed.Succeeded) { 
                response.AddErrors(changed.Errors.ToList().ConvertAll(item => new ErrorMessage(item.Description)));
            }

            return response;
        }

        public async Task<BaseResponse<GetUserResponseDto>> GetCurrentUserAsync()
        {
            var response = new BaseResponse<GetUserResponseDto>();
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is null)
            {
                response.AddError(new ErrorMessage("Usuário não autenticado."));

                return response;
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                response.AddError(new ErrorMessage("Usuário não encontrado."));

                return response;
            }

            response.Data = new GetUserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Username = user.UserName,
                CreateDate = user.CreateDate
            };

            return response;
        }
    }
}
