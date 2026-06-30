using AutoOcrs.Core.DTOs.NamingRules;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class NamingRuleEndpoints
{
    public static void MapNamingRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/naming-rule").RequireAuthorization();

        group.MapGet("/", async (Guid projectId, NamingRuleService service) =>
        {
            try
            {
                var rule = await service.GetRuleAsync(projectId);
                return Results.Ok(rule);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPut("/", async (Guid projectId, UpdateNamingRuleRequest request, NamingRuleService service) =>
        {
            try
            {
                var rule = await service.UpdateRuleAsync(projectId, request);
                return Results.Ok(rule);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        group.MapPost("/preview", async (Guid projectId, PreviewNamingRuleRequest request, NamingRuleService service) =>
        {
            try
            {
                var preview = await service.PreviewAsync(projectId, request);
                return Results.Ok(preview);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
    }
}
