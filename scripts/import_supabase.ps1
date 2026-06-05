param(
    [string]$ConnectionString = "",
    [string]$FixturePath = "",
    [switch]$Flush
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$EnvPath = Join-Path $ProjectRoot ".env"

function Get-EnvFileValue {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path $Path)) {
        return ""
    }

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) {
            continue
        }

        $parts = $trimmed.Split("=", 2)
        if ($parts.Length -eq 2 -and $parts[0].Trim() -eq $Name) {
            return $parts[1].Trim().Trim('"').Trim("'")
        }
    }

    return ""
}

if (-not $ConnectionString) {
    $ConnectionString = $env:SUPABASE_DATABASE_URL
}

if (-not $ConnectionString) {
    $ConnectionString = Get-EnvFileValue -Path $EnvPath -Name "SUPABASE_DATABASE_URL"
}

if (-not $ConnectionString) {
    throw "Set SUPABASE_DATABASE_URL in .env or pass -ConnectionString."
}

if (-not $FixturePath) {
    $FixturePath = Join-Path $env:TEMP "diplom_supabase_seed.json"
}

if (-not (Test-Path $FixturePath)) {
    throw "Fixture file was not found: $FixturePath"
}

$uri = [Uri]$ConnectionString
if ($uri.Scheme -ne "postgres" -and $uri.Scheme -ne "postgresql") {
    throw "SUPABASE_DATABASE_URL must start with postgres:// or postgresql://"
}

$userInfoParts = $uri.UserInfo.Split(":", 2)
if ($userInfoParts.Length -ne 2) {
    throw "Connection string must include database username and password."
}

$env:DATABASE_URL = ""
$env:DB_ENGINE = "postgres"
$env:POSTGRES_HOST = $uri.Host
$env:POSTGRES_PORT = if ($uri.Port -gt 0) { [string]$uri.Port } else { "5432" }
$env:POSTGRES_DB = $uri.AbsolutePath.TrimStart("/")
$env:POSTGRES_USER = [Uri]::UnescapeDataString($userInfoParts[0])
$env:POSTGRES_PASSWORD = [Uri]::UnescapeDataString($userInfoParts[1])
$env:POSTGRES_SSLMODE = "require"

Push-Location $ProjectRoot
try {
    .\.venv\Scripts\python.exe manage.py migrate

    if ($Flush) {
        .\.venv\Scripts\python.exe manage.py flush --no-input
        .\.venv\Scripts\python.exe manage.py migrate
    }

    .\.venv\Scripts\python.exe manage.py loaddata $FixturePath
    .\.venv\Scripts\python.exe manage.py sync_roles
    .\.venv\Scripts\python.exe manage.py check
}
finally {
    Pop-Location
}

Write-Host "Supabase import finished."
