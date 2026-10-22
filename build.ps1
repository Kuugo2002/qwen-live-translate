param([switch]$Portable)
$ErrorActionPreference = 'Stop'
$sdkPath = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path $sdkPath) { $sdkPath } else { 'dotnet' }
$projectPath = Join-Path $PSScriptRoot 'QwenLiveTranslate/QwenLiveTranslate.csproj'
$outputPath = Join-Path $PSScriptRoot 'dist/俄语实时翻译助手'
if ($Portable) {
    & $dotnetCommand publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outputPath
} else {
    & $dotnetCommand publish $projectPath -c Release -r win-x64 --self-contained false -o $outputPath
}
if ($LASTEXITCODE -ne 0) { throw '构建失败。' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs/usage.zh-CN.md') -Destination (Join-Path $outputPath '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $outputPath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.en.md') -Destination $outputPath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $outputPath -Recurse -Force
Write-Host "程序已生成：$outputPath"
