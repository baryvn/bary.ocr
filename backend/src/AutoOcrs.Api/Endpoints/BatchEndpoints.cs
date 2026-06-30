using AutoOcrs.Core.DTOs.Batches;
using AutoOcrs.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoOcrs.Api.Endpoints;

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Danh sách lô của dự án
        group.MapGet("/projects/{projectId:guid}/batches", async (Guid projectId, [FromQuery] int page, [FromQuery] int pageSize, BatchService service) =>
        {
            try
            {
                var result = await service.GetBatchesAsync(projectId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Tạo lô
        group.MapPost("/projects/{projectId:guid}/batches", async (Guid projectId, CreateBatchRequest request, BatchService service) =>
        {
            try
            {
                var result = await service.CreateBatchAsync(projectId, request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Chi tiết lô
        group.MapGet("/batches/{id:guid}", async (Guid id, BatchService service) =>
        {
            try
            {
                var result = await service.GetBatchByIdAsync(id);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Thêm file vào lô
        group.MapPost("/batches/{id:guid}/documents/upload", async (Guid id, AddDocumentsRequest request, BatchService service) =>
        {
            try
            {
                var result = await service.AddDocumentsAsync(id, request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Danh sách file trong lô
        group.MapGet("/batches/{id:guid}/documents", async (Guid id, [FromQuery] int page, [FromQuery] int pageSize, BatchService service) =>
        {
            try
            {
                var result = await service.GetDocumentsAsync(id, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Tiến độ lô
        group.MapGet("/batches/{id:guid}/progress", async (Guid id, BatchService service) =>
        {
            try
            {
                var result = await service.GetProgressAsync(id);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Bắt đầu xử lý OCR cho lô
        group.MapPost("/batches/{id:guid}/process", async (Guid id, BatchService service, OcrJobPublisher publisher) =>
        {
            try
            {
                await service.ProcessBatchAsync(id, publisher);
                return Results.Ok(new { message = "Đã bắt đầu xử lý lô." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Cập nhật lô
        group.MapPut("/batches/{id:guid}", async (Guid id, UpdateBatchRequest request, BatchService service) =>
        {
            try
            {
                var result = await service.UpdateBatchAsync(id, request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Xóa lô
        group.MapDelete("/batches/{id:guid}", async (Guid id, BatchService service) =>
        {
            try
            {
                await service.DeleteBatchAsync(id);
                return Results.Ok(new { message = "Đã xóa lô thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));
    }
}
