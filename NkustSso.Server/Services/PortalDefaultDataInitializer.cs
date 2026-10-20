// 首次啟動時匯入校方公開服務連結。
using System.Text.Json;
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>校方公開指南的服務入口；只匯入一次，不建立帳號、密鑰或假 SSO 串接。</summary>
public static class PortalDefaultDataInitializer
{
    // 來源與查核日期見 docs/portal-design.md。S / E / A 分別為學生、教職員、校友目錄。
    private sealed record Entry(string Id, string Name, string Url, string Category, string Areas);
    private static readonly Entry[] Entries =
    [
        new("link-stdsel", "選課系統", "https://stdsel.nkust.edu.tw/dispatch", "academic", "S"),
        new("link-selcrs", "選課管理", "https://selcrs.nkust.edu.tw/selcrs/Account/Login", "academic", "E"),
        new("link-elearning", "教學平台", "https://elearning.nkust.edu.tw/", "academic", "SE"),
        new("link-waiver", "學分抵免申請", "https://ws3.nkust.edu.tw/Waiver", "academic", "SE"),
        new("link-addcrs", "選課表單申請暨簽核", "https://apply2.nkust.edu.tw/AddCrsApply", "academic", "SE"),
        new("link-centapp", "學籍成績證明申請", "https://centapp.nkust.edu.tw/Home", "academic", "SA"),
        new("link-oosaf", "學務資訊系統", "https://oosaf.nkust.edu.tw/", "campus", "SE"),
        new("link-leave", "線上請假", "https://oosaf.nkust.edu.tw/Student/Leave/Create", "campus", "S"),
        new("link-care", "預約諮商輔導", "https://care.nkust.edu.tw/", "campus", "S"),
        new("link-acadmis", "入學獎學金申請", "https://acadmis.nkust.edu.tw/", "campus", "SE"),
        new("link-activity", "活動訊息平台", "https://ws1.nkust.edu.tw/Activity/", "campus", "SEA"),
        new("link-ptsign", "兼任助理簽到", "https://ntapp.nkust.edu.tw/ptSign/", "campus", "S"),
        new("link-empservice", "在職服務證明申請", "https://empservice.nkust.edu.tw/EmployService", "hr", "E"),
        new("link-ohr", "進用暨保費管理", "https://ohr.nkust.edu.tw/", "hr", "E"),
        new("link-account02", "主計請購系統", "https://account02.nkust.edu.tw/", "finance", "E"),
        new("link-account03", "經費結報簽核", "https://account03.nkust.edu.tw/", "finance", "E"),
        new("link-vms", "校車預約", "https://vms.nkust.edu.tw/", "affairs", "SE"),
        new("link-webap0", "停車證申請", "https://webap0.nkust.edu.tw/nkust/", "affairs", "SE"),
        new("link-newvenue", "場地租借暨門禁", "https://newvenue.nkust.edu.tw/rental-system/", "affairs", "SEA"),
        new("link-fixall", "各式維修申請", "https://ws2.nkust.edu.tw/fixall", "affairs", "SE"),
        new("link-post", "郵件查詢", "https://post.nkust.edu.tw/", "affairs", "SE"),
        new("link-projectrad", "政府產學計畫", "https://ws3.nkust.edu.tw/ProjectRAD", "research", "E"),
        new("link-projectiac", "非政府產學計畫", "https://ws3.nkust.edu.tw/ProjectIAC", "research", "E"),
        new("link-higheredu", "教學補助申請", "https://higheredu.nkust.edu.tw/service/index?type=teacher", "research", "E"),
        new("link-docu", "文件流程查詢", "https://docu.nkust.edu.tw/", "document", "E"),
        new("link-sop", "業務流程 SOP", "https://sop.nkust.edu.tw/", "document", "E"),
        new("link-officemail", "公務電子郵件平台", "https://officemail.nkust.edu.tw/", "it", "E"),
        new("link-suggest", "校務建言", "https://suggest.nkust.edu.tw/", "general", "SEA"),
        new("link-primo", "圖書資源查詢", "https://nkust.primo.exlibrisgroup.com/discovery/search", "general", "SE"),
        new("link-giving", "捐贈興學", "https://giving.nkust.edu.tw/", "general", "EA"),
        new("link-alumni-center", "校友服務中心", "https://ascdc.nkust.edu.tw/", "general", "A"),
        new("link-alumni-system", "校友服務系統", "https://as.nkust.edu.tw/Login", "general", "A"),
        new("link-alumni-card", "校友證申請", "https://ascdc.nkust.edu.tw/p/412-1022-1425.php?Lang=zh-tw", "general", "A"),
        new("link-alumni-jobs", "就業職缺資訊", "https://ascdc.nkust.edu.tw/p/412-1022-8177.php", "general", "A"),
        new("link-alumni-benefits", "校友優惠資訊", "https://ascdc.nkust.edu.tw/p/412-1022-6565.php", "general", "A")
    ];

    public static async Task InitializeAsync(DatabaseFactory database)
    {
        await using var db = database.Open();
        await using var transaction = await db.BeginTransactionAsync();
        const string migration = "public-campus-directory-v1";
        if (await db.GetTable<SchemaMigration>().AnyAsync(m => m.Id == migration))
        {
            return;
        }

        var categories = await db.PortalCategories.Select(c => c.Key).ToListAsync();
        foreach (var (entry, index) in Entries.Select((entry, index) => (entry, index)))
        {
            var current = await db.RegisteredSystems.SingleOrDefaultAsync(s => s.ClientId == entry.Id);
            // 已改成其他網址、隱藏或已串接的入口由管理者決定是否發布，不自動公開。
            var changedByAdmin = current is not null
                && (current.Protocol != "link"
                    || current.EntryUrl != entry.Url
                    || !current.ShowOnPortal);
            if (changedByAdmin)
            {
                continue;
            }

            var student = entry.Areas.Contains('S');
            var employee = entry.Areas.Contains('E');
            var alumni = entry.Areas.Contains('A');
            if (current is null)
            {
                await db.InsertAsync(new RegisteredSystem
                {
                    ClientId = entry.Id,
                    DisplayName = entry.Name,
                    EntryUrl = entry.Url,
                    Protocol = "link",
                    Enabled = false,
                    Category = categories.Contains(entry.Category) ? entry.Category : PortalCategoryService.DefaultKey,
                    SortOrder = 100 + index,
                    PublicStudent = student,
                    PublicEmployee = employee,
                    PublicAlumni = alumni
                });
                var types = new List<string>();
                if (student)
                {
                    types.Add("student");
                }

                if (employee)
                {
                    types.AddRange(["teacher", "staff", "employee"]);
                }
                // 校友公開資訊供所有帳號查閱，不自行建立校友身分或猜測畢業狀態。
                await db.InsertAsync(new PortalAudience
                {
                    ClientId = entry.Id,
                    IdentityTypesJson = alumni ? "[]" : JsonSerializer.Serialize(types)
                });
            }
            else
            {
                await db.RegisteredSystems
                    .Where(s => s.ClientId == entry.Id)
                    .Set(s => s.PublicStudent, student)
                    .Set(s => s.PublicEmployee, employee)
                    .Set(s => s.PublicAlumni, alumni)
                    .Set(s => s.Revision, Guid.NewGuid().ToString("N"))
                    .UpdateAsync();
            }
        }
        await db.InsertAsync(new SchemaMigration { Id = migration });
        await transaction.CommitAsync();
    }
}
