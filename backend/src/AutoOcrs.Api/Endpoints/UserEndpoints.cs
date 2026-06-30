using AutoOcrs.Core.DTOs.Users;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, string? search, UserRole? role, UserService svc) =>
            Results.Ok(await svc.GetAllAsync(page ?? 1, pageSize ?? 20, search, role)));

        group.MapGet("/{id:guid}", async (Guid id, UserService svc) =>
        {
            var user = await svc.GetByIdAsync(id);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        group.MapPost("/", async (CreateUserRequest request, UserService svc) =>
        {
            var user = await svc.CreateAsync(request);
            return user is null
                ? Results.BadRequest(new { message = "Username đã tồn tại" })
                : Results.Created($"/api/users/{user.Id}", user);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserService svc) =>
            await svc.UpdateAsync(id, request) ? Results.Ok() : Results.NotFound());

        group.MapDelete("/{id:guid}", async (Guid id, UserService svc) =>
            await svc.DeleteAsync(id) ? Results.Ok() : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole("Admin", "Manager"));

        group.MapPut("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, UserService svc) =>
            await svc.ResetPasswordAsync(id, request.NewPassword) ? Results.Ok() : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole("Admin", "Manager"));

        group.MapGet("/reviewers", async (UserService svc) =>
            Results.Ok(await svc.GetReviewersAsync()));
    }
}
