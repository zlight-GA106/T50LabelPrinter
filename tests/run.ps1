param([string]$OutputDirectory = "$PSScriptRoot\..\dist\verification")
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $taskRepo
try {
    dotnet msbuild T50LabelPrinter.sln /t:Rebuild /p:Configuration=Release /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $testDirectory = Join-Path $taskRepo 'dist\tests'
    New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe "/out:$testDirectory\RegressionTests.exe" /r:dist\T50LabelPrinter\T50LabelPrinter.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll tests\RegressionTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    Copy-Item -LiteralPath dist\T50LabelPrinter\T50LabelPrinter.exe -Destination $testDirectory
    & "$testDirectory\RegressionTests.exe" $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
} finally { Pop-Location }
