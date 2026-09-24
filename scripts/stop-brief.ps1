# Stops the local stack. Your data (interests, sources, briefs) is kept — the next Brief.cmd
# picks up where you left off. Double-click Stop.cmd (in the repo root) to run it.
#
# NOTE: no $ErrorActionPreference = 'Stop' on purpose — under Windows PowerShell 5.1 the progress
# docker compose writes to stderr would otherwise be treated as a fatal error.

Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

try {
    Write-Host 'Apagando la app (se conservan tus datos)...'
    docker compose stop 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'Listo. La app esta apagada. Corre Brief.cmd cuando quieras tu proximo brief.' -ForegroundColor Green
    } else {
        Write-Host 'No se pudo apagar del todo. Revisa que Docker Desktop este abierto.' -ForegroundColor Yellow
        Read-Host 'Enter para cerrar'
    }
    Start-Sleep -Seconds 3
}
catch {
    Write-Host "Ocurrio un problema: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Enter para cerrar'
}
