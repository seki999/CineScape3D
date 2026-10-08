param(
    [string]$EditorRoot = "$env:LOCALAPPDATA\CineScapeBuildTools\Unity-6000.0.23f1",
    [switch]$InstallOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$cacheRoot = Join-Path $env:LOCALAPPDATA 'CineScapeBuildTools'
$installer = Join-Path $cacheRoot 'UnitySetup64-6000.0.23f1.exe'
$editor = Join-Path $EditorRoot 'Editor\Unity.exe'
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
if (-not (Test-Path -LiteralPath $editor)) {
    if (-not (Test-Path -LiteralPath $installer) -or (Get-Item -LiteralPath $installer).Length -ne 4033958912) {
        & curl.exe --fail --location --continue-at - --output $installer 'https://download.unity3d.com/download_unity/1c4764c07fb4/Windows64EditorInstaller/UnitySetup64-6000.0.23f1.exe'
        if ($LASTEXITCODE -ne 0) { throw '编辑器下载失败；重新执行可继续下载。' }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') {
        throw 'Unity 安装包签名验证失败，未执行安装。'
    }
    $installProcess = Start-Process -FilePath $installer -ArgumentList "/S /D=$EditorRoot" -WindowStyle Hidden -Wait -PassThru
    if ($installProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $editor)) {
        throw '编辑器安装失败；请查看 Windows 安装权限或在 Unity Hub 安装指定版本。'
    }
}
if ($InstallOnly) { Write-Output "编辑器安装完成：$editor"; return }
& (Join-Path $PSScriptRoot 'Build-Windows.ps1') -UnityEditor $editor
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Builds\Windows\CineScape3D.exe'))) {
    throw '未生成 EXE；确认 Unity Hub 已登录并激活有效许可证，查看 Builds/build.log。'
}
