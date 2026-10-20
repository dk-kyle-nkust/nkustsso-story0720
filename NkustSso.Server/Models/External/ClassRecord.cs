using LinqToDB;
using LinqToDB.Mapping;

namespace NkustSso.Server.Models.External;

[Table("s90_class", Schema = "dbo", IsColumnAttributeRequired = true)]
public sealed class ClassRecord
{
    [Column("cls_id", DataType = DataType.VarChar, Length = 8), NotNull]
    public string Code { get; set; } = "";

    [Column("cls_name_full", DataType = DataType.VarChar, Length = 45), Nullable]
    public string? Name { get; set; }

    [Column("cls_name_abr", DataType = DataType.VarChar, Length = 24), Nullable]
    public string? ShortName { get; set; }

    [Column("unt_id", DataType = DataType.VarChar, Length = 4), Nullable]
    public string? UnitCode { get; set; }

    [Column("dgr_id", DataType = DataType.VarChar, Length = 2), Nullable]
    public string? DegreeCode { get; set; }

    [Column("cls_year", DataType = DataType.VarChar, Length = 1), Nullable]
    public string? YearCode { get; set; }

    [Column("cmp_area"), Nullable]
    public int? CampusCode { get; set; }
}
