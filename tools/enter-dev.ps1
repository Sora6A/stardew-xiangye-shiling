# Dot-source this file: . ./tools/enter-dev.ps1
# Scope all environment changes to the current PowerShell session.
$devRoot = Split-Path -Parent $PSScriptRoot
$devSdk = Join-Path $devRoot '.local/dotnet-sdk'
if (-not (Test-Path -LiteralPath (Join-Path $devSdk 'dotnet.exe'))) {
    throw 'Install the .NET SDK into .local/dotnet-sdk first; see docs/DEVELOPMENT.md.'
}
$env:DOTNET_ROOT = $devSdk
$env:DOTNET_CLI_HOME = Join-Path $devRoot '.local/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:PYTHONUTF8 = '1'
# Use a canonical workspace path: Windows user TEMP may contain an 8.3 alias.
$env:TEMP = Join-Path $devRoot '.local/tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$env:PATH = "$devSdk;$env:PATH"
$devPython = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python'
if (Test-Path -LiteralPath (Join-Path $devPython 'python.exe')) {
    $env:PATH = "$devPython;$env:PATH"
}
Write-Host "Development environment: $devRoot"
& dotnet --version
& python --version
