using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Notes.Api.Models;

namespace Notes.Api.Services;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
    {
        _config = config;
        _userManager = userManager;
    }

    /// <summary>Access Token 有效期（短，用于常规请求鉴权）。</summary>
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Refresh Token 有效期（长，仅用于换取新 Access Token）。</summary>
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>签发 Access Token（短有效期）。</summary>
    public async Task<string> CreateToken(ApplicationUser user)
    {
        return await CreateTokenCore(user, AccessTokenLifetime, null);
    }

    /// <summary>签发一对 token：短效 Access Token + 长效 Refresh Token。</summary>
    public async Task<(string AccessToken, string RefreshToken)> CreateTokenPair(ApplicationUser user)
    {
        var access = await CreateTokenCore(user, AccessTokenLifetime, null);
        var refresh = await CreateTokenCore(user, RefreshTokenLifetime, "refresh");
        return (access, refresh);
    }

    /// <summary>用 Refresh Token 换发新的 Access Token（滑动续期）。</summary>
    public async Task<string> RefreshAccessToken(ApplicationUser user)
    {
        return await CreateTokenCore(user, AccessTokenLifetime, null);
    }

    private async Task<string> CreateTokenCore(ApplicationUser user, TimeSpan lifetime, string? tokenType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }

        // 将用户角色写入 JWT，供后端 Admin 策略鉴权使用
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 标记 token 用途：access 或 refresh，便于在 /refresh 端点校验 token 类型
        if (!string.IsNullOrEmpty(tokenType))
        {
            claims.Add(new Claim("token_type", tokenType));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
