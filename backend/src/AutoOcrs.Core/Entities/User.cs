using AutoOcrs.Core.Enums;

namespace AutoOcrs.Core.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<ReviewAssignment> ReviewAssignments { get; set; } = [];
}
