using AutoOcrs.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoOcrs.Api.Endpoints;

public static class StorageEndpoints
{
    public static void MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/storage").RequireAuthorization();

        group.MapGet("/presign-put", async ([FromQuery] string fileName, [FromQuery] string? prefix, [FromQuery] bool keepExactName, MinioStorageService service) =>
        {
            try
            {
                var objectName = keepExactName ? fileName : $"{Guid.NewGuid():N}_{fileName}";
                if (!string.IsNullOrEmpty(prefix))
                {
                    objectName = $"{prefix.Trim('/')}/{objectName}";
                }
                var url = await service.GetPresignedPutUrlAsync(objectName);
                return Results.Ok(new { uploadUrl = url, objectKey = objectName });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
        
        // Lấy Presigned URL để tải/xem file
        group.MapGet("/presign-get", async ([FromQuery] string objectKey, MinioStorageService service) =>
        {
            try
            {
                var url = await service.GetPresignedUrlAsync(objectKey);
                return Results.Ok(new { downloadUrl = url });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
    }
}
