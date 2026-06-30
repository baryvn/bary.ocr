using AutoOcrs.Worker;

using AutoOcrs.Infrastructure.Data;
using AutoOcrs.Infrastructure.Services;
using AutoOcrs.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// === Cấu hình AppDbContext ===
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// === Services ===
builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddScoped<PdfRendererService>();
builder.Services.AddScoped<PdfBuilderService>();
builder.Services.AddHttpClient<OcrService>(client => client.Timeout = TimeSpan.FromMinutes(10));
builder.Services.AddHttpClient<LlmService>(client => client.Timeout = TimeSpan.FromMinutes(10));
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<ExtractionService>();
builder.Services.AddScoped<NamingRuleService>();
builder.Services.AddScoped<TextCleanupService>();

// === MassTransit ===
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OcrJobConsumer>();

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

        cfg.ReceiveEndpoint("ocr-jobs-queue", e =>
        {
            e.ConfigureConsumer<OcrJobConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
