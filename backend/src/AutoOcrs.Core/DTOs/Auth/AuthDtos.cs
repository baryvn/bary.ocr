namespace AutoOcrs.Core.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserInfo User);

public record UserInfo(Guid Id, string Username, string FullName, string? Email, string Role);

public record RefreshTokenRequest(string RefreshToken);

public record ChangePasswordRequest(string OldPassword, string NewPassword);
