// 依舊 SOAP 方法分派查詢，套用回傳欄位授權。
using System.Data;
using NkustSso.Server.Models;
using NkustSso.Server.Services.Sources;

namespace NkustSso.Server.Services;

public static class LegacySoapOperationDispatcher
{
    public static async Task<LegacySoapResult> ExecuteAsync(
        LegacySoapRequest request,
        IntegrationClient client,
        IReadOnlySet<string> fields,
        CredentialVerificationService verification,
        ICampusSoapQueries? queries,
        bool soap12,
        CancellationToken token)
    {
        var method = request.Operation.Name;

        if (LegacySoapOperationCatalog.RequiresCaller(method))
        {
            var denied = CheckCaller(request, client, method, soap12);
            if (denied is not null)
            {
                return denied;
            }
        }

        if (method == "KuasSSO_Certify2")
        {
            return await VerifyAccountAsync(request, fields, verification, soap12, token);
        }

        if (method == "KuasSSO_Certify1")
        {
            return await RedeemTicketAsync(request, client, fields, queries, method, soap12, token);
        }

        if (method == "KuasSSO_SchCardSearch")
        {
            return await VerifySmartCardAsync(request, verification, method, soap12, token);
        }

        var returnsDataSet = LegacySoapOperationCatalog.ReturnsDataSet(method)
            && LegacySoapOperationCatalog.Available.Contains(method);
        if (returnsDataSet)
        {
            return await QueryDataSetAsync(request, fields, queries, method, soap12, token);
        }

        throw new InvalidOperationException("方法尚未完成資料對應。");
    }

    /// <summary>
    /// 部分舊方法會帶原系統的呼叫帳密；驗證失敗時回傳對應格式的拒絕結果，而不是 Fault。
    /// </summary>
    private static LegacySoapResult? CheckCaller(
        LegacySoapRequest request,
        IntegrationClient client,
        string method,
        bool soap12)
    {
        // 交通查詢沿用 usr_id／usr_pwd，其餘方法用 SearchUsr_ 前綴。
        var traffic = method.StartsWith("Get_Traffic_", StringComparison.Ordinal);
        var nameField = traffic ? "usr_id" : "SearchUsr_id";
        var passwordField = traffic ? "usr_pwd" : "SearchUsr_pwd";

        var valid = IntegrationClientRegistrationService.VerifyCaller(
            client,
            request.Get(nameField),
            request.Get(passwordField));
        if (valid)
        {
            return null;
        }

        var xml = LegacySoapOperationCatalog.ReturnsDataSet(method)
            ? LegacySoapContract.DataSetResponse(method, CampusSoapQueries.Error("501"), soap12)
            : LegacySoapContract.ArrayResponse(method, ["400", "管理者登入失敗!", false], soap12);

        return new LegacySoapResult(xml, "caller-denied");
    }

    private static async Task<LegacySoapResult> VerifyAccountAsync(
        LegacySoapRequest request,
        IReadOnlySet<string> fields,
        CredentialVerificationService verification,
        bool soap12,
        CancellationToken token)
    {
        var result = await verification.VerifyAsync(request.Get("usr_id"), request.Get("usr_pwd"), token);
        var includeStatus = fields.Contains(IntegrationReturnFields.Certify2Status);

        return new LegacySoapResult(
            LegacySoapContract.Response(result, soap12, includeStatus),
            result.Verified ? "success" : "failed",
            result.Unavailable);
    }

    private static async Task<LegacySoapResult> RedeemTicketAsync(
        LegacySoapRequest request,
        IntegrationClient client,
        IReadOnlySet<string> fields,
        ICampusSoapQueries? queries,
        string method,
        bool soap12,
        CancellationToken token)
    {
        var function = request.Get("fnc_id");

        // 只接受這個串接登記過的 fnc_id，避免其他系統的票據被拿來驗證。
        var allowed = client.AllowedFunctionIds
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Contains(function, StringComparer.Ordinal);
        if (!allowed)
        {
            var denied = LegacySoapContract.ArrayResponse(method, [false, "NoPass"], soap12);
            return new LegacySoapResult(denied, "function-denied");
        }

        if (queries is null)
        {
            throw new InvalidOperationException("校務查詢來源未設定。");
        }

        var result = await queries.RedeemAsync(request.Get("usr_id"), function, request.Get("Rnd_id"), token);

        var status = result.Verified
            ? (fields.Contains(method + "/sts_id") ? result.Status : "")
            : "NoPass";
        var xml = LegacySoapContract.ArrayResponse(method, [result.Verified, status], soap12);

        return new LegacySoapResult(xml, result.Verified ? "success" : "failed");
    }

    private static async Task<LegacySoapResult> VerifySmartCardAsync(
        LegacySoapRequest request,
        CredentialVerificationService verification,
        string method,
        bool soap12,
        CancellationToken token)
    {
        var result = await verification.VerifyAsync(request.Get("auth_id"), request.Get("auth_pwd"), token);

        object[] values = result.Verified
            ? ["200", "登入成功!", true]
            : ["500", "查詢使用者登入失敗!", false];
        var xml = LegacySoapContract.ArrayResponse(method, values, soap12);

        return new LegacySoapResult(xml, result.Verified ? "success" : "failed", result.Unavailable);
    }

    private static async Task<LegacySoapResult> QueryDataSetAsync(
        LegacySoapRequest request,
        IReadOnlySet<string> fields,
        ICampusSoapQueries? queries,
        string method,
        bool soap12,
        CancellationToken token)
    {
        if (queries is null)
        {
            throw new InvalidOperationException("校務查詢來源未設定。");
        }

        using var data = method == LegacySoapOperationCatalog.DocumentPayment
            ? await queries.DocumentPaymentAsync(
                request.Get("IDNO"),
                request.Get("Birthday"),
                request.Get("identity"),
                token)
            : await queries.TrafficAsync(
                method,
                request.Get(method == "Get_Traffic_Stud" ? "searchid" : "searchID"),
                token);

        var success = data.Tables.Count == 1
            && data.Tables[0].Rows.Count > 0
            && data.Tables[0].Rows[0]["message"].ToString() == "200";

        ClearUnauthorizedColumns(data, fields, method);

        return new LegacySoapResult(
            LegacySoapContract.DataSetResponse(method, data, soap12),
            success ? "success" : "failed");
    }

    /// <summary>
    /// 欄位名稱保留在回應裡以維持舊契約，但未授權的欄位值一律清空。
    /// </summary>
    private static void ClearUnauthorizedColumns(DataSet data, IReadOnlySet<string> fields, string method)
    {
        var contract = IntegrationReturnFields.Contracts.Single(c => c.Operation == method);

        foreach (DataTable table in data.Tables)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (IsColumnAllowed(contract, fields, method, column.ColumnName))
                {
                    continue;
                }

                foreach (DataRow row in table.Rows)
                {
                    row[column] = "";
                }
            }
        }
    }

    private static bool IsColumnAllowed(
        IntegrationReturnContract contract,
        IReadOnlySet<string> fields,
        string method,
        string columnName)
    {
        var field = contract.Fields.SingleOrDefault(f => f.Name == columnName);

        // 不在契約內或已標記不開放的欄位一律不給值。
        if (field is null || field.Blocked)
        {
            return false;
        }

        // Required 是固定回傳，其餘要這個串接有勾選才給。
        return field.Required || fields.Contains(method + "/" + field.Name);
    }
}

public sealed record LegacySoapResult(string Xml, string Outcome, bool Unavailable = false);
