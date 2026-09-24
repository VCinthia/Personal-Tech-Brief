[CmdletBinding()]
param(
    [ValidateRange(1, 86400)]
    [int] $IntervalSeconds = 14400,
    [switch] $Once
)

$ErrorActionPreference = 'Stop'
$repositoryPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryPath 'src/dotnet/PersonalTechBrief.Ingestion'

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__PersonalTechBrief)) {
    throw 'Set ConnectionStrings__PersonalTechBrief to your local SQL connection before running ingestion. See README.md.'
}

Push-Location -LiteralPath $repositoryPath
try {
    do {
        & dotnet run --project $projectPath --configuration Release --no-build
        $ingestionExitCode = $LASTEXITCODE
        if ($Once) {
            exit $ingestionExitCode
        }

        if ($ingestionExitCode -ne 0) {
            Write-Warning "Ingestion exited with code $ingestionExitCode. Check the correlated run logs; the next invocation remains scheduled."
        }

        Write-Host "Next ingestion invocation in $IntervalSeconds seconds. Press Ctrl+C to stop."
        Start-Sleep -Seconds $IntervalSeconds
    } while ($true)
}
finally {
    Pop-Location
}
