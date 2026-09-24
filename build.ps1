# StickyTodo 一键编译脚本
# 使用 Windows 自带的 C# 编译器（.NET Framework 4.8），无需安装任何 SDK。
# 用法：  powershell -ExecutionPolicy Bypass -File .\build.ps1
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

function Quote-Arg {
    param([string]$Value)
    if ($Value -match '\s') { return '"' + $Value + '"' }
    return $Value
}

# ---------- 1. 定位编译器 ----------
$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    throw '未找到 csc.exe。请确认已安装 .NET Framework 4.x（Windows 10 默认自带）。'
}

# ---------- 2. 准备输出目录 ----------
$outDir = Join-Path $root 'dist'
$exePath = Join-Path $outDir 'StickyTodo.exe'
if ($Clean -and (Test-Path -LiteralPath $outDir)) {
    Remove-Item -LiteralPath $outDir -Recurse -Force
}
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# ---------- 3. 图标 ----------
$icoPath = Join-Path $root 'app.ico'
if (-not (Test-Path -LiteralPath $icoPath)) {
    Write-Host '未发现 app.ico，正在生成…'
    & (Join-Path $root 'make-icon.ps1')
}

# ---------- 4. 收集源文件 ----------
$sources = @(
    Get-ChildItem -LiteralPath $root -Filter '*.cs' -File |
        Sort-Object Name |
        ForEach-Object { $_.FullName }
)
if ($sources.Count -eq 0) { throw "在 $root 下未找到任何 .cs 源文件。" }

# ---------- 5. 组装编译参数 ----------
$argumentList = New-Object System.Collections.Generic.List[string]
$argumentList.Add('/nologo')
$argumentList.Add('/target:winexe')
$argumentList.Add('/platform:anycpu')
$argumentList.Add('/optimize+')
$argumentList.Add('/langversion:5')     # csc.exe 自带的编译器上限
$argumentList.Add('/codepage:65001')    # 源文件为 UTF-8（无 BOM）
$argumentList.Add('/utf8output')
$argumentList.Add('/warn:4')
$argumentList.Add('/r:System.dll')
$argumentList.Add('/r:System.Drawing.dll')
$argumentList.Add('/r:System.Windows.Forms.dll')
$argumentList.Add('/r:System.Runtime.Serialization.dll')
$argumentList.Add('/win32manifest:' + $root + '\app.manifest')
if (Test-Path -LiteralPath $icoPath) {
    $argumentList.Add('/win32icon:' + $icoPath)
}
$argumentList.Add('/out:' + $exePath)
foreach ($source in $sources) { $argumentList.Add($source) }

$quoted = @()
foreach ($item in $argumentList) { $quoted += (Quote-Arg $item) }

# ---------- 6. 编译 ----------
Write-Host ("使用编译器: {0}" -f $csc)
Write-Host ("源文件 {0} 个，输出: {1}" -f $sources.Count, $exePath)
Write-Host ''

$stdOut = Join-Path ([System.IO.Path]::GetTempPath()) 'stickytodo_build_stdout.txt'
$stdErr = Join-Path ([System.IO.Path]::GetTempPath()) 'stickytodo_build_stderr.txt'

$proc = Start-Process -FilePath $csc -ArgumentList $quoted -Wait -PassThru -NoNewWindow `
    -RedirectStandardOutput $stdOut -RedirectStandardError $stdErr
$code = $proc.ExitCode

if (Test-Path -LiteralPath $stdOut) {
    $text = Get-Content -LiteralPath $stdOut -Encoding UTF8
    if ($text) { $text | Write-Host }
}
if (Test-Path -LiteralPath $stdErr) {
    $text = Get-Content -LiteralPath $stdErr -Encoding UTF8
    if ($text) { $text | Write-Host }
}

Write-Host ''
if ($code -ne 0) {
    throw ("编译失败，csc 退出码 {0}" -f $code)
}

$item = Get-Item -LiteralPath $exePath
Write-Host ("编译成功: {0}" -f $item.FullName)
Write-Host ("大小: {0:N0} 字节" -f $item.Length)
Write-Host ''
Write-Host '运行方式：直接双击 dist\StickyTodo.exe'
