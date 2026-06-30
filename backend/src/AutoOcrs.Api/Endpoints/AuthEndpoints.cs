using System.Security.Claims;
using AutoOcrs.Core.DTOs.Auth;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, AuthService authService) =>
        {
            var result = await authService.LoginAsync(request);
            return result is null
                ? Results.Json(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" }, statusCode: 401)
                : Results.Ok(result);
        }).AllowAnonymous();

        group.MapGet("/me", async (ClaimsPrincipal user, AuthService authService) =>
        {
            var info = await authService.GetCurrentUserAsync(user);
            return info is null ? Results.Unauthorized() : Results.Ok(info);
        }).RequireAuthorization();

        group.MapPut("/password", async (ChangePasswordRequest request, ClaimsPrincipal user, AuthService authService) =>
        {
            var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var success = await authService.ChangePasswordAsync(userId, request);
            return success ? Results.Ok() : Results.BadRequest(new { message = "Mật khẩu cũ không đúng" });
        }).RequireAuthorization();

        group.MapGet("/refresh", async (ClaimsPrincipal user, AuthService authService) =>
        {
            var result = await authService.RefreshTokenAsync(user);
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        }).RequireAuthorization();
    }
}
