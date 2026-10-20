// 將校務單位整理為前台可閱讀的層級。
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>依四碼單位編碼呈現兩層關係，僅供顯示，不擴張入口規則的比對範圍。</summary>
public static class DepartmentHierarchy
{
    public static IReadOnlyDictionary<string, string> Parents(IEnumerable<IdentityOption> options)
    {
        var codes = options
            .Where(o => o.Dimension == "department")
            .Select(o => o.Code)
            .ToHashSet(StringComparer.Ordinal);
        return codes.Where(code => code.Length == 4 && char.IsAsciiDigit(code[2]) && char.IsAsciiDigit(code[3])
                && !code.EndsWith("00", StringComparison.Ordinal) && codes.Contains(code[..2] + "00"))
            .ToDictionary(code => code, code => code[..2] + "00", StringComparer.Ordinal);
    }

    public static List<IdentityOption> Order(IEnumerable<IdentityOption> options)
    {
        var units = options.ToList();
        var parents = Parents(units);
        var children = units
            .Where(o => parents.ContainsKey(o.Code))
            .ToLookup(o => parents[o.Code], StringComparer.Ordinal);

        // 保留來源對上層及同層單位的排序，找不到上層的代碼仍獨立顯示。
        return units
            .Where(o => !parents.ContainsKey(o.Code))
            .SelectMany(o => new[] { o }.Concat(children[o.Code]))
            .ToList();
    }
}
