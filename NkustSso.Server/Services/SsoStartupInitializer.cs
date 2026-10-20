// 啟動時建立必要資料，依設定建立初始管理者。
using Microsoft.AspNetCore.Identity;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>初始化 Server 必要資料；只在明確設定時建立本機管理者，不注入示範用戶端。</summary>
public static class SsoStartupInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseFactory>().InitializeAsync();
        await PortalDefaultDataInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<DatabaseFactory>());
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(LoginSessionAdministration.Role))
        {
            Ensure(await roles.CreateAsync(new IdentityRole(LoginSessionAdministration.Role)));
        }

        var name = configuration["Sso:Administrator:UserName"];
        var password = configuration["Sso:Administrator:Password"];
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrEmpty(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("建立本機管理者須設定 Sso:Administrator:UserName。");
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        // 重啟不得覆寫既有密碼、解除鎖定，或重新授予已撤除的權限。
        if (await users.FindByNameAsync(name) is not null)
        {
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("建立本機管理者須設定 Sso:Administrator:Password。");
        }

        var account = new Account { UserName = name, DisplayName = "系統管理員", IdentityType = "staff" };
        Ensure(await users.CreateAsync(account, password));
        Ensure(await users.AddToRoleAsync(account, LoginSessionAdministration.Role));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
