using System.Text;
using AutoOcrs.Api.Endpoints;
using AutoOcrs.Infrastructure;
using AutoOcrs.Infrastructure.Data;
using AutoOcrs.Infrastructure.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// === Infrastructure ===
builder.Services.AddInfrastructure(builder.Configuration);

// === Services ===
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<MetadataFieldService>();
builder.Services.AddScoped<NamingRuleService>();
builder.Services.AddScoped<BatchService>();
builder.Services.AddScoped<OcrJobPublisher>();
builder.Services.AddSingleton<MinioStorageService>(); // MinIO client should be singleton generally, but we can use Scoped if we prefer. Let's use Singleton.

builder.Services.AddHttpClient<OcrService>();
builder.Services.AddScoped<PdfRendererService>();
builder.Services.AddScoped<NamingRuleService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<ReportService>();

// === MassTransit (RabbitMQ) ===
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitCs = builder.Configuration.GetConnectionString("rabbitmq");
        if (!string.IsNullOrEmpty(rabbitCs) && Uri.TryCreate(rabbitCs, UriKind.Absolute, out var uri))
        {
            cfg.Host(uri.Host, (ushort)(uri.Port > 0 ? uri.Port : 5672), "/", h =>
            {
                var userInfo = uri.UserInfo.Split(':');
                if (userInfo.Length == 2)
                {
                    h.Username(userInfo[0]);
                    h.Password(userInfo[1]);
                }
            });
        }
        else
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });
        }
        
        cfg.ConfigureEndpoints(context);
    });
});

// === Authentication & Authorization ===
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();

// === CORS ===
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow any origin for development
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c => ...);

var app = builder.Build();

// === Initialize Database & MinIO Bucket ===
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AutoOcrs.Infrastructure.Data.AppDbContext>();

    db.Database.Migrate();

    // Force reset admin password in case seed hash was invalid
    var admin = db.Users.FirstOrDefault(u => u.Username == "admin");
    if (admin != null)
    {
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
        db.SaveChanges();
    }

    var minioService = scope.ServiceProvider.GetRequiredService<MinioStorageService>();
    minioService.EnsureBucketAsync().GetAwaiter().GetResult();
}

// === Middleware ===
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

// === Endpoints ===
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapProjectEndpoints();
app.MapLabelEndpoints();
app.MapMetadataFieldEndpoints();
app.MapNamingRuleEndpoints();
app.MapStorageEndpoints();
app.MapBatchEndpoints();
app.MapDocumentEndpoints();
app.MapReviewEndpoints();
app.MapReportEndpoints();

app.MapGet("/debug-db", async (AutoOcrs.Infrastructure.Data.AppDbContext db) => {
    var doc = await db.Documents.OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync();
    var pages = await db.DocumentPages.Where(d => d.DocumentId == doc.Id).ToListAsync();
    return Results.Ok(new { doc, pages });
}).AllowAnonymous();

app.Run();
