using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoOcrs.Core.DTOs.Auth;
using AutoOcrs.Core.Entities;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AutoOcrs.Infrastructure.Services;

/// <summary>Xử lý authentication: login, JWT, đổi mật khẩu</summary>
public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var expiresIn = config.GetValue<int>("Jwt:AccessTokenExpirationMinutes") * 60;

        return new LoginResponse(
            accessToken, refreshToken, expiresIn,
            new UserInfo(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString())
        );
    }

    public async Task<UserInfo?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return null;

        var user = await db.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return null;

        return new UserInfo(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString());
    }

    public async Task<LoginResponse?> RefreshTokenAsync(ClaimsPrincipal principal)
    {
        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return null;

        var user = await db.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return null;

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken(); // We don't store it, just return it for compatibility
        var expiresIn = config.GetValue<int>("Jwt:AccessTokenExpirationMinutes") * 60;

        return new LoginResponse(
            accessToken, refreshToken, expiresIn,
            new UserInfo(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString())
        );
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return true;
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(config.GetValue<int>("Jwt:AccessTokenExpirationMinutes"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
