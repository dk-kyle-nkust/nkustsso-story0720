// 身分類別維護頁的選項及篩選條件。
using NkustSso.Server.Services;

namespace NkustSso.Server.ViewModels;

public sealed record IdentityCatalogListViewModel(IdentityCatalogSnapshot Catalog, string Dimension);
