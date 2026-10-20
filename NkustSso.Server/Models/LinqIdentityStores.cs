using LinqToDB;
using Microsoft.AspNetCore.Identity;

using NkustSso.Server.Models;

namespace NkustSso.Server.Models;

public sealed class LinqUserStore(DatabaseFactory database) :
    IUserPasswordStore<Account>, IUserSecurityStampStore<Account>, IUserLockoutStore<Account>,
    IUserRoleStore<Account>, IUserEmailStore<Account>
{
    public void Dispose() { }
    public Task<string> GetUserIdAsync(Account u, CancellationToken ct) => Task.FromResult(u.Id);
    public Task<string?> GetUserNameAsync(Account u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(Account u, string? name, CancellationToken ct) { u.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(Account u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(Account u, string? name, CancellationToken ct) { u.NormalizedUserName = name; return Task.CompletedTask; }
    public async Task<Account?> FindByIdAsync(string id, CancellationToken ct) { await using var db = database.Open(); return await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct); }
    public async Task<Account?> FindByNameAsync(string name, CancellationToken ct) { await using var db = database.Open(); return await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == name, ct); }
    public async Task<IdentityResult> CreateAsync(Account u, CancellationToken ct)
    {
        await using var db = database.Open();
        try { await db.InsertAsync(u, token: ct); return IdentityResult.Success; }
        catch (Exception e) when (SsoStorage.IsUniqueViolation(e)) { return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateUserName(u.UserName ?? "")); }
    }
    public async Task<IdentityResult> UpdateAsync(Account u, CancellationToken ct)
    {
        await using var db = database.Open();
        var oldStamp = u.ConcurrencyStamp;
        var stamp = Guid.NewGuid().ToString();
        var count = await db.Users.Where(x => x.Id == u.Id && x.ConcurrencyStamp == oldStamp)
            .Set(x => x.UserName, u.UserName).Set(x => x.NormalizedUserName, u.NormalizedUserName)
            .Set(x => x.Email, u.Email).Set(x => x.NormalizedEmail, u.NormalizedEmail).Set(x => x.EmailConfirmed, u.EmailConfirmed)
            .Set(x => x.PasswordHash, u.PasswordHash).Set(x => x.SecurityStamp, u.SecurityStamp).Set(x => x.ConcurrencyStamp, stamp)
            .Set(x => x.PhoneNumber, u.PhoneNumber).Set(x => x.PhoneNumberConfirmed, u.PhoneNumberConfirmed)
            .Set(x => x.TwoFactorEnabled, u.TwoFactorEnabled).Set(x => x.LockoutEnd, u.LockoutEnd)
            .Set(x => x.LockoutEnabled, u.LockoutEnabled).Set(x => x.AccessFailedCount, u.AccessFailedCount)
            .Set(x => x.DisplayName, u.DisplayName).Set(x => x.IdentityType, u.IdentityType).Set(x => x.Enabled, u.Enabled)
            .Set(x => x.AuthenticationSource, u.AuthenticationSource).Set(x => x.SourceAccountId, u.SourceAccountId)
            .Set(x => x.SourceStatusCode, u.SourceStatusCode).Set(x => x.SourceStatusName, u.SourceStatusName)
            .Set(x => x.UnitCode, u.UnitCode).Set(x => x.UnitName, u.UnitName)
            .Set(x => x.ClassCode, u.ClassCode).Set(x => x.ClassName, u.ClassName)
            .Set(x => x.StudentStatusCode, u.StudentStatusCode).Set(x => x.StudentStatusName, u.StudentStatusName)
            .Set(x => x.EducationLevelCode, u.EducationLevelCode).Set(x => x.EducationLevelName, u.EducationLevelName).UpdateAsync(ct);
        if (count != 1) return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        u.ConcurrencyStamp = stamp;
        return IdentityResult.Success;
    }
    public async Task<IdentityResult> DeleteAsync(Account u, CancellationToken ct)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync(ct);
        if (await db.Users.Where(x => x.Id == u.Id && x.ConcurrencyStamp == u.ConcurrencyStamp).DeleteAsync(ct) != 1)
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        await db.UserRoles.Where(x => x.UserId == u.Id).DeleteAsync(ct);
        await tx.CommitAsync(ct);
        return IdentityResult.Success;
    }
    public Task SetPasswordHashAsync(Account u, string? hash, CancellationToken ct) { u.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(Account u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(Account u, CancellationToken ct) => Task.FromResult(u.PasswordHash is not null);
    public Task SetSecurityStampAsync(Account u, string stamp, CancellationToken ct) { u.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(Account u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(Account u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(Account u, DateTimeOffset? end, CancellationToken ct) { u.LockoutEnd = end; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(Account u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(Account u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(Account u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(Account u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
    public Task SetLockoutEnabledAsync(Account u, bool enabled, CancellationToken ct) { u.LockoutEnabled = enabled; return Task.CompletedTask; }
    public async Task AddToRoleAsync(Account u, string role, CancellationToken ct)
    {
        await using var db = database.Open();
        var r = await db.Roles.SingleAsync(x => x.NormalizedName == role, ct);
        await db.InsertAsync(new AccountRole { UserId = u.Id, RoleId = r.Id }, token: ct);
    }
    public async Task RemoveFromRoleAsync(Account u, string role, CancellationToken ct)
    {
        await using var db = database.Open();
        await db.UserRoles.Where(x => x.UserId == u.Id && db.Roles.Any(r => r.Id == x.RoleId && r.NormalizedName == role)).DeleteAsync(ct);
    }
    public async Task<IList<string>> GetRolesAsync(Account u, CancellationToken ct)
    {
        await using var db = database.Open();
        return await (from r in db.Roles join ur in db.UserRoles on r.Id equals ur.RoleId where ur.UserId == u.Id select r.Name!).ToListAsync(ct);
    }
    public async Task<bool> IsInRoleAsync(Account u, string role, CancellationToken ct)
    {
        await using var db = database.Open();
        return await db.UserRoles.AnyAsync(x => x.UserId == u.Id && db.Roles.Any(r => r.Id == x.RoleId && r.NormalizedName == role), ct);
    }
    public async Task<IList<Account>> GetUsersInRoleAsync(string role, CancellationToken ct)
    {
        await using var db = database.Open();
        return await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id where r.NormalizedName == role select u).ToListAsync(ct);
    }
    public Task SetEmailAsync(Account u, string? email, CancellationToken ct) { u.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(Account u, CancellationToken ct) => Task.FromResult(u.Email);
    public Task<bool> GetEmailConfirmedAsync(Account u, CancellationToken ct) => Task.FromResult(u.EmailConfirmed);
    public Task SetEmailConfirmedAsync(Account u, bool confirmed, CancellationToken ct) { u.EmailConfirmed = confirmed; return Task.CompletedTask; }
    public async Task<Account?> FindByEmailAsync(string email, CancellationToken ct) { await using var db = database.Open(); return await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email, ct); }
    public Task<string?> GetNormalizedEmailAsync(Account u, CancellationToken ct) => Task.FromResult(u.NormalizedEmail);
    public Task SetNormalizedEmailAsync(Account u, string? email, CancellationToken ct) { u.NormalizedEmail = email; return Task.CompletedTask; }
}

public sealed class LinqRoleStore(DatabaseFactory database) : IRoleStore<IdentityRole>
{
    public void Dispose() { }
    public Task<string> GetRoleIdAsync(IdentityRole r, CancellationToken ct) => Task.FromResult(r.Id);
    public Task<string?> GetRoleNameAsync(IdentityRole r, CancellationToken ct) => Task.FromResult(r.Name);
    public Task SetRoleNameAsync(IdentityRole r, string? name, CancellationToken ct) { r.Name = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole r, CancellationToken ct) => Task.FromResult(r.NormalizedName);
    public Task SetNormalizedRoleNameAsync(IdentityRole r, string? name, CancellationToken ct) { r.NormalizedName = name; return Task.CompletedTask; }
    public async Task<IdentityRole?> FindByIdAsync(string id, CancellationToken ct) { await using var db = database.Open(); return await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct); }
    public async Task<IdentityRole?> FindByNameAsync(string name, CancellationToken ct) { await using var db = database.Open(); return await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == name, ct); }
    public async Task<IdentityResult> CreateAsync(IdentityRole r, CancellationToken ct)
    {
        await using var db = database.Open();
        try { await db.InsertAsync(r, token: ct); return IdentityResult.Success; }
        catch (Exception e) when (SsoStorage.IsUniqueViolation(e)) { return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateRoleName(r.Name ?? "")); }
    }
    public async Task<IdentityResult> UpdateAsync(IdentityRole r, CancellationToken ct)
    {
        await using var db = database.Open();
        var stamp = Guid.NewGuid().ToString();
        var count = await db.Roles.Where(x => x.Id == r.Id && x.ConcurrencyStamp == r.ConcurrencyStamp)
            .Set(x => x.Name, r.Name).Set(x => x.NormalizedName, r.NormalizedName).Set(x => x.ConcurrencyStamp, stamp).UpdateAsync(ct);
        if (count != 1) return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        r.ConcurrencyStamp = stamp;
        return IdentityResult.Success;
    }
    public async Task<IdentityResult> DeleteAsync(IdentityRole r, CancellationToken ct)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginTransactionAsync(ct);
        if (await db.Roles.Where(x => x.Id == r.Id && x.ConcurrencyStamp == r.ConcurrencyStamp).DeleteAsync(ct) != 1)
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        await db.UserRoles.Where(x => x.RoleId == r.Id).DeleteAsync(ct);
        await tx.CommitAsync(ct);
        return IdentityResult.Success;
    }
}
