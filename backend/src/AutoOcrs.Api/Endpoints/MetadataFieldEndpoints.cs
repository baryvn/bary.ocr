using AutoOcrs.Core.DTOs.MetadataFields;
using AutoOcrs.Infrastructure.Services;

namespace AutoOcrs.Api.Endpoints;

public static class MetadataFieldEndpoints
{
    public static void MapMetadataFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Lấy danh sách field của một nhãn
        group.MapGet("/labels/{labelId:guid}/fields", async (Guid labelId, MetadataFieldService service) =>
        {
            var fields = await service.GetFieldsByLabelAsync(labelId);
            return Results.Ok(fields);
        });

        // Lấy danh sách field của nhãn và tất cả nhãn cha (inherited)
        group.MapGet("/labels/{labelId:guid}/fields/inherited", async (Guid labelId, MetadataFieldService service) =>
        {
            var fields = await service.GetInheritedFieldsByLabelAsync(labelId);
            return Results.Ok(fields);
        });

        // Thêm trường mới cho nhãn
        group.MapPost("/labels/{labelId:guid}/fields", async (Guid labelId, CreateMetadataFieldRequest request, MetadataFieldService service) =>
        {
            try
            {
                var result = await service.CreateAsync(labelId, request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Sửa trường
        group.MapPut("/fields/{id:guid}", async (Guid id, UpdateMetadataFieldRequest request, MetadataFieldService service) =>
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

        // Xóa trường
        group.MapDelete("/fields/{id:guid}", async (Guid id, MetadataFieldService service) =>
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
    }
}
