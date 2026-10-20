// 以新校務資料驗證 SOAP／JSON 呼叫中的帳密。
using LinqToDB;
using NkustSso.Server.Models;
using NkustSso.Server.Models.External;

namespace NkustSso.Server.Services.Sources;

// 新串接統一使用校務 MSSQL，不回退舊 LDAP／Sybase，也不執行舊密碼同步。
public sealed class CampusSoapAccountSource(ICampusDatabaseFactory campus) : IIntegrationAccountSource
{
    public async Task<ServiceVerification> VerifyAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        await using var db = campus.Open();

        // 取兩筆就夠判斷「結果不只一筆」。
        var rows = await db.GetTable<CampusCredentialRecord>()
            .Where(a => a.MemberId == userName)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rows.Count != 1)
        {
            return new ServiceVerification(false);
        }

        var account = rows[0];
        var valid = TienFangPasswordVerifier.Verify(account.EncodedPassword, password);

        // 比對完立刻清掉記憶體中的密碼欄位。
        account.EncodedPassword = null;

        if (!valid || string.IsNullOrWhiteSpace(account.StatusCode))
        {
            return new ServiceVerification(false);
        }

        return new ServiceVerification(true, account.StatusCode.Trim());
    }
}
