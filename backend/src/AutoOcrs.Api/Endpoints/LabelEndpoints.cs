using AutoOcrs.Core.DTOs.Labels;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class LabelEndpoints
{
    public static void MapLabelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Lấy cây nhãn của một dự án
        group.MapGet("/projects/{projectId:guid}/labels", async (Guid projectId, LabelService service) =>
        {
            var tree = await service.GetTreeAsync(projectId);
            return Results.Ok(tree);
        });

        // Lấy danh sách phẳng
        group.MapGet("/projects/{projectId:guid}/labels/flat", async (Guid projectId, LabelService service) =>
        {
            var list = await service.GetFlatListAsync(projectId);
            return Results.Ok(list);
        });

        // Tạo nhãn mới
        group.MapPost("/projects/{projectId:guid}/labels", async (Guid projectId, CreateLabelRequest request, LabelService service) =>
        {
            if (projectId != request.ProjectId) return Results.BadRequest("ID dự án không khớp.");
            try
            {
                var result = await service.CreateAsync(request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Sửa nhãn
        group.MapPut("/labels/{id:guid}", async (Guid id, UpdateLabelRequest request, LabelService service) =>
        {
            try
            {
                var result = await service.UpdateAsync(id, request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Xóa nhãn
        group.MapDelete("/labels/{id:guid}", async (Guid id, LabelService service) =>
        {
            try
            {
                await service.DeleteAsync(id);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Di chuyển nhãn
        group.MapPut("/labels/{id:guid}/move", async (Guid id, MoveLabelRequest request, LabelService service) =>
        {
            try
            {
                await service.MoveAsync(id, request);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Sắp xếp thứ tự
        group.MapPut("/labels/reorder", async (List<ReorderLabelRequest> request, LabelService service) =>
        {
            try
            {
                await service.ReorderAsync(request);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));
    }
}
