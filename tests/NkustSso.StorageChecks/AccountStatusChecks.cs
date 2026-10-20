using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NkustSso.Server.Models;

internal static class AccountStatusChecks
{
    public static async Task RunAsync(string directory, DatabaseFactory? factory = null)
    {
        Directory.CreateDirectory(directory);
        factory ??= new DatabaseFactory(directory);
        // 模擬尚未有狀態欄位的既有帳號表，驗證兩種 provider 原地補欄位而不清資料。
        await using (var db = factory.Open())
        {
            await db.CreateTableAsync<Account>();
            await db.InsertAsync(new Account
            {
                Id = "status-migration", UserName = "retained-account", DisplayName = "保留原姓名",
                PasswordHash = "synthetic-hash", Enabled = false
            });
            await db.ExecuteAsync("ALTER TABLE AspNetUsers DROP COLUMN SourceStatusCode");
            await db.ExecuteAsync("ALTER TABLE AspNetUsers DROP COLUMN SourceStatusName");
        }
        await factory.InitializeAsync();
        await using (var db = factory.Open())
        {
            var account = await db.Users.SingleAsync();
            if (account.DisplayName != "保留原姓名" || account.PasswordHash != "synthetic-hash"
                || account.Enabled || account.SourceStatusCode != "" || account.SourceStatusName != "")
                throw new Exception("狀態欄位升級未保留既有帳號。");
            account.SourceStatusCode = "10";
            account.SourceStatusName = "已離職人員";
            using var users = new LinqUserStore(factory);
            if (!(await users.UpdateAsync(account, default)).Succeeded)
                throw new Exception("來源狀態無法更新。");
        }
        await factory.InitializeAsync();
        await using (var db = factory.Open())
        {
            var account = await db.Users.SingleAsync();
            if (account.SourceStatusCode != "10" || account.SourceStatusName != "已離職人員")
                throw new Exception("重新初始化不應覆蓋已保存的來源狀態。");
        }
        Console.WriteLine($"PASS 來源狀態欄位升級、既有帳號保留與重啟保存 ({(factory.IsSqlServer ? "SQL Server" : "SQLite")})");
    }
}
