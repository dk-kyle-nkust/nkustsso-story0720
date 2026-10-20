// 服務分類、系統數量及異動紀錄清單。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed record PortalCategoryListViewModel(List<PortalCategory> Categories, Dictionary<string, int> Counts, List<PortalCategoryAudit> Audits);
