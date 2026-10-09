param([string]$Server = 'http://192.168.95.55:19910')
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskRefs = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$taskSdk = (dotnet --list-sdks | Select-Object -Last 1) -replace '^([^ ]+) \[(.*)\]$', '$2\$1'
$taskCompiler = Join-Path $taskSdk 'Roslyn\bincore\csc.dll'
$taskTestDirectory = Join-Path $taskRepo 'dist\update-integration'
New-Item -ItemType Directory -Path $taskTestDirectory -Force | Out-Null
$taskArguments = @('/nologo','/target:exe','/nostdlib+',"/out:$taskTestDirectory\UpdateIntegration.exe")
foreach ($taskLibrary in @('mscorlib','System','System.Core','System.Xml','System.Net.Http','System.Runtime.Serialization','System.IO.Compression','System.IO.Compression.FileSystem')) {
    $taskArguments += "/r:$taskRefs\$taskLibrary.dll"
}
$taskArguments += @((Join-Path $taskRepo 'src\T50LabelPrinter\EasyUpdateClient.cs'), (Join-Path $PSScriptRoot 'UpdateIntegration.cs'))
& dotnet $taskCompiler @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Update integration test build failed.' }
& "$taskTestDirectory\UpdateIntegration.exe" $Server $taskTestDirectory
if ($LASTEXITCODE -ne 0) { throw 'Update integration test failed.' }
