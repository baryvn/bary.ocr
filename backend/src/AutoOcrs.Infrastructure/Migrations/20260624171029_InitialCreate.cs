using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoOcrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourcePath = table.Column<string>(type: "text", nullable: true),
                    OutputPath = table.Column<string>(type: "text", nullable: true),
                    NamingRule = table.Column<string>(type: "jsonb", nullable: true),
                    FolderRule = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SourceFolder = table.Column<string>(type: "text", nullable: true),
                    TotalFiles = table.Column<int>(type: "integer", nullable: false),
                    ProcessedFiles = table.Column<int>(type: "integer", nullable: false),
                    ApprovedFiles = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    MinioPrefix = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batches_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LlmPrompt = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_labels_labels_ParentId",
                        column: x => x.ParentId,
                        principalTable: "labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_labels_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalDocuments = table.Column<int>(type: "integer", nullable: false),
                    ReviewedDocuments = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_assignments_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_assignments_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_review_assignments_users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFilename = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: true),
                    OcrPdfKey = table.Column<string>(type: "text", nullable: true),
                    OutputFilename = table.Column<string>(type: "text", nullable: true),
                    OutputKey = table.Column<string>(type: "text", nullable: true),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassificationConfidence = table.Column<float>(type: "real", nullable: true),
                    ExtractedMetadata = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewedMetadata = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ReviewerNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OcrCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClassifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_documents_labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "metadata_fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    FieldType = table.Column<short>(type: "smallint", nullable: false),
                    Options = table.Column<string>(type: "jsonb", nullable: true),
                    LlmExtractionPrompt = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metadata_fields_labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    ImageKey = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    OcrResult = table.Column<string>(type: "jsonb", nullable: true),
                    FullText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_pages_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "CreatedAt", "Email", "FullName", "IsActive", "PasswordHash", "Role", "UpdatedAt", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Quản trị viên", true, "$2a$11$G7V.t.kUeG.w.2tA2E2T9Oe8f7B7v2q0v7.Qv1.T.5f3K1W7/T9V.", (short)0, null, "admin" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityType_EntityId",
                table: "audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_batches_ProjectId",
                table: "batches",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_batches_Status",
                table: "batches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_document_pages_DocumentId_PageNumber",
                table: "document_pages",
                columns: new[] { "DocumentId", "PageNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_BatchId",
                table: "documents",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_BatchId_Status",
                table: "documents",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_LabelId",
                table: "documents",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_ReviewedBy",
                table: "documents",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_documents_Status",
                table: "documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_labels_ParentId",
                table: "labels",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_labels_ProjectId",
                table: "labels",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_metadata_fields_LabelId",
                table: "metadata_fields",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_CreatedBy",
                table: "projects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_AssignedBy",
                table: "review_assignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_BatchId",
                table: "review_assignments",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_review_assignments_ReviewerId_Status",
                table: "review_assignments",
                columns: new[] { "ReviewerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "document_pages");

            migrationBuilder.DropTable(
                name: "metadata_fields");

            migrationBuilder.DropTable(
                name: "review_assignments");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
