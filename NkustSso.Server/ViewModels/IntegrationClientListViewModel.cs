// 帳密驗證串接系統清單、授權及稽核資料。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed record IntegrationClientListViewModel(List<IntegrationClient> Clients, List<IntegrationAudit> Audits, IntegrationServiceOptions Options,
    List<IntegrationClientService> Grants, List<IntegrationCapability> Services, string Kind, int ExistingCount, int NewCount);
