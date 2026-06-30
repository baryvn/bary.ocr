using AutoOcrs.Core.DTOs.Common;
using AutoOcrs.Core.DTOs.Users;
using AutoOcrs.Core.Entities;
using AutoOcrs.Core.Enums;
using AutoOcrs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoOcrs.Infrastructure.Services;

/// <summary>CRUD quản lý người dùng</summary>
public class UserService(AppDbContext db)
{
    public async Task<PagedResult<UserResponse>> GetAllAsync(int page = 1, int pageSize = 20, string? search = null, UserRole? role = null)
    {
        var query = db.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Username.Contains(search));
        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserResponse(u.Id, u.Username, u.FullName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToListAsync();

        return new PagedResult<UserResponse>(users, total, page, pageSize);
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var u = await db.Users.FindAsync(id);
        return u == null ? null : new UserResponse(u.Id, u.Username, u.FullName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt);
    }

    public async Task<UserResponse?> CreateAsync(CreateUserRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username))
            return null;

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            Email = request.Email,
            Role = request.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new UserResponse(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return false;

        if (request.FullName != null) user.FullName = request.FullName;
        if (request.Email != null) user.Email = request.Email;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return false;

        user.IsActive = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid id, string newPassword)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Danh sách reviewers active (cho dropdown assign)</summary>
    public async Task<List<UserResponse>> GetReviewersAsync()
    {
        return await db.Users
            .Where(u => u.Role == UserRole.Reviewer && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new UserResponse(u.Id, u.Username, u.FullName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToListAsync();
    }
}
