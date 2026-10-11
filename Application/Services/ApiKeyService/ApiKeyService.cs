using Domain.Entitites.ApplicationContextDb;
using Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using Web.Api.Toolkit.Helpers.Application.Dtos;

namespace Application.Services.ApiKeyService
{
    public class ApiKeyService : IApiKeyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiKeyService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor
        )
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<BaseResponse<string>> GenerateApiKeyAsync()
        {
            var response = new BaseResponse<string>();
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is null)
            {
                response.AddError(new ErrorMessage("Usuário não autenticado."));
                return response;
            }

            // Invalidar API key anterior se existir
            var existingApiKey = _context.Set<UserApiKey>()
                .FirstOrDefault(x => x.UserId == userId && x.IsActive);

            if (existingApiKey != null)
            {
                existingApiKey.IsActive = false;
                existingApiKey.UpdateDate = DateTime.UtcNow;
                _context.Set<UserApiKey>().Update(existingApiKey);
            }

            // Gerar nova API key
            var newApiKey = GenerateSecureApiKey();
            var userApiKey = new UserApiKey
            {
                UserId = userId,
                ApiKey = newApiKey,
                IsActive = true,
                LastUsed = null,
                CreateDate = DateTime.UtcNow,
                UpdateDate = DateTime.UtcNow
            };

            await _context.Set<UserApiKey>().AddAsync(userApiKey);
            await _context.SaveChangesAsync();

            response.Data = newApiKey;
            response.Success = true;

            return response;
        }

        public async Task<BaseResponse<string>> GetCurrentApiKeyAsync()
        {
            var response = new BaseResponse<string>();
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is null)
            {
                response.AddError(new ErrorMessage("Usuário não autenticado."));
                return response;
            }

            var activeApiKey = _context.Set<UserApiKey>()
                .FirstOrDefault(x => x.UserId == userId && x.IsActive);

            if (activeApiKey == null)
            {
                response.AddError(new ErrorMessage("Nenhuma API Key ativa encontrada."));
                return response;
            }

            response.Data = activeApiKey.ApiKey;
            response.Success = true;

            return response;
        }

        private string GenerateSecureApiKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
        }
    }
}

