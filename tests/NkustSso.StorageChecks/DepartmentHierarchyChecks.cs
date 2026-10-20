using NkustSso.Server.Models;
using NkustSso.Server.Services;

internal static class DepartmentHierarchyChecks
{
    public static void Run()
    {
        var units = new[] { "IC02", "HR00", "IC00", "IC01", "ZZ01", "ICAB", "IC001", "IC" }
            .Select(code => new IdentityOption { Dimension = "department", Code = code, Label = code }).ToList();
        var parents = DepartmentHierarchy.Parents(units);
        Check(parents.Count == 2 && parents["IC01"] == "IC00" && parents["IC02"] == "IC00",
            "Only four-character numeric suffixes with an existing parent form a hierarchy.");
        Check(DepartmentHierarchy.Order(units).Select(o => o.Code).SequenceEqual(["HR00", "IC00", "IC02", "IC01", "ZZ01", "ICAB", "IC001", "IC"]),
            "Parents precede children, preserving source order within each level and retaining unmatched units.");
        var catalog = new IdentityCatalogSnapshot([new() { Key = "department" }],
            [.. units, new() { Dimension = "identity_type", Code = "staff", Family = "employee" }]);
        var rule = new PortalAudienceSelection { Mode = "rules", Groups = [new() { Family = "any",
            Conditions = [new() { Dimension = "department", Values = ["IC00"] }] }] };
        rule.ValidateCatalog(catalog, new());
        Check(rule.Matches([new("staff", new() { ["department"] = "IC00" })], catalog)
            && !rule.Matches([new("staff", new() { ["department"] = "IC01" })], catalog),
            "Displaying hierarchy must not broaden a parent-only rule to child departments.");
        rule.Groups[0].Conditions[0].Values = ["IC00", "IC01"];
        Check(rule.Matches([new("staff", new() { ["department"] = "IC01" })], catalog)
            && !rule.Matches([new("staff", new() { ["department"] = "IC02" })], catalog),
            "Only explicitly selected children match.");
        units.RemoveAll(o => o.Code == "IC00");
        Check(DepartmentHierarchy.Parents(units).Count == 0 && DepartmentHierarchy.Order(units).Count == units.Count,
            "Removed parents leave all child options independently visible.");
        Console.WriteLine("PASS department hierarchy, orphan retention, source ordering and unchanged exact-code matching");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
