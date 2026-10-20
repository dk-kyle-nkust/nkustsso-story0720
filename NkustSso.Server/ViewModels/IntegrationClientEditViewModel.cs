// 帳密驗證串接系統的編輯表單與可授權方法。
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

public sealed class IntegrationClientEditViewModel
{
    public IntegrationClientInput Client { get; set; } = new();
    public bool Creating { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public List<IntegrationCapability> Services { get; set; } = [];
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public string ImportBatchId { get; set; } = "";
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public long? ImportedAt { get; set; }
}
