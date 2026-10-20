using LinqToDB.Mapping;
namespace NkustSso.Server.Models;

// 僅在 SSO 自有儲存層記錄已兌換摘要，不更新外部票據來源。
[Table("IntegrationConsumedTickets", IsColumnAttributeRequired = false)]
public sealed class IntegrationConsumedTicket
{
    [PrimaryKey] public string Hash { get; set; } = "";
    public long ExpiresAt { get; set; }
}
