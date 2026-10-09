param([string]$Version = '1.7.0')
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $taskRepo
try {
    dotnet msbuild T50LabelPrinter.sln /t:Rebuild /p:Configuration=Release /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $manifest = Get-Content 'dist\T50LabelPrinter\easyupdate.json' -Raw | ConvertFrom-Json
    if ($manifest.version_name -ne $Version) { throw 'Requested version does not match easyupdate.json.' }
    $taskArchive = Join-Path $taskRepo "dist\T50LabelPrinter-v$Version.zip"
    Compress-Archive -Path 'dist\T50LabelPrinter\*' -DestinationPath $taskArchive -Force
    $taskHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  T50LabelPrinter-v$Version.zip" | Set-Content 'dist\SHA256SUMS.txt' -Encoding ascii
    Write-Output $taskArchive
    Write-Output $taskHash
} finally { Pop-Location }
