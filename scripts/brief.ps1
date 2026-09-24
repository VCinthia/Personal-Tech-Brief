# One command to get your brief: starts the local stack, pulls fresh items from your sources,
# generates a brief, and opens it in your browser. Safe to run any time — it just brings things
# up to date. Double-click Brief.cmd (in the repo root) to run it.
#
# NOTE: we deliberately do NOT set $ErrorActionPreference = 'Stop'. Under Windows PowerShell 5.1
# that turns the harmless progress/warnings docker and docker compose print to stderr into fatal
# errors, and the window would close instantly. Native calls are checked via $LASTEXITCODE and the
# health probe; a top-level try/catch keeps the window open if anything unexpected happens.

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

# Read the web port from .env (defaults to 8080).
$webPort = 8080
if (Test-Path .env) {
    $line = Select-String -Path .env -Pattern '^\s*WEB_PORT\s*=\s*(\d+)' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($line) { $webPort = [int]$line.Matches[0].Groups[1].Value }
}
$baseUrl = "http://localhost:$webPort"

function Test-Docker { docker info 2>&1 | Out-Null; return ($LASTEXITCODE -eq 0) }

function Wait-For($url, $minutes) {
    $deadline = (Get-Date).AddMinutes($minutes)
    while ((Get-Date) -lt $deadline) {
        try { if ((Invoke-WebRequest -UseBasicParsing $url -TimeoutSec 3).StatusCode -eq 200) { return $true } } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

try {
    Write-Host ''
    Write-Host 'Personal Tech Brief' -ForegroundColor Cyan

    # 1. Make sure Docker Desktop is running.
    if (-not (Test-Docker)) {
        Write-Host 'Iniciando Docker Desktop (puede tardar un minuto)...'
        $dockerExe = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (Test-Path $dockerExe) { Start-Process $dockerExe }
        $deadline = (Get-Date).AddMinutes(3)
        while (-not (Test-Docker) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 3 }
        if (-not (Test-Docker)) {
            Write-Host 'No pude iniciar Docker. Abri Docker Desktop a mano y volve a intentar.' -ForegroundColor Red
            Read-Host 'Enter para cerrar'; return
        }
    }

    # 2. .env must exist and accept the licenses (one-time setup).
    if (-not (Test-Path .env)) {
        Copy-Item .env.example .env
        Write-Host 'Cree un archivo .env. Abrilo, pone una contrasena en MSSQL_SA_PASSWORD y ACCEPT_EULA=Y, y volve a correr.' -ForegroundColor Yellow
        Read-Host 'Enter para cerrar'; return
    }

    # 3. Start the stack (builds images the first time, reuses them afterwards).
    Write-Host 'Levantando la app...'
    docker compose up -d 2>&1 | Out-Null

    if (-not (Wait-For "$baseUrl/health/ready" 3)) {
        Write-Host 'La app tardo demasiado en responder. Reintenta en un momento.' -ForegroundColor Red
        Read-Host 'Enter para cerrar'; return
    }

    # 4. Pull fresh items from your enabled sources (one pass, then it exits).
    Write-Host 'Buscando novedades en tus fuentes...'
    docker compose run --rm ingestion 2>&1 | Out-Null

    # 5. Give the processor a moment to analyze the new items before composing the brief.
    Write-Host 'Analizando y armando tu brief...'
    Start-Sleep -Seconds 30

    # 6. Generate a fresh brief (of what is new since the last one).
    try { Invoke-RestMethod -Method Post "$baseUrl/api/v1/briefs" -TimeoutSec 15 | Out-Null } catch { }
    Start-Sleep -Seconds 12

    # 7. Open the brief. If nothing is new since your last brief, open History instead so you
    #    always land on something with content rather than an empty page.
    $target = "$baseUrl/brief"
    $emptyBrief = $false
    try {
        $current = Invoke-RestMethod "$baseUrl/api/v1/briefs/current" -TimeoutSec 8
        if (-not $current.brief -or [int]$current.brief.selectedCount -eq 0) { $target = "$baseUrl/history"; $emptyBrief = $true }
    } catch { }
    Start-Process $target

    Write-Host ''
    if ($emptyBrief) {
        Write-Host 'No hubo novedades relevantes desde tu ultimo brief. Abri tu historial de briefs.' -ForegroundColor Green
    } else {
        Write-Host "Listo. Tu brief se abrio en el navegador ($baseUrl/brief)." -ForegroundColor Green
    }
    Write-Host 'Cuando termines, corre Stop.cmd para apagar la app (o dejala corriendo).'
    Start-Sleep -Seconds 4
}
catch {
    Write-Host ''
    Write-Host "Ocurrio un problema: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Enter para cerrar'
}
