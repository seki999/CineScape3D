$ErrorActionPreference = 'Stop'
$cacheRoot = Join-Path $env:LOCALAPPDATA 'CineScapeBuildTools'
$partRoot = Join-Path $cacheRoot 'editor-parts-8mb'
$installer = Join-Path $cacheRoot 'UnitySetup64-6000.0.23f1.exe'
$url = 'https://download.unity3d.com/download_unity/1c4764c07fb4/Windows64EditorInstaller/UnitySetup64-6000.0.23f1.exe'
$totalBytes = 4033958912L
$partSize = 8388608L
$count = [int][Math]::Ceiling($totalBytes / $partSize)
New-Item -ItemType Directory -Force -Path $partRoot | Out-Null
$results = 0..($count - 1) | ForEach-Object -Parallel {
    $i = $_
    $start = $i * $using:partSize
    $end = [Math]::Min($using:totalBytes - 1, $start + $using:partSize - 1)
    $part = Join-Path $using:partRoot "$i.part"
    if ((Test-Path -LiteralPath $part) -and (Get-Item -LiteralPath $part).Length -eq ($end - $start + 1)) { return 0 }
    & curl.exe --fail --location --silent --show-error --retry 3 --max-time 120 --range "$start-$end" --output $part $using:url
    return $LASTEXITCODE
} -ThrottleLimit 32
if ($results | Where-Object { $_ -ne 0 }) { throw '分段下载失败，重新执行可重用已完成分段。' }
$merged = $installer + '.complete.exe'
$destination = [IO.File]::Create($merged)
try {
    for ($i = 0; $i -lt $count; $i++) {
        $part = Join-Path $partRoot "$i.part"
        $expected = [Math]::Min($partSize, $totalBytes - $i * $partSize)
        if ((Get-Item -LiteralPath $part).Length -ne $expected) { throw "分段 $i 长度不正确。" }
        $source = [IO.File]::OpenRead($part)
        try { $source.CopyTo($destination) } finally { $source.Dispose() }
    }
} finally { $destination.Dispose() }
$signature = Get-AuthenticodeSignature -LiteralPath $merged
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') { throw '合并安装包签名验证失败。' }
Move-Item -LiteralPath $merged -Destination $installer -Force
Write-Output 'Unity 官方安装包下载并验签完成。'
& (Join-Path $PSScriptRoot 'Prepare-And-Build.ps1')
