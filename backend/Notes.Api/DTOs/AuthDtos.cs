namespace Notes.Api.DTOs;

public record RegisterDto(string Email, string Password, string? DisplayName);

public record LoginDto(string Email, string Password);

public record AuthResponseDto(string Token, string Email, string? DisplayName);

public record UserDto(
    string Id,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    string? DefaultNoteColor,
    List<string> CustomColors,
    DateTime CreatedAt);

public record UpdateProfileDto(
    string? DisplayName,
    string? AvatarUrl,
    string? DefaultNoteColor,
    List<string>? CustomColors);

public record AvatarUploadResponse(string AvatarUrl);
