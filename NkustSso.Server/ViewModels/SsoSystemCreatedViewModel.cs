// 系統建立成功頁資料，密鑰僅顯示一次。
using NkustSso.Server.Models;

namespace NkustSso.Server.ViewModels;

/// <summary>
/// 新增成功頁專用資料，包含僅顯示一次的系統密鑰；不可寫入一般紀錄。
/// </summary>
public sealed record SsoSystemCreatedViewModel(RegisteredSystem System, string Secret);
