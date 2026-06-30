using System.Security.Claims;
using AutoOcrs.Core.DTOs.Projects;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization();

        group.MapGet("/", async (int? page, int? pageSize, string? search, ProjectService svc) =>
            Results.Ok(await svc.GetAllAsync(page ?? 1, pageSize ?? 20, search)));

        group.MapGet("/{id:guid}", async (Guid id, ProjectService svc) =>
        {
            var project = await svc.GetByIdAsync(id);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });

        group.MapPost("/", async (CreateProjectRequest request, ClaimsPrincipal user, ProjectService svc) =>
        {
            var userId = Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var project = await svc.CreateAsync(request, userId);
            return Results.Created($"/api/projects/{project.Id}", project);
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        group.MapPut("/{id:guid}", async (Guid id, UpdateProjectRequest request, ProjectService svc) =>
            await svc.UpdateAsync(id, request) ? Results.Ok() : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole("Manager"));

        group.MapDelete("/{id:guid}", async (Guid id, ProjectService svc) =>
            await svc.DeleteAsync(id) ? Results.Ok() : Results.NotFound())
            .RequireAuthorization(p => p.RequireRole("Manager"));
    }
}
