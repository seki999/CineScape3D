param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [string]$OutputPath = "Builds/Windows-Cinema/CineScape3D.exe"
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity Editor 路径不存在：$UnityEditor" }
$consoleEditor = [System.IO.Path]::ChangeExtension($UnityEditor, '.com')
if (Test-Path -LiteralPath $consoleEditor) { $UnityEditor = $consoleEditor }
$logPath = Join-Path $projectRoot 'Builds/build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $logPath) | Out-Null
& $UnityEditor -batchmode -quit -projectPath $projectRoot -buildTarget Win64 -executeMethod CineScape.Editor.ProjectBuilder.BuildWindows -outputPath $OutputPath -logFile $logPath
if ($LASTEXITCODE -ne 0) { throw "构建失败，查看 $logPath" }
$exePath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $projectRoot $OutputPath }
if (-not (Test-Path -LiteralPath $exePath)) { throw '未发现 EXE，检查 Unity 日志。' }
Write-Output "已构建：$exePath；分发时保留同目录全部运行文件。"


