[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._/-]*$')][string]$ReleaseRef,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$MSBuildPath,
    [switch]$Candidate
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$commit = (& git -C $repository rev-parse --verify "$ReleaseRef^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw 'ReleaseRef must identify an existing commit.' }
$tag = & git -C $repository describe --exact-match --tags $commit 2>$null
if (-not $Candidate -and $LASTEXITCODE -ne 0) { throw 'Final packages require a tagged commit. Use -Candidate only for preparation checks.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Choose a new output directory; existing packages are never overwritten.' }
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Supply -MSBuildPath for Visual Studio MSBuild.' }
    $MSBuildPath = (& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
}
if (-not (Test-Path -LiteralPath $MSBuildPath)) { throw 'Visual Studio MSBuild was not found.' }
New-Item -ItemType Directory -Path $output | Out-Null
$sourceZip = Join-Path $output 'source.zip'
& git -C $repository archive --format=zip "--output=$sourceZip" $commit
if ($LASTEXITCODE -ne 0) { throw 'Could not archive the release commit.' }
$source = Join-Path $output 'source'
Expand-Archive -LiteralPath $sourceZip -DestinationPath $source
$web = Join-Path $output 'web'
& $MSBuildPath (Join-Path $source 'Web\CourierService.Web.csproj') /restore /t:Build /p:Configuration=Release /p:MvcBuildViews=true /p:DeployOnBuild=true /p:PublishProfile=DemoFolder "/p:DemoPublishDirectory=$web" /p:DeleteExistingFiles=false /v:quiet /nologo
if ($LASTEXITCODE -ne 0) { throw 'Release build or filesystem publish failed.' }
& dotnet test (Join-Path $source 'Tests\CourierService.Tests.csproj') --filter 'FullyQualifiedName!~CourierService.Tests.Integration' --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed; package is not ready.' }
foreach ($required in @('Web.config', 'bin\CourierService.Web.dll', 'Views\Account\Login.cshtml', 'Scripts\app\courier-app.js', 'Scripts\html5-qrcode.min.js', 'Content\Images\icon_inverted.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $web $required))) { throw "Published asset missing: $required" }
}
[xml]$config = Get-Content -LiteralPath (Join-Path $web 'Web.config') -Raw
if ($config.configuration.'system.web'.compilation.debug -ne 'false' -or $config.configuration.'system.web'.httpCookies.requireSSL -ne 'true') {
    throw 'Release configuration transform was not applied.'
}
$files = @(Get-ChildItem -LiteralPath $web -File -Recurse | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName.Substring($web.Length).TrimStart('\'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[pscustomobject]@{
    commit = $commit; tag = $tag; candidate = [bool]$Candidate; preparedAtUtc = [DateTime]::UtcNow.ToString('o')
    deployed = $false; hardwareVerified = $false; files = $files
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Compress-Archive -Path (Join-Path $web '*') -DestinationPath (Join-Path $output 'web.zip')
Write-Output "Package prepared at $output from $commit. No IIS site or database was changed."
