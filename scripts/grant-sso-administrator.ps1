<#
.SYNOPSIS
    授予或撤除最高管理者（SsoAdministrator）資格。

.DESCRIPTION
    最高管理者是 ASP.NET Identity 角色，存在 AspNetUserRoles；後台「權限管理」頁
    管的是 AdminRoles／AdminRoleMembers，兩者是不同的表，該頁刻意不碰這個角色。
    設計上這份資格只能由技術端明確授予（見 README「本機管理者」一節），啟動程序
    不會對既有帳號補授，否則重啟就會把管理者撤除過的權限還原回去。

    啟用校務登入時本機管理者帳號無法建立 Session，所以校務帳號的第一位最高管理者
    只能用這個腳本指派，否則 /admin 會因為沒有任何最高管理者而完全進不去。

.PARAMETER UserName
    校務帳號或本機帳號名稱，例如 42665。

.PARAMETER Revoke
    撤除資格而非授予。不允許撤除最後一位，避免再次把自己鎖在外面。

.PARAMETER List
    只列出目前的最高管理者，不做任何異動。

.PARAMETER ConnectionString
    覆寫連線字串。未指定時讀取 NkustSso.Server/appsettings.json 的 ConnectionStrings:Sso。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 -List

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 42665

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/grant-sso-administrator.ps1 42665 -Revoke
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string]$UserName,

    [switch]$Revoke,

    [switch]$List,

    [string]$ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 角色名稱與 LoginSessionAdministration.Role 一致；Identity 以大寫形式比對。
$RoleName = 'SsoAdministrator'
$NormalizedRoleName = $RoleName.ToUpperInvariant()

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$settingsPath = Join-Path $repositoryRoot 'NkustSso.Server\appsettings.json'

function Get-SsoConnectionString {
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $ConnectionString
    }

    if (-not (Test-Path $settingsPath)) {
        throw "找不到 $settingsPath，請改用 -ConnectionString 指定連線字串。"
    }

    # 直接用 .NET 讀檔，讓它處理 UTF-8 BOM；Get-Content 在 5.1 會把 BOM 留在字串開頭。
    $settings = [System.IO.File]::ReadAllText($settingsPath) | ConvertFrom-Json

    $provider = 'SqlServer'
    if ($settings.PSObject.Properties.Name -contains 'Sso') {
        if ($settings.Sso.PSObject.Properties.Name -contains 'StorageProvider') {
            $provider = $settings.Sso.StorageProvider
        }
    }

    # 這個腳本只處理 SQL Server；SQLite 模式請直接以 SQLite 工具操作 .local\sso.db。
    if ($provider -ne 'SqlServer') {
        throw "Sso:StorageProvider 目前是 $provider，此腳本只支援 SqlServer。SQLite 模式請直接操作 .local\sso.db 的 AspNetUserRoles。"
    }

    $value = $settings.ConnectionStrings.Sso
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw 'appsettings.json 沒有 ConnectionStrings:Sso，請改用 -ConnectionString 指定連線字串。'
    }

    return $value
}

function Invoke-SsoQuery {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql

        # 一律用參數化查詢，不把帳號名稱拼進 SQL。
        foreach ($key in $Parameters.Keys) {
            [void]$command.Parameters.AddWithValue($key, $Parameters[$key])
        }

        $table = New-Object System.Data.DataTable
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
        try {
            [void]$adapter.Fill($table)
        }
        finally {
            $adapter.Dispose()
        }

        # 逗號前綴阻止 PowerShell 把 DataTable 展開成 DataRow 陣列。
        return ,$table
    }
    finally {
        $command.Dispose()
    }
}

function Invoke-SsoNonQuery {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sql,
        [hashtable]$Parameters = @{}
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql
        foreach ($key in $Parameters.Keys) {
            [void]$command.Parameters.AddWithValue($key, $Parameters[$key])
        }

        return $command.ExecuteNonQuery()
    }
    finally {
        $command.Dispose()
    }
}

