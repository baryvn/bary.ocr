using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<MetadataField> MetadataFields => Set<MetadataField>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();
    public DbSet<ReviewAssignment> ReviewAssignments => Set<ReviewAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === User ===
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Role).HasConversion<short>();
        });

        // === Project ===
        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.Property(p => p.Status).HasConversion<short>();
            e.HasOne(p => p.Creator).WithMany(u => u.Projects).HasForeignKey(p => p.CreatedBy);
        });

        // === Label (self-referencing tree) ===
        modelBuilder.Entity<Label>(e =>
        {
            e.ToTable("labels");
            e.HasOne(l => l.Project).WithMany(p => p.Labels).HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Parent).WithMany(l => l.Children).HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => l.ProjectId);
            e.HasIndex(l => l.ParentId);
        });

        // === MetadataField ===
        modelBuilder.Entity<MetadataField>(e =>
        {
            e.ToTable("metadata_fields");
            e.Property(f => f.FieldType).HasConversion<short>();
            e.Property(f => f.Options).HasColumnType("jsonb");
            e.HasOne(f => f.Label).WithMany(l => l.MetadataFields).HasForeignKey(f => f.LabelId).OnDelete(DeleteBehavior.Cascade);
        });

        // === Batch ===
        modelBuilder.Entity<Batch>(e =>
        {
            e.ToTable("batches");
            e.Property(b => b.Status).HasConversion<short>();
            e.HasOne(b => b.Project).WithMany(p => p.Batches).HasForeignKey(b => b.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => b.ProjectId);
            e.HasIndex(b => b.Status);
        });

        // === Document ===
        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.Property(d => d.Status).HasConversion<short>();
            e.Property(d => d.ExtractedMetadata).HasColumnType("jsonb");
            e.Property(d => d.ReviewedMetadata).HasColumnType("jsonb");
            e.HasOne(d => d.Batch).WithMany(b => b.Documents).HasForeignKey(d => d.BatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Label).WithMany(l => l.Documents).HasForeignKey(d => d.LabelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.Reviewer).WithMany().HasForeignKey(d => d.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(d => d.BatchId);
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.LabelId);
            e.HasIndex(d => new { d.BatchId, d.Status });
        });

        // === DocumentPage ===
        modelBuilder.Entity<DocumentPage>(e =>
        {
            e.ToTable("document_pages");
            e.Property(p => p.OcrResult).HasColumnType("jsonb");
            e.HasOne(p => p.Document).WithMany(d => d.Pages).HasForeignKey(p => p.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.DocumentId, p.PageNumber });
        });

        // === ReviewAssignment ===
        modelBuilder.Entity<ReviewAssignment>(e =>
        {
            e.ToTable("review_assignments");
            e.Property(r => r.Status).HasConversion<short>();
            e.HasOne(r => r.Batch).WithMany(b => b.ReviewAssignments).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reviewer).WithMany(u => u.ReviewAssignments).HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Assigner).WithMany().HasForeignKey(r => r.AssignedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(r => new { r.ReviewerId, r.Status });
        });

        // === AuditLog ===
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        // Seed admin mặc định
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "admin",
            PasswordHash = "$2a$11$G7V.t.kUeG.w.2tA2E2T9Oe8f7B7v2q0v7.Qv1.T.5f3K1W7/T9V.", // Admin@123
            FullName = "Quản trị viên",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
