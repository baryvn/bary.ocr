var builder = DistributedApplication.CreateBuilder(args);

// --- Infrastructure ---
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("adms");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume()
    .WithManagementPlugin();

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "s3")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithEnvironment("MINIO_API_CORS_ALLOW_ORIGIN", "*")
    .WithBindMount("../../../data/minio", "/data");

// --- Backend Services ---
var api = builder.AddProject<Projects.AutoOcrs_Api>("api")
    .WithReference(postgres)
    .WithEnvironment("ConnectionStrings__DefaultConnection", postgres)
    .WithReference(rabbitmq)
    .WithEnvironment("RabbitMQ__Host", rabbitmq.GetEndpoint("tcp"))
    .WithReference(redis)
    .WithEnvironment("Minio__Endpoint", minio.GetEndpoint("s3"))
    .WithEnvironment("Minio__AccessKey", "minioadmin")
    .WithEnvironment("Minio__SecretKey", "minioadmin")
    .WithEnvironment("Minio__UseSSL", "false");

var ocrWorker = builder.AddContainer("ocr-worker", "ocr-worker", "latest")
    .WithHttpEndpoint(port: 8091, targetPort: 8091, name: "http");

var llmServer = builder.AddContainer("llm-server", "llm-server", "latest")
    .WithEnvironment("CTX_SIZE", "32768")
    .WithEnvironment("PARALLEL_SLOTS", "4")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http");

var worker = builder.AddProject<Projects.AutoOcrs_Worker>("worker")
    .WithReference(postgres)
    .WithEnvironment("ConnectionStrings__DefaultConnection", postgres)
    .WithReference(rabbitmq)
    .WithEnvironment("RabbitMQ__Host", rabbitmq.GetEndpoint("tcp"))
    .WithReference(redis)
    .WithEnvironment("Minio__Endpoint", minio.GetEndpoint("s3"))
    .WithEnvironment("OcrWorker__Url", ocrWorker.GetEndpoint("http"))
    .WithEnvironment("LlmServer__Url", llmServer.GetEndpoint("http"))
    .WithEnvironment("Minio__AccessKey", "minioadmin")
    .WithEnvironment("Minio__SecretKey", "minioadmin")
    .WithEnvironment("Minio__UseSSL", "false");

// --- Frontend ---
var frontend = builder.AddNpmApp("frontend", "../../../frontend", "dev")
    .WithReference(api)
    .WithEnvironment("NUXT_PUBLIC_API_BASE_URL", api.GetEndpoint("http"))
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
