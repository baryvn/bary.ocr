using System.Security.Claims;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using AutoOcrs.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization();

        // Lấy danh sách lô được giao cho Reviewer hiện tại
        group.MapGet("/my-assignments", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Results.Unauthorized();

            var assignments = await db.ReviewAssignments
                .Include(r => r.Batch)
                .ThenInclude(b => b.Project)
                .Where(r => r.ReviewerId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.BatchId,
                    BatchName = r.Batch.Name,
                    ProjectName = r.Batch.Project.Name,
                    r.Status,
                    r.TotalDocuments,
                    r.ReviewedDocuments,
                    r.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(assignments);
        }).RequireAuthorization(p => p.RequireRole("Reviewer", "Manager"));
        
        // Phân lô cho Reviewer
        var batchGroup = app.MapGroup("/api/batches").RequireAuthorization();
        batchGroup.MapPost("/{id:guid}/assign", async (Guid id, Guid reviewerId, ClaimsPrincipal user, AppDbContext db, AuditLogService audit) =>
        {
            try
            {
                var assignerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(assignerIdStr, out var assignerId)) return Results.Unauthorized();

                var batch = await db.Batches.Include(b => b.Documents).FirstOrDefaultAsync(b => b.Id == id);
                if (batch == null) throw new Exception("Không tìm thấy lô tài liệu.");

                var reviewer = await db.Users.FindAsync(reviewerId);
                if (reviewer == null || reviewer.Role != UserRole.Reviewer) throw new Exception("Reviewer không hợp lệ.");

                var assignment = new ReviewAssignment
                {
                    BatchId = batch.Id,
                    ReviewerId = reviewerId,
                    AssignedBy = assignerId,
                    TotalDocuments = batch.Documents.Count(d => d.Status >= DocumentStatus.Classified),
                    ReviewedDocuments = 0,
                    Status = ReviewStatus.Assigned
                };

                db.ReviewAssignments.Add(assignment);
                await db.SaveChangesAsync();

                await audit.LogActionAsync(assignerId, "AssignBatch", "Batch", batch.Id, null, $"ReviewerId: {reviewerId}");

                return Results.Ok(new { message = "Phân lô thành công." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(p => p.RequireRole("Manager"));
    }
}
