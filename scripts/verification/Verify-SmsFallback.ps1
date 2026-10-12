[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$EnvironmentFile)
$ErrorActionPreference = 'Stop'
$environment = Get-Content -LiteralPath $EnvironmentFile -Raw | ConvertFrom-Json
if ($environment.database -notmatch '^CourierService_TaskVerification_[A-Za-z0-9_]+$' -or $environment.baseUrl -notmatch '^http://localhost:\d+$') { throw 'Only the isolated synthetic environment is allowed.' }
$connection = New-Object Data.SqlClient.SqlConnection($environment.connectionString)
$connection.Open()
$results = @()
function SignIn([string]$username) {
    $script:session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $body = @{ username = $username; password = 'Demo@2026!' } | ConvertTo-Json
    $null = Invoke-RestMethod -Uri "$($environment.baseUrl)/api/auth/login" -Method Post -ContentType 'application/json' -Body $body -WebSession $script:session
}
function PostJson([string]$path, $body) {
    Invoke-RestMethod -Uri ($environment.baseUrl + $path) -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 5) -WebSession $script:session
}
try {
    foreach ($scenario in @('Disabled', 'StubEnabled', 'MissingPhone')) {
        $configPath = Join-Path $environment.source 'Web/Web.config'
        [xml]$config = [IO.File]::ReadAllText($configPath)
        ($config.configuration.appSettings.add | Where-Object key -eq 'Sms.Enabled').SetAttribute('value', $(if ($scenario -eq 'Disabled') { 'false' } else { 'true' }))
        $config.Save($configPath)
        # A configuration change recycles the isolated site; wait on a read-only page before creating any data.
        $ready = $false
        for ($attempt = 0; $attempt -lt 15; $attempt++) {
            try { $null = Invoke-WebRequest -Uri "$($environment.baseUrl)/Account/Login" -UseBasicParsing -TimeoutSec 20; $ready = $true; break } catch { Start-Sleep -Seconds 1 }
        }
        if (-not $ready) { throw 'The isolated site did not restart.' }
        SignIn 'intake.demo'
        $package = PostJson '/api/packages' @{
            recipient = @{ fullName = "Synthetic SMS $scenario"; identifierNo = "QA-SMS-$scenario"; email = 'sms-acceptance@example.test'; phone = '0001234567' }
            senderName = 'Synthetic Sender'; packageType = 'Box'; classification = 'Personal'; storageLocationId = 1
        }
        if ($scenario -eq 'MissingPhone') {
            $command = $connection.CreateCommand()
            $command.CommandText = "UPDATE r SET PhoneNumber='' FROM dbo.Recipients r JOIN dbo.Packages p ON p.RecipientId=r.RecipientId WHERE p.PackageId=@Id;"
            $null = $command.Parameters.AddWithValue('@Id', $package.packageId)
            if ($command.ExecuteNonQuery() -ne 1) { throw 'Synthetic missing-phone setup failed.' }
        }
        SignIn 'storage.demo'
        $null = PostJson "/api/packages/$($package.f20Identifier)/status" @{ newStatus = 'InStorage' }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $updated = PostJson "/api/packages/$($package.f20Identifier)/status" @{ newStatus = 'ReadyForCollection' }
        $watch.Stop()
        if ($updated.status -ne 'ReadyForCollection') { throw 'Staff workflow did not complete.' }
        $rows = $null
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $command = $connection.CreateCommand()
            $command.CommandText = 'SELECT q.Channel, q.Status, q.AttemptCount, l.Status AS LogStatus, l.ErrorDetail FROM dbo.NotificationQueue q OUTER APPLY (SELECT TOP (1) Status, ErrorDetail FROM dbo.NotificationLog WHERE PackageId=q.PackageId AND Channel=q.Channel ORDER BY NotificationLogId DESC) l WHERE q.PackageId=@Id;'
            $null = $command.Parameters.AddWithValue('@Id', $package.packageId)
            $table = New-Object Data.DataTable
            $reader = $command.ExecuteReader()
            $table.Load($reader)
            $reader.Dispose()
            if ($table.Rows.Count -gt 0 -and -not ($table.Rows | Where-Object Status -eq 'Pending')) { $rows = $table.Rows; break }
            Start-Sleep -Seconds 1
        }
        if (-not $rows) { throw "Notifications did not finish in $scenario." }
        $email = $rows | Where-Object Channel -eq 'Email'
        $sms = $rows | Where-Object Channel -eq 'Sms'
        if ($email.Status -ne 'Sent' -or $email.LogStatus -ne 'Sent') { throw "Email fallback failed in $scenario." }
        if ($scenario -eq 'Disabled' -and $sms) { throw 'SMS was queued while disabled.' }
        if ($scenario -eq 'StubEnabled' -and ($sms.Status -ne 'Sent' -or $sms.LogStatus -ne 'Sent')) { throw 'Enabled SMS stub did not complete.' }
        if ($scenario -eq 'MissingPhone' -and ($sms.Status -ne 'Failed' -or $sms.LogStatus -ne 'Failed' -or $sms.ErrorDetail -notlike '*no phone number*')) { throw 'SMS failure was not recorded.' }
        $results += [pscustomobject]@{ scenario = $scenario; identifier = $package.f20Identifier; staffStatus = $updated.status; statusRequestMs = [Math]::Round($watch.Elapsed.TotalMilliseconds, 2); email = $email.Status; sms = $(if ($sms) { $sms.Status } else { 'Not queued' }); emailLog = $email.LogStatus; smsLog = $(if ($sms) { $sms.LogStatus } else { 'Not queued' }) }
        Write-Output "PASS $scenario - staff completed, email sent/logged, SMS state verified."
    }
} finally {
    $connection.Dispose()
    [xml]$config = [IO.File]::ReadAllText((Join-Path $environment.source 'Web/Web.config'))
    ($config.configuration.appSettings.add | Where-Object key -eq 'Sms.Enabled').SetAttribute('value', 'false')
    $config.Save((Join-Path $environment.source 'Web/Web.config'))
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path (Split-Path $EnvironmentFile) 'sms-results.json') -Encoding UTF8
