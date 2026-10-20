namespace NkustSso.Server.Models.External;

/// <summary>唯讀查詢結果；名稱缺少維持 null，不將代碼或其他紀錄當作名稱補入。</summary>
public sealed class StudentClassProfile
{
    public decimal StudentKey { get; set; }
    public string StudentId { get; set; } = "";
    public string? Name { get; set; }
    public string? ClassCode { get; set; }
    public string? ClassName { get; set; }
    public string? ClassShortName { get; set; }
    public string? UnitCode { get; set; }
    public string? UnitName { get; set; }
    public string? UnitShortName { get; set; }
    public string? DegreeCode { get; set; }
    public string? YearCode { get; set; }
    // 保留各來源校區供核對，尚未確認是否屬於複合關聯鍵。
    public int? StudentCampusCode { get; set; }
    public int? ClassCampusCode { get; set; }
    public int? UnitCampusCode { get; set; }
}
