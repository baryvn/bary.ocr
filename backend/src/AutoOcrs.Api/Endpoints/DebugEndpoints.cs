using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoOcrs.Infrastructure.Data;

namespace AutoOcrs.Api.Endpoints;

public static class DebugEndpoints
{
    public static void MapDebugEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/debug-db", async ([FromServices] AppDbContext db) =>
        {
            var doc = await db.Documents
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();
                
            if (doc == null) return Results.NotFound();
            
            var pages = await db.DocumentPages.Where(p => p.DocumentId == doc.Id).ToListAsync();
            
            return Results.Ok(new { doc, pages });
        });
    }
}
