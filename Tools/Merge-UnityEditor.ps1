$ErrorActionPreference = 'Stop'
$cacheRoot = Join-Path $env:LOCALAPPDATA 'CineScapeBuildTools'
$installer = Join-Path $cacheRoot 'UnitySetup64-6000.0.23f1.exe'
$partRoot = Join-Path $cacheRoot 'editor-parts'
$partSize = 252122432L
$merged = $installer + '.complete.exe'
$destination = [IO.File]::Create($merged)
try {
    for ($i = 0; $i -lt 16; $i++) {
        $source = [IO.File]::OpenRead((Join-Path $partRoot "$i.part"))
        try {
            if ($source.Length -ne $partSize) { throw "分段 $i 尚未完整下载。" }
            $source.CopyTo($destination)
        } finally { $source.Dispose() }
    }
} finally { $destination.Dispose() }
$signature = Get-AuthenticodeSignature -LiteralPath $merged
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') { throw '合并安装包签名无效。' }
Move-Item -LiteralPath $merged -Destination $installer -Force
Write-Output 'Unity 官方安装包已合并，签名有效。'
& (Join-Path $PSScriptRoot 'Prepare-And-Build.ps1')
