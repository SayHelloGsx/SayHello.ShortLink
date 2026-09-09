[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipMigration,
    [switch]$SkipHealthCheck,
    [switch]$GenerateCertificate,
    [switch]$ForceRegenerateCertificate,
    [int]$HealthTimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$deployDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $deployDirectory
$environmentFile = Join-Path $deployDirectory '.env'
$environmentExampleFile = Join-Path $deployDirectory 'env.example'
$certificateFile = Join-Path $deployDirectory 'secrets/openiddict.pfx'

function Write-Stage {
    param([Parameter(Mandatory)][string]$Message)

    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Invoke-DockerCompose {
    param([Parameter(Mandatory)][string[]]$ComposeArguments)

    & docker compose @ComposeArguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($ComposeArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-DotEnvValue {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $pattern = '^[\s]*' + [regex]::Escape($Name) + '[\s]*=[\s]*(.*)$'
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match $pattern) {
            $value = $Matches[1].Trim()
            if ($value.Length -ge 2 -and
                (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
                return $value.Substring(1, $value.Length - 2)
            }

            return $value
        }
    }

    return $null
}

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function New-OpenIddictCertificate {
    param([Parameter(Mandatory)][string]$Password)

    Assert-Command 'openssl'

    $secretsDirectory = Split-Path -Parent $certificateFile
    New-Item -ItemType Directory -Path $secretsDirectory -Force | Out-Null

    $keyFile = Join-Path ([System.IO.Path]::GetTempPath()) ("shortlink-" + [guid]::NewGuid().ToString('N') + '.key')
    $certificatePemFile = Join-Path ([System.IO.Path]::GetTempPath()) ("shortlink-" + [guid]::NewGuid().ToString('N') + '.crt')

    try {
        & openssl req -x509 -newkey rsa:4096 -sha256 -days 825 -nodes `
            -subj '/CN=ShortLink OpenIddict' -keyout $keyFile -out $certificatePemFile
        if ($LASTEXITCODE -ne 0) {
            throw "OpenSSL failed while creating the OpenIddict certificate."
        }

        # This follows the existing shell script. OpenSSL accepts the PFX password only as an argument.
        & openssl pkcs12 -export -out $certificateFile -inkey $keyFile -in $certificatePemFile `
            -passout "pass:$Password"
        if ($LASTEXITCODE -ne 0) {
            throw "OpenSSL failed while exporting the OpenIddict certificate."
        }
    }
    finally {
        Remove-Item -LiteralPath $keyFile, $certificatePemFile -Force -ErrorAction SilentlyContinue
    }
}

function Wait-ForWebReadiness {
    param([Parameter(Mandatory)][int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        & docker compose exec -T web curl --fail --silent http://localhost:8080/health/ready
        if ($LASTEXITCODE -eq 0) {
            Write-Host 'Web application is ready.' -ForegroundColor Green
            return
        }

        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)

    throw "Web application did not become ready within $TimeoutSeconds seconds. Run 'docker compose logs web caddy' to diagnose."
}

if ($HealthTimeoutSeconds -lt 1) {
    throw 'HealthTimeoutSeconds must be at least 1.'
}

Assert-Command 'docker'
& docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Compose v2 is required.'
}

if (-not (Test-Path -LiteralPath $environmentFile -PathType Leaf)) {
    Copy-Item -LiteralPath $environmentExampleFile -Destination $environmentFile
    throw "Created '$environmentFile'. Fill in every placeholder, then run this script again."
}

$certificatePassword = Get-DotEnvValue -Path $environmentFile -Name 'OPENIDDICT_CERTIFICATE_PASSWORD'
if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
    throw 'OPENIDDICT_CERTIFICATE_PASSWORD must be set in deploy/.env.'
}

if ($ForceRegenerateCertificate -and -not $GenerateCertificate) {
    throw 'ForceRegenerateCertificate requires GenerateCertificate.'
}

if ($GenerateCertificate -and ((-not (Test-Path -LiteralPath $certificateFile)) -or $ForceRegenerateCertificate)) {
    Write-Stage 'Generating the OpenIddict signing certificate'
    New-OpenIddictCertificate -Password $certificatePassword
}

if (-not (Test-Path -LiteralPath $certificateFile -PathType Leaf)) {
    throw "Missing '$certificateFile'. Run with -GenerateCertificate (requires OpenSSL), or generate it with scripts/generate-openiddict-certificate.sh."
}

Push-Location $deployDirectory
try {
    Write-Stage 'Validating Docker Compose configuration'
    Invoke-DockerCompose -ComposeArguments @('config', '--quiet')

    if (-not $SkipBuild) {
        Write-Stage 'Building the ShortLink image'
        Invoke-DockerCompose -ComposeArguments @('build', 'web')
    }

    Write-Stage 'Starting PostgreSQL and Redis'
    Invoke-DockerCompose -ComposeArguments @('up', '-d', 'postgres', 'redis')

    if (-not $SkipMigration) {
        Write-Stage 'Running database migrations'
        Invoke-DockerCompose -ComposeArguments @('--profile', 'migration', 'run', '--rm', 'dbmigrator')
    }

    Write-Stage 'Starting the web application and Caddy'
    Invoke-DockerCompose -ComposeArguments @('up', '-d', 'web', 'caddy')
    Invoke-DockerCompose -ComposeArguments @('ps')

    if (-not $SkipHealthCheck) {
        Write-Stage 'Waiting for the readiness endpoint'
        Wait-ForWebReadiness -TimeoutSeconds $HealthTimeoutSeconds
    }

    $domain = Get-DotEnvValue -Path $environmentFile -Name 'DOMAIN'
    if (-not [string]::IsNullOrWhiteSpace($domain)) {
        Write-Host "Deployment complete. External readiness URL: https://$domain/health/ready" -ForegroundColor Green
    }
    else {
        Write-Host 'Deployment complete.' -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
