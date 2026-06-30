using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.DTOs.Users;

public record CreateUserRequest(string Username, string Password, string FullName, string? Email, UserRole Role);

public record UpdateUserRequest(string? FullName, string? Email, UserRole? Role, bool? IsActive);

public record UserResponse(Guid Id, string Username, string FullName, string? Email, string Role, bool IsActive, DateTime CreatedAt);

public record ResetPasswordRequest(string NewPassword);
