using System.Diagnostics;

namespace NkustSso.Tests;

internal static class SqlServerBrowserChecks
{
    public static async Task RunAsync(string root, string script)
    {
        if (script is not ("e2e.py" or "webapi_sso.py")) throw new ArgumentException("不支援的 SQL Server 瀏覽器測試。");
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var start = new ProcessStartInfo("python") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root, "tests", script));
        start.Environment["NKUST_TEST_BUILD_DIRECTORY"] = AppContext.BaseDirectory;
        start.Environment["NKUST_TEST_SQLSERVER_CONNECTION"] = database.ConnectionString;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Console.Write(await output);
        Console.Error.Write(await errors);
        if (process.ExitCode != 0) throw new InvalidOperationException("SQL Server 瀏覽器測試失敗。");
    }
}
