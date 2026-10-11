using Application.Dtos.User;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Identity.Application.Dtos;

namespace Application.Services.Identity
{
    public interface IIdentityService
    {
        Task<BaseResponse<LoginUserResponse>> LoginAsync(LoginUserRequest loginData);
        Task<BaseResponse> AddUser(CreateUserRequest userData);
        Task<BaseResponse> DeleteSignedUser(LoginUserRequest email);
        Task<BaseResponse> PutUser(PutUserRequest userData);
        Task<BaseResponse> ValidateUsernameAsync(string email);
        Task<BaseResponse> ValidateEmailAsync(string email);
        Task<BaseResponse<GetUserResponseDto>> GetCurrentUserAsync();
        Task<BaseResponse> ChangePasswordAsync(ChangePasswordRequest changePasswordData);
    }
}