using AutoOcrs.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoOcrs.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Dashboard Stats
        group.MapGet("/dashboard/stats", async (ReportService reportService) =>
        {
            var stats = await reportService.GetDashboardStatsAsync();
            return Results.Ok(stats);
        }).RequireAuthorization(p => p.RequireRole("Manager"));

        // Project Summary Report
        group.MapGet("/projects/{id:guid}/reports/summary", async (Guid id, ReportService reportService) =>
        {
            var stats = await reportService.GetProjectSummaryAsync(id);
            return Results.Ok(stats);
        });

        // Export Excel - Project
        group.MapGet("/projects/{id:guid}/reports/export", async (Guid id, ReportService reportService) =>
        {
            var excelBytes = await reportService.ExportExcelAsync(projectId: id);
            return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ProjectReport_{id}.xlsx");
        });

        // Export Excel - Batch
        group.MapGet("/batches/{id:guid}/reports/export", async (Guid id, ReportService reportService) =>
        {
            var excelBytes = await reportService.ExportExcelAsync(batchId: id);
            return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BatchReport_{id}.xlsx");
        });
    }
}
