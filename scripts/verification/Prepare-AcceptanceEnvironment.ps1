[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [ValidatePattern('^CourierService_TaskVerification_[A-Za-z0-9_]+$')]
    [string]$DatabaseName = 'CourierService_TaskVerification_20261012_Remaining',
    [ValidateRange(1024, 65535)][int]$Port = 48131,
    [ValidateRange(1024, 65535)][int]$SmtpPort = 48132
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $source 'CourierService.sln'))) { throw 'SourceDirectory must be a built courier source checkout.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$master = New-Object Data.SqlClient.SqlConnection('Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;Connect Timeout=10;')
try {
    $master.Open()
    $exists = $master.CreateCommand()
    $exists.CommandText = 'SELECT DB_ID(@Name);'
    $null = $exists.Parameters.AddWithValue('@Name', $DatabaseName)
    if ($exists.ExecuteScalar() -isnot [DBNull]) { throw "Refusing to reuse existing database $DatabaseName; choose a new isolated name." }
} finally { $master.Dispose() }
foreach ($file in @('schema.sql', 'seed.sql')) {
    $sql = [IO.File]::ReadAllText((Join-Path $source "db/$file")).Replace('CourierService', $DatabaseName)
    $destination = Join-Path $output $file
    [IO.File]::WriteAllText($destination, $sql, (New-Object Text.UTF8Encoding($true)))
    & sqlcmd -S '(localdb)\MSSQLLocalDB' -E -i $destination -b -l 10
    if ($LASTEXITCODE -ne 0) { throw "Isolated database setup failed at $file." }
}
$configPath = Join-Path $source 'Web/Web.config'
[xml]$config = [IO.File]::ReadAllText($configPath)
$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$DatabaseName;Trusted_Connection=True;"
$config.configuration.connectionStrings.add.SetAttribute('connectionString', $connectionString)
$settings = @{
    'Https.RedirectEnabled' = 'false'; 'Frontend.MockEnabled' = 'false';
    'Smtp.Host' = '127.0.0.1'; 'Smtp.Port' = "$SmtpPort"; 'Smtp.UseSsl' = 'false';
    'Smtp.FromAddress' = 'acceptance@example.test'; 'Notifications.WorkerEnabled' = 'true';
    'Notifications.PollSeconds' = '1'; 'Notifications.MaxAttempts' = '2';
    'Notifications.RetryDelaySeconds' = '1'; 'Sms.Enabled' = 'false'
}
foreach ($key in $settings.Keys) {
    $node = $config.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if (-not $node) { $node = $config.CreateElement('add'); $node.SetAttribute('key', $key); $null = $config.configuration.appSettings.AppendChild($node) }
    $node.SetAttribute('value', $settings[$key])
}
$config.Save($configPath)
$site = Join-Path $source 'Web'
$iis = 'C:/Program Files/IIS Express/iisexpress.exe'
if (-not (Test-Path -LiteralPath $iis)) { throw 'IIS Express is required.' }
$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, $Port)
try { $listener.Start() } finally { $listener.Stop() }
$process = Start-Process -FilePath $iis -ArgumentList @("/path:$site", "/port:$Port", '/systray:false') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'iis.stdout.log') -RedirectStandardError (Join-Path $output 'iis.stderr.log')
$metadata = [pscustomobject]@{
    database = $DatabaseName; connectionString = $connectionString; source = $source;
    sourceCommit = (& git -C $source rev-parse HEAD).Trim(); baseUrl = "http://localhost:$Port";
    smtpPort = $SmtpPort; iisProcessId = $process.Id; startedAtUtc = [DateTime]::UtcNow.ToString('o');
    syntheticOnly = $true; https = $false; deployedToDemoMachine = $false
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'environment.json') -Encoding UTF8
Write-Output "Isolated acceptance site starting at $($metadata.baseUrl), database $DatabaseName, process $($process.Id)."
