using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Notes.Api.DTOs;
using Notes.Api.Models;
using Notes.Api.Services;

namespace Notes.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const long MaxAvatarSize = 5 * 1024 * 1024; // 5MB
    private static readonly HashSet<string> AllowedAvatarExts = new() { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly IFileStorageService _files;

    public AuthController(UserManager<ApplicationUser> userManager, TokenService tokenService, IFileStorageService files)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _files = files;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "邮箱和密码不能为空" });

        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing != null)
            return Conflict(new { message = "该邮箱已被注册" });

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto(token, user.Email!, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Unauthorized(new { message = "邮箱或密码错误" });

        var valid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!valid)
            return Unauthorized(new { message = "邮箱或密码错误" });

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto(token, user.Email!, user.DisplayName));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null) return NotFound();

        return Ok(new UserDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.CreatedAt));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null) return NotFound();

        if (dto.DisplayName != null)
        {
            user.DisplayName = dto.DisplayName.Trim();
            if (user.DisplayName.Length > 50)
                return BadRequest(new { message = "昵称长度不能超过 50 个字符" });
        }

        if (dto.AvatarUrl != null)
        {
            user.AvatarUrl = dto.AvatarUrl;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new UserDto(user.Id, user.Email!, user.DisplayName, user.AvatarUrl, user.CreatedAt));
    }

    [Authorize]
    [HttpPost("avatar")]
    [RequestSizeLimit(MaxAvatarSize + 1024 * 1024)]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请选择图片" });

        if (file.Length > MaxAvatarSize)
            return BadRequest(new { message = "头像大小不能超过 5MB" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAvatarExts.Contains(ext))
            return BadRequest(new { message = "仅支持 jpg/png/gif/webp 格式" });

        var (path, name, _) = await _files.SaveAvatarAsync(file, UserId);

        var user = await _userManager.FindByIdAsync(UserId);
        if (user != null && !string.IsNullOrEmpty(user.AvatarUrl))
        {
            var oldName = user.AvatarUrl.StartsWith("/api/auth/avatar/")
                ? user.AvatarUrl.Substring("/api/auth/avatar/".Length)
                : Path.GetFileName(user.AvatarUrl);
            var oldPath = Path.Combine(Directory.GetParent(path)!.FullName, oldName);
            if (System.IO.File.Exists(oldPath))
            {
                try { System.IO.File.Delete(oldPath); } catch { /* ignore */ }
            }
        }

        var avatarUrl = $"/api/auth/avatar/{name}";
        return Ok(new AvatarUploadResponse(avatarUrl));
    }

    [Authorize]
    [HttpGet("avatar/{fileName}")]
    public async Task<IActionResult> GetAvatar(string fileName)
    {
        var sanitized = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(sanitized)) return NotFound();

        var avatarFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Avatars");
        var fullPath = Path.Combine(avatarFolder, sanitized);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        var (stream, contentType, name) = await _files.GetAsync(fullPath);
        return File(stream, contentType, name);
    }
}