function Show-Administrators {
    param([Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection)

    $sql = @'
SELECT u.UserName, u.Enabled, u.AuthenticationSource
FROM AspNetUserRoles ur
JOIN AspNetUsers u ON u.Id = ur.UserId
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE r.NormalizedName = @role
ORDER BY u.UserName
'@

    $rows = Invoke-SsoQuery -Connection $Connection -Sql $sql -Parameters @{ '@role' = $NormalizedRoleName }

    Write-Output ''
    Write-Output "目前的最高管理者（$RoleName）："
    if ($rows.Rows.Count -eq 0) {
        Write-Output '  （無）後台將完全無法進入，必須先指派一位。'
        return
    }

    foreach ($row in $rows.Rows) {
        $source = $row.AuthenticationSource
        if ([string]::IsNullOrWhiteSpace($source)) {
            $source = '本機帳號'
        }

        $state = '啟用'
        if (-not $row.Enabled) {
            $state = '已停用'
        }

        Write-Output ("  - {0}（{1}，{2}）" -f $row.UserName, $source, $state)
    }
}

if (-not $List -and [string]::IsNullOrWhiteSpace($UserName)) {
    throw '請指定 -UserName，或改用 -List 列出目前的最高管理者。'
}

$connection = New-Object System.Data.SqlClient.SqlConnection (Get-SsoConnectionString)
try {
    $connection.Open()

    # 角色由 SsoStartupInitializer 在啟動時建立，不存在代表伺服器還沒跑過。
    $roleRows = Invoke-SsoQuery -Connection $connection `
        -Sql 'SELECT Id FROM AspNetRoles WHERE NormalizedName = @role' `
        -Parameters @{ '@role' = $NormalizedRoleName }
    if ($roleRows.Rows.Count -eq 0) {
        throw "資料庫沒有 $RoleName 角色，請先啟動一次 Server 讓它初始化。"
    }

    if ($List) {
        Show-Administrators -Connection $connection
        return
    }

    $normalizedUserName = $UserName.Trim().ToUpperInvariant()
    $accountRows = Invoke-SsoQuery -Connection $connection `
        -Sql 'SELECT Id, UserName, Enabled, AuthenticationSource FROM AspNetUsers WHERE NormalizedUserName = @name' `
        -Parameters @{ '@name' = $normalizedUserName }

    if ($accountRows.Rows.Count -eq 0) {
        throw "找不到帳號 $UserName。校務帳號要先登入過一次，或由最高管理者在 /admin/access 以「加入校務人員」預先建立。"
    }

    $account = $accountRows.Rows[0]
    if (-not $Revoke -and -not $account.Enabled) {
        throw "帳號 $($account.UserName) 已停用，請先啟用再授予最高管理者。"
    }

    if ($Revoke) {
        # 撤除最後一位會讓後台再也進不去，只能回頭改資料庫，所以這裡直接擋下。
        $countRows = Invoke-SsoQuery -Connection $connection `
            -Sql 'SELECT COUNT(*) AS Total FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.NormalizedName = @role' `
            -Parameters @{ '@role' = $NormalizedRoleName }
        if ([int]$countRows.Rows[0].Total -le 1) {
            throw '這是最後一位最高管理者，撤除後將無法進入後台。請先指派另一位。'
        }

        if ($PSCmdlet.ShouldProcess($account.UserName, "撤除 $RoleName")) {
            $sql = @'
DELETE ur
FROM AspNetUserRoles ur
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE ur.UserId = @userId AND r.NormalizedName = @role
'@
            $affected = Invoke-SsoNonQuery -Connection $connection -Sql $sql -Parameters @{
                '@userId' = $account.Id
                '@role'   = $NormalizedRoleName
            }

            if ($affected -eq 0) {
                Write-Output "帳號 $($account.UserName) 原本就不是最高管理者，未做任何異動。"
            }
            else {
                Write-Output "已撤除 $($account.UserName) 的最高管理者資格。"
            }
        }
    }
    else {
        if ($PSCmdlet.ShouldProcess($account.UserName, "授予 $RoleName")) {
            # NOT EXISTS 讓重複執行不會失敗，也不會寫入第二筆。
            $sql = @'
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT @userId, r.Id
FROM AspNetRoles r
WHERE r.NormalizedName = @role
  AND NOT EXISTS (
      SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = @userId AND x.RoleId = r.Id
  )
'@
            $affected = Invoke-SsoNonQuery -Connection $connection -Sql $sql -Parameters @{
                '@userId' = $account.Id
                '@role'   = $NormalizedRoleName
            }

            if ($affected -eq 0) {
                Write-Output "帳號 $($account.UserName) 已經是最高管理者，未做任何異動。"
            }
            else {
                Write-Output "已授予 $($account.UserName) 最高管理者資格。"
                # 權限每次請求都重查資料庫，所以不必重新登入或重啟。
                Write-Output '下一個請求即生效，不需要重新登入或重啟服務。'
            }
        }
    }

    Show-Administrators -Connection $connection
}
finally {
    $connection.Dispose()
}
