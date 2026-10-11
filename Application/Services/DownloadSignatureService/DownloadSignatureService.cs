using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Services.StorageService;
using Domain.Entitites.ApplicationContextDb;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Web.Api.Toolkit.Entity.Infraestructure.Repositories;
using Web.Api.Toolkit.Helpers.Application.Dtos;
using Web.Api.Toolkit.Identity.Application.Configuration;

namespace Application.Services.DownloadSignatureService
{
    public class DownloadSignatureService : IDownloadSignatureService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly IBaseRepository<AppStoredFile> _appStoredFileRepository;
        private readonly IMinioStorageService _minioStorageService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DownloadSignatureService
        (
            IOptions<JwtOptions> jwtOptions,
            IBaseRepository<AppStoredFile> appStoredFileRepository,
            IMinioStorageService minioStorageService,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _jwtOptions = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
            _appStoredFileRepository = appStoredFileRepository;
            _minioStorageService = minioStorageService;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            if (string.IsNullOrEmpty(_jwtOptions.SecurityKey))
            {
                throw new InvalidOperationException("JwtOptions.SecurityKey is not configured");
            }
        }

        public BaseResponse<string> GenerateSignedDownloadUrl(int fileId)
        {
            var response = new BaseResponse<string>();
            var httpContext = _httpContextAccessor.HttpContext;
                
            if (httpContext == null)
            {
                response.AddError(new ErrorMessage("HTTP context is not available"));
                    
                return response;
            }

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
            if (string.IsNullOrEmpty(userId))
            {
                response.AddError(new ErrorMessage("User not authenticated", 401));
                    
                return response;
            }

            var appStoredFile = _appStoredFileRepository.Get().FirstOrDefault(e => e.Id == fileId);

            if (appStoredFile == null || appStoredFile.StoredFileId == null)
            {
                response.AddError(new ErrorMessage("File not found", 404));
                return response;
            }

            var downloadUrl = _minioStorageService.GenerateDownloadUrlAsync(appStoredFile.StoredFileId.Value).GetAwaiter().GetResult();

            if (!downloadUrl.Success)
            {
                response.AddError(new ErrorMessage("Failed to generate download url"));
                return response;
            }

            response.Data = downloadUrl.Data;
    
            return response;
        }

        public BaseResponse<ClaimsPrincipal> ValidateAndExtractClaims(string token)
        {
            var response = new BaseResponse<ClaimsPrincipal>();

            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    response.AddError(new ErrorMessage("Token is required", 401));
                    
                    return response;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtOptions.SecurityKey);
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken ||!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    response.AddError(new ErrorMessage("Invalid token", 401));

                    return response;
                }

                var purposeClaim = principal.FindFirst("purpose")?.Value;
                
                if (purposeClaim != "download")
                {
                    response.AddError(new ErrorMessage("Invalid token purpose", 401));
                
                    return response;
                }

                response.Data = principal;
            }
            catch (SecurityTokenExpiredException)
            {
                response.AddError(new ErrorMessage("Token has expired", 401));
            }
            catch (SecurityTokenException ex)
            {
                response.AddError(new ErrorMessage($"Invalid token: {ex.Message}", 401));
            }

            return response;
        }
    }
}
