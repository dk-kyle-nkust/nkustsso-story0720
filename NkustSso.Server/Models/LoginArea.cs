namespace NkustSso.Server.Models;

/// <summary>只決定登入頁的提示文字，不作為帳號身分、驗證來源或授權依據。</summary>
public static class LoginArea
{
    public static string Normalize(string? value) => value is "student" or "employee" or "alumni" ? value : "";
    public static string Label(string value) => value switch
    {
        "student" => "學生專區",
        "employee" => "教職員專區",
        "alumni" => "校友專區",
        _ => "統一登入"
    };
}
