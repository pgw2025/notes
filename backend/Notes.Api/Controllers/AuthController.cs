using System.Security.Claims;
using System.Text.Json;
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

    /// <summary>用户首次注册时自动填入的默认色板（10 个常用色）</summary>
    private static readonly List<string> DefaultPalette = new()
    {
        "#FFFFFF", "#FFF9C4", "#FFE0B2", "#FFCDD2", "#C8E6C9",
        "#BBDEFB", "#E1BEE7", "#1A237E", "#1B5E20", "#212121"
    };

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
            CreatedAt = DateTime.UtcNow,
            // 注册时自动初始化默认色板
            CustomColors = JsonSerializer.Serialize(DefaultPalette)
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

        var colors = ParseCustomColors(user.CustomColors);
        // 如果是老用户（注册时还没初始化色板），自动补上默认色板
        if (colors == null)
        {
            colors = new List<string>(DefaultPalette);
            user.CustomColors = JsonSerializer.Serialize(colors);
            await _userManager.UpdateAsync(user);
        }

        return Ok(new UserDto(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.AvatarUrl,
            user.DefaultNoteColor,
            colors,
            user.CreatedAt));
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

        // DefaultNoteColor: null 表示清空默认色（用系统白色），空字符串忽略，非空字符串校验 HEX 格式
        if (dto.DefaultNoteColor != null)
        {
            var color = dto.DefaultNoteColor.Trim();
            if (color.Length == 0)
            {
                user.DefaultNoteColor = null;
            }
            else
            {
                if (!IsValidHexColor(color))
                    return BadRequest(new { message = "颜色格式不正确，需为 #RRGGBB 格式" });
                user.DefaultNoteColor = color;
            }
        }

        // CustomColors: 整体替换（前端负责 add/remove 后传完整列表）
        if (dto.CustomColors != null)
        {
            // 校验每一项都是合法 HEX，最多 50 个颜色
            var validColors = dto.CustomColors
                .Where(c => !string.IsNullOrWhiteSpace(c) && IsValidHexColor(c.Trim()))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .Take(50)
                .ToList();
            user.CustomColors = JsonSerializer.Serialize(validColors);
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        var colorsOut = ParseCustomColors(user.CustomColors) ?? new List<string>();
        return Ok(new UserDto(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.AvatarUrl,
            user.DefaultNoteColor,
            colorsOut,
            user.CreatedAt));
    }

    private static bool IsValidHexColor(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s[0] != '#') return false;
        return s.Length == 7 && s[1..].All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    /// <summary>解析用户色板 JSON 字符串。返回 null 表示未初始化。</summary>
    private static List<string>? ParseCustomColors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch
        {
            return null;
        }
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
